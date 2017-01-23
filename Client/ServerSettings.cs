﻿using System.Collections.Generic;
using System.Windows.Forms.VisualStyles;

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
        public string BackupMasterServer { get; set; }

        public bool AllowNickNames { get; set; }
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
            AllowNickNames = true;
            AllowOutdatedClients = false;
            MasterServer = "http://46.101.1.92/";
            BackupMasterServer = "http://79.143.189.135/";
            Filterscripts = new string[] { "" };
        }
    }
}