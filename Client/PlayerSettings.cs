using System.Windows.Forms;

namespace GTACoOp
{
    public class PlayerSettings
    {
        public string DisplayName { get; set; }
        public string LastIP { get; set; }
        public int LastPort { get; set; }
        public string LastPassword { get; set; }
        public bool SyncWorld { get; set; }
        public bool SyncTraffic { get; set; }
        public bool OldChat { get; set; }
        public int MaxStreamedNpcs { get; set; }
        public string MasterServerAddress { get; set; }
        public Keys ActivationKey { get; set; }
        public bool HidePasswords { get; set; }
        public bool AutoConnect { get; set; }
        public bool AutoReconnect { get; set; }
        public string AutoLogin { get; set; }
        public bool AutoRegister { get; set; }

        public PlayerSettings()
        {
            DisplayName = string.IsNullOrWhiteSpace(GTA.Game.Player.Name) ? "Player" : GTA.Game.Player.Name;
            MaxStreamedNpcs = 10;
            MasterServerAddress = "http://46.101.1.92/";
            ActivationKey = Keys.F9;
            HidePasswords = false;
            LastIP = "127.0.0.1";
            LastPort = 4499;
            LastPassword = "changeme";
            OldChat = false;
            SyncWorld = true;
            SyncTraffic = false;
            AutoConnect = false;
            AutoReconnect = true;
            AutoLogin = "";
            AutoRegister = false;
        }
    }
}