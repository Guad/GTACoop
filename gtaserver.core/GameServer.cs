using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GTAServer.ProtocolMessages;
using Lidgren.Network;

namespace GTAServer
{
    public class GameServer
    {
        public string Location => Directory.GetCurrentDirectory();
        public NetPeerConfiguration Config;

        public List<Client> Clients;
        public int MaxPlayers;
        public int Port;
        public int GamemodeName;
        public int Name;
        public string Password;
        public string MasterServer;
        public string BackupMasterServer;
        public bool AnnounceSelf;


        public NetServer Server;

        public GameServer(int port, string name, string gamemodeName)
        {
            Clients = new List<Client>();
            MaxPlayers = 32;
            GamemodeName = gamemodeName;
            Name = name;
            Port = port;
            MasterServerIp = "https://gtamaster.nofla.me";
            Config = new NetPeerConfiguration("GTAVOnlineRaces") {Port = port};
            Config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            Config.EnableMessageType(NetIncomingMessageType.DiscoveryRequest);
            Config.EnableMessageType(NetIncomingMessageType.UnconnectedData);
            Config.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);
            Server = new NetServer(Config);
        }
    }
}
