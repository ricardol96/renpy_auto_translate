# Ren’Py Auto Translate

A small Windows program that reads your Ren’Py translation files, sends the text through an online translator, and saves new files you can copy back into your game when you’re ready.

---

## What you need

- A **Windows** computer (64-bit).
- Your game on disk with the usual Ren’Py layout: a **`tl`** folder inside **`game`** (`…YourGame\game\tl`), with one folder per language (`english`, `spanish`, and so on).
- A **stable internet connection** while it’s working.

---

## How to open the program

**If someone gave you `RenPyAutoTranslate.exe`:**  
Double-click it (you can keep it on its own).

**If you only have this source project and need to create the program yourself:**  
Install Microsoft’s **.NET 8** developer tools (free), open the **`dotnet`** folder inside this project, and run **`publish.ps1`**. When it finishes, **`RenPyAutoTranslate.exe`** appears at the **top level of the project** (next to this readme). Double-click it to run. The first build can take a few minutes; later builds are quicker.

---

## Using it for the first time

1. Click **Browse…** and choose your game’s **`tl`** folder — the one whose name is exactly **`tl`**, not the `game` folder above it and not a single language folder inside `tl`.

2. Under **Translate into**, tick the **one or more** languages you want filled or updated. They are processed **in order**, one language after another.

3. Leave **Source language** on **English** unless your script is written in something else.

4. Press **Translate** and wait until the window shows it’s finished.

Your translated files appear in a folder **next to the program**, named after your game. Copy those files into your real Ren’Py project when you’re happy with them. Make a **backup** of your `tl` folder before big runs.

---

## What the buttons do

- **Translate** — Starts a **full run** for every language you checked. It prepares the output folders when needed and sends **all** the dialogue it finds to be translated (or refreshed).

- **Resume** — Does **not** start over from zero. It only picks up **what’s still missing or unfinished** — for example after you clicked **Cancel**, closed the program, or had a shaky connection. Use this when you want to **continue** instead of redoing lines that already saved.

- **Cancel** — Stops the work in progress as soon as it can. Anything **already written** stays on disk; use **Resume** later for the rest.

- **Open output folder** — Opens the folder where this program **wrote** the translated files (the copy next to `RenPyAutoTranslate.exe`, under your game’s name).

- **Open logs folder** — Opens the folder where the program keeps **session log files**. That’s the first place to look if something failed or you need to share details when asking for help.

---

## Workers

**Workers** sets how much work the program sends to the translator at the same time. **A high value is rarely a good idea:** it can overwhelm your connection or cause the translator to throttle or refuse requests. Prefer the default or a modest setting. Lower it if runs keep failing or stalling; use **Resume** after you change it.

---

## If Windows blocks the app

You may see “Windows protected your PC.” Choose **More info**, then **Run anyway**. That’s normal for tools that aren’t signed in the Microsoft Store.

---

**License:** [MIT](LICENSE).

**Developers:** build and architecture notes live in [`dotnet/DEVELOPER.md`](dotnet/DEVELOPER.md).
