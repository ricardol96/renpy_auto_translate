using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using RenPyAutoTranslate.Core.Settings;

namespace RenPyAutoTranslate.Wpf;

/// <summary>Light/dark without external UI packages (avoids NuGet resolving "Wpf.Ui" to the wrong deprecated "WPF.UI" package).</summary>
internal static class ThemeApplier
{
    public static void Apply(AppTheme theme)
    {
        var dark = theme switch
        {
            AppTheme.Light => false,
            AppTheme.Dark => true,
            _ => IsWindowsAppsDark()
        };

        var app = Application.Current;
        if (dark)
        {
            app.Resources["WindowBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x22));
            app.Resources["WindowForegroundBrush"] = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xEA));
            app.Resources["TextMutedBrush"] = new SolidColorBrush(Color.FromRgb(0xA8, 0xA8, 0xB0));
            app.Resources["GroupBoxBorderBrush"] = new SolidColorBrush(Color.FromRgb(0x48, 0x48, 0x52));
            app.Resources["LogBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x1A));
            app.Resources["CardBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x32));
            app.Resources["CardBorderBrush"] = new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x4A));
            app.Resources["ProgressTrackBrush"] = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x44));
            app.Resources["AccentBrush"] = new SolidColorBrush(Color.FromRgb(0xD4, 0xA8, 0x34));
            app.Resources["AccentBrushHover"] = new SolidColorBrush(Color.FromRgb(0xE4, 0xBC, 0x4A));
            app.Resources["AccentOnAccentBrush"] = new SolidColorBrush(Color.FromRgb(0x1A, 0x14, 0x08));
            app.Resources["DangerBrush"] = new SolidColorBrush(Color.FromRgb(0xF0, 0x6B, 0x6B));
            app.Resources["LogTimestampBrush"] = new SolidColorBrush(Color.FromRgb(0x8A, 0x9E, 0xB8));
            app.Resources["LogInfoBrush"] = new SolidColorBrush(Color.FromRgb(0xD8, 0xD8, 0xDC));
            app.Resources["LogWarnBrush"] = new SolidColorBrush(Color.FromRgb(0xF5, 0xC2, 0x4C));
            app.Resources["LogErrorBrush"] = new SolidColorBrush(Color.FromRgb(0xFC, 0x8A, 0x8A));
            app.Resources["LogSuccessBrush"] = new SolidColorBrush(Color.FromRgb(0x6E, 0xE7, 0x9A));
            app.Resources["LogAccentBrush"] = new SolidColorBrush(Color.FromRgb(0xD4, 0xA8, 0x34));
        }
        else
        {
            app.Resources["WindowBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF7));
            app.Resources["WindowForegroundBrush"] = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x22));
            app.Resources["TextMutedBrush"] = new SolidColorBrush(Color.FromRgb(0x5C, 0x5C, 0x66));
            app.Resources["GroupBoxBorderBrush"] = new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xD0));
            app.Resources["LogBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            app.Resources["CardBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            app.Resources["CardBorderBrush"] = new SolidColorBrush(Color.FromRgb(0xD8, 0xD8, 0xE0));
            app.Resources["ProgressTrackBrush"] = new SolidColorBrush(Color.FromRgb(0xE2, 0xE2, 0xEA));
            app.Resources["AccentBrush"] = new SolidColorBrush(Color.FromRgb(0xB8, 0x7A, 0x14));
            app.Resources["AccentBrushHover"] = new SolidColorBrush(Color.FromRgb(0xC9, 0x8E, 0x1C));
            app.Resources["AccentOnAccentBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            app.Resources["DangerBrush"] = new SolidColorBrush(Color.FromRgb(0xC4, 0x2B, 0x2B));
            app.Resources["LogTimestampBrush"] = new SolidColorBrush(Color.FromRgb(0x3B, 0x5B, 0x8C));
            app.Resources["LogInfoBrush"] = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x30));
            app.Resources["LogWarnBrush"] = new SolidColorBrush(Color.FromRgb(0xA1, 0x62, 0x07));
            app.Resources["LogErrorBrush"] = new SolidColorBrush(Color.FromRgb(0xB9, 0x1C, 0x1C));
            app.Resources["LogSuccessBrush"] = new SolidColorBrush(Color.FromRgb(0x16, 0x7A, 0x3A));
            app.Resources["LogAccentBrush"] = new SolidColorBrush(Color.FromRgb(0x8A, 0x5A, 0x0E));
        }
    }

    private static bool IsWindowsAppsDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var v = key?.GetValue("AppsUseLightTheme");
            return v is int i && i == 0;
        }
        catch
        {
            return false;
        }
    }
}
