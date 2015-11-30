namespace GTAServer
{
    public class ServerSettings
    {
        public string Name { get; set; }
        public int MaxPlayers { get; set; }
        public int Port { get; set; }
        public bool PasswordProtected { get; set; }
        public string Password { get; set; }
        public bool Announce { get; set; }

        public string Gamemode { get; set; }
        public string[] Filterscripts { get; set; }

        public string MasterServer { get; set; }
    }
}