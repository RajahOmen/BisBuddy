using BisBuddy.Services.Configuration;
using Dalamud.Utility;

namespace BisBuddy.Services
{
    public class DebugService(
        ITypedLogger<DebugService> logger,
        IConfigurationService configurationService
        ) : IDebugService
    {
        private readonly ITypedLogger<DebugService> logger = logger;
        private readonly IConfigurationService configurationService = configurationService;

        public void AssertMainThreadDebug()
        {
            if (!configurationService.DebugFrameworkAsserts)
                return;

            ThreadSafety.AssertMainThread();
        }
    }

    public interface IDebugService
    {
        public void AssertMainThreadDebug();
    }
}
