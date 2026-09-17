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
            app.Resources["WindowBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0x18, 0x19, 0x1D));
            app.Resources["WindowForegroundBrush"] = new SolidColorBrush(Color.FromRgb(0xF1, 0xF1, 0xF3));
            app.Resources["TextMutedBrush"] = new SolidColorBrush(Color.FromRgb(0xA2, 0xA4, 0xAC));
            app.Resources["GroupBoxBorderBrush"] = new SolidColorBrush(Color.FromRgb(0x37, 0x39, 0x40));
            app.Resources["LogBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0x14, 0x15, 0x18));
            app.Resources["CardBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0x22, 0x23, 0x28));
            app.Resources["CardBorderBrush"] = new SolidColorBrush(Color.FromRgb(0x31, 0x33, 0x39));
            app.Resources["ProgressTrackBrush"] = new SolidColorBrush(Color.FromRgb(0x34, 0x36, 0x3D));
            app.Resources["AccentBrush"] = new SolidColorBrush(Color.FromRgb(0x7E, 0x9C, 0xE8));
            app.Resources["AccentBrushHover"] = new SolidColorBrush(Color.FromRgb(0x93, 0xAE, 0xF2));
            app.Resources["AccentOnAccentBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            app.Resources["DangerBrush"] = new SolidColorBrush(Color.FromRgb(0xEF, 0x76, 0x76));
            app.Resources["LogTimestampBrush"] = new SolidColorBrush(Color.FromRgb(0x8A, 0xA2, 0xD8));
            app.Resources["LogInfoBrush"] = new SolidColorBrush(Color.FromRgb(0xD8, 0xD9, 0xDE));
            app.Resources["LogWarnBrush"] = new SolidColorBrush(Color.FromRgb(0xE7, 0xB5, 0x52));
            app.Resources["LogErrorBrush"] = new SolidColorBrush(Color.FromRgb(0xFC, 0x8A, 0x8A));
            app.Resources["LogSuccessBrush"] = new SolidColorBrush(Color.FromRgb(0x6E, 0xE7, 0x9A));
            app.Resources["LogAccentBrush"] = new SolidColorBrush(Color.FromRgb(0x7E, 0x9C, 0xE8));
        }
        else
        {
            app.Resources["WindowBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0xF7, 0xF7, 0xF8));
            app.Resources["WindowForegroundBrush"] = new SolidColorBrush(Color.FromRgb(0x1B, 0x1D, 0x22));
            app.Resources["TextMutedBrush"] = new SolidColorBrush(Color.FromRgb(0x6B, 0x70, 0x7C));
            app.Resources["GroupBoxBorderBrush"] = new SolidColorBrush(Color.FromRgb(0xD9, 0xDB, 0xE0));
            app.Resources["LogBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            app.Resources["CardBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            app.Resources["CardBorderBrush"] = new SolidColorBrush(Color.FromRgb(0xE1, 0xE2, 0xE6));
            app.Resources["ProgressTrackBrush"] = new SolidColorBrush(Color.FromRgb(0xE6, 0xE8, 0xED));
            app.Resources["AccentBrush"] = new SolidColorBrush(Color.FromRgb(0x4F, 0x6F, 0xC9));
            app.Resources["AccentBrushHover"] = new SolidColorBrush(Color.FromRgb(0x3F, 0x60, 0xBA));
            app.Resources["AccentOnAccentBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            app.Resources["DangerBrush"] = new SolidColorBrush(Color.FromRgb(0xB9, 0x1C, 0x1C));
            app.Resources["LogTimestampBrush"] = new SolidColorBrush(Color.FromRgb(0x3B, 0x5B, 0x8C));
            app.Resources["LogInfoBrush"] = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x30));
            app.Resources["LogWarnBrush"] = new SolidColorBrush(Color.FromRgb(0x9A, 0x63, 0x08));
            app.Resources["LogErrorBrush"] = new SolidColorBrush(Color.FromRgb(0xB9, 0x1C, 0x1C));
            app.Resources["LogSuccessBrush"] = new SolidColorBrush(Color.FromRgb(0x16, 0x7A, 0x3A));
            app.Resources["LogAccentBrush"] = new SolidColorBrush(Color.FromRgb(0x4F, 0x6F, 0xC9));
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
