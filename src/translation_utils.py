# Shared helpers for writing Ren'Py translation .rpy files safely.
import os
import re

from deep_translator import GoogleTranslator

# A single-line `old "..."` block (body may contain \" and \\). Optional trailing # comment.
RENPY_OLD_LINE_RE = re.compile(
    r'^(\s*)old\s+"((?:[^"\\]|\\.)*)"\s*(?:#.*)?$'
)

# Narrator / inner monologue in tl files: `# "..."` with no speaker id before the quote
# (unlike `# pc "..."`, which REGEX_UTIL handles).
RENPY_COMMENT_NARRATOR_RE = re.compile(
    r'^\s*#\s*"((?:[^"\\]|\\.)*)"\s*(?:#.*)?$'
)

# Same idea as the original REGEX_UTIL in legacy Ren'Py auto_translate scripts
REGEX_UTIL = re.compile(r'#\s.*\s".*"')


def extract_quoted_body_after_old(line):
    """Return the string inside `old "..."` or None if the line does not match."""
    s = line.rstrip("\r\n")
    m = RENPY_OLD_LINE_RE.match(s)
    if m:
        return m.group(2)
    return None


def extract_quoted_body_narrator_comment(line):
    """Return the string inside `# "..."` narrator lines (not `# speaker "..."`)."""
    s = line.rstrip("\r\n")
    m = RENPY_COMMENT_NARRATOR_RE.match(s)
    if m:
        return m.group(1)
    return None


def extract_first_quoted_body(line):
    """First \"...\" segment with escape-aware parsing (for # ... \"...\" lines)."""
    s = line.rstrip("\r\n")
    m = re.search(r'"((?:[^"\\]|\\.)*)"', s)
    if m:
        return m.group(1)
    return None


def extract_all_quoted_bodies(line):
    """All ``"...`` string bodies in order (escape-aware)."""
    s = line.rstrip("\r\n")
    return [m.group(1) for m in re.finditer(r'"((?:[^"\\]|\\.)*)"', s)]


def extract_speaker_and_dialogue_from_hash_comment(line):
    """
    For ``# "Speaker" "Dialogue"`` (two+ quoted strings on a ``#`` line), return
    ``(speaker, dialogue)`` — Ren'Py literal speaker name + line. If not this
    shape, return None.
    """
    s = line.rstrip("\r\n")
    if not s.lstrip().startswith("#"):
        return None
    bodies = extract_all_quoted_bodies(s)
    if len(bodies) >= 2:
        return (bodies[0], bodies[1])
    return None


def renpy_escape_for_double_quoted_string(s):
    """
    Escape text so it can be placed inside Ren'Py double-quoted strings in .rpy files.
    Order: backslashes, double quotes, then newlines.
    """
    if s is None:
        return ""
    s = str(s)
    s = s.replace("\\", "\\\\")
    s = s.replace('"', '\\"')
    s = s.replace("\r\n", "\n")
    s = s.replace("\r", "\n")
    s = s.replace("\n", "\\n")
    return s


def fill_new_empty_string_line(line, translated_raw):
    """
    Replace the first \"\" on a `new` line with a properly escaped quoted string.
    Returns None if \"\" is not present.
    """
    escaped = renpy_escape_for_double_quoted_string(translated_raw)
    if '""' not in line:
        return None
    return line.replace('""', '"' + escaped + '"', 1)


def fill_new_double_speaker_line(line, speaker_tr, dialogue_tr):
    """
    ``    "Speaker" ""`` → ``    "TrSpeaker" "TrDialogue"`` (both slots translated).
    """
    m = re.match(r'^(\s*)"((?:[^"\\]|\\.)*)"\s*""', line)
    if not m:
        return None
    indent = m.group(1)
    esc_s = renpy_escape_for_double_quoted_string(speaker_tr)
    esc_d = renpy_escape_for_double_quoted_string(dialogue_tr)
    rest = line[m.end() :]
    return f'{indent}"{esc_s}" "{esc_d}"{rest}'


def get_string_to_translate(line):
    """Return substring to send to the translator, or None."""
    body = extract_quoted_body_after_old(line)
    if body is not None:
        return body
    body = extract_quoted_body_narrator_comment(line)
    if body is not None:
        return body
    s = line.rstrip("\r\n")
    if REGEX_UTIL.search(s) is not None:
        return extract_first_quoted_body(line)
    return None


def translate_rpy_file(origin_file, tl_root, output_root, from_l, to_l):
    """Translate one .rpy; writes mirror path under output_root (Ren'Py ``game/tl`` tree).

    Writes to a ``.tmp`` file first, then ``os.replace`` into place so partial
    outputs are never left on failure.
    """
    tl_root = os.path.normpath(tl_root)
    output_root = os.path.normpath(output_root)
    rel = os.path.relpath(origin_file, tl_root)
    out_path = os.path.join(output_root, rel)
    tmp_path = out_path + ".tmp"
    os.makedirs(os.path.dirname(out_path), exist_ok=True)

    try:
        with open(origin_file, "rt", encoding="utf-8") as input_file:
            with open(tmp_path, "wt", encoding="utf-8") as output_file:
                pending = None  # ("single", str) | ("double", str, str)

                def tr_one(text: str) -> str:
                    try:
                        if text is None:
                            return ""
                        t = GoogleTranslator(source=from_l, target=to_l).translate(
                            text
                        )
                        return t if t is not None else text
                    except Exception:
                        return text

                for line in input_file:
                    if pending is not None:
                        if pending[0] == "single":
                            filled = replace_first_quoted_body(line, pending[1])
                        else:
                            filled = fill_new_double_speaker_line(
                                line, pending[1], pending[2]
                            )
                        if filled is None:
                            output_file.write(line)
                        else:
                            output_file.write(filled)
                        pending = None
                        continue

                    pair = extract_speaker_and_dialogue_from_hash_comment(line)
                    if pair is not None:
                        spk, dlg = pair
                        output_file.write(line)
                        pending = (
                            "double",
                            tr_one(spk),
                            tr_one(dlg),
                        )
                        continue

                    line_to_translate = get_string_to_translate(line)
                    if line_to_translate is not None:
                        output_file.write(line)
                        pending = ("single", tr_one(line_to_translate))
                        continue

                    output_file.write(line)
        os.replace(tmp_path, out_path)
    except Exception:
        if os.path.isfile(tmp_path):
            try:
                os.remove(tmp_path)
            except OSError:
                pass
        raise
