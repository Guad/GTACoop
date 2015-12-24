using System.Windows.Forms;

namespace GTAServer
{
    public class PlayerSettings
    {
        public string DisplayName { get; set; }
        public int MaxStreamedNpcs { get; set; }
        public string MasterServerAddress { get; set; }
        public Keys ActivationKey { get; set; }

        public PlayerSettings()
        {
            DisplayName = string.IsNullOrWhiteSpace(GTA.Game.Player.Name) ? "Player" : GTA.Game.Player.Name;
            MaxStreamedNpcs = 10;
            MasterServerAddress = "http://46.101.1.92/";
            ActivationKey = Keys.F9;
        }
    }
}