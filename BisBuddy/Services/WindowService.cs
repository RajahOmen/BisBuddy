using Autofac;
using Autofac.Features.Indexed;
using BisBuddy.Ui;
using BisBuddy.Ui.Config;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BisBuddy.Services
{
    public class WindowService : IWindowService
    {
        private readonly IUiBuilder uiBuilder;
        private readonly WindowSystem windowSystem;
        private readonly IIndex<WindowType, Window> windowIndex;
        private readonly MainWindow mainWindow;
        private readonly ConfigWindow configWindow;
        private readonly ImportGearsetWindow importGearsetWindow;
        private readonly MeldPlanSelectorWindow meldPlanSelectorWindow;

        public WindowService(
            IUiBuilder uiBuilder,
            IComponentContext componentContext,
            IIndex<WindowType, Window> windowIndex,
            IEnumerable<Window> windows,
            MainWindow mainWindow,
            ConfigWindow configWindow,
            ImportGearsetWindow importGearsetWindow,
            MeldPlanSelectorWindow meldPlanSelectorWindow
            )
        {
            this.uiBuilder = uiBuilder;
            this.windowIndex = windowIndex;
            this.mainWindow = mainWindow;
            this.configWindow = configWindow;
            this.importGearsetWindow = importGearsetWindow;
            this.meldPlanSelectorWindow = meldPlanSelectorWindow;

            windowSystem = new WindowSystem();
            foreach (var window in windows)
                windowSystem.AddWindow(window);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            uiBuilder.Draw += windowSystem.Draw;

            uiBuilder.OpenMainUi += mainWindow.Toggle;
            uiBuilder.OpenConfigUi += configWindow.Toggle;

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            uiBuilder.Draw -= windowSystem.Draw;

            uiBuilder.OpenMainUi -= mainWindow.Toggle;
            uiBuilder.OpenConfigUi -= configWindow.Toggle;

            return Task.CompletedTask;
        }

        public void ToggleWindow(WindowType windowType)
        {
            if (!windowIndex.TryGetValue(windowType, out var window))
                throw new ArgumentException($"Window type {Enum.GetName(windowType)} not registered");

            window.Toggle();
        }

        public void SetWindowState(WindowType windowType, bool state)
        {
            if (!windowIndex.TryGetValue(windowType, out var window))
                throw new ArgumentException($"Window type {Enum.GetName(windowType)} not registered");

            window.IsOpen = state;
        }
    }

    public interface IWindowService : IHostedService
    {
        public void ToggleWindow(WindowType windowType);
        public void SetWindowState(WindowType windowType, bool open);
    }
}
