using BisBuddy.Resources;
using Dalamud.Plugin;
using Microsoft.Extensions.Hosting;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace BisBuddy.Services
{
    public delegate void LanguageChangeDelegate(string langCode);

    public class LanguageService(
        ITypedLogger<LanguageService> logger,
        IDalamudPluginInterface pluginInterface
        ) : ILanguageService
    {
        private readonly ITypedLogger<LanguageService> logger = logger;
        private readonly IDalamudPluginInterface pluginInterface = pluginInterface;

        public event LanguageChangeDelegate? OnLanguageChange;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            setLanguage(pluginInterface.UiLanguage);
            pluginInterface.LanguageChanged += handleLanguageChange;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            pluginInterface.LanguageChanged -= handleLanguageChange;
            return Task.CompletedTask;
        }

        private void setLanguage(string langCode)
        {
            var newCultureInfo = new CultureInfo(langCode);
            logger.Info($"Setting plugin language to \"{newCultureInfo}\"");
            Resource.Culture = newCultureInfo;
        }

        private void handleLanguageChange(string langCode)
        {
            setLanguage(langCode);
            OnLanguageChange?.Invoke(langCode);
        }
    }

    public interface ILanguageService : IHostedService
    {
        public event LanguageChangeDelegate? OnLanguageChange;
    }
}
