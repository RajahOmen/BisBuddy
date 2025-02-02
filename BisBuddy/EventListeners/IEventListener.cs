using System;

namespace BisBuddy.EventListeners
{
    public interface IEventListener : IDisposable
    {
        protected bool IsEnabled { get; }

        public void SetListeningStatus(bool toEnable);

        protected void registerListeners();

        protected void unregisterListeners();
    }
}
