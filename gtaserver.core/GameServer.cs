using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using GTAServer.PluginAPI;
using GTAServer.ProtocolMessages;
using Lidgren.Network;
using Microsoft.Extensions.Logging;
using GTAServer.PluginAPI.Events;

namespace GTAServer
{
    public class GameServer
    {
        public string Location => Directory.GetCurrentDirectory();
        public NetPeerConfiguration Config;

        public List<Client> Clients { get; set; }
        public int MaxPlayers { get; set; }
        public int Port { get; set; }
        public string GamemodeName { get; set; }
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
        public bool DebugMode { get; set; }
        public NetServer Server;
        public int CurrentTick { get; set; } = 0;

        public readonly Dictionary<string, ICommand> Commands = new Dictionary<string, ICommand>();

        public int TicksPerSecond { get; set; }

        private DateTime _lastAnnounceDateTime;
        private ILogger logger;
        private Dictionary<string, Action<object>> _callbacks = new Dictionary<string, Action<object>>();
        private int _ticksLastSecond;

        private Timer _tpsTimer;
        public GameServer(int port, string name, string gamemodeName, bool isDebug)
        {
            logger = Util.LoggerFactory.CreateLogger<GameServer>();
            logger.LogInformation("Server ready to start");
            Clients = new List<Client>();
            MaxPlayers = 32;
            GamemodeName = gamemodeName;
            Name = name;
            Port = port;
            MasterServer = "http://46.101.1.92/";
            BackupMasterServer = "https://gtamaster.nofla.me";
            Config = new NetPeerConfiguration("GTAVOnlineRaces") { Port = port };
            Config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            Config.EnableMessageType(NetIncomingMessageType.DiscoveryRequest);
            Config.EnableMessageType(NetIncomingMessageType.UnconnectedData);
            Config.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);
            Server = new NetServer(Config);

            logger.LogInformation($"NetServer created with port {Config.Port}");

            _tpsTimer = new Timer(state => CalculateTicksPerSecond(), null, 0, 1000);
        }

        public void Start()
        {
            logger.LogInformation("Server starting");

            logger.LogDebug("Loading gamemode");
            if (GamemodeName != "freeroam")
            {
                var assemblyName = Location + Path.DirectorySeparatorChar + GamemodeName + ".dll";
                var pluginAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyName);
                var types = pluginAssembly.GetExportedTypes();
                var validTypes = types.Where(t => typeof(IGamemode).IsAssignableFrom(t)).ToArray();
                if (!validTypes.Any())
                {
                    logger.LogError("No gamemodes found in gamemode assembly, using freeroam");
                    GamemodeName = "freeroam";
                    return;
                }
                if (validTypes.Count() > 1)
                {
                    logger.LogError("Multiple valid gamemodes found in gamemode assembly, using freeroam");
                    GamemodeName = "freeroam";
                    return;
                }
                var gamemode = Activator.CreateInstance(validTypes.First()) as IGamemode;
                if (gamemode == null)
                {
                    logger.LogError(
                        "Could not create instance of gamemode (Activator.CreateInstance returned null), using freeroam");
                    GamemodeName = "freeroam";
                    return;
                }
                GamemodeName = gamemode.GamemodeName;
                gamemode.OnEnable(this, false);
            }
            logger.LogDebug("Gamemode loaded");
            Server.Start();
            if (AnnounceSelf)
            {
                AnnounceToMaster();
            }
        }

        private async void AnnounceToMaster()
        {
            if (DebugMode) return;
            logger.LogInformation("Announcing to master server");
            _lastAnnounceDateTime = DateTime.Now;
            var payload = Port.ToString();
            var enc = new UTF8Encoding();

            var request = WebRequest.Create(MasterServer);
            request.Method = "POST";
            request.ContentType = "text/plain";
            var dataStream = await request.GetRequestStreamAsync();
            dataStream.Write(enc.GetBytes(payload), 0, payload.Length);
            await request.GetResponseAsync();

            request = WebRequest.Create(BackupMasterServer);
            request.Method = "POST";
            request.ContentType = "text/plain";
            dataStream = await request.GetRequestStreamAsync();
            dataStream.Write(enc.GetBytes(payload), 0, payload.Length);
            await request.GetResponseAsync();
        }

        private void CalculateTicksPerSecond()
        {
            TicksPerSecond = CurrentTick - _ticksLastSecond;
            _ticksLastSecond = CurrentTick;

            //logger.LogTrace("TPS: " + TicksPerSecond);
            Console.Title = "GTAServer - " + Name + " (" + Clients.Count + "/" + MaxPlayers + " players) - Port: " + Port + " - TPS: " + TicksPerSecond;
            return;
        }

        public void Tick()
        {
            CurrentTick++;
            GameEvents.Tick(CurrentTick);

            if (AnnounceSelf && DateTime.Now.Subtract(_lastAnnounceDateTime).TotalMinutes >= 5)
            {
                AnnounceToMaster();
            }
            //throw new Exception("test");
            NetIncomingMessage msg;
            while ((msg = Server.ReadMessage()) != null)
            {
                Client client = null;
                lock (Clients)
                {
                    foreach (var c in Clients)
                    {
                        if (c?.NetConnection == null || c.NetConnection.RemoteUniqueIdentifier == 0 ||
                            msg.SenderConnection == null ||
                            c.NetConnection.RemoteUniqueIdentifier != msg.SenderConnection.RemoteUniqueIdentifier)
                            continue;
                        client = c;
                        break;
                    }
                }
                if (client == null)
                {
                    logger.LogDebug("Client not found for remote ID " + msg.SenderConnection?.RemoteUniqueIdentifier + ", creating client. Current number of clients: " + Clients.Count());
                    client = new Client(msg.SenderConnection);
                }

                // Plugin event: OnIncomingPacket
                var pluginPacketHandlerResult = PacketEvents.IncomingPacket(client, msg);
                msg = pluginPacketHandlerResult.Data;
                if (!pluginPacketHandlerResult.ContinueServerProc)
                {
                    Server.Recycle(msg);
                    return;
                }

                //logger.LogInformation("Packet received - type: " + ((NetIncomingMessageType)msg.MessageType).ToString());
                switch (msg.MessageType)
                {
                    case NetIncomingMessageType.UnconnectedData:
                        var ucType = msg.ReadString();
                        // ReSharper disable once ConvertIfStatementToSwitchStatement
                        if (ucType == "ping")
                        {
                            if (!PacketEvents.Ping(client, msg).ContinueServerProc)
                            {
                                Server.Recycle(msg);
                                return;
                            }
                            logger.LogInformation("Ping received from " + msg.SenderEndPoint.Address.ToString());
                            var reply = Server.CreateMessage("pong");
                            Server.SendMessage(reply, client.NetConnection, NetDeliveryMethod.ReliableOrdered);
                        }
                        else if (ucType == "query")
                        {
                            if (!PacketEvents.Query(client, msg).ContinueServerProc)
                            {
                                Server.Recycle(msg);
                                return;
                            }
                            var playersOnline = 0;
                            lock (Clients) playersOnline = Clients.Count;
                            logger.LogInformation("Query received from " + msg.SenderEndPoint.Address.ToString());
                            var reply = Server.CreateMessage($"{Name}%{PasswordProtected}%{playersOnline}%{MaxPlayers}%{GamemodeName}");
                            Server.SendMessage(reply, client.NetConnection, NetDeliveryMethod.ReliableOrdered);
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
                        var connectionApprovalPacketResult = PacketEvents.IncomingConnectionApproval(client, msg);
                        msg = connectionApprovalPacketResult.Data;
                        if (!connectionApprovalPacketResult.ContinueServerProc)
                        {
                            Server.Recycle(msg);
                            return;
                        }
                        HandleClientConnectionApproval(client, msg);
                        break;
                    case NetIncomingMessageType.StatusChanged:
                        pluginPacketHandlerResult = PacketEvents.IncomingStatusChange(client, msg);
                        msg = pluginPacketHandlerResult.Data;
                        if (!pluginPacketHandlerResult.ContinueServerProc)
                        {
                            Server.Recycle(msg);
                            return;
                        }
                        HandleClientStatusChange(client, msg);
                        break;
                    case NetIncomingMessageType.DiscoveryRequest:
                        pluginPacketHandlerResult = PacketEvents.IncomingDiscoveryRequest(client, msg);
                        msg = pluginPacketHandlerResult.Data;
                        if (!pluginPacketHandlerResult.ContinueServerProc)
                        {
                            Server.Recycle(msg);
                            return;
                        }
                        HandleClientDiscoveryRequest(client, msg);
                        break;
                    case NetIncomingMessageType.Data:
                        pluginPacketHandlerResult = PacketEvents.IncomingData(client, msg);
                        msg = pluginPacketHandlerResult.Data;
                        if (!pluginPacketHandlerResult.ContinueServerProc)
                        {
                            Server.Recycle(msg);
                            return;
                        }
                        HandleClientIncomingData(client, msg);
                        break;
                    default:
                        // We shouldn't get packets reaching this, so throw warnings when it happens.
                        logger.LogWarning("Unknown packet received: " +
                                          msg.MessageType.ToString());
                        break;

                }
                Server.Recycle(msg);
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

            var pluginResponse = ConnectionEvents.ConnectionRequest(client, connReq);
            if (!pluginResponse.ContinueServerProc) return;
            connReq = pluginResponse.Data;

            client.DisplayName = connReq.DisplayName;
            client.Name = connReq.Name;
            client.GameVersion = connReq.GameVersion;
            client.RemoteScriptVersion = (ScriptVersion)connReq.ScriptVersion;


            // If nicknames are disabled on the server, set the nickname to the player's social club name.
            if (!AllowNicknames)
            {
                SendNotificationToPlayer(client,
                    $"Nicknames are disabled on this server. Your nickname has been set to {connReq.Name}");
                client.DisplayName = client.Name;
            }


            logger.LogInformation(
                $"New connection request: {client.DisplayName}@{msg.SenderEndPoint.Address.ToString()} | Game version: {client.GameVersion.ToString()} | Script version: {client.RemoteScriptVersion.ToString()}");

            var latestScriptVersion = Enum.GetValues(typeof(ScriptVersion)).Cast<ScriptVersion>().Last();
            if (!AllowOutdatedClients &&
                (ScriptVersion)connReq.ScriptVersion != latestScriptVersion)
            {
                var latestReadableScriptVersion = latestScriptVersion.ToString();
                latestReadableScriptVersion = Regex.Replace(latestReadableScriptVersion, "VERSION_", "",
                    RegexOptions.IgnoreCase);
                latestReadableScriptVersion = Regex.Replace(latestReadableScriptVersion, "_", ".",
                    RegexOptions.IgnoreCase);

                logger.LogInformation($"Client {client.DisplayName} tried to connect with an outdated script version {client.RemoteScriptVersion.ToString()} but the server requires {latestScriptVersion.ToString()}");
                DenyConnect(client, $"Please update to version ${latestReadableScriptVersion} from http://bit.ly/gtacoop", true, msg);
                return;
            }
            else if (client.RemoteScriptVersion != latestScriptVersion)
            {
                SendNotificationToPlayer(client, "You are currently on an outdated client. Please go to http://bit.ly/gtacoop and update.");
            }
            else if (client.RemoteScriptVersion == ScriptVersion.VERSION_UNKNOWN)
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

            lock (Clients)
                if (Clients.Any(c => c.DisplayName == client.DisplayName))
                {
                    DenyConnect(client, "A player already exists with the current display name.");
                }
                else
                {
                    Clients.Add(client);
                }



            var channelHail = Server.CreateMessage();
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
                    SendNotificationToAll($"Player connected: {client.DisplayName}");
                    break;

                case NetConnectionStatus.Disconnected:
                    lock (Clients)
                    {
                        if (Clients.Contains(client))
                        {
                            if (!client.Silent)
                            {
                                if (client.Kicked)
                                {
                                    if (string.IsNullOrEmpty(client.KickReason)) client.KickReason = "Unknown";
                                    SendNotificationToAll(
                                        $"Player kicked: {client.DisplayName} - Reason: {client.KickReason}");
                                }
                                else
                                {
                                    SendNotificationToAll(
                                        $"Player disconnected: {client.DisplayName}");
                                }
                            }
                            var dcMsg = new PlayerDisconnect()
                            {
                                Id = client.NetConnection.RemoteUniqueIdentifier
                            };

                            SendToAll(dcMsg, PacketType.PlayerDisconnect, true);

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
            var responsePkt = Server.CreateMessage();
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
            Server.SendDiscoveryResponse(responsePkt, msg.SenderEndPoint);
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
                            // Plugin chat handling
                            var chatPluginResult = GameEvents.ChatMessage(client, chatData);
                            if (!chatPluginResult.ContinueServerProc) return;
                            chatData = chatPluginResult.Data;

                            // Command handling
                            if (chatData.Message.StartsWith("/"))
                            {
                                var cmdArgs = chatData.Message.Split(' ');
                                var cmdName = cmdArgs[0].Remove(0, 1);
                                if (Commands.ContainsKey(cmdName))
                                {
                                    Commands[cmdName].OnCommandExec(client, chatData);
                                    return;
                                }
                                SendChatMessageToPlayer(client, "Command not found");
                                return;
                            }
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
                                SendToAll(chatData, PacketType.ChatData, true);
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
                            var vehiclePluginResult = GameEvents.VehicleDataUpdate(client, vehicleData);
                            if (!vehiclePluginResult.ContinueServerProc) return;
                            vehicleData = vehiclePluginResult.Data;

                            vehicleData.Id = client.NetConnection.RemoteUniqueIdentifier;
                            vehicleData.Name = client.Name;
                            vehicleData.Latency = client.Latency;

                            client.Health = vehicleData.PlayerHealth;
                            client.LastKnownPosition = vehicleData.Position;
                            client.IsInVehicle = false;

                            SendToAll(vehicleData, PacketType.VehiclePositionData, false, client);
                        }
                    }
                    break;
                case PacketType.PedPositionData:
                    {
                        var len = msg.ReadInt32();
                        var pedPosData = Util.DeserializeBinary<PedData>(msg.ReadBytes(len));
                        if (pedPosData != null)
                        {
                            var pedPluginResult = GameEvents.PedDataUpdate(client, pedPosData);
                            if (!pedPluginResult.ContinueServerProc) return;
                            pedPosData = pedPluginResult.Data;

                            pedPosData.Id = client.NetConnection.RemoteUniqueIdentifier;
                            pedPosData.Name = client.DisplayName;
                            pedPosData.Latency = client.Latency;

                            client.Health = pedPosData.PlayerHealth;
                            client.LastKnownPosition = pedPosData.Position;
                            client.IsInVehicle = false;

                            SendToAll(pedPosData, PacketType.PedPositionData, false, client);
                        }
                    }
                    break;
                case PacketType.NpcVehPositionData:
                    {
                        var len = msg.ReadInt32();
                        var vehData = Util.DeserializeBinary<VehicleData>(msg.ReadBytes(len));

                        if (vehData != null)
                        {
                            var pluginVehData = GameEvents.NpcVehicleDataUpdate(client, vehData);
                            if (!pluginVehData.ContinueServerProc) return;
                            vehData = pluginVehData.Data;

                            vehData.Id = client.NetConnection.RemoteUniqueIdentifier;
                            SendToAll(vehData, PacketType.NpcVehPositionData, false, client);
                        }
                    }
                    break;
                case PacketType.NpcPedPositionData:
                    {
                        var len = msg.ReadInt32();
                        var pedData = Util.DeserializeBinary<PedData>(msg.ReadBytes(len));
                        if (pedData != null)
                        {
                            var pluginPedData = GameEvents.NpcPedDataUpdate(client, pedData);
                            if (!pluginPedData.ContinueServerProc) return;
                            pedData = pluginPedData.Data;

                            pedData.Id = msg.SenderConnection.RemoteUniqueIdentifier;
                        }
                        SendToAll(pedData, PacketType.NpcPedPositionData, false, client);
                    }
                    break;
                case PacketType.WorldSharingStop:
                    {
                        GameEvents.WorldSharingStop(client);
                        var dcObj = new PlayerDisconnect()
                        {
                            Id = client.NetConnection.RemoteUniqueIdentifier
                        };
                        SendToAll(dcObj, PacketType.WorldSharingStop, true);
                    }
                    break;
                case PacketType.NativeResponse:
                    {
                        var len = msg.ReadInt32();
                        var nativeResponse = Util.DeserializeBinary<NativeResponse>(msg.ReadBytes(len));
                        if (nativeResponse == null || !_callbacks.ContainsKey(nativeResponse.Id)) return;
                        object response = nativeResponse.Response;
                        if (response is IntArgument)
                        {
                            response = ((IntArgument)response).Data;
                        }
                        else if (response is UIntArgument)
                        {
                            response = ((UIntArgument)response).Data;
                        }
                        else if (response is StringArgument)
                        {
                            response = ((StringArgument)response).Data;
                        }
                        else if (response is FloatArgument)
                        {
                            response = ((FloatArgument)response).Data;
                        }
                        else if (response is BooleanArgument)
                        {
                            response = ((BooleanArgument)response).Data;
                        }
                        else if (response is Vector3Argument)
                        {
                            var tmp = (Vector3Argument)response;
                            response = new Vector3()
                            {
                                X = tmp.X,
                                Y = tmp.Y,
                                Z = tmp.Z
                            };
                        }
                        _callbacks[nativeResponse.Id].Invoke(response);
                        _callbacks.Remove(nativeResponse.Id);
                    }
                    break;
                case PacketType.PlayerSpawned:
                    {
                        GameEvents.PlayerSpawned(client);
                        logger.LogInformation("Player spawned: " + client.DisplayName);
                    }
                    break;
                // The following is normally only received on the client.
                case PacketType.PlayerDisconnect:
                    break;
                case PacketType.DiscoveryResponse:
                    break;
                case PacketType.ConnectionRequest:
                    break;
                case PacketType.NativeCall:
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
                    // ReSharper disable once NotResolvedInText
                    // resharper wants to see a variable name in the below... w/e.
                    throw new ArgumentOutOfRangeException("Received unknown packet type. Server out of date or modded client?");
            }
        }

        public void SendToAll(object dataToSend, PacketType packetType, bool packetIsImportant)
        {
            var data = Util.SerializeBinary(dataToSend);
            var msg = Server.CreateMessage();
            msg.Write((int)packetType);
            msg.Write(data.Length);
            msg.Write(data);
            Server.SendToAll(msg, packetIsImportant ? NetDeliveryMethod.ReliableOrdered : NetDeliveryMethod.ReliableSequenced);
        }

        public void SendToAll(object dataToSend, PacketType packetType, bool packetIsImportant, Client clientToExclude)
        {
            var data = Util.SerializeBinary(dataToSend);
            var msg = Server.CreateMessage();
            msg.Write((int)packetType);
            msg.Write(data.Length);
            msg.Write(data);
            Server.SendToAll(msg, clientToExclude.NetConnection, packetIsImportant ? NetDeliveryMethod.ReliableOrdered : NetDeliveryMethod.ReliableSequenced, GetChannelForClient(clientToExclude));
        }

        public void DenyConnect(Client player, string reason, bool silent = true, NetIncomingMessage msg = null,
            int duraction = 60)
        {
            player.NetConnection.Deny(reason);
            logger.LogInformation($"Player rejected from server: {player.DisplayName} for {reason}");
            if (!silent)
            {
                SendNotificationToAll($"Player rejected by server: {player.DisplayName} - {reason}");
            }

            Clients.Remove(player);
            if (msg != null) Server.Recycle(msg);
        }

        public int GetChannelForClient(Client c)
        {
            lock (Clients) return (Clients.IndexOf(c) % 31) + 1;
        }

        // Native call functions
        public List<NativeArgument> ParseNativeArguments(params object[] args) // literally copypasted from old gtaserver
        {
            var list = new List<NativeArgument>();
            foreach (var o in args)
            {
                if (o is int)
                {
                    list.Add(new IntArgument() { Data = ((int)o) });
                }
                else if (o is uint)
                {
                    list.Add(new UIntArgument() { Data = ((uint)o) });
                }
                else if (o is string)
                {
                    list.Add(new StringArgument() { Data = ((string)o) });
                }
                else if (o is float)
                {
                    list.Add(new FloatArgument() { Data = ((float)o) });
                }
                else if (o is bool)
                {
                    list.Add(new BooleanArgument() { Data = ((bool)o) });
                }
                else if (o is Vector3)
                {
                    var tmp = (Vector3)o;
                    list.Add(new Vector3Argument()
                    {
                        X = tmp.X,
                        Y = tmp.Y,
                        Z = tmp.Z,
                    });
                }
                else if (o is LocalPlayerArgument)
                {
                    list.Add((LocalPlayerArgument)o);
                }
                else if (o is LocalGamePlayerArgument)
                {
                    list.Add((LocalGamePlayerArgument)o);
                }
            }

            return list;
        }

        public void SendNativeCallToPlayer(Client player, ulong hash, params object[] arguments)
        {
            var obj = new NativeData
            {
                Hash = hash,
                Arguments = ParseNativeArguments(arguments)
            };

            var bin = Util.SerializeBinary(obj);

            var msg = Server.CreateMessage();

            msg.Write((int)PacketType.NativeCall);
            msg.Write(bin.Length);
            msg.Write(bin);

            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, GetChannelForClient(player));
        }

        public void SendNativeCallToAll(ulong hash, params object[] arguments)
        {
            var obj = new NativeData
            {
                Hash = hash,
                Arguments = ParseNativeArguments(arguments),
                ReturnType = null,
                Id = null,
            };

            var bin = Util.SerializeBinary(obj);

            var msg = Server.CreateMessage();

            msg.Write((int)PacketType.NativeCall);
            msg.Write(bin.Length);
            msg.Write(bin);

            Server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        public void GetNativeCallFromPlayer(Client player, string salt, ulong hash, NativeArgument returnType,
            Action<object> callback, params object[] arguments)
        {
            var obj = new NativeData()
            {
                Hash = hash,
                ReturnType = returnType
            };
            salt = Environment.TickCount.ToString() + salt + player.NetConnection.RemoteUniqueIdentifier.ToString();
            obj.Id = salt;
            obj.Arguments = ParseNativeArguments(arguments);
            var bin = Util.SerializeBinary(obj);
            var msg = Server.CreateMessage();
            msg.Write((int)PacketType.NativeCall);
            msg.Write(bin.Length);
            msg.Write(bin);
            _callbacks.Add(salt, callback);
            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, GetChannelForClient(player));
        }



        // Stuff for scripting

        // Notification stuff
        public void SendNotificationToPlayer(Client player, string message, bool flashing = false)
        {
            for (var i = 0; i < message.Length; i += 99)
            {
                SendNativeCallToPlayer(player, 0x202709F4C58A0424, "STRING");
                SendNativeCallToPlayer(player, 0x6C188BE134E074AA, message.Substring(i, Math.Min(99, message.Length - i)));
                SendNativeCallToPlayer(player, 0xF020C96915705B3A, flashing, true);
            }
        }

        public void SendNotificationToAll(string message, bool flashing = false)
        {
            for (var i = 0; i < message.Length; i += 99)
            {
                SendNativeCallToAll(0x202709F4C58A0424, "STRING");
                SendNativeCallToAll(0x6C188BE134E074AA, message.Substring(i, Math.Min(99, message.Length - i)));
                SendNativeCallToAll(0xF020C96915705B3A, flashing, true);
            }
        }

        public void SendPictureNotificationToAll(string body, NotificationPicType pic, int flash, NotificationIconType iconType, string sender, string subject)
        {
            //Crash with new LocalPlayerArgument()!
            SendNativeCallToAll(0x202709F4C58A0424, "STRING");
            SendNativeCallToAll(0x6C188BE134E074AA, body);
            SendNativeCallToAll(0x1CCD9A37359072CF, pic.ToString(), pic.ToString(), flash, (int)iconType, sender, subject);
            SendNativeCallToAll(0xF020C96915705B3A, false, true);
        }

        public void SendPictureNotificationToAll(string body, string pic, int flash, int iconType, string sender, string subject)
        {
            //Crash with new LocalPlayerArgument()!
            SendNativeCallToAll(0x202709F4C58A0424, "STRING");
            SendNativeCallToAll(0x6C188BE134E074AA, body);
            SendNativeCallToAll(0x1CCD9A37359072CF, pic, pic, flash, iconType, sender, subject);
            SendNativeCallToAll(0xF020C96915705B3A, false, true);
        }

        public void SendChatMessageToAll(string msg) => SendChatMessageToAll("", msg);

        public void SendChatMessageToAll(string sender, string message)
        {
            var chatObj = new ChatData()
            {
                Sender = sender,
                Message = message
            };
            SendToAll(chatObj, PacketType.ChatData, true);
        }

        public void SendChatMessageToPlayer(Client player, string message) => SendChatMessageToPlayer(player, "", message);

        public void SendChatMessageToPlayer(Client player, string sender, string message)
        {
            var chatObj = new ChatData()
            {
                Sender = sender,
                Message = message
            };
            var data = Util.SerializeBinary(chatObj);
            var msg = Server.CreateMessage();
            msg.Write((int)PacketType.ChatData);
            msg.Write(data.Length);
            msg.Write(data);
            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, 0);
        }

        public void GivePlayerWeapon(Client player, uint weaponHash, int ammo, bool equipNow, bool ammoLoaded) =>
            SendNativeCallToPlayer(player, 0xBF0FD6E56C964FCB, new LocalPlayerArgument(), weaponHash, ammo, equipNow,
                ammo);

        public void KickPlayer(Client player, string reason = null, bool silent = false, Client sender = null)
        {
            player.Kicked = true;
            player.KickReason = reason?.ToString();
            player.Silent = silent;
            player.KickedBy = sender;
            player.NetConnection.Disconnect("Kicked: " + reason);
        }

        public void SetPlayerPosition(Client player, Vector3 newPosition) => 
            SendNativeCallToPlayer(player, 0x06843DA7060A026B, new LocalPlayerArgument(), 
                newPosition.X, newPosition.Y, newPosition.Z, 0, 0, 0, 1);

        public void GetPlayerPosition(Client player, Action<object> callback, string salt = "salt") =>
            GetNativeCallFromPlayer(player, salt, 0x3FEF770D40960D5A, new Vector3Argument(), 
                callback, new LocalPlayerArgument(), 0);

        public void HasPlayerControlBeenPressed(Client player, int controlId, Action<object> callback, string salt = "salt") => 
            GetNativeCallFromPlayer(player, salt, 0x580417101DDB492F, new BooleanArgument(), 
                callback, 0, controlId);

        public void SetPlayerHealth(Client player, int health) => 
            SendNativeCallToPlayer(player, 0x6B76DC1F3AE6E6A3, new LocalPlayerArgument(), 
                health + 100);

        public void GetPlayerHealthg(Client player, Action<object> callback, string salt = "salt") => 
            GetNativeCallFromPlayer(player, salt, 0xEEF059FAD016D209, new IntArgument(),
                callback, new LocalPlayerArgument());

        public void SetNightVisionForPlayer(Client player, bool status) =>
            SendNativeCallToPlayer(player, 0x18F621F7A5B1F85D, status);

        public void SetNightVisionForAll(Client player, bool status) =>
            SendNativeCallToAll(0x18F621F7A5B1F85D, status);

        public void IsNightVisionActive(Client player, Action<object> callback, string salt = "salt") =>
            GetNativeCallFromPlayer(player, salt, 0x2202A3F42C8E5F79, new BooleanArgument(), 
                callback, new LocalPlayerArgument());
    }
}
