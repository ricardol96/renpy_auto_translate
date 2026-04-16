# Parallel Ren'Py .rpy translation: job queue, workers, rate limiting, retries.
# Requires: translation_utils.translate_rpy_file

from __future__ import annotations

import os
import queue
import re
import random
import threading
import time
from dataclasses import dataclass
from typing import Callable, List, Optional, Tuple

import requests

from translation_utils import translate_rpy_file

Task = Tuple[str, str, str, str]  # origin, rel, from_iso, to_iso


@dataclass
class RunResult:
    total: int
    success: int
    failed: int


def _is_rate_limit_error(exc: Exception) -> bool:
    """HTTP 429 / provider wording — widen pacing across workers."""
    if isinstance(exc, requests.exceptions.HTTPError):
        resp = getattr(exc, "response", None)
        if resp is not None and getattr(resp, "status_code", None) == 429:
            return True
    msg = str(exc).lower()
    return (
        "429" in msg
        or "too many" in msg
        or "rate limit" in msg
        or "quota" in msg
    )


class AdaptiveRateLimiter:
    """
    Shared minimum interval between translation attempts (all workers).
    Starts with no extra delay; increases when throttling is detected, decays
    slowly after successes so the user does not tune delays manually.
    """

    def __init__(
        self,
        *,
        ceiling_sec: float = 8.0,
        first_bump_sec: float = 0.05,
        on_adjust: Optional[Callable[[str], None]] = None,
    ):
        self._ceiling = max(0.05, float(ceiling_sec))
        self._first_bump = max(0.0, float(first_bump_sec))
        self._on_adjust = on_adjust
        self._lock = threading.Lock()
        self._next_allowed = 0.0
        self._interval = 0.0

    def acquire(self) -> None:
        with self._lock:
            interval = self._interval
            now = time.monotonic()
            if now < self._next_allowed:
                time.sleep(self._next_allowed - now)
                now = time.monotonic()
            self._next_allowed = now + interval

    def record_success(self) -> None:
        with self._lock:
            if self._interval <= 0:
                return
            self._interval = max(0.0, self._interval * 0.992)
            if self._interval < 0.001:
                self._interval = 0.0

    def record_throttle(self) -> None:
        with self._lock:
            old = self._interval
            if self._interval <= 0:
                self._interval = self._first_bump
            else:
                self._interval = min(self._interval * 1.85, self._ceiling)
            if self._interval > old and self._on_adjust:
                ms = self._interval * 1000.0
                self._on_adjust(
                    f"Server throttling detected; spacing requests ~{ms:.0f}ms apart"
                )


# Standalone ``""`` line = untranslated Ren'Py ``new`` slot (in-place resume).
_RPY_UNFILLED_EMPTY_NEW = re.compile(r'^\s*""\s*$', re.MULTILINE)


def rpy_has_unfilled_empty_new_strings(path: str) -> bool:
    """True if the file still has at least one empty ``new`` string line."""
    try:
        with open(path, "rt", encoding="utf-8", errors="replace") as f:
            data = f.read()
    except OSError:
        return True
    return _RPY_UNFILLED_EMPTY_NEW.search(data) is not None


def collect_rpy_paths_under(root: str) -> List[str]:
    """Absolute paths to all .rpy files under root."""
    out: List[str] = []
    root = os.path.normpath(root)
    if not os.path.isdir(root):
        return out
    for dir_name, _dirs, files in os.walk(root):
        for file_name in files:
            _base, ext = os.path.splitext(file_name)
            if ext == ".rpy":
                out.append(os.path.join(dir_name, file_name))
    return out


def list_missing_rpy_files(
    tl_root: str, output_root: str, lang_folder: str
) -> List[str]:
    """
    Paths under tl/<lang>/ that still need work: missing or empty output, or
    when writing in-place (same path as ``tl``), files that still contain empty
    ``new`` lines (``""``).
    """
    tl_root = os.path.normpath(tl_root)
    output_root = os.path.normpath(output_root)
    in_place = os.path.normcase(tl_root) == os.path.normcase(output_root)
    lang_dir = os.path.join(tl_root, lang_folder)
    missing: List[str] = []
    for origin in collect_rpy_paths_under(lang_dir):
        rel = os.path.relpath(origin, tl_root)
        out_path = os.path.join(output_root, rel)
        if not os.path.isfile(out_path) or os.path.getsize(out_path) == 0:
            missing.append(origin)
            continue
        if in_place and rpy_has_unfilled_empty_new_strings(origin):
            missing.append(origin)
    return missing


def build_tasks(
    origins: List[str],
    tl_root: str,
    source_iso: str,
    target_iso: str,
) -> List[Task]:
    """Build (origin, rel, from_iso, to_iso) tasks; common.rpy uses en -> target."""
    tl_root = os.path.normpath(tl_root)
    tasks: List[Task] = []
    for origin in origins:
        rel = os.path.relpath(origin, tl_root)
        base = os.path.basename(origin)
        if base == "common.rpy":
            from_l, to_l = "en", target_iso
        else:
            from_l, to_l = source_iso, target_iso
        tasks.append((origin, rel, from_l, to_l))
    return tasks


def translate_rpy_file_with_retry(
    origin: str,
    tl_root: str,
    output_root: str,
    from_l: str,
    to_l: str,
    limiter: AdaptiveRateLimiter,
    max_retries: int = 3,
    base_delay: float = 0.5,
) -> None:
    """Call translate_rpy_file with adaptive rate limiting and retries."""
    last_exc: Optional[Exception] = None
    for attempt in range(max_retries + 1):
        limiter.acquire()
        try:
            translate_rpy_file(origin, tl_root, output_root, from_l, to_l)
            limiter.record_success()
            return
        except (
            requests.exceptions.RequestException,
            requests.exceptions.Timeout,
            ConnectionError,
            OSError,
        ) as exc:
            last_exc = exc
        except Exception as exc:
            msg = str(exc).lower()
            if (
                "429" in msg
                or "too many" in msg
                or "timeout" in msg
                or "connection" in msg
            ):
                last_exc = exc
            else:
                raise
        if attempt < max_retries:
            if last_exc is not None and _is_rate_limit_error(last_exc):
                limiter.record_throttle()
            delay = base_delay * (2**attempt) + random.uniform(0, 0.25)
            time.sleep(delay)
    if last_exc is not None:
        raise last_exc
    raise RuntimeError("translate_rpy_file_with_retry: exhausted retries")


def run_translation_workers(
    tasks: List[Task],
    tl_root: str,
    output_root: str,
    *,
    max_workers: int,
    max_retries: int = 3,
    on_progress: Optional[Callable[[int, int, str], None]] = None,
    on_log: Optional[Callable[[str, str], None]] = None,
    on_summary: Optional[Callable[[RunResult], None]] = None,
) -> RunResult:
    """
    Process tasks with a queue and worker threads. on_progress(completed, total, last_rel)
    after each file finishes (success or failure). on_log(level, message) for optional logging.
    """
    tl_root = os.path.normpath(tl_root)
    output_root = os.path.normpath(output_root)
    total = len(tasks)
    if total == 0:

        def log(level: str, msg: str) -> None:
            if on_log:
                on_log(level, msg)

        empty = RunResult(0, 0, 0)
        log("INFO", "Translated: 0 / 0")
        log("INFO", "Failed: 0 / 0")
        if on_summary:
            on_summary(empty)
        return empty

    workers = max(1, min(int(max_workers), 32))

    def log_adjust(msg: str) -> None:
        if on_log:
            on_log("INFO", msg)

    limiter = AdaptiveRateLimiter(on_adjust=log_adjust)

    q: queue.Queue = queue.Queue()
    for t in tasks:
        q.put(t)

    for _ in range(workers):
        q.put(None)

    success_count = 0
    fail_count = 0
    completed = 0
    counter_lock = threading.Lock()

    def log(level: str, msg: str) -> None:
        if on_log:
            on_log(level, msg)

    def worker() -> None:
        nonlocal success_count, fail_count, completed
        while True:
            item = q.get()
            try:
                if item is None:
                    break
                origin, rel, from_l, to_l = item
                try:
                    translate_rpy_file_with_retry(
                        origin,
                        tl_root,
                        output_root,
                        from_l,
                        to_l,
                        limiter,
                        max_retries=max_retries,
                    )
                    with counter_lock:
                        success_count += 1
                        completed += 1
                        c = completed
                    if on_progress:
                        on_progress(c, total, rel)
                    log("INFO", f"[{c}/{total}] OK {rel.replace(os.sep, '/')}")
                except Exception as exc:
                    with counter_lock:
                        fail_count += 1
                        completed += 1
                        c = completed
                    if on_progress:
                        on_progress(c, total, rel)
                    log("ERROR", f"[{c}/{total}] FAILED {rel.replace(os.sep, '/')}: {exc}")
            finally:
                q.task_done()

    threads: List[threading.Thread] = []
    for _ in range(workers):
        t = threading.Thread(target=worker, daemon=True)
        t.start()
        threads.append(t)

    q.join()
    for t in threads:
        t.join(timeout=600.0)

    result = RunResult(total=total, success=success_count, failed=fail_count)
    log("INFO", f"Translated: {success_count} / {total}")
    log("INFO", f"Failed: {fail_count} / {total}")
    if on_summary:
        on_summary(result)
    return result
