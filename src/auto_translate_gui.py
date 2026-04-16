# GUI for Ren'Py automatic translation (single language).
# Requires: pip install deep-translator pycountry

import os
import queue
import shutil
import sys
import threading
import traceback
import tkinter as tk
from datetime import datetime
from tkinter import filedialog, messagebox, ttk

import pycountry

from parallel_translate import (
    RunResult,
    build_tasks,
    collect_rpy_paths_under,
    list_missing_rpy_files,
    run_translation_workers,
)


def tool_repo_root() -> str:
    """Folder used for ``<game>/game/tl`` output and ``logs/`` (not Python cwd)."""
    if getattr(sys, "frozen", False):
        return os.path.normpath(os.path.dirname(sys.executable))
    here = os.path.dirname(os.path.abspath(__file__))
    if os.path.basename(here).lower() == "src":
        return os.path.normpath(os.path.join(here, os.pardir))
    return os.path.normpath(here)


def logs_directory() -> str:
    """``<repo>/logs`` — session log files."""
    return os.path.join(tool_repo_root(), "logs")


def output_tl_path(repo_root: str, game_name: str) -> str:
    """``{repo}/{game_name}/game/tl`` — where translated .rpy files are written."""
    gn = game_name.strip()
    if not gn:
        raise ValueError("game name is empty")
    if gn in (".", "..") or any(c in gn for c in r'\/:*?"<>|'):
        raise ValueError(f"invalid game folder name: {game_name!r}")
    return os.path.normpath(os.path.join(repo_root, gn, "game", "tl"))


def game_name_from_tl_path(tl_root: str) -> str:
    """
    Ren'Py layout: ``.../<game name>/game/tl``.

    The game name is the parent folder of the ``game`` directory (the folder
    that contains ``game/tl``).
    """
    tl_root = os.path.normpath(tl_root)
    if not tl_root or not os.path.isdir(tl_root):
        raise ValueError("Invalid TL path.")
    if os.path.basename(tl_root).lower() != "tl":
        raise ValueError(
            'Select the folder named "tl" inside the game directory '
            "(path must be …/<game name>/game/tl)."
        )
    game_dir = os.path.dirname(tl_root)
    if os.path.basename(game_dir).lower() != "game":
        raise ValueError(
            'The folder above "tl" must be named "game" (expected …/<game name>/game/tl).'
        )
    project_dir = os.path.dirname(game_dir)
    name = os.path.basename(project_dir)
    if not name or name in (".", ".."):
        raise ValueError("Could not determine the game folder name from this path.")
    return name


def get_languages_dict():
    """Map lowercase English language name -> ISO 639-1 code (pycountry)."""
    values = [lang.alpha_2 for lang in pycountry.languages if hasattr(lang, "alpha_2")]
    keys = []
    for code in values:
        name = pycountry.languages.get(alpha_2=code).name
        keys.append(name.lower())
    return dict(zip(keys, values))


def list_language_folders(tl_path):
    """Subfolders of the Ren'Py `tl` directory (language folders)."""
    if not tl_path or not os.path.isdir(tl_path):
        return []
    names = []
    for name in os.listdir(tl_path):
        full = os.path.join(tl_path, name)
        if os.path.isdir(full) and not name.startswith("."):
            names.append(name)
    return sorted(names, key=str.lower)


def copy_empty_language_tree(src_lang_dir, dst_lang_dir):
    """Mirror folder structure without files (empty dirs only)."""
    if os.path.isdir(dst_lang_dir):
        shutil.rmtree(dst_lang_dir)
    shutil.copytree(
        src_lang_dir,
        dst_lang_dir,
        ignore=shutil.ignore_patterns("*.*"),
    )


def run_translation(
    tl_root,
    output_root,
    lang_folder,
    source_iso,
    langs_dict,
    log_queue,
    *,
    resume: bool = False,
    max_workers: int = 4,
):
    """Worker: read strings from ``tl_root`` (source game), write under ``output_root``."""
    tl_root = os.path.normpath(tl_root)
    output_root = os.path.normpath(output_root)
    lang_key = lang_folder

    if lang_key not in langs_dict:
        err = f'Language folder "{lang_folder}" is not supported by pycountry.'
        log_queue.put(("error", err))
        log_queue.put(("done", None))
        return

    to_iso = langs_dict[lang_key]
    src_lang_dir = os.path.join(tl_root, lang_folder)
    dst_lang_dir = os.path.join(output_root, lang_folder)

    try:
        log_queue.put(
            (
                "log",
                "INFO",
                f"Source TL (read): {tl_root}\n"
                f"Output TL (write): {output_root}\n"
                f"Target folder: {lang_folder} (ISO {to_iso})\n"
                f"Source ISO: {source_iso}\n---",
            )
        )
        os.makedirs(output_root, exist_ok=True)
        log_queue.put(("log", "INFO", f"Output root: {output_root}"))

        if not resume:
            if os.path.normpath(src_lang_dir) != os.path.normpath(dst_lang_dir):
                copy_empty_language_tree(src_lang_dir, dst_lang_dir)
                log_queue.put(
                    ("log", "INFO", f"Prepared folder structure: {lang_folder}")
                )
            else:
                log_queue.put(
                    (
                        "log",
                        "INFO",
                        f"In-place: writing into {lang_folder} (skipping empty-folder clone).",
                    )
                )
            origins = collect_rpy_paths_under(src_lang_dir)
            log_queue.put(
                ("log", "INFO", f"Full run: {len(origins)} file(s)")
            )
        else:
            origins = list_missing_rpy_files(tl_root, output_root, lang_folder)
            log_queue.put(
                ("log", "INFO", f"Resume: {len(origins)} missing file(s)")
            )

        tasks = build_tasks(origins, tl_root, source_iso, to_iso)
        total = len(tasks)
        log_queue.put(("progress", 0, total, ""))

        def on_progress(completed: int, tot: int, rel: str) -> None:
            log_queue.put(("progress", completed, tot, rel))

        def on_log(level: str, msg: str) -> None:
            log_queue.put(("log", level, msg))

        result: RunResult = run_translation_workers(
            tasks,
            tl_root,
            output_root,
            max_workers=max_workers,
            on_progress=on_progress,
            on_log=on_log,
        )

        log_queue.put(("log", "INFO", "Done."))
        log_queue.put(("success", output_root, result))
    except Exception as exc:
        log_queue.put(("log", "ERROR", traceback.format_exc()))
        log_queue.put(("error", str(exc)))
    finally:
        log_queue.put(("done", None))


class AutoTranslateApp(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title("Ren'Py — Translate TL folder")
        self.minsize(560, 420)
        self.langs_dict = get_languages_dict()
        self.log_queue = queue.Queue()
        self.worker = None
        self._log_fp = None
        self._log_path = None

        self._build_ui()
        self._progress_idle()
        self.after(100, self._drain_log_queue)

    def _progress_idle(self):
        """Progress widgets when idle or after a run finishes."""
        self.progress_count_var.set("—")
        self.progress_file_var.set("")
        self.progress_bar.configure(maximum=1, value=0)

    def _reset_progress_ui(self):
        """Right before starting a translation run."""
        self.progress_count_var.set("…")
        self.progress_file_var.set("")
        self.progress_bar.configure(maximum=1, value=0)

    def _update_progress_ui(self, i, total, rel):
        """i = completed count (0..total), total = number of tasks in this run."""
        rel_disp = rel.replace("\\", "/") if rel else "—"
        if total <= 0:
            self.progress_count_var.set("0 / 0")
            self.progress_bar.configure(maximum=1, value=0)
            self.progress_file_var.set(rel_disp)
            return
        self.progress_count_var.set(f"{i} / {total}")
        self.progress_bar.configure(maximum=total, value=i)
        self.progress_file_var.set(rel_disp)

    def _build_ui(self):
        pad = {"padx": 10, "pady": 6}
        main = ttk.Frame(self, padding=12)
        main.pack(fill=tk.BOTH, expand=True)

        # TL folder (source — e.g. Steam game install)
        row1 = ttk.Frame(main)
        row1.pack(fill=tk.X, **pad)
        ttk.Label(row1, text="Source TL folder", width=18).pack(side=tk.LEFT)
        self.tl_var = tk.StringVar()
        self.tl_entry = ttk.Entry(row1, textvariable=self.tl_var, state="readonly")
        self.tl_entry.pack(side=tk.LEFT, fill=tk.X, expand=True, padx=(0, 8))
        ttk.Button(row1, text="Browse…", command=self._browse_tl).pack(side=tk.RIGHT)

        row_game = ttk.Frame(main)
        row_game.pack(fill=tk.X, **pad)
        ttk.Label(row_game, text="Detected game", width=18).pack(side=tk.LEFT)
        self.detected_game_var = tk.StringVar(value="—")
        ttk.Label(row_game, textvariable=self.detected_game_var).pack(
            side=tk.LEFT, anchor=tk.W
        )
        ttk.Label(
            row_game,
            text="(from …/<name>/game/tl)  →  output under tool repo",
            foreground="#555",
        ).pack(side=tk.LEFT, padx=(12, 0))

        # Target language (subfolder of tl)
        row2 = ttk.Frame(main)
        row2.pack(fill=tk.X, **pad)
        ttk.Label(row2, text="Translate into", width=18).pack(side=tk.LEFT)
        self.lang_var = tk.StringVar()
        self.lang_combo = ttk.Combobox(
            row2,
            textvariable=self.lang_var,
            state="readonly",
            width=48,
        )
        self.lang_combo.pack(side=tk.LEFT, fill=tk.X, expand=True)

        # Source language (game script language)
        row3 = ttk.Frame(main)
        row3.pack(fill=tk.X, **pad)
        ttk.Label(row3, text="Source language (ISO)", width=18).pack(side=tk.LEFT)
        self.source_var = tk.StringVar(value="en")
        ttk.Entry(row3, textvariable=self.source_var, width=12).pack(side=tk.LEFT)
        ttk.Label(
            row3,
            text="ISO 639-1 code (e.g. en, es). common.rpy always uses en → target.",
            foreground="#555",
        ).pack(side=tk.LEFT, padx=(12, 0))

        row_workers = ttk.Frame(main)
        row_workers.pack(fill=tk.X, **pad)
        ttk.Label(row_workers, text="Workers", width=18).pack(side=tk.LEFT)
        self.workers_var = tk.StringVar(value="4")
        tk.Spinbox(
            row_workers,
            from_=1,
            to=16,
            textvariable=self.workers_var,
            width=6,
        ).pack(side=tk.LEFT)
        ttk.Label(
            row_workers,
            text="Request pacing adjusts automatically if the server throttles (HTTP 429).",
            foreground="#555",
        ).pack(side=tk.LEFT, padx=(12, 0))

        # Actions
        row4 = ttk.Frame(main)
        row4.pack(fill=tk.X, **pad)
        self.run_btn = ttk.Button(
            row4,
            text="Translate",
            command=self._start_translation,
        )
        self.run_btn.pack(side=tk.LEFT)
        self.resume_btn = ttk.Button(
            row4,
            text="Resume",
            command=self._start_resume,
        )
        self.resume_btn.pack(side=tk.LEFT, padx=(8, 0))

        # Progress (updated from worker thread via queue)
        prog_outer = ttk.LabelFrame(main, text="Progress", padding=(8, 6))
        prog_outer.pack(fill=tk.X, **pad)
        self.progress_count_var = tk.StringVar(value="—")
        self.progress_file_var = tk.StringVar(value="")
        ttk.Label(
            prog_outer,
            textvariable=self.progress_count_var,
            font=("TkDefaultFont", 10, "bold"),
        ).pack(anchor=tk.W)
        self.progress_bar = ttk.Progressbar(
            prog_outer,
            mode="determinate",
            maximum=1,
            value=0,
        )
        self.progress_bar.pack(fill=tk.X, pady=(4, 2))
        file_row = ttk.Frame(prog_outer)
        file_row.pack(fill=tk.X)
        ttk.Label(file_row, text="Last completed:", width=12).pack(
            side=tk.LEFT, anchor=tk.NW
        )
        self.progress_file_label = ttk.Label(
            file_row,
            textvariable=self.progress_file_var,
            wraplength=520,
            justify=tk.LEFT,
        )
        self.progress_file_label.pack(side=tk.LEFT, fill=tk.X, expand=True, anchor=tk.NW)

        # Log
        ttk.Label(main, text="Log").pack(anchor=tk.W, **pad)
        self.log_text = tk.Text(main, height=14, wrap=tk.WORD, state=tk.DISABLED)
        scroll = ttk.Scrollbar(main, command=self.log_text.yview)
        self.log_text.configure(yscrollcommand=scroll.set)
        log_frame = ttk.Frame(main)
        log_frame.pack(fill=tk.BOTH, expand=True)
        self.log_text.pack(in_=log_frame, side=tk.LEFT, fill=tk.BOTH, expand=True)
        scroll.pack(in_=log_frame, side=tk.RIGHT, fill=tk.Y)

    def _close_log_file(self):
        if self._log_fp is not None:
            try:
                self._log_fp.close()
            except OSError:
                pass
            self._log_fp = None

    def _append_log(self, text, level="INFO"):
        """Write each line with timestamp, log level, and message (on-screen and log file)."""
        if text is None:
            return
        level = (level or "INFO").upper()
        if level not in ("DEBUG", "INFO", "WARNING", "ERROR", "CRITICAL"):
            level = "INFO"
        raw = text.rstrip("\n")
        lines = raw.split("\n") if raw else []
        if not lines:
            return
        self.log_text.configure(state=tk.NORMAL)
        for line in lines:
            ts = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
            out = f"{ts} | {level:8} | {line}\n"
            self.log_text.insert(tk.END, out)
            if self._log_fp is not None:
                try:
                    self._log_fp.write(out)
                except OSError:
                    pass
        if self._log_fp is not None:
            try:
                self._log_fp.flush()
            except OSError:
                pass
        self.log_text.see(tk.END)
        self.log_text.configure(state=tk.DISABLED)

    def _browse_tl(self):
        path = filedialog.askdirectory(title="Select source game tl folder (read-only)")
        if not path:
            return
        try:
            game_name = game_name_from_tl_path(path)
        except ValueError as exc:
            messagebox.showerror("Invalid TL folder", str(exc))
            return
        self.tl_var.set(path)
        self.detected_game_var.set(game_name)
        folders = list_language_folders(path)
        self.lang_combo["values"] = folders
        if folders:
            self.lang_var.set(folders[0])
        else:
            self.lang_var.set("")
            messagebox.showwarning(
                "No language folders",
                "This folder has no subfolders. Generate translations in the Ren'Py launcher first.",
            )

    def _parse_workers(self):
        try:
            w = int(self.workers_var.get().strip())
        except ValueError:
            messagebox.showerror("Workers", "Workers must be a whole number.")
            return None
        w = max(1, min(w, 16))
        return w

    def _start_translation(self):
        self._start_run(resume=False)

    def _start_resume(self):
        self._start_run(resume=True)

    def _start_run(self, resume: bool):
        tl_root = self.tl_var.get().strip()
        lang_folder = self.lang_var.get().strip()
        source = self.source_var.get().strip().lower()

        if not tl_root or not os.path.isdir(tl_root):
            messagebox.showerror("Invalid folder", "Choose a valid source tl folder.")
            return
        try:
            game_name = game_name_from_tl_path(tl_root)
        except ValueError as exc:
            messagebox.showerror("Invalid TL folder", str(exc))
            return
        try:
            output_root = output_tl_path(tool_repo_root(), game_name)
        except ValueError as exc:
            messagebox.showerror("Output path", str(exc))
            return
        if not lang_folder:
            messagebox.showerror("No language", "Select which language folder to translate.")
            return
        if not source:
            messagebox.showerror("Source language", "Enter a source language code (e.g. en).")
            return
        lang_path = os.path.join(tl_root, lang_folder)
        if not os.path.isdir(lang_path):
            messagebox.showerror("Missing folder", f"Not found:\n{lang_path}")
            return

        max_workers = self._parse_workers()
        if max_workers is None:
            return

        if self.worker and self.worker.is_alive():
            return

        self._close_log_file()
        stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        log_dir = logs_directory()
        os.makedirs(log_dir, exist_ok=True)
        self._log_path = os.path.join(log_dir, f"auto_translate_gui_{stamp}.log")
        try:
            self._log_fp = open(self._log_path, "w", encoding="utf-8")
        except OSError as exc:
            messagebox.showerror("Log file", f"Could not create log file:\n{exc}")
            return

        self.run_btn.configure(state=tk.DISABLED)
        self.resume_btn.configure(state=tk.DISABLED)
        self.log_text.configure(state=tk.NORMAL)
        self.log_text.delete("1.0", tk.END)
        self.log_text.configure(state=tk.DISABLED)
        self._append_log(f"Log file: {self._log_path}\n---", "INFO")
        self._reset_progress_ui()

        def _worker():
            run_translation(
                tl_root,
                output_root,
                lang_folder,
                source,
                self.langs_dict,
                self.log_queue,
                resume=resume,
                max_workers=max_workers,
            )

        self.worker = threading.Thread(target=_worker, daemon=True)
        self.worker.start()

    def _drain_log_queue(self):
        try:
            while True:
                item = self.log_queue.get_nowait()
                kind = item[0]
                if kind == "log":
                    if len(item) == 3:
                        _k, level, payload = item
                        self._append_log(payload, level)
                    else:
                        self._append_log(item[1], "INFO")
                elif kind == "error":
                    err_msg = str(item[1])
                    self._append_log(err_msg, "ERROR")
                    err_text = (
                        err_msg
                        + "\n\n(Full traceback is in the log if one was logged.)"
                    )
                    if self._log_path:
                        err_text += f"\n\nLog file:\n{self._log_path}"
                    messagebox.showerror("Translation error", err_text)
                elif kind == "success":
                    out_root = item[1]
                    res = item[2] if len(item) > 2 else None
                    log_note = ""
                    if self._log_path:
                        log_note = f"\n\nLog saved to:\n{self._log_path}"
                    summary = ""
                    if res is not None:
                        summary = (
                            f"\n\nTranslated: {res.success} / {res.total}\n"
                            f"Failed: {res.failed} / {res.total}"
                        )
                    messagebox.showinfo(
                        "Finished",
                        f"Wrote translated files under:\n{out_root}{summary}{log_note}",
                    )
                elif kind == "progress":
                    _k, i, total, rel = item
                    self._update_progress_ui(i, total, rel)
                elif kind == "done":
                    self.run_btn.configure(state=tk.NORMAL)
                    self.resume_btn.configure(state=tk.NORMAL)
                    self._close_log_file()
                    self._progress_idle()
        except queue.Empty:
            pass
        self.after(100, self._drain_log_queue)


if __name__ == "__main__":
    app = AutoTranslateApp()
    app.mainloop()
