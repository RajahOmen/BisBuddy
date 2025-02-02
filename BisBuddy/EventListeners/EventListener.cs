using System;

namespace BisBuddy.EventListeners
{
    public abstract class EventListener(Plugin plugin) : IEventListener
    {
        protected Plugin Plugin { get; } = plugin;
        public bool IsEnabled { get; private set; } = false;

        public void Dispose()
        {
            try
            {
                dispose();
                Services.Log.Verbose($"\"{GetType().Name}\" disposed");
            }
            catch (Exception ex)
            {
                Services.Log.Fatal(ex, $"Failed to dispose \"{GetType().Name}\"");
            }
        }

        public void registerListeners()
        {
            try
            {
                register();
                IsEnabled = true;
                Services.Log.Verbose($"Registered listener(s) for \"{GetType().Name}\"");
            }
            catch (Exception ex)
            {
                Services.Log.Error(ex, $"Failed to register listener(s) in \"{GetType().Name}\"");
            }
        }

        public void unregisterListeners()
        {
            try
            {
                unregister();
                IsEnabled = false;
                Services.Log.Verbose($"Unregistered listener(s) for \"{GetType().Name}\"");
            }
            catch (Exception ex)
            {
                Services.Log.Error(ex, $"Failed to unregister listener(s) for \"{GetType().Name}\"");
            }
        }

        protected abstract void dispose();
        protected abstract void register();
        protected abstract void unregister();

        public void SetListeningStatus(bool toEnable)
        {
            if (toEnable && !IsEnabled) // If we want to enable and it's not enabled
            {
                registerListeners();
            }
            else if (!toEnable && IsEnabled) // If we want to disable and it's enabled
            {
                unregisterListeners();
            }
            // Otherwise, do nothing
        }
    }
}
