using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using RenPyAutoTranslate.Core;
using RenPyAutoTranslate.Core.Paths;
using RenPyAutoTranslate.Core.Settings;
using Serilog;

namespace RenPyAutoTranslate.Wpf;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        services.AddRenpyCoreServices();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        Services = services.BuildServiceProvider();

        var settingsStore = Services.GetRequiredService<ISettingsStore>();
        var settings = settingsStore.LoadAsync().GetAwaiter().GetResult();
        ThemeApplier.Apply(settings.Theme);

        var toolRoot = RenpyPaths.ToolRepoRootFromBaseDirectory();
        var logDir = RenpyPaths.LogsDirectory(toolRoot);
        Directory.CreateDirectory(logDir);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(logDir, "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14)
            .CreateLogger();

        var main = Services.GetRequiredService<MainWindow>();
        main.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
