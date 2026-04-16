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
        app.Resources["WindowBackgroundBrush"] = new SolidColorBrush(dark ? Color.FromRgb(0x25, 0x25, 0x28) : Color.FromRgb(0xFF, 0xFF, 0xFF));
        app.Resources["WindowForegroundBrush"] = new SolidColorBrush(dark ? Color.FromRgb(0xE8, 0xE8, 0xE8) : Color.FromRgb(0x1E, 0x1E, 0x1E));
        app.Resources["TextMutedBrush"] = new SolidColorBrush(dark ? Color.FromRgb(0xAD, 0xAD, 0xAD) : Color.FromRgb(0x55, 0x55, 0x55));
        app.Resources["GroupBoxBorderBrush"] = new SolidColorBrush(dark ? Color.FromRgb(0x3F, 0x3F, 0x46) : Color.FromRgb(0xD0, 0xD0, 0xD0));
        app.Resources["LogBackgroundBrush"] = new SolidColorBrush(dark ? Color.FromRgb(0x1E, 0x1E, 0x1E) : Color.FromRgb(0xFA, 0xFA, 0xFA));
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
