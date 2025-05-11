using System;

namespace BisBuddy.EventListeners
{
    public class LoginLoadEventListener : EventListener
    {
        public LoginLoadEventListener(Plugin plugin) : base(plugin)
        {
            Services.ClientState.Login += handleLogin; // for keeping track of the player content id
            Services.ClientState.Logout += handleLogout;

            if (Services.ClientState.IsLoggedIn)
            {
                handleLogin();
            }

            if (Plugin.Configuration.AutoScanInventory)
            {
                register();
                if (Services.ClientState.IsLoggedIn)
                {
                    Services.Log.Verbose($"Initialized while logged in. Initiating inventory scan.");
                    handleAutoCompleteItems();
                }
            }
        }

        protected override void register()
        {
            Services.ClientState.Login += handleAutoCompleteItems;
        }

        protected override void unregister()
        {
            Services.ClientState.Login -= handleAutoCompleteItems;
        }

        protected override void dispose()
        {
            // only unregister logout when disposing. Keep active otherwise
            Services.ClientState.Login -= handleLogin;
            Services.ClientState.Logout -= handleLogout;

            if (IsEnabled) unregister();
        }

        private void handleAutoCompleteItems()
        {
            try
            {
                Plugin.ScheduleUpdateFromInventory(Plugin.Gearsets);
            }
            catch (Exception ex)
            {
                Services.Log.Error(ex, "Failed to handle AutoCompleteItems");
            }

        }

        private void handleLogin()
        {
            Services.Log.Verbose($"[Login] Updating player content id to {Services.ClientState.LocalContentId}");
            Plugin.PlayerContentId = Services.ClientState.LocalContentId;
        }

        private void handleLogout(int type, int code)
        {
            Services.Log.Verbose($"[Logout] Updating player content id to {Services.ClientState.LocalContentId}");
            Plugin.PlayerContentId = 0;
        }
    }
}
