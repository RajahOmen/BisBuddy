using Autofac;
using Autofac.Features.Indexed;
using Autofac.Features.OwnedInstances;
using BisBuddy.Ui.Renderers.Tabs;
using BisBuddy.Ui.Renderers.Tabs.Main;
using BisBuddy.Ui.Windows;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BisBuddy.Services
{
    public class WindowService : IWindowService, IDisposable
    {
        private readonly IUiBuilder uiBuilder;
        private readonly WindowSystem windowSystem;
        private readonly IIndex<WindowType, Func<Window>> windowFuncIndex;
        private readonly Dictionary<WindowType, Window> windows = [];
        private readonly List<Action> disposeActions = [];

        public WindowService(
            IUiBuilder uiBuilder,
            IIndex<WindowType, Func<Window>> windowIndex,
            WindowSystem windowSystem
            )
        {
            this.uiBuilder = uiBuilder;
            this.windowSystem = windowSystem;
            this.windowFuncIndex = windowIndex;
        }

        public void Dispose()
        {
            unregister();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                foreach (var type in Enum.GetValues<WindowType>())
                {
                    if (!windowFuncIndex.TryGetValue(type, out var windowFunc))
                        throw new ArgumentException($"Window type {Enum.GetName(type)} not registered");

                    var window = windowFunc();
                    windowSystem.AddWindow(window);
                    windows[type] = window;
                }

                var mainWindow = windows[WindowType.Main];
                var configWindow = windows[WindowType.Config];

                uiBuilder.OpenMainUi += mainWindow.Toggle;
                disposeActions.Add(() => uiBuilder.OpenMainUi -= mainWindow.Toggle);

                uiBuilder.OpenConfigUi += configWindow.Toggle;
                disposeActions.Add(() => uiBuilder.OpenConfigUi -= configWindow.Toggle);

                uiBuilder.Draw += windowSystem.Draw;
                disposeActions.Add(() => uiBuilder.Draw -= windowSystem.Draw);

                return Task.CompletedTask;
            }
            catch
            {
                unregister();
                throw;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            unregister();
            return Task.CompletedTask;
        }

        private void unregister()
        {
            var exceptions = new List<Exception>();
            foreach (var action in disposeActions)
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }

            disposeActions.Clear();
            if (exceptions.Count != 0)
                throw new AggregateException("One or more errors occurred during unregistering windowservice.", exceptions);
        }

        public void ToggleWindow(WindowType windowType)
        {
            if (!windows.TryGetValue(windowType, out var window))
                throw new ArgumentException($"Window type {Enum.GetName(windowType)} not registered");

            window.Toggle();
        }

        public void SetWindowOpenState(WindowType windowType, bool state)
        {
            if (!windows.TryGetValue(windowType, out var window))
                throw new ArgumentException($"Window type {Enum.GetName(windowType)} not registered");

            window.IsOpen = state;
        }

        public bool IsWindowOpen(WindowType windowType)
        {
            if (!windows.TryGetValue(windowType, out var window))
                throw new ArgumentException($"Window type {Enum.GetName(windowType)} not registered");
            return window.IsOpen;
        }

        public void SetMainWindowTab(MainWindowTab tabToOpen, TabState? tabState = null)
        {
            if (!windows.TryGetValue(WindowType.Main, out var window))
                throw new ArgumentException($"Window type {Enum.GetName(WindowType.Main)} not registered");

            if (window is not MainWindow mainWindow)
                throw new InvalidOperationException("Registered main window is not of type MainWindow");

            mainWindow.SetNextActiveTab(tabToOpen, tabState);
        }
    }

    public interface IWindowService : IHostedService
    {
        public void ToggleWindow(WindowType windowType);
        public void SetWindowOpenState(WindowType windowType, bool open);
        public bool IsWindowOpen(WindowType windowType);
        public void SetMainWindowTab(MainWindowTab tabToOpen, TabState? tabState = null);
    }
}
