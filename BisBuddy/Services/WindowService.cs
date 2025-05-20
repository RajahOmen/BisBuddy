using Autofac;
using BisBuddy.Windows;
using BisBuddy.Windows.ConfigWindow;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
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
        private readonly MainWindow mainWindow;
        private readonly ConfigWindow configWindow;
        private readonly ImportGearsetWindow importGearsetWindow;
        private readonly MeldPlanSelectorWindow meldPlanSelectorWindow;

        public WindowService(
            IUiBuilder uiBuilder,
            IComponentContext componentContext,
            IEnumerable<Window> windows,
            MainWindow mainWindow,
            ConfigWindow configWindow,
            ImportGearsetWindow importGearsetWindow,
            MeldPlanSelectorWindow meldPlanSelectorWindow
            )
        {
            this.uiBuilder = uiBuilder;
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

        public void ToggleMainWindow() =>
            mainWindow.Toggle();

        public void ToggleConfigWindow() =>
            configWindow.Toggle();

        public void ToggleImportGearsetWindow() =>
            importGearsetWindow.Toggle();

        public void ToggleMeldPlanSelectorWindow() =>
            meldPlanSelectorWindow.Toggle();
    }

    public interface IWindowService : IHostedService
    {
        public void ToggleMainWindow();
        public void ToggleConfigWindow();
        public void ToggleImportGearsetWindow();
        public void ToggleMeldPlanSelectorWindow();
    }
}
