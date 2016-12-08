using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GTAServer.ProtocolMessages;
using Lidgren.Network;
using Microsoft.Extensions.Logging;

namespace GTAServer
{
    public class GameServer
    {
        public string Location => Directory.GetCurrentDirectory();
        public NetPeerConfiguration Config;

        public List<Client> Clients { get; set; }
        public int MaxPlayers { get; set; }
        public int Port { get; set; }
        public string GamemodeName { get; set; } // This is only what is sent to the client. No GM loading is done yet.
        public string Name { get; set; }
        public string Password { get; set; }
        public string MasterServer { get; set; }
        public string BackupMasterServer { get; set; }
        public bool AnnounceSelf { get; set; }
        public bool AllowNicknames { get; set; }
        public bool AllowOutdatedClients { get; set; }
        public readonly ScriptVersion ServerVersion = ScriptVersion.VERSION_0_9_3;
        public string LastKickedIP { get; set; }
        public Client LastKickedClient { get; set; }


        private DateTime _lastAnnounceDateTime;
        private NetServer _server;
        private ILogger logger;

        public GameServer(int port, string name, string gamemodeName)
        {
            Clients = new List<Client>();
            MaxPlayers = 32;
            GamemodeName = gamemodeName;
            Name = name;
            Port = port;
            MasterServer = "https://gtamaster.nofla.me";
            BackupMasterServer = "http://fakemaster.nofla.me";
            Config = new NetPeerConfiguration("GTAVOnlineRaces") {Port = port};
            Config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            Config.EnableMessageType(NetIncomingMessageType.DiscoveryRequest);
            Config.EnableMessageType(NetIncomingMessageType.UnconnectedData);
            Config.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);
            _server = new NetServer(Config);
            var loggerFactory = new LoggerFactory()
                .AddConsole()
                .AddDebug();
            logger = loggerFactory.CreateLogger<GameServer>();
            logger.LogInformation("Server ready to start");
        }

        public void Start(string[] filterscripts)
        {
            logger.LogInformation("Server starting");
            _server.Start();
            if (AnnounceSelf)
            {
                AnnounceToMaster();
            }
            // TODO: Gamemode loading here... we need a module system first
        }

        private void AnnounceToMaster()
        {
            logger.LogDebug("Announcing to master server");
            _lastAnnounceDateTime = DateTime.Now;
            logger.LogDebug("Server announcer not implemented");
            // TODO: implement server announcing
        }

        public void Tick()
        {
            if (AnnounceSelf && DateTime.Now.Subtract(_lastAnnounceDateTime).TotalMinutes >= 5)
            {
                AnnounceToMaster();
            }
            try
            {
                NetIncomingMessage msg;
                while ((msg = _server.ReadMessage()) != null)
                {
                    Client client = null;
                    lock (Clients)
                    {
                        client = Clients.Where(d => d.NetConnection != null)
                                        .Where(d => d.NetConnection.RemoteUniqueIdentifier != 0)
                                        .Where(d => msg.SenderConnection != null) // almost pointless but w/e
                                        .First(d => d.NetConnection.RemoteUniqueIdentifier == msg.SenderConnection.RemoteUniqueIdentifier);

                    }
                    if (client == null) client = new Client(msg.SenderConnection);

                    switch (msg.MessageType)
                    {
                        case NetIncomingMessageType.UnconnectedData:
                            var ucType = msg.ReadString();
                            if (ucType == "ping")
                            {
                                logger.LogInformation("Ping received from " + msg.SenderEndPoint.Address.ToString());
                                var reply = _server.CreateMessage("pong");
                                _server.SendMessage(reply, client.NetConnection, NetDeliveryMethod.ReliableOrdered);
                            }
                            break;
                        case NetIncomingMessageType.VerboseDebugMessage:
                        case NetIncomingMessageType.DebugMessage:
                            logger.LogDebug("Network (Nerbose)DebugMessage: " + msg.ReadString());
                            break;
                        case NetIncomingMessageType.WarningMessage:
                            logger.LogWarning("Network WarningMessage: " + msg.ReadString());
                            break;
                        case NetIncomingMessageType.ErrorMessage:
                            logger.LogError("Network ErrorMessage: " + msg.ReadString());
                            break;
                        case NetIncomingMessageType.ConnectionLatencyUpdated:
                            client.Latency = msg.ReadFloat();
                            break;
                        case NetIncomingMessageType.ConnectionApproval:
                        case NetIncomingMessageType.StatusChanged:
                        case NetIncomingMessageType.DiscoveryRequest:
                        case NetIncomingMessageType.Data:
                            // TODO: Handle connection packets instead of just dropping the client
                            client.NetConnection.Disconnect("Sorry, this server is currently in development.");
                            break;
                        default:
                            // We shouldn't get packets reaching this, so throw warnings when it happens.
                            logger.LogWarning("Unknown packet received: " +
                                              ((NetIncomingMessageType) msg.MessageType).ToString());
                            break;

                    }
                }
            }
            catch (Exception e)
            {
                logger.LogError("Uncaught exception in Tick()",e);
                // TODO: Error catching/reporting w/ Sentry
            }
        }
    }
}
