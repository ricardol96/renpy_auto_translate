using Microsoft.Extensions.DependencyInjection;
using RenPyAutoTranslate.Core.Parallel;
using RenPyAutoTranslate.Core.Renpy;
using RenPyAutoTranslate.Core.Settings;
using RenPyAutoTranslate.Core.Translation;

namespace RenPyAutoTranslate.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddRenpyCoreServices(this IServiceCollection services)
    {
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<ITranslationProvider>(_ =>
            new CachingTranslationProvider(new GoogleGtxTranslationProvider()));
        services.AddSingleton<RenpyFileTranslator>();
        services.AddSingleton<TranslationCoordinator>();
        return services;
    }
}
