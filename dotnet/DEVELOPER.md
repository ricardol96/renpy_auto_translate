# Ren’Py Auto Translate — developer notes

Technical reference for working in the **`dotnet`** solution. End-user documentation stays in the repository **README** at the project root.

---

## Requirements

- **.NET 8 SDK** (Windows; WPF targets `net8.0-windows`).
- **Visual Studio 2022** or **VS Code** / terminal builds are fine.

---

## Solution layout

| Project | Role |
|--------|------|
| **RenPyAutoTranslate.Core** | Translation providers, `.rpy` parsing and rewriting, parallelism, paths, JSON settings. |
| **RenPyAutoTranslate.Wpf** | WPF UI (`MainWindow`), `MainViewModel`, DI bootstrap, theming. |
| **RenPyAutoTranslate.Core.Tests** | xUnit tests against **Core** (no UI tests). |

Entry point: `RenPyAutoTranslate.Wpf` (`App.xaml` → `MainWindow`). Core registers services via `DependencyInjection.AddRenpyCoreServices` (`GoogleGtxTranslationProvider` behind `CachingTranslationProvider`, `TranslationCoordinator`, `RenpyFileTranslator`).

---

## Build and test

From the **`dotnet`** directory:

```powershell
dotnet build .\RenPyAutoTranslate.sln -c Release
dotnet test .\RenPyAutoTranslate.Core.Tests\RenPyAutoTranslate.Core.Tests.csproj -c Release
```

Run the UI without publishing:

```powershell
dotnet run --project .\RenPyAutoTranslate.Wpf\RenPyAutoTranslate.Wpf.csproj -c Release
```

---

## Publish (release binary)

### Default: self-contained (`publish.ps1`)

**`publish.ps1`** produces a **single-file, self-contained `win-x64`** executable. The bundle embeds the .NET runtime, so the file is large (often well over 100 MB before compression). **`EnableCompressionInSingleFile=true`** is enabled to shrink the on-disk size (self-contained only; see [single-file bundling](https://learn.microsoft.com/dotnet/core/deploying/single-file/overview)).

Output is written to **`PublishDir`** in `RenPyAutoTranslate.Wpf.csproj` (**repository root**). The script strips symbols and removes extra `RenPyAutoTranslate.*` artifacts so only **`RenPyAutoTranslate.exe`** remains; it also deletes a legacy **`publish\`** folder if present.

Close a running instance of the app before publishing, or MSBuild may fail copying or overwriting the output.

### Smaller EXE: framework-dependent (`publish-framework-dependent.ps1`)

For a **much smaller** `RenPyAutoTranslate.exe` (typically a few MB or less—suitable for GitHub without LFS), use **`publish-framework-dependent.ps1`**. It publishes **`--self-contained false`**: the [.NET 8 **Desktop** Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/8.0) must be installed on the target PC (“run desktop apps”). Compression is **not** applied here because the SDK only supports bundle compression for self-contained apps (`NETSDK1176`).

Distribute the runtime installer alongside your build if your users may not have .NET installed.

### Git and large binaries

Do **not** commit the self-contained `.exe` to Git: it often exceeds GitHub’s **100 MB** per-file limit. Keep `/RenPyAutoTranslate.exe` gitignored, use **GitHub Releases**, **Git LFS**, or ship the **framework-dependent** build instead. If a large file was already pushed, remove it from history (e.g. [`git filter-repo`](https://github.com/newren/git-filter-repo) or BFG) before pushing again.

---

## Runtime paths

- **`RenpyPaths.ToolRepoRootFromBaseDirectory()`** uses `AppContext.BaseDirectory` (the folder containing the running assembly / extracted single-file host). Logs and per-game output live **next to the executable**.
- **Output:** `{toolRoot}\{GameName}\game\tl\{language}\…` where `GameName` is inferred from `…/game/tl` by walking up the path.
- **Settings:** `%AppData%\RenPyAutoTranslate\settings.json` via `JsonSettingsStore`.

---

## Translation pipeline (short)

1. **Discovery:** `TlDiscovery` collects `.rpy` paths and builds `TranslationTask` list (source/target ISO from folder names via `LanguageNames` / embedded data).
2. **Rewrite:** `RenpyFileTranslator` scans lines; quoted dialogue is sent through **`ITranslationProvider`**.
3. **Placeholders:** `RenpyInterpolationProtector` masks balanced `[…]` segments before translation so variable interpolations are not translated.
4. **Parallelism:** `TranslationCoordinator` uses `Parallel.ForEachAsync` with an **adaptive rate limiter** and retries on transient HTTP / rate-limit style failures.
5. **Provider:** Default implementation calls Google’s public **gtx**-style translate endpoint (`GoogleGtxTranslationProvider`), cached by `CachingTranslationProvider`.

---

## UI behaviour (WPF)

- **Workers** in the UI is clamped **1–16** (`MainViewModel`); the coordinator further clamps to **1–32** internally.
- **Multiple languages:** checkbox list; selected folders are processed **sequentially** in UI list order. Settings persist `LastLanguageFolders` (and legacy `LastLanguageFolder`).

---

## Dependencies (NuGet)

- **RenPyAutoTranslate.Core:** `Microsoft.Extensions.DependencyInjection` (+ Abstractions), `Microsoft.Extensions.Logging.Abstractions`. Translation uses **HttpClient** in `GoogleGtxTranslationProvider`.
- **RenPyAutoTranslate.Wpf:** Core transitively, plus **CommunityToolkit.Mvvm**, **Serilog**, **Serilog.Sinks.File** (rolling file under `logs` next to the exe).

---

## Git

The repo root **`.gitignore`** ignores **`/RenPyAutoTranslate.exe`** and normal **`bin`/`obj`** outputs so local publish artifacts are not committed by mistake.

## License

This project is released under the **MIT License** — see [`../LICENSE`](../LICENSE) at the repository root. Add your name to the copyright line there if you maintain a fork or release.
