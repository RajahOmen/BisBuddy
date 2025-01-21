using System;

namespace BisBuddy.EventListeners
{
    internal interface IEventListener : IDisposable
    {
        protected bool IsEnabled { get; }

        internal void SetListeningStatus(bool toEnable);

        protected void registerListeners();

        protected void unregisterListeners();
    }
}
