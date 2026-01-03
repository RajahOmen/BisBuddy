using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BisBuddy.Services.Gearsets;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Microsoft.Extensions.Hosting;

namespace BisBuddy.Services.IPC;

public class BisBuddyIpcService : IHostedService
{
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly IGearsetsService _gearsetsService;

    private ICallGateProvider<bool>? _isInitialized;
    private ICallGateProvider<bool, bool>? _initialized;
    private ICallGateProvider<List<uint>>? _getBisItems;
    private ICallGateProvider<List<uint>, bool>? _bisItemsChanged;

    public BisBuddyIpcService(IDalamudPluginInterface pluginInterface, IGearsetsService gearsetsService)
    {
        _pluginInterface = pluginInterface;
        _gearsetsService = gearsetsService;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _isInitialized = _pluginInterface.GetIpcProvider<bool>("BisBuddy.IsInitialized");
        _isInitialized.RegisterFunc(() => true);

        _getBisItems = _pluginInterface.GetIpcProvider<List<uint>>("BisBuddy.GetBisItems");
        _getBisItems.RegisterFunc(GetBisItemsInternal);

        _bisItemsChanged = _pluginInterface.GetIpcProvider<List<uint>, bool>("BisBuddy.BisItemsChanged");

        _initialized = _pluginInterface.GetIpcProvider<bool, bool>("BisBuddy.Initialized");

        _gearsetsService.OnGearsetsChange += OnGearsetsChanged;

        _initialized.SendMessage(true);

        return Task.CompletedTask;
    }

    private List<uint> GetBisItemsInternal()
    {
        return _gearsetsService.AllItemRequirements.Keys.ToList();
    }

    private void OnGearsetsChanged()
    {
        _bisItemsChanged?.SendMessage(GetBisItemsInternal());
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _gearsetsService.OnGearsetsChange -= OnGearsetsChanged;
        _isInitialized?.UnregisterFunc();
        _getBisItems?.UnregisterFunc();
        _initialized?.SendMessage(false);
        return Task.CompletedTask;
    }
}
