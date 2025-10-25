using BisBuddy.Services.Configuration;
using Dalamud.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BisBuddy.Services
{
    public class DebugService(IConfigurationService configurationService) : IDebugService
    {
        private readonly IConfigurationService configurationService = configurationService;

        public void DebugAssertMainThread()
        {
            if (!configurationService.DebugFrameworkAsserts)
                return;

            ThreadSafety.AssertMainThread();
        }
    }

    public interface IDebugService
    {
        public void DebugAssertMainThread();
    }
}
