namespace GTAServer
{
    /// <summary>
    /// Contains server settings
    /// </summary>
    public class ServerSettings
    {
        /// <summary>
        /// Server name
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Server max players
        /// </summary>
        public int MaxPlayers { get; set; }
        /// <summary>
        /// Server port
        /// </summary>
        public int Port { get; set; }
        /// <summary>
        /// If the server is password protected
        /// </summary>
        public bool PasswordProtected { get; set; }
        /// <summary>
        /// Server password
        /// </summary>
        public string Password { get; set; }
        /// <summary>
        /// If the server should announce itself
        /// </summary>
        public bool Announce { get; set; }
        /// <summary>
        /// Master server address
        /// </summary>
        public string MasterServer { get; set; }

        /// <summary>
        /// If the server should allow display names
        /// </summary>
        public bool AllowDisplayNames { get; set; }
        /// <summary>
        /// If the server should allow outdated clients
        /// </summary>
        public bool AllowOutdatedClients { get; set; }

        /// <summary>
        /// Server gamemode
        /// </summary>
        public string Gamemode { get; set; }
        /// <summary>
        /// Filterscripts to load
        /// </summary>
        public string[] Filterscripts { get; set; }
        /// <summary>
        /// Server LAN IP
        /// </summary>
        public string LANIP { get; set; }
        /// <summary>
        /// Server WAN IP
        /// </summary>
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