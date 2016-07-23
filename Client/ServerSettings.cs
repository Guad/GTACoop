namespace GTACoOp
{
    public class ServerSettings
    {
        public string Name { get; set; }
        public int MaxPlayers { get; set; }
        public int Port { get; set; }
        public bool PasswordProtected { get; set; }
        public string Password { get; set; }
        public bool Announce { get; set; }
        public string MasterServer { get; set; }

        public bool AllowDisplayNames { get; set; }
        public bool AllowOutdatedClients { get; set; }

        public string Gamemode { get; set; }
        public string[] Filterscripts { get; set; }

        public string LANIP { get; set; }
        public string WANIP { get; set; }

        public ServerSettings()
        {
            Port = 4499;
            MaxPlayers = 16;
            Name = "Simple GTA Server";
            Password = "changeme";
            PasswordProtected = false;
            Gamemode = "freeroam";
            Announce = true;
            AllowDisplayNames = true;
            AllowOutdatedClients = false;
            MasterServer = "http://46.101.1.92/";
            Filterscripts = new string[] { "" };
        }
    }
}