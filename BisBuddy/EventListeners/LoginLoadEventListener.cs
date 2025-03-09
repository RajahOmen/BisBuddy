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
                    Plugin.ScheduleUpdateFromInventory(Plugin.Gearsets); // scan on initialization
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
                if (!Services.ClientState.IsLoggedIn)
                    throw new Exception("Auto complete item event triggered while logged out");

                Plugin.ScheduleUpdateFromInventory(Plugin.Gearsets);
            }
            catch (Exception ex)
            {
                Services.Log.Error(ex, "Failed to handle AutoCompleteItems");
            }

        }

        private void handleLogin()
        {
            try
            {
                if (!Services.ClientState.IsLoggedIn)
                    throw new Exception("Login event triggered while logged out");

                Services.Log.Verbose($"Updating player content id to {Services.ClientState.LocalContentId}");

                Plugin.PlayerContentId = Services.ClientState.LocalContentId;
            }
            catch (Exception ex)
            {
                Services.Log.Error(ex, "Failed to handle Login");
            }
        }

        private void handleLogout(int type, int code)
        {
            try
            {
                if (Services.ClientState.IsLoggedIn)
                    throw new Exception("Logout event triggered while logged in");

                Plugin.PlayerContentId = 0;
            }
            catch (Exception ex)
            {
                Services.Log.Error(ex, "Failed to handle Logout");
            }
        }
    }
}
