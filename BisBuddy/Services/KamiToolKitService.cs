using Dalamud.Plugin;
using KamiToolKit;
using System;
using System.Collections.Generic;
using System.Text;

namespace BisBuddy.Services;

public class KamiToolKitService : IDisposable
{
    public KamiToolKitService(IDalamudPluginInterface pluginInterface)
    {
        KamiToolKitLibrary.Initialize(pluginInterface);
    }

    public void Dispose()
    {
        KamiToolKitLibrary.Dispose();
    }
}
