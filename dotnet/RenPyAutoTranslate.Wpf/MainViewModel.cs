using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using RenPyAutoTranslate.Core;
using RenPyAutoTranslate.Core.Parallel;
using RenPyAutoTranslate.Core.Paths;
using RenPyAutoTranslate.Core.Renpy;
using RenPyAutoTranslate.Core.Settings;

namespace RenPyAutoTranslate.Wpf;

public partial class MainViewModel : ObservableObject
{
    private readonly TranslationCoordinator _coordinator;
    private readonly ISettingsStore _settingsStore;
    private CancellationTokenSource? _runCts;

    [ObservableProperty] private string _sourceTlPath = "";
    [ObservableProperty] private string _detectedGame = "—";
    [ObservableProperty] private string _outputPreview = "—";
    [ObservableProperty] private string _sourceIso = "en";
    [ObservableProperty] private int _workers = 4;
    [ObservableProperty] private string _lastFile = "";
    [ObservableProperty] private double _progressMaximum = 1;
    [ObservableProperty] private double _progressValue;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private AppTheme _uiTheme = AppTheme.System;
    [ObservableProperty] private string _statusTitle = "Ready to translate";
    [ObservableProperty] private string _statusSubtitle = "";

    public AppTheme[] ThemeOptions { get; } = { AppTheme.System, AppTheme.Light, AppTheme.Dark };

    public ObservableCollection<LanguageOptionViewModel> LanguageOptions { get; } = new();
    public ObservableCollection<LogLineViewModel> LogLines { get; } = new();

    public string ThemeDisplayLabel => UiTheme switch
    {
        AppTheme.Dark => "Dark · Fluent",
        AppTheme.Light => "Light · Fluent",
        _ => "System · Fluent"
    };

    partial void OnUiThemeChanged(AppTheme value)
    {
        ThemeApplier.Apply(value);
        _ = PersistThemeAsync();
        OnPropertyChanged(nameof(ThemeDisplayLabel));
    }

    private async Task PersistThemeAsync()
    {
        var s = await _settingsStore.LoadAsync().ConfigureAwait(true);
        s.Theme = UiTheme;
        await _settingsStore.SaveAsync(s).ConfigureAwait(true);
    }

    public MainViewModel(TranslationCoordinator coordinator, ISettingsStore settingsStore)
    {
        _coordinator = coordinator;
        _settingsStore = settingsStore;
        _ = LoadSettingsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        var s = await _settingsStore.LoadAsync().ConfigureAwait(true);
        SourceTlPath = s.LastSourceTlPath ?? "";
        SourceIso = s.SourceLanguageIso;
        Workers = Math.Clamp(s.Workers, 1, 16);
        UiTheme = s.Theme;
        if (!string.IsNullOrEmpty(SourceTlPath) && Directory.Exists(SourceTlPath))
        {
            try
            {
                DetectedGame = RenpyPaths.GameNameFromTlPath(SourceTlPath);
                OutputPreview = RenpyPaths.OutputTlPath(RenpyPaths.ToolRepoRootFromBaseDirectory(), DetectedGame);
                RefreshLanguageFolders(SourceTlPath);
                ApplySavedLanguageSelection(s);
            }
            catch
            {
                /* ignore invalid saved path */
            }
        }

        UpdateIdleStatus();
    }

    private void LanguageOptionOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LanguageOptionViewModel.IsSelected))
            UpdateIdleStatus();
    }

    private void RefreshLanguageFolders(string tlRoot)
    {
        foreach (var opt in LanguageOptions)
            opt.PropertyChanged -= LanguageOptionOnPropertyChanged;
        LanguageOptions.Clear();
        if (!Directory.Exists(tlRoot))
        {
            UpdateIdleStatus();
            return;
        }

        foreach (var name in Directory.GetDirectories(tlRoot)
                     .Select(Path.GetFileName)
                     .Where(n => !string.IsNullOrEmpty(n) && !n!.StartsWith('.'))
                     .OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            var opt = new LanguageOptionViewModel { FolderName = name! };
            opt.PropertyChanged += LanguageOptionOnPropertyChanged;
            LanguageOptions.Add(opt);
        }

        UpdateIdleStatus();
    }

    private void ApplySavedLanguageSelection(AppSettings s)
    {
        if (s.LastLanguageFolders is { Count: > 0 })
        {
            var want = new HashSet<string>(s.LastLanguageFolders, StringComparer.OrdinalIgnoreCase);
            foreach (var opt in LanguageOptions)
                opt.IsSelected = want.Contains(opt.FolderName);
            return;
        }

        if (string.IsNullOrEmpty(s.LastLanguageFolder))
            return;
        foreach (var opt in LanguageOptions)
            opt.IsSelected = string.Equals(opt.FolderName, s.LastLanguageFolder, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateIdleStatus()
    {
        if (IsBusy)
            return;
        StatusTitle = "Ready to translate";
        var n = LanguageOptions.Count(o => o.IsSelected);
        StatusSubtitle = n == 0 ? "No languages selected" : $"{n} language(s) selected";
    }

    [RelayCommand]
    private void BrowseTl()
    {
        var dlg = new OpenFolderDialog
        {
            Title = "Select source game tl folder (read-only)"
        };
        if (dlg.ShowDialog() != true || string.IsNullOrEmpty(dlg.FolderName))
            return;
        try
        {
            SourceTlPath = dlg.FolderName;
            DetectedGame = RenpyPaths.GameNameFromTlPath(SourceTlPath);
            OutputPreview = RenpyPaths.OutputTlPath(RenpyPaths.ToolRepoRootFromBaseDirectory(), DetectedGame);
            RefreshLanguageFolders(SourceTlPath);
            foreach (var opt in LanguageOptions)
                opt.IsSelected = false;
            if (LanguageOptions.Count > 0)
                LanguageOptions[0].IsSelected = true;
            if (LanguageOptions.Count == 0)
                MessageBox.Show(
                    "This folder has no subfolders. Generate translations in the Ren'Py launcher first.",
                    "No language folders",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Invalid TL folder", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Sync RelayCommands (not async Task) so we use plain <see cref="RelayCommand"/>, not <see cref="AsyncRelayCommand"/>.
    /// AsyncRelayCommand tracks an internal IsRunning flag that can block Execute even when the UI looks enabled.
    /// </summary>
    [RelayCommand]
    private void Translate() => _ = RunAsync(resume: false);

    [RelayCommand]
    private void Resume() => _ = RunAsync(resume: true);

    [RelayCommand]
    private void CancelRun() => _runCts?.Cancel();

    [RelayCommand]
    private void OpenLogsFolder()
    {
        var dir = RenpyPaths.LogsDirectory(RenpyPaths.ToolRepoRootFromBaseDirectory());
        Directory.CreateDirectory(dir);
        Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
    }

    [RelayCommand]
    private void OpenOutputFolder()
    {
        if (string.IsNullOrEmpty(OutputPreview) || OutputPreview == "—")
            return;
        Directory.CreateDirectory(OutputPreview);
        Process.Start(new ProcessStartInfo { FileName = OutputPreview, UseShellExecute = true });
    }

    private static LogLineDisplayKind ClassifyDisplayKind(string level, string line)
    {
        if (string.Equals(level, "ERROR", StringComparison.OrdinalIgnoreCase))
            return LogLineDisplayKind.Error;
        if (line.StartsWith("=====", StringComparison.Ordinal))
            return LogLineDisplayKind.Accent;
        if (line.Contains("FAILED", StringComparison.OrdinalIgnoreCase))
            return LogLineDisplayKind.Error;
        if (line.Contains("] OK ", StringComparison.Ordinal))
            return LogLineDisplayKind.Success;
        if (line.Contains("throttling", StringComparison.OrdinalIgnoreCase)
            || line.Contains("429", StringComparison.Ordinal)
            || line.Contains("spacing requests", StringComparison.OrdinalIgnoreCase))
            return LogLineDisplayKind.Warn;
        return LogLineDisplayKind.Info;
    }

    private void AppendLog(string level, string msg)
    {
        var ts = DateTime.Now.ToString("HH:mm:ss");
        var lines = msg.Replace("\r\n", "\n").Split('\n');
        foreach (var raw in lines)
        {
            var line = raw;
            if (line.Length == 0)
                continue;
            var kind = ClassifyDisplayKind(level, line);
            var levelLabel = level.Trim().ToUpperInvariant();
            if (levelLabel.Length > 5)
                levelLabel = levelLabel[..5];
            AddLogLine(new LogLineViewModel(ts, levelLabel, line, kind));
        }
    }

    private void AddLogLine(LogLineViewModel line)
    {
        var app = Application.Current;
        void Add()
        {
            LogLines.Add(line);
        }

        if (app?.Dispatcher.CheckAccess() == true)
            Add();
        else
            app?.Dispatcher.Invoke(Add, DispatcherPriority.Background);
    }

    private async Task RunAsync(bool resume)
    {
        if (IsBusy)
            return;
        var tlRoot = SourceTlPath.Trim();
        if (string.IsNullOrEmpty(tlRoot) || !Directory.Exists(tlRoot))
        {
            MessageBox.Show("Choose a valid source tl folder.", "Invalid folder", MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        string gameName;
        try
        {
            gameName = RenpyPaths.GameNameFromTlPath(tlRoot);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Invalid TL folder", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        string outputRoot;
        try
        {
            outputRoot = RenpyPaths.OutputTlPath(RenpyPaths.ToolRepoRootFromBaseDirectory(), gameName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Output path", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var selectedOpts = LanguageOptions.Where(x => x.IsSelected).ToList();
        if (selectedOpts.Count == 0)
        {
            MessageBox.Show("Select at least one language folder to translate.", "No language", MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        var source = SourceIso.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(source))
        {
            MessageBox.Show("Enter a source language code (e.g. en).", "Source language", MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        var w = Math.Clamp(Workers, 1, 16);
        Workers = w;

        _runCts = new CancellationTokenSource();
        IsBusy = true;
        LogLines.Clear();
        ProgressValue = 0;
        ProgressMaximum = 1;
        LastFile = "";

        var toolRoot = RenpyPaths.ToolRepoRootFromBaseDirectory();
        var logDir = RenpyPaths.LogsDirectory(toolRoot);
        Directory.CreateDirectory(logDir);
        var sessionLog = Path.Combine(logDir, $"auto_translate_gui_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        await using var logFile = new StreamWriter(sessionLog, false, new System.Text.UTF8Encoding(false));
        var logFileLock = new object();

        void AppendLogFile(string level, string msg)
        {
            var ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var fileLine = $"{ts} | {level,-8} | {msg}\n";
            lock (logFileLock)
            {
                try
                {
                    logFile.Write(fileLine);
                    logFile.Flush();
                }
                catch
                {
                    /* ignore */
                }
            }

            AppendLog(level, msg);
        }

        try
        {
            var langNames = string.Join(", ", selectedOpts.Select(o => o.FolderName));
            AppendLogFile("INFO",
                $"Source TL (read): {tlRoot}\nOutput TL (write): {outputRoot}\nLanguages ({selectedOpts.Count}): {langNames}\nSource ISO: {source}\n---");

            Directory.CreateDirectory(outputRoot);

            var totalLangs = selectedOpts.Count;
            var langIndex = 0;
            var sumSuccess = 0;
            var sumFail = 0;
            var sumTotal = 0;

            foreach (var opt in selectedOpts)
            {
                _runCts.Token.ThrowIfCancellationRequested();
                langIndex++;
                var langFolder = opt.FolderName;

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    StatusTitle = $"• {langFolder} in progress";
                    StatusSubtitle = "…";
                });

                AppendLogFile("INFO", $"===== Language {langIndex}/{totalLangs}: {langFolder} =====");

                var progress = new Progress<TranslationProgress>(p =>
                {
                    var disp = Application.Current?.Dispatcher;
                    if (disp is null)
                        return;
                    disp.Invoke(() =>
                    {
                        StatusTitle = $"• {langFolder} in progress";
                        StatusSubtitle = $"{p.Completed:N0} / {p.Total:N0}";
                        ProgressValue = p.Completed;
                        ProgressMaximum = Math.Max(1, p.Total);
                        LastFile = string.IsNullOrEmpty(p.LastRelativePath)
                            ? "—"
                            : p.LastRelativePath.Replace('\\', '/');
                    });
                });

                var result = await RunSingleLanguageAsync(
                    langFolder,
                    tlRoot,
                    outputRoot,
                    source,
                    resume,
                    w,
                    progress,
                    AppendLogFile,
                    _runCts.Token).ConfigureAwait(true);

                if (result is null)
                    return;

                sumSuccess += result.Value.Success;
                sumFail += result.Value.Failed;
                sumTotal += result.Value.Total;
            }

            AppendLogFile("INFO", "Done (all selected languages).");

            var s = await _settingsStore.LoadAsync().ConfigureAwait(true);
            s.LastSourceTlPath = tlRoot;
            s.LastLanguageFolder = selectedOpts[^1].FolderName;
            s.LastLanguageFolders = selectedOpts.Select(o => o.FolderName).ToList();
            s.SourceLanguageIso = source;
            s.Workers = w;
            s.Theme = UiTheme;
            await _settingsStore.SaveAsync(s).ConfigureAwait(true);

            MessageBox.Show(
                $"Wrote translated files under:\n{outputRoot}\n\nAll languages — translated: {sumSuccess} / {sumTotal}\nFailed: {sumFail} / {sumTotal}\n\nLog saved to:\n{sessionLog}",
                "Finished",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            AppendLogFile("INFO", "Cancelled.");
        }
        catch (Exception ex)
        {
            AppendLogFile("ERROR", ex.ToString());
            MessageBox.Show(
                $"{ex.Message}\n\n(Full details in the log.)\n\nLog file:\n{sessionLog}",
                "Translation error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            LastFile = "";
            ProgressValue = 0;
            ProgressMaximum = 1;
            _runCts?.Dispose();
            _runCts = null;
            UpdateIdleStatus();
        }
    }

    private async Task<RunResult?> RunSingleLanguageAsync(
        string langFolder,
        string tlRoot,
        string outputRoot,
        string source,
        bool resume,
        int workers,
        IProgress<TranslationProgress> progress,
        Action<string, string> appendLog,
        CancellationToken cancellationToken)
    {
        if (!LanguageNames.TryGetIso(langFolder, out var toIso))
        {
            MessageBox.Show(
                $"Language folder \"{langFolder}\" is not supported (pycountry name list).",
                "Unsupported language",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return null;
        }

        var langPath = Path.Combine(tlRoot, langFolder);
        if (!Directory.Exists(langPath))
        {
            MessageBox.Show($"Not found:\n{langPath}", "Missing folder", MessageBoxButton.OK,
                MessageBoxImage.Error);
            return null;
        }

        appendLog("INFO", $"Target ISO: {toIso}");

        var srcLangDir = Path.Combine(tlRoot, langFolder);
        var dstLangDir = Path.Combine(outputRoot, langFolder);

        IReadOnlyList<string> origins;
        if (!resume)
        {
            if (!RenpyPaths.PathsEqualCaseInsensitive(srcLangDir, dstLangDir))
            {
                TlDiscovery.CopyEmptyLanguageTree(srcLangDir, dstLangDir);
                appendLog("INFO", $"Prepared folder structure: {langFolder}");
            }
            else
            {
                appendLog("INFO", $"In-place: writing into {langFolder} (skipping empty-folder clone).");
            }

            origins = TlDiscovery.CollectRpyPathsUnder(srcLangDir).ToList();
            appendLog("INFO", $"Full run: {origins.Count} file(s)");
        }
        else
        {
            origins = TlDiscovery.ListMissingRpyFiles(tlRoot, outputRoot, langFolder).ToList();
            appendLog("INFO", $"Resume: {origins.Count} missing file(s)");
        }

        var tasks = TlDiscovery.BuildTasks(origins, tlRoot, source, toIso);
        var total = tasks.Count;
        if (total > 0)
        {
            ProgressMaximum = total;
            ProgressValue = 0;
        }

        return await _coordinator.RunAsync(
            tasks,
            tlRoot,
            outputRoot,
            workers,
            progress,
            appendLog,
            cancellationToken: cancellationToken).ConfigureAwait(true);
    }
}
