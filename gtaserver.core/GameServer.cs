using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GTAServer.ProtocolMessages;
using Lidgren.Network;
using Microsoft.Extensions.Logging;
using ProtoBuf;

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
        public bool PasswordProtected => !string.IsNullOrEmpty(Password);
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
            Config = new NetPeerConfiguration("GTAVOnlineRaces") { Port = port };
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
            // TODO: Gamemode loading here... we need a module/plugin system first
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
                            // ReSharper disable once ConvertIfStatementToSwitchStatement
                            if (ucType == "ping")
                            {
                                logger.LogInformation("Ping received from " + msg.SenderEndPoint.Address.ToString());
                                var reply = _server.CreateMessage("pong");
                                _server.SendMessage(reply, client.NetConnection, NetDeliveryMethod.ReliableOrdered);
                            }
                            else if (ucType == "query")
                            {
                                var playersOnline = 0;
                                lock (Clients) playersOnline = Clients.Count;
                                logger.LogInformation("Query received from " + msg.SenderEndPoint.Address.ToString());
                                var reply = _server.CreateMessage($"{Name}%{PasswordProtected}%{playersOnline}%{MaxPlayers}%{GamemodeName}");
                                _server.SendMessage(reply, client.NetConnection, NetDeliveryMethod.ReliableOrdered);
                            }
                            break;
                        case NetIncomingMessageType.VerboseDebugMessage:
                        case NetIncomingMessageType.DebugMessage:
                            logger.LogDebug("Network (Verbose)DebugMessage: " + msg.ReadString());
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
                            HandleClientConnectionApproval(client, msg);
                            break;
                        case NetIncomingMessageType.StatusChanged:
                            HandleClientStatusChange(client, msg);
                            break;
                        case NetIncomingMessageType.DiscoveryRequest:
                            HandleClientDiscoveryRequest(client, msg);
                            break;
                        case NetIncomingMessageType.Data:
                            HandleClientIncomingData(client, msg);
                            break;
                        default:
                            // We shouldn't get packets reaching this, so throw warnings when it happens.
                            logger.LogWarning("Unknown packet received: " +
                                              ((NetIncomingMessageType)msg.MessageType).ToString());
                            break;

                    }
                    _server.Recycle(msg);
                }
            }
            catch (Exception e)
            {
                logger.LogError("Uncaught exception in Tick()", e);
                // TODO: Error catching/reporting w/ Sentry
            }
        }
        private void HandleClientConnectionApproval(Client client, NetIncomingMessage msg)
        {
            var type = msg.ReadInt32();
            var length = msg.ReadInt32();
            var connReq = Util.DeserializeBinary<ConnectionRequest>(msg.ReadBytes(length));
            if (connReq == null)
            {
                DenyConnect(client, "Connection is null, this is most likely a bug in the client.", true, msg);
                return;
            }

            // If nicknames are disabled on the server, set the nickname to the player's social club name.
            // TODO: Notify client that nicknames are disabled
            if (!AllowNicknames) connReq.DisplayName = connReq.Name;


            logger.LogInformation(
                $"New connection request: {connReq.DisplayName}@{msg.SenderEndPoint.Address.ToString()} | Game version: {connReq.GameVersion.ToString()} | Script version: {connReq.ScriptVersion.ToString()}");

            var latestScriptVersion = Enum.GetValues(typeof(ScriptVersion)).Cast<ScriptVersion>().Last();
            if (!AllowOutdatedClients &&
                (ScriptVersion)connReq.ScriptVersion != latestScriptVersion)
            {
                var latestReadableScriptVersion = latestScriptVersion.ToString();
                latestReadableScriptVersion = Regex.Replace(latestReadableScriptVersion, "VERSION_", "",
                    RegexOptions.IgnoreCase);
                latestReadableScriptVersion = Regex.Replace(latestReadableScriptVersion, "_", ".",
                    RegexOptions.IgnoreCase);

                logger.LogInformation($"Client {client.DisplayName} tried to connect with an outdated script version {connReq.ScriptVersion.ToString()} but the server requires {latestScriptVersion.ToString()}");
                DenyConnect(client, $"Please update to version ${latestReadableScriptVersion} from http://bit.ly/gtacoop", true, msg);
                return;
            }
            else if ((ScriptVersion)connReq.ScriptVersion != latestScriptVersion)
            {
                // TODO: send message to player here
            }
            else if ((ScriptVersion)connReq.ScriptVersion == ScriptVersion.VERSION_UNKNOWN)
            {
                logger.LogInformation($"Client {client.DisplayName} tried to connect with an unknown script version (client too old?)");
                DenyConnect(client, $"Unknown version. Please re-download GTACoop from http://bit.ly/gtacoop", true, msg);
                return;
            }
            var numClients = 0;
            lock (Clients) numClients = Clients.Count;
            if (numClients >= MaxPlayers)
            {
                logger.LogInformation($"Player tried to join while server is full: {client.DisplayName}");
                DenyConnect(client, "No available player slots.", true, msg);
            }

            if (PasswordProtected && connReq.Password != Password)
            {
                logger.LogInformation($"Client {client.DisplayName} tried to connect with the wrong password.");
                DenyConnect(client, "Wrong password.", true, msg);
            }

            lock (Clients) if (Clients.Any(c => c.DisplayName == connReq.DisplayName))
                {
                    DenyConnect(client, "A player already exists with the current display name.");
                }

            client.ApplyConnectionRequest(connReq);

            var channelHail = _server.CreateMessage();
            channelHail.Write(GetChannelForClient(client));
            client.NetConnection.Approve(channelHail);
        }
        private void HandleClientStatusChange(Client client, NetIncomingMessage msg)
        {
            var newStatus = (NetConnectionStatus)msg.ReadByte();
            switch (newStatus)
            {
                case NetConnectionStatus.Connected:
                    logger.LogInformation($"Connected: {client.DisplayName}@{msg.SenderEndPoint.Address.ToString()}");
                    break;

                // TODO: Send notification to all players about new player connecting
                case NetConnectionStatus.Disconnected:
                    lock (Clients)
                    {
                        if (Clients.Contains(client))
                        {
                            if (client.Kicked)
                            {
                                // TODO: Send notification to all players about kicked player
                            }
                            else
                            {
                                //TODO: send notification to all players about disconnecting player
                            }
                            var dcMsg = new PlayerDisconnect()
                            {
                                Id = client.NetConnection.RemoteUniqueIdentifier
                            };

                            // TODO: Send dcMsg to all connections
                            if (client.Kicked)
                            {
                                logger.LogInformation(
                                    $"Player kicked: {client.DisplayName}@{msg.SenderEndPoint.Address.ToString()}");
                                LastKickedClient = client;
                                LastKickedIP = client.NetConnection.RemoteEndPoint.ToString();
                            }
                            else
                            {
                                logger.LogInformation($"Player disconnected: {client.DisplayName}@{msg.SenderEndPoint.Address.ToString()}");
                            }
                            Clients.Remove(client);
                        }
                        break;
                    }
                // resharper was bugging me about not having the below case statements
                case NetConnectionStatus.None:
                case NetConnectionStatus.InitiatedConnect:
                case NetConnectionStatus.ReceivedInitiation:
                case NetConnectionStatus.RespondedAwaitingApproval:
                case NetConnectionStatus.RespondedConnect:
                case NetConnectionStatus.Disconnecting:
                default:
                    break;
            }
        }
        private void HandleClientDiscoveryRequest(Client client, NetIncomingMessage msg)
        {
            var responsePkt = _server.CreateMessage();
            var discoveryResponse = new DiscoveryResponse
            {
                ServerName = Name,
                MaxPlayers = MaxPlayers,
                PasswordProtected = PasswordProtected,
                Gamemode = GamemodeName,
                Port = Port,
            };
            lock (Clients) discoveryResponse.PlayerCount = Clients.Count;

            var serializedResponse = Util.SerializeBinary(discoveryResponse);
            responsePkt.Write((int)PacketType.DiscoveryResponse);
            responsePkt.Write(serializedResponse.Length);
            responsePkt.Write(serializedResponse);
            logger.LogInformation($"Server status requested by {msg.SenderEndPoint.Address.ToString()}");
            _server.SendDiscoveryResponse(responsePkt, msg.SenderEndPoint);
        }

        private void HandleClientIncomingData(Client client, NetIncomingMessage msg)
        {
            var packetType = (PacketType)msg.ReadInt32();

            switch (packetType)
            {
                case PacketType.ChatData:
                    {
                        // TODO: This code really could use refactoring.. right now only trying to make sure this all works on .NET Core and fixing small issues.
                        var len = msg.ReadInt32();
                        var chatData = Util.DeserializeBinary<ChatData>(msg.ReadBytes(len));
                        if (chatData != null)
                        {
                            var chatMsg = new ChatMessage(chatData, client);

                            if (!chatMsg.Suppress)
                            {
                                chatData.Id = client.NetConnection.RemoteUniqueIdentifier;
                                chatData.Sender = "";
                                if (!string.IsNullOrWhiteSpace(chatMsg.Prefix))
                                    chatData.Sender += "[" + chatMsg.Prefix + "] ";
                                chatData.Sender += chatMsg.Sender.DisplayName;

                                if (!string.IsNullOrWhiteSpace(chatMsg.Suffix))
                                    chatData.Sender += $" ({chatMsg.Suffix}) ";
                                // TODO: Send chat message here
                                logger.LogInformation($"[Chat] <{chatData.Sender}>: {chatData.Message}");
                            }
                        }
                    }
                    break;
                case PacketType.VehiclePositionData:
                    {
                        var len = msg.ReadInt32();
                        var vehicleData = Util.DeserializeBinary<VehicleData>(msg.ReadBytes(len));
                        if (vehicleData != null)
                        {
                            vehicleData.Id = client.NetConnection.RemoteUniqueIdentifier;
                            vehicleData.Name = client.Name;
                            vehicleData.Latency = client.Latency;

                            client.Health = vehicleData.PlayerHealth;
                            client.LastKnownPosition = vehicleData.Position;
                            client.IsInVehicle = false;

                            // TODO: broadcast VehicleData packet
                        }
                    }
                    break;
                case PacketType.PlayerDisconnect:
                    break;
                case PacketType.PedPositionData:
                    break;
                case PacketType.NpcVehPositionData:
                    break;
                case PacketType.NpcPedPositionData:
                    break;
                case PacketType.WorldSharingStop:
                    break;
                case PacketType.DiscoveryResponse:
                    break;
                case PacketType.ConnectionRequest:
                    break;
                case PacketType.NativeCall:
                    break;
                case PacketType.NativeResponse:
                    break;
                case PacketType.PlayerSpawned:
                    break;
                case PacketType.NativeTick:
                    break;
                case PacketType.NativeTickRecall:
                    break;
                case PacketType.NativeOnDisconnect:
                    break;
                case PacketType.NativeOnDisconnectRecall:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void DenyConnect(Client player, string reason, bool silent = true, NetIncomingMessage msg = null,
            int duraction = 60)
        {
            player.NetConnection.Deny(reason);
            logger.LogInformation($"Player rejected from server: {player.DisplayName} for {reason}");
            if (!silent)
            {
                // TODO: Send notification to players here
            }

            Clients.Remove(player);
            if (msg != null) _server.Recycle(msg);
        }

        public int GetChannelForClient(Client c)
        {
            lock (Clients) return (Clients.IndexOf(c) % 31) + 1;
        }
    }
}
