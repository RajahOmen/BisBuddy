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
            switch (configurationService.DebugFrameworkThreadBehavior)
            {
                case FrameworkThreadBehaviorType.Warning:
                    if (!ThreadSafety.IsMainThread)
                    {
                        var stackTrace = new System.Diagnostics.StackTrace(fNeedFileInfo: true, skipFrames: 1);
                        logger.Warning($"Not on main thread!\n{stackTrace}");
                    }
                    break;
                case FrameworkThreadBehaviorType.Assert:
                    ThreadSafety.AssertMainThread();
                    break;
                default:
                    break;
            }
        }
    }

    public interface IDebugService
    {
        public void AssertMainThreadDebug();
    }
}
