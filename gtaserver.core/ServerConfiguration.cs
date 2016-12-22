using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;

namespace GTAServer
{
    public class ServerConfiguration
    {
        public int Port { get; set; } = 4499;
        public int MaxClients { get; set; } = 16;
        public string GamemodeName { get; set; } = "freeroam";
        public string ServerName { get; set; } = "GTACoOp Server";
        public string Password { get; set; } = "";
        public string PrimaryMasterServer { get; set; } = "http://46.101.1.92/";
        public string BackupMasterServer { get; set; } = "https://gtamaster.nofla.me";
        public bool AnnounceSelf { get; set; } = false;
        public bool AllowNicknames { get; set; } = true;
        public bool AllowOutdatedClients { get; set; } = false;
        public bool DebugMode { get; set; } = false;

        public List<string> ServerPlugins { get; set; } = new List<string>() {};
    }
}
