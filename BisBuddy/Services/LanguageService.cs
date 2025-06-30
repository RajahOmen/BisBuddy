using BisBuddy.Resources;
using Dalamud.Plugin;
using Microsoft.Extensions.Hosting;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace BisBuddy.Services
{
    public class LanguageService(
        ITypedLogger<LanguageService> logger,
        IDalamudPluginInterface pluginInterface
        ) : ILanguageService
    {
        private readonly ITypedLogger<LanguageService> logger = logger;
        private readonly IDalamudPluginInterface pluginInterface = pluginInterface;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            handleLanguageChange(pluginInterface.UiLanguage);
            pluginInterface.LanguageChanged += handleLanguageChange;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            pluginInterface.LanguageChanged -= handleLanguageChange;
            return Task.CompletedTask;
        }

        private void handleLanguageChange(string langCode)
        {
            var newCultureInfo = new CultureInfo(langCode);
            logger.Info($"Setting plugin language to \"{newCultureInfo}\"");
            Resource.Culture = newCultureInfo;
        }
    }

    public interface ILanguageService : IHostedService
    {

    }
}
