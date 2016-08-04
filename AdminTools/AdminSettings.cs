namespace AdminTools
{
    public class AdminSettings
    {
        public bool Debug { get; set; }
        public bool KickOnDefaultNickName { get; set; }
        public bool KickOnNameDifference { get; set; }
        public bool SocialClubOnly { get; set; }
        public string MOTD { get; set; }
        public int MaxPing { get; set; }
        public bool AntiClones { get; set; }
        public bool KickOnDifferentScript { get; set; }
        public GTAServer.ScriptVersion NeededScriptVersion { get; set; }
        public bool KickOnOutdatedGame { get; set; }
        public int MinGameVersion { get; set; }
        public bool ColoredNicknames { get; set; }
        public bool OnlyAsciiNickName { get; set; }
        public bool OnlyAsciiUserName { get; set; }
        public bool LimitNickNames { get; set; }
        public string CountryRestriction { get; set; }
        public string ProtectedNickname { get; set; }
        public string ProtectedNicknameIP { get; set; }

        /*public bool WhiteListEnabled { get; set; }
public string[] WhiteList { get; set; }
public bool BlackListEnabled { get; set; }
public string[] BlackList { get; set; }*/

        public AdminSettings()
        {
            Debug = false;
            KickOnDefaultNickName = true;
            KickOnNameDifference = false;
            SocialClubOnly = false;
            MOTD = "Welcome to this GTA 5 Co-op Server! Max Ping: 250";
            MaxPing = 250;
            AntiClones = true;
            KickOnDifferentScript = true;
            NeededScriptVersion = GTAServer.ScriptVersion.VERSION_0_9_3;
            KickOnOutdatedGame = false;
            MinGameVersion = 25;
            ColoredNicknames = true;
            OnlyAsciiNickName = true;
            OnlyAsciiUserName = false;
            LimitNickNames = true;
            CountryRestriction = "";
            ProtectedNickname = "";
            ProtectedNicknameIP = "";
            /*WhiteListEnabled = false;
            WhiteList[0] = "Bluscream";
            WhiteList[1] = "Redscream";
            BlackListEnabled = false;
            BlackList[0] = "Faggot";
            BlackList[1] = "Bastard";*/
        }
    }
}