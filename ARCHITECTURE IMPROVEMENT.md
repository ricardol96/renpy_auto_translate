# Ren'Py Translator — Production Architecture Plan (Tauri + Python Sidecar)

## 🎯 Objective

Build a desktop application that:

- Has a modern UI (React via Tauri)
- Runs fully offline
- Bundles into a single installer `.exe`
- Uses Python only for translation logic
- Does NOT require Python installation on user machine

---

# 🧠 Final System Architecture

## Runtime view


User runs: MyApp.exe
│
├── Tauri (UI + app shell)
│ ├── React frontend
│ └── Rust bridge layer
│
└── Embedded Python executable (sidecar)
└── translator.exe (PyInstaller build)
├── Ren'Py parser
├── Translation engine (deep-translator)
├── File writer
└── CLI interface


---

# 🚫 What you are explicitly NOT doing

- No FastAPI
- No HTTP server
- No API layer
- No Tkinter
- No long-running Python GUI
- No network communication between UI and backend

Everything is **process-based communication only**.

---

# 🐍 Python Backend Design

## 1. Backend becomes a CLI tool only

Entry point:


backend/cli.py


### Supported command

```bash
translator.exe translate \
  --input "C:/game/game/tl" \
  --output "C:/output" \
  --lang fr \
  --workers 4
2. Internal Python structure
backend/
├── cli.py              # entry point (ONLY entry point)
├── core/
│   ├── parser.py       # extract Ren'Py strings
│   ├── translator.py   # deep-translator wrapper
│   ├── writer.py       # rebuild .rpy safely
│   └── pipeline.py     # orchestrates full flow
├── state/
│   └── job_state.py    # resume support
└── utils/
    ├── logging.py
    └── filesystem.py
3. Output contract (IMPORTANT)

Python MUST communicate ONLY via stdout JSON lines:

Example:
{"type":"start","total_files":120}
{"type":"progress","file":"script1.rpy","done":45,"total":120}
{"type":"warning","file":"script2.rpy","msg":"rate limited"}
{"type":"done","translated":110,"failed":10}
4. Execution rules
No UI code
No prints except JSON events
Exit code:
0 success
1 failure
⚙️ Packaging Python (CRITICAL STEP)
Build executable using PyInstaller

Tool:
PyInstaller

pyinstaller --onefile backend/cli.py --name translator

Output:

dist/translator.exe
Validation requirement

Before integrating with Tauri:

translator.exe translate --input test --output test --lang fr

Must work standalone on a clean machine.

🧩 Tauri Integration Design
1. Sidecar configuration

Tauri bundles external binaries.

Place:

src-tauri/bin/translator.exe
2. Tauri config
{
  "tauri": {
    "bundle": {
      "externalBin": ["bin/translator"]
    }
  }
}
3. Execution model

Rust layer:

Spawns translator.exe
Reads stdout stream
Forwards events to frontend

No logic inside Rust except orchestration.

4. Rust responsibility
Start process
Stream output line-by-line
Emit events to React
Handle process kill / restart
⚛️ Frontend (React) Architecture
Responsibilities
Select folder (tl directory)
Choose language
Set worker count
Start / resume translation
Display progress in real time
Show logs + errors
UI components
FilePicker
LanguageSelector
WorkerSlider
ProgressBar
LogConsole
StartButton
ResumeButton
Data flow (CRITICAL)
React UI
  ↓ invoke Tauri command
Rust backend
  ↓ spawns process
translator.exe (Python)
  ↓ stdout JSON stream
Rust parses events
  ↓ emits to frontend
React updates UI
📦 Build Pipeline (Final Product)
Step 1 — Build Python executable
pyinstaller --onefile backend/cli.py --name translator
Step 2 — Move binary into Tauri
src-tauri/bin/translator.exe
Step 3 — Build desktop app
npm run tauri build
Output:
MyApp.exe (installer)
⚠️ Critical constraints (DO NOT IGNORE)
1. Path handling

Always pass absolute paths from frontend.

2. Process isolation

Each translation run is a separate process.

No shared Python state.

3. Streaming requirement

Backend MUST output JSON line-by-line or UI will not update.

4. Dependency bundling

PyInstaller must include:

deep-translator
pycountry
requests
5. Antivirus risk

Expected with PyInstaller.

Not a bug.

🚀 Future upgrades (optional)
Job queue system (multi-run history)
Glossary / term consistency engine
Incremental translation mode
Partial file retranslation
Cache translated strings
Multi-language batch mode
🧭 Final Result

You end up with:

✔ One installer (MyApp.exe)
✔ Modern UI (React)
✔ Offline translation tool
✔ No Python required for user
✔ Clean separation of concerns
✔ Maintainable architecture