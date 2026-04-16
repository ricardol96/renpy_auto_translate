namespace RenPyAutoTranslate.Core.Settings;

public enum AppTheme
{
    System,
    Light,
    Dark
}

public sealed class AppSettings
{
    public string? LastSourceTlPath { get; set; }
    /// <summary>Legacy single selection; used when <see cref="LastLanguageFolders"/> is absent.</summary>
    public string? LastLanguageFolder { get; set; }
    /// <summary>Selected language folder names (order preserved).</summary>
    public List<string>? LastLanguageFolders { get; set; }
    public string SourceLanguageIso { get; set; } = "en";
    public int Workers { get; set; } = 4;
    public AppTheme Theme { get; set; } = AppTheme.System;
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
}
