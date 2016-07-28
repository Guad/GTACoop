using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using Lidgren.Network;
using ProtoBuf;
using System.Text.RegularExpressions;
using MaxMind.GeoIP2;
using System.Security.Principal;
using System.Diagnostics;

namespace GTAServer
{
    public class ChatMessage
    {
        public Client Sender { get; set; }
        public Client Reciever { get; set; }
        public bool isPrivate { get; set; }
        public string Message { get; set; }
        public ConsoleColor Color { get; set; }
        public string Prefix { get; set; }
        public string Suffix { get; set; }
        public bool Supress { get; set; }
    }
    public class Client
    {
        public NetConnection NetConnection { get; private set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public float Latency { get; set; }
        public ScriptVersion RemoteScriptVersion { get; set; }
        public int GameVersion { get; set; }

        public Vector3 LastKnownPosition { get; internal set; }
        public int Health { get; internal set; }
        public int VehicleHealth { get; internal set; }
        public bool IsInVehicle { get; internal set; }
        public bool afk { get; set; }
        public bool Banned { get; set; }
        public string BanReason { get; set; }
        public bool Kicked { get; set; }
        public string KickReason { get; set; }
        public bool Silent { get; set; }
        public MaxMind.GeoIP2.Responses.CountryResponse geoIP { get; set; }

        public Client(NetConnection nc)
        {
            NetConnection = nc;
        }
    }

    public enum NotificationIconType
    {
        Chatbox = 1,
        Email = 2,
        AddFriendRequest = 3,
        Nothing = 4,
        RightJumpingArrow = 7,
        RP_Icon = 8,
        DollarIcon = 9,
    }

    public enum NotificationPicType
    {
        CHAR_DEFAULT, // : Default profile pic
        CHAR_FACEBOOK, // Facebook
        CHAR_SOCIAL_CLUB, // Social Club Star
        CHAR_CARSITE2, // Super Auto San Andreas Car Site
        CHAR_BOATSITE, // Boat Site Anchor
        CHAR_BANK_MAZE, // Maze Bank Logo
        CHAR_BANK_FLEECA, // Fleeca Bank
        CHAR_BANK_BOL, // Bank Bell Icon
        CHAR_MINOTAUR, // Minotaur Icon
        CHAR_EPSILON, // Epsilon E
        CHAR_MILSITE, // Warstock W
        CHAR_CARSITE, // Legendary Motorsports Icon
        CHAR_DR_FRIEDLANDER, // Dr Freidlander Face
        CHAR_BIKESITE, // P&M Logo
        CHAR_LIFEINVADER, // Liveinvader
        CHAR_PLANESITE, // Plane Site E
        CHAR_MICHAEL, // Michael's Face
        CHAR_FRANKLIN, // Franklin's Face
        CHAR_TREVOR, // Trevor's Face
        CHAR_SIMEON, // Simeon's Face
        CHAR_RON, // Ron's Face
        CHAR_JIMMY, // Jimmy's Face
        CHAR_LESTER, // Lester's Shadowed Face
        CHAR_DAVE, // Dave Norton's Face
        CHAR_LAMAR, // Chop's Face
        CHAR_DEVIN, // Devin Weston's Face
        CHAR_AMANDA, // Amanda's Face
        CHAR_TRACEY, // Tracey's Face
        CHAR_STRETCH, // Stretch's Face
        CHAR_WADE, // Wade's Face
        CHAR_MARTIN, // Martin Madrazo's Face

    }

    public class GameServer
    {
        public GameServer(int port, string name, string gamemodeName)
        {
            Clients = new List<Client>();
            MaxPlayers = 32;
            Port = port;
            GamemodeName = gamemodeName;
            Name = name;
            WanIP = "";
            LanIP = "";
            geoIP = null;
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
            NetPeerConfiguration config = new NetPeerConfiguration("GTAVOnlineRaces");
            config.Port = port;
            config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            config.EnableMessageType(NetIncomingMessageType.DiscoveryRequest);
            config.EnableMessageType(NetIncomingMessageType.UnconnectedData);
            config.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);
            Server = new NetServer(config);
        }

        public NetServer Server;

        public int MaxPlayers { get; set; }
        public int Port { get; set; }
        public List<Client> Clients { get; set; }
        public string Name { get; set; }
        public string Password { get; set; }
        public bool PasswordProtected { get; set; }
        public string GamemodeName { get; set; }
        public string MasterServer { get; set; }
        public bool AnnounceSelf { get; set; }

        public bool AllowDisplayNames { get; set; }
        public bool AllowOutdatedClients { get; set; }

        public readonly ScriptVersion ServerVersion = ScriptVersion.VERSION_0_9_2;

        private ServerScript _gamemode { get; set; }

        private List<ServerScript> _filterscripts;
        public string WanIP { get; set; }
        public string LanIP { get; set; }
        public string LastKicked { get; set; }
        public MaxMind.GeoIP2.Responses.CountryResponse geoIP { get; set; }

        private DateTime _lastAnnounceDateTime;

        public void Start(string[] filterscripts)
        {
            Server.Start();
            if (AnnounceSelf)
            {
                _lastAnnounceDateTime = DateTime.Now;
                Console.WriteLine("Announcing to master server...");
                AnnounceSelfToMaster();
            }

            if (GamemodeName.ToLower() != "freeroam")
            {
                try
                {
                    Console.WriteLine("Loading gamemode...");

                    try
                    {
                        Program.DeleteFile(Program.Location + "gamemodes" + Path.DirectorySeparatorChar + GamemodeName + ".dll:Zone.Identifier");
                    }
                    catch
                    {
                    }

                    var asm = Assembly.LoadFrom(Program.Location + "gamemodes" + Path.DirectorySeparatorChar + GamemodeName + ".dll");
                    var types = asm.GetExportedTypes();
                    var validTypes = types.Where(t =>
                        !t.IsInterface &&
                        !t.IsAbstract)
                        .Where(t => typeof(ServerScript).IsAssignableFrom(t));
                    if (!validTypes.Any())
                    {
                        Console.WriteLine("ERROR: No classes that inherit from ServerScript have been found in the assembly. Starting freeroam.");
                        return;
                    }

                    _gamemode = Activator.CreateInstance(validTypes.ToArray()[0]) as ServerScript;
                    if (_gamemode == null) Console.WriteLine("Could not create gamemode: it is null.");
                    else _gamemode.Start();
                }
                catch (Exception e)
                {
                    Console.WriteLine("ERROR: Error while loading script: " + e.Message + " at " + e.Source +
                                      ".\nStack Trace:" + e.StackTrace);
                    Console.WriteLine("Inner Exception: ");
                    Exception r = e;
                    while (r != null && r.InnerException != null)
                    {
                        Console.WriteLine("at " + r.InnerException);
                        r = r.InnerException;
                    }
                }
            }

            Console.WriteLine("Loading filterscripts..");
            var list = new List<ServerScript>();
            foreach (var path in filterscripts)
            {
                if (string.IsNullOrWhiteSpace(path)) continue;

                try {
                    try {
                        Program.DeleteFile(Program.Location + "filterscripts" + Path.DirectorySeparatorChar + GamemodeName + ".dll:Zone.Identifier");
                    } catch { }

                    var fsAsm = Assembly.LoadFrom(Program.Location + "filterscripts" + Path.DirectorySeparatorChar + path + ".dll");
                    var fsObj = InstantiateScripts(fsAsm);
                    list.AddRange(fsObj);
                } catch (Exception ex) {
                    Console.WriteLine("Failed to load filterscript \"" + path + "\", error: " + ex.ToString());
                }
            }

            list.ForEach(fs =>
            {
                fs.Start();
                Console.WriteLine("Starting filterscript " + fs.Name + "...");
            });
            _filterscripts = list;
            PrintServerInfo(); PrintPlayerList();
        }

        public void AnnounceSelfToMaster()
        {
            using (var wb = new WebClient())
            {
                try
                {
                    wb.UploadData(MasterServer, Encoding.UTF8.GetBytes(Port.ToString()));
                }
                catch (WebException)
                {
                    Console.WriteLine("Failed to announce self: master server is not available at this time.");
                }
            }
        }

        private IEnumerable<ServerScript> InstantiateScripts(Assembly targetAssembly)
        {
            var types = targetAssembly.GetExportedTypes();
            var validTypes = types.Where(t =>
                !t.IsInterface &&
                !t.IsAbstract)
                .Where(t => typeof(ServerScript).IsAssignableFrom(t));
            if (!validTypes.Any())
            {
                yield break;
            }
            foreach (var type in validTypes)
            {
                var obj = Activator.CreateInstance(type) as ServerScript;
                if (obj != null)
                    yield return obj;
            }
        }
        static void LogToConsole(int flag, bool debug, string module, string message)
        {
            if (module == null || module.Equals("")) { module = "SERVER"; }
            if (debug && !Program.Debug) return;
            if (flag == 1)
            {
                Console.ForegroundColor = ConsoleColor.Cyan; Console.WriteLine("[" + DateTime.Now + "] (DEBUG) " + module.ToUpper() + ": " + message);
            }
            else if (flag == 2)
            {
                Console.ForegroundColor = ConsoleColor.Green; Console.WriteLine("[" + DateTime.Now + "] (SUCCESS) " + module.ToUpper() + ": " + message);
            }
            else if (flag == 3)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow; Console.WriteLine("[" + DateTime.Now + "] (WARNING) " + module.ToUpper() + ": " + message);
            }
            else if (flag == 4)
            {
                Console.ForegroundColor = ConsoleColor.Red; Console.WriteLine("[" + DateTime.Now + "] (ERROR) " + module.ToUpper() + ": " + message);
            }
            else if (flag == 6)
            {
                Console.ForegroundColor = ConsoleColor.Magenta; Console.WriteLine("[" + DateTime.Now + "] " + module.ToUpper() + ": " + message);
            }
            else
            {
                Console.WriteLine("[" + DateTime.Now + "] " + module.ToUpper() + ": " + message);
            }
            Console.ForegroundColor = ConsoleColor.White;
        }
        public void Tick()
        {
            try
            {
                if (AnnounceSelf && DateTime.Now.Subtract(_lastAnnounceDateTime).TotalMinutes >= 5)
                {
                    _lastAnnounceDateTime = DateTime.Now;
                    AnnounceSelfToMaster();
                }

                NetIncomingMessage msg;
                while ((msg = Server.ReadMessage()) != null)
                {
                    Client client = null;
                    lock (Clients)
                    {
                        foreach (Client c in Clients)
                        {
                            if (c != null && c.NetConnection != null &&
                                c.NetConnection.RemoteUniqueIdentifier != 0 &&
                                msg.SenderConnection != null &&
                                c.NetConnection.RemoteUniqueIdentifier == msg.SenderConnection.RemoteUniqueIdentifier)
                            {
                                client = c;
                                break;
                            }
                        }
                    }

                    if (client == null) client = new Client(msg.SenderConnection);

                    switch (msg.MessageType)
                    {
                        case NetIncomingMessageType.UnconnectedData:
                            var isPing = msg.ReadString();
                            if (isPing == "ping")
                            {
                                LogToConsole(0, false, "Network", "INFO: ping received from " + msg.SenderEndPoint.Address.ToString());
                                var pong = Server.CreateMessage();
                                pong.Write("pong");
                                Server.SendMessage(pong, client.NetConnection, NetDeliveryMethod.ReliableOrdered);
                            }
                            if (isPing == "query")
                            {
                                int playersonline = 0;
                                lock (Clients) playersonline = Clients.Count;
                                Console.WriteLine("INFO: query received from " + msg.SenderEndPoint.Address.ToString());
                                var pong = Server.CreateMessage();
                                pong.Write(Name + "%" + PasswordProtected + "%" + playersonline + "%" + MaxPlayers + "%" + GamemodeName);
                                Server.SendMessage(pong, client.NetConnection, NetDeliveryMethod.ReliableOrdered);
                            }
                            break;
                        case NetIncomingMessageType.VerboseDebugMessage:
                            LogToConsole(0, true, "Network", msg.ReadString()); break;
                        case NetIncomingMessageType.DebugMessage:
                            LogToConsole(1, false, "Network", msg.ReadString()); break;
                        case NetIncomingMessageType.WarningMessage:
                            LogToConsole(3, true, "Network", msg.ReadString()); break;
                        case NetIncomingMessageType.ErrorMessage:
                            LogToConsole(4, false, "Network", msg.ReadString()); break;
                        case NetIncomingMessageType.ConnectionLatencyUpdated:
                            client.Latency = msg.ReadFloat(); break;
                        case NetIncomingMessageType.ConnectionApproval:
                            var type = msg.ReadInt32();
                            var leng = msg.ReadInt32();
                            var connReq = DeserializeBinary<ConnectionRequest>(msg.ReadBytes(leng)) as ConnectionRequest;
                            if (connReq == null)
                            {
                                DenyPlayer(client, "Connection Object is null", true, msg); continue;
                            }
                            Console.Write("New connection request: ");
                            try { Console.Write("Nickname: " + connReq.DisplayName.ToString() + " | "); } catch (Exception) { }
                            try { Console.Write("Name: " + connReq.Name.ToString() + " | "); } catch (Exception) { }
                            #if debug
                            try { Console.Write("Password: " + connReq.Password.ToString() + " | "); } catch (Exception) { }
                            #endif
                            try { Console.Write("Game Version: " + connReq.GameVersion.ToString() + " | "); } catch (Exception) { }
                            try { Console.Write("Script Version: ["+connReq.ScriptVersion.ToString()+ "] "+(ScriptVersion)connReq.ScriptVersion + "("+(byte)connReq.ScriptVersion + ") | "); } catch (Exception) { }
                            try { Console.Write("IP: " + msg.SenderEndPoint.Address.ToString() + ":" + msg.SenderEndPoint.Port.ToString() + " | "); } catch (Exception) { }
                            Console.Write("\n");
                            if (!AllowOutdatedClients && (ScriptVersion)connReq.ScriptVersion != Enum.GetValues(typeof(ScriptVersion)).Cast<ScriptVersion>().Last())
                            {
                                var ReadableScriptVersion = Enum.GetValues(typeof(ScriptVersion)).Cast<ScriptVersion>().Last().ToString();
                                ReadableScriptVersion = Regex.Replace(ReadableScriptVersion, "VERSION_", "", RegexOptions.IgnoreCase);
                                ReadableScriptVersion = Regex.Replace(ReadableScriptVersion, "_", ".", RegexOptions.IgnoreCase);
                                LogToConsole(3, true, "Network", "Client " + connReq.DisplayName + " tried to connect with outdated scriptversion " + connReq.ScriptVersion.ToString() + " but the server requires " + Enum.GetValues(typeof(ScriptVersion)).Cast<ScriptVersion>().Last().ToString());
                                DenyPlayer(client, string.Format("Update your GTACoop to v{0} from bit.ly/gtacoop", ReadableScriptVersion), true, msg); continue;
                            }
                            if ((ScriptVersion)connReq.ScriptVersion == ScriptVersion.VERSION_UNKNOWN)
                            {
                                LogToConsole(3, true, "Network", "Client " + connReq.DisplayName + " tried to connect with unknown scriptversion " + connReq.ScriptVersion.ToString());
                                DenyPlayer(client, "Unknown version. Please update your client from bit.ly/gtacoop", true, msg); continue;
                            }

                            int clients = 0;
                            lock (Clients) clients = Clients.Count;
                            if (clients < MaxPlayers)
                            {
                                if (PasswordProtected && !string.IsNullOrWhiteSpace(Password))
                                {
                                    if (Password != connReq.Password)
                                    {
                                        LogToConsole(3, false, "Network", connReq.DisplayName + " connection refused: Wrong password: " + connReq.Password.ToString());
                                        DenyPlayer(client, "Wrong password.", true, msg); continue;
                                    }
                                }

                                lock (Clients)
                                {
                                    int duplicate = 0;
                                    string displayname = connReq.DisplayName;
                                    while (AllowDisplayNames && Clients.Any(c => c.DisplayName == connReq.DisplayName))
                                    {
                                        duplicate++;

                                        connReq.DisplayName = displayname + " (" + duplicate + ")";
                                    }

                                    Clients.Add(client);
                                }
                                client.Name = connReq.Name;
                                client.DisplayName = AllowDisplayNames ? connReq.DisplayName : connReq.Name;

                                if (client.RemoteScriptVersion != (ScriptVersion)connReq.ScriptVersion) client.RemoteScriptVersion = (ScriptVersion)connReq.ScriptVersion;
                                if (client.GameVersion != connReq.GameVersion) client.GameVersion = connReq.GameVersion;
                                PrintPlayerInfo(client, "New incoming connection: ");

                                if (_gamemode != null) _gamemode.OnIncomingConnection(client);
                                if (_filterscripts != null) _filterscripts.ForEach(fs => fs.OnIncomingConnection(client));

                                var channelHail = Server.CreateMessage();
                                channelHail.Write(GetChannelIdForConnection(client));
                                client.NetConnection.Approve(channelHail);
                            }
                            else
                            {
                                LogToConsole(4, false, "Network", client.DisplayName + " connection refused: server full with " + clients.ToString() + " of " + MaxPlayers + " players.");
                                DenyPlayer(client, "No available player slots.", true, msg);
                            }
                            break;
                        case NetIncomingMessageType.StatusChanged:
                            var newStatus = (NetConnectionStatus)msg.ReadByte();

                            if (newStatus == NetConnectionStatus.Connected)
                            {
                                bool sendMsg = true;

                                Console.ForegroundColor = ConsoleColor.DarkGreen; PrintPlayerInfo(client, "Connected: "); Console.ResetColor();
                                var path = Program.Location + "geoip.mmdb";
                                try
                                {
                                    using (var reader = new DatabaseReader(path))
                                    {
                                        client.geoIP = reader.Country(client.NetConnection.RemoteEndPoint.Address);
                                    }
                                }
                                catch (Exception ex) { LogToConsole(3, false, "GeoIP", ex.Message); }
                                if (_gamemode != null) sendMsg = sendMsg && _gamemode.OnPlayerConnect(client);
                                if (_filterscripts != null) _filterscripts.ForEach(fs => sendMsg = sendMsg && fs.OnPlayerConnect(client));
                                if (sendMsg && !client.Silent)
                                    try
                                    {
                                        SendNotificationToAll("~h~" + client.DisplayName + "~h~~w~ connected from " + client.geoIP.Country.Name.ToString() + ".");
                                    }
                                    catch
                                    {
                                        SendNotificationToAll("~h~" + client.DisplayName + "~h~~w~ connected.");
                                    }
                            }
                            else if (newStatus == NetConnectionStatus.Disconnected)
                            {
                                lock (Clients)
                                {
                                    if (Clients.Contains(client))
                                    {
                                        var sendMsg = true;

                                        if (_gamemode != null) sendMsg = sendMsg && _gamemode.OnPlayerDisconnect(client);
                                        if (_filterscripts != null) _filterscripts.ForEach(fs => sendMsg = sendMsg && fs.OnPlayerDisconnect(client));
                                        if (client.NetConnection.RemoteEndPoint.Address.ToString().Equals(LastKicked)) { client.Silent = true; }
                                        if (sendMsg && !client.Silent)
                                            if (client.Banned)
                                            {
                                                if (!client.BanReason.Equals(""))
                                                {
                                                    SendNotificationToAll("~h~" + client.DisplayName + "~h~~w~ has been banned for " + client.BanReason);
                                                }
                                                else
                                                {
                                                    SendNotificationToAll("~h~" + client.DisplayName + "~h~~w~ has been banned.");
                                                }
                                            }
                                            else if (client.Kicked)
                                            {
                                                if (!client.KickReason.Equals(""))
                                                {
                                                    SendNotificationToAll("~h~" + client.DisplayName + "~h~~w~ was kicked for " + client.KickReason);
                                                }
                                                else
                                                {
                                                    SendNotificationToAll("~h~" + client.DisplayName + "~h~~w~ has been kicked.");
                                                }
                                            }
                                            else
                                            {
                                                SendNotificationToAll("~h~" + client.DisplayName + "~h~~w~ disconnected.");
                                            }

                                        var dcObj = new PlayerDisconnect()
                                        {
                                            Id = client.NetConnection.RemoteUniqueIdentifier,
                                        };

                                        SendToAll(dcObj, PacketType.PlayerDisconnect, true);

                                        if (client.Banned)
                                        {
                                            if (!client.BanReason.Equals(""))
                                            {
                                                Console.WriteLine("Player banned: \"" + client.Name + "\" (" + client.DisplayName + ") for " + client.BanReason);
                                            }
                                            else
                                            {
                                                Console.ForegroundColor = ConsoleColor.Red; PrintPlayerInfo(client, "Banned: "); Console.ResetColor();
                                            }
                                        }
                                        else if (client.Kicked)
                                        {
                                            if (!client.KickReason.Equals(""))
                                            {
                                                Console.WriteLine("Player kicked: \"" + client.Name + "\" (" + client.DisplayName + ") for " + client.KickReason);
                                            }
                                            else
                                            {
                                                Console.ForegroundColor = ConsoleColor.Red; PrintPlayerInfo(client, "Kicked: "); Console.ResetColor();
                                            }
                                        }
                                        else
                                        {
                                            Console.ForegroundColor = ConsoleColor.DarkRed; PrintPlayerInfo(client, "Disconnected: "); Console.ResetColor();
                                        }
                                        LastKicked = client.NetConnection.RemoteEndPoint.Address.ToString();
                                        Clients.Remove(client);
                                    }
                                }
                            }
                            break;
                        case NetIncomingMessageType.DiscoveryRequest:
                            NetOutgoingMessage response = Server.CreateMessage();
                            var obj = new DiscoveryResponse();
                            obj.ServerName = Name;
                            obj.MaxPlayers = MaxPlayers;
                            obj.PasswordProtected = PasswordProtected;
                            obj.Gamemode = GamemodeName;
                            lock (Clients) obj.PlayerCount = Clients.Count;
                            obj.Port = Port;

                            var bin = SerializeBinary(obj);

                            response.Write((int)PacketType.DiscoveryResponse);
                            response.Write(bin.Length);
                            response.Write(bin);

                            LogToConsole(1, false, "Network", "Server Status requested by " + msg.SenderEndPoint.Address);
                            Server.SendDiscoveryResponse(response, msg.SenderEndPoint);
                            break;
                        case NetIncomingMessageType.Data:
                            var packetType = (PacketType)msg.ReadInt32();

                            switch (packetType)
                            {
                                case PacketType.ChatData:
                                    {
                                        try
                                        {
                                            var len = msg.ReadInt32();
                                            var data = DeserializeBinary<ChatData>(msg.ReadBytes(len)) as ChatData;
                                            if (data != null)
                                            {
                                                var Msg = new ChatMessage();
                                                Msg.Message = data.Message;
                                                Msg.Sender = client;
                                                if (_gamemode != null) Msg = _gamemode.OnChatMessage(Msg);

                                                if (_filterscripts != null) _filterscripts.ForEach(fs => Msg = fs.OnChatMessage(Msg));

                                                if (!Msg.Supress)
                                                {
                                                    data.Id = client.NetConnection.RemoteUniqueIdentifier;
                                                    if (!string.IsNullOrWhiteSpace(Msg.Prefix))
                                                        data.Sender += "[" + Msg.Prefix + "] ";
                                                    data.Sender += client.DisplayName;
                                                    if (!string.IsNullOrWhiteSpace(Msg.Suffix))
                                                        data.Sender += " (" + Msg.Suffix + ") ";
                                                    SendToAll(data, PacketType.ChatData, true);
                                                    LogToConsole(6, false, "Chat", data.Sender + ": " + data.Message);
                                                }
                                            }
                                        }
                                        catch { }
                                    }
                                    break;
                                case PacketType.VehiclePositionData:
                                    {
                                        try
                                        {
                                            var len = msg.ReadInt32();
                                            var data =
                                                DeserializeBinary<VehicleData>(msg.ReadBytes(len)) as
                                                    VehicleData;
                                            if (data != null)
                                            {
                                                data.Id = client.NetConnection.RemoteUniqueIdentifier;
                                                data.Name = client.DisplayName;
                                                data.Latency = client.Latency;

                                                client.Health = data.PlayerHealth;
                                                client.LastKnownPosition = data.Position;
                                                client.VehicleHealth = data.VehicleHealth;
                                                client.IsInVehicle = true;

                                                SendToAll(data, PacketType.VehiclePositionData, false, client);
                                            }
                                        }
                                        catch (IndexOutOfRangeException)
                                        { }
                                    }
                                    break;
                                case PacketType.PedPositionData:
                                    {
                                        try
                                        {
                                            var len = msg.ReadInt32();
                                            var data = DeserializeBinary<PedData>(msg.ReadBytes(len)) as PedData;
                                            if (data != null)
                                            {
                                                data.Id = client.NetConnection.RemoteUniqueIdentifier;
                                                data.Name = client.DisplayName;
                                                data.Latency = client.Latency;

                                                client.Health = data.PlayerHealth;
                                                client.LastKnownPosition = data.Position;
                                                client.IsInVehicle = false;

                                                SendToAll(data, PacketType.PedPositionData, false, client);
                                            }
                                        }
                                        catch (IndexOutOfRangeException)
                                        { }
                                    }
                                    break;
                                case PacketType.NpcVehPositionData:
                                    {
                                        try
                                        {
                                            var len = msg.ReadInt32();
                                            var data =
                                                DeserializeBinary<VehicleData>(msg.ReadBytes(len)) as
                                                    VehicleData;
                                            if (data != null)
                                            {
                                                data.Id = client.NetConnection.RemoteUniqueIdentifier;
                                                SendToAll(data, PacketType.NpcVehPositionData, false, client);
                                            }
                                        }
                                        catch (IndexOutOfRangeException)
                                        { }
                                    }
                                    break;
                                case PacketType.NpcPedPositionData:
                                    {
                                        try
                                        {
                                            var len = msg.ReadInt32();
                                            var data =
                                                DeserializeBinary<PedData>(msg.ReadBytes(len)) as PedData;
                                            if (data != null)
                                            {
                                                data.Id = msg.SenderConnection.RemoteUniqueIdentifier;
                                                SendToAll(data, PacketType.NpcPedPositionData, false, client);
                                            }
                                        }
                                        catch (IndexOutOfRangeException)
                                        { }
                                    }
                                    break;
                                case PacketType.WorldSharingStop:
                                    {
                                        var dcObj = new PlayerDisconnect()
                                        {
                                            Id = client.NetConnection.RemoteUniqueIdentifier,
                                        };
                                        SendToAll(dcObj, PacketType.WorldSharingStop, true);
                                    }
                                    break;
                                case PacketType.NativeResponse:
                                    {
                                        var len = msg.ReadInt32();
                                        var data = DeserializeBinary<NativeResponse>(msg.ReadBytes(len)) as NativeResponse;
                                        if (data == null || !_callbacks.ContainsKey(data.Id)) continue;
                                        object resp = null;
                                        if (data.Response is IntArgument)
                                        {
                                            resp = ((IntArgument)data.Response).Data;
                                        }
                                        else if (data.Response is UIntArgument)
                                        {
                                            resp = ((UIntArgument)data.Response).Data;
                                        }
                                        else if (data.Response is StringArgument)
                                        {
                                            resp = ((StringArgument)data.Response).Data;
                                        }
                                        else if (data.Response is FloatArgument)
                                        {
                                            resp = ((FloatArgument)data.Response).Data;
                                        }
                                        else if (data.Response is BooleanArgument)
                                        {
                                            resp = ((BooleanArgument)data.Response).Data;
                                        }
                                        else if (data.Response is Vector3Argument)
                                        {
                                            var tmp = (Vector3Argument)data.Response;
                                            resp = new Vector3()
                                            {
                                                X = tmp.X,
                                                Y = tmp.Y,
                                                Z = tmp.Z,
                                            };
                                        }
                                        if (_callbacks.ContainsKey(data.Id))
                                            _callbacks[data.Id].Invoke(resp);
                                        _callbacks.Remove(data.Id);
                                    }
                                    break;
                                case PacketType.PlayerSpawned:
                                    {
                                        if (_gamemode != null) _gamemode.OnPlayerSpawned(client);
                                        if (_filterscripts != null) _filterscripts.ForEach(fs => fs.OnPlayerSpawned(client));
                                        PrintPlayerInfo(client, "Player spawned: ");
                                    }
                                    break;
                            }
                            break;
                        default:
                            Console.WriteLine("WARN: Unhandled type: " + msg.MessageType);
                            break;
                    }
                    Server.Recycle(msg);
                }
                if (_gamemode != null) _gamemode.OnTick();
                if (_filterscripts != null) _filterscripts.ForEach(fs => fs.OnTick());
            }catch(Exception ex) { LogToConsole(4, false, "", "Can't handle tick: "+ex.Message); }
        }
        public void Infoscreen()
        {
            while (true)
            {
                PrintServerInfo();
                PrintPlayerList();
                Thread.Sleep(60000);
            }
        }

        public void PrintPlayerList(string message = "Online Players: ")
        {
            for (var i = 0; i < Program.ServerInstance.Clients.Count; i++)
            {
                PrintPlayerInfo(Program.ServerInstance.Clients[i], "#"+i.ToString()+ " ");
            }
        }

        public void PrintPlayerInfo( Client client, string message = "Player Info: ")
        {
            Console.Write(message);
            try { Console.Write("Nickname: " + client.DisplayName.ToString() + " | "); } catch (Exception) { }
            try { Console.Write("Name: " + client.Name.ToString() + " | "); } catch (Exception) { }
            try { Console.Write("Game Version: " + client.GameVersion.ToString() + " | "); } catch (Exception) { }
            try { Console.Write("Script Version: " + client.RemoteScriptVersion.ToString() + " | "); } catch (Exception) { }
            try { Console.Write("Health: " + client.Health.ToString() + " | "); } catch (Exception) { }
            try { Console.Write("Vehicle: " + client.IsInVehicle.ToString() + " | "); } catch (Exception) { }
            try { if(client.IsInVehicle) Console.Write("Veh Health: " + client.VehicleHealth.ToString() + " | "); } catch (Exception) { }
            try { Console.Write("Position: X:" + client.LastKnownPosition.X.ToString() + " Y: " + client.LastKnownPosition.Y.ToString() + " Z: " + client.LastKnownPosition.Z.ToString() + " | "); } catch (Exception) { }
            try { Console.Write("IP: " + client.NetConnection.RemoteEndPoint.Address.ToString() + ":" + client.NetConnection.RemoteEndPoint.Port.ToString() + " | "); } catch (Exception) { }
            try { if((client.Latency * 1000) > 0)Console.Write("Ping: " + (client.Latency*1000).ToString() + " | "); } catch (Exception) { }
            try { Console.Write("Status: " + client.NetConnection.Status.ToString() + " | "); } catch (Exception) { }
            try { Console.Write("NetUID: " + client.NetConnection.RemoteUniqueIdentifier.ToString() + " | "); } catch (Exception) { }
            try { Console.Write("MPU: " + client.NetConnection.CurrentMTU.ToString() + " | "); } catch (Exception) { }
            try { Console.Write("Continent: " + client.geoIP.Continent.Name + " | "); } catch (Exception) { }
            try { Console.Write("Country: " + client.geoIP.Country.Name + " [" + client.geoIP.Country.IsoCode +  "] | "); } catch (Exception) { }
            /*try { Console.Write("City: " + client.geoIP.City + " | "); } catch (Exception) { }
            try { Console.Write("Sent Messages: " + client.NetConnection.Statistics.SentMessages.ToString() + " | "); } catch (Exception) { }
            try { Console.Write("Recieved Messages: " + client.NetConnection.Statistics.ReceivedMessages.ToString() + " | "); } catch (Exception) { }
            try { Console.Write("Dropped Messages: " + client.NetConnection.Statistics.DroppedMessages.ToString() + " | "); } catch (Exception) { }
            try { Console.Write("Sent Bytes: " + client.NetConnection.Statistics.SentBytes.ToString() + " | "); } catch (Exception) { }
            try { Console.Write("Recieved Bytes: " + client.NetConnection.Statistics.ReceivedBytes.ToString() + " | "); } catch (Exception) { }*/
            Console.Write("\n");//Console.Write("\n");
        }

        public void PrintServerInfo( string message = "Server Info: ")
        {
            Console.Write(message);
            try { Console.Write("Name: " + Name.ToString() + " | "); } catch (Exception) { }
            try { Console.Write("Password?: " + PasswordProtected.ToString() + " | "); } catch (Exception) { }
            try { int playersonline = 0; lock (Clients) playersonline = Clients.Count;
                Console.Write("Players: " + playersonline.ToString() + " / " + MaxPlayers + " | "); } catch (Exception) { }
            try { Console.Write("Gamemode: " + GamemodeName.ToString() + " | "); } catch (Exception) { }
            Console.Write("\n");
        }

        public void Stop()
        {
            foreach (Client player in Clients)
            {
                KickPlayer(player, "Server shutting down");
            }
            Server.Shutdown("Stopping server");
        }

        public void SendToAll(object newData, PacketType packetType, bool important)
        {
            try
            {
                var data = SerializeBinary(newData);
                NetOutgoingMessage msg = Server.CreateMessage();
                msg.Write((int)packetType);
                msg.Write(data.Length);
                msg.Write(data);
                Server.SendToAll(msg, important ? NetDeliveryMethod.ReliableOrdered : NetDeliveryMethod.ReliableSequenced);
            }catch(Exception ex) { LogToConsole(5, false, "Network", "Error in SendToAll: " + ex.Message); }
        }

        public void SendToAll(object newData, PacketType packetType, bool important, Client exclude)
        {
            var data = SerializeBinary(newData);
            NetOutgoingMessage msg = Server.CreateMessage();
            msg.Write((int)packetType);
            msg.Write(data.Length);
            msg.Write(data);
            Server.SendToAll(msg, exclude.NetConnection, important ? NetDeliveryMethod.ReliableOrdered : NetDeliveryMethod.ReliableSequenced, GetChannelIdForConnection(exclude));
        }

        public object DeserializeBinary<T>(byte[] data)
        {
            using (var stream = new MemoryStream(data))
            {
                try
                {
                    return Serializer.Deserialize<T>(stream);
                }
                catch (ProtoException e)
                {
                    Console.WriteLine("WARN: Deserialization failed: " + e.Message);
                    return null;
                }
            }
        }

        public byte[] SerializeBinary(object data)
        {
            using (var stream = new MemoryStream())
            {
                Serializer.Serialize(stream, data);
                return stream.ToArray();
            }
        }

        public int GetChannelIdForConnection(Client conn)
        {
            lock (Clients) return (Clients.IndexOf(conn) % 31) + 1;
        }

        private List<NativeArgument> ParseNativeArguments(params object[] args)
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
                else if (o is OpponentPedHandleArgument)
                {
                    list.Add((OpponentPedHandleArgument)o);
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
            var obj = new NativeData();
            obj.Hash = hash;
            obj.Arguments = ParseNativeArguments(arguments);

            var bin = SerializeBinary(obj);

            var msg = Server.CreateMessage();

            msg.Write((int)PacketType.NativeCall);
            msg.Write(bin.Length);
            msg.Write(bin);

            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, GetChannelIdForConnection(player));
        }

        public void SendNativeCallToAllPlayers(ulong hash, params object[] arguments)
        {
            var obj = new NativeData();
            obj.Hash = hash;
            obj.Arguments = ParseNativeArguments(arguments);
            obj.ReturnType = null;
            obj.Id = null;

            var bin = SerializeBinary(obj);

            var msg = Server.CreateMessage();

            msg.Write((int)PacketType.NativeCall);
            msg.Write(bin.Length);
            msg.Write(bin);

            Server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        public void SetNativeCallOnTickForPlayer(Client player, string identifier, ulong hash, params object[] arguments)
        {
            var obj = new NativeData();
            obj.Hash = hash;

            obj.Arguments = ParseNativeArguments(arguments);

            var wrapper = new NativeTickCall();
            wrapper.Identifier = identifier;
            wrapper.Native = obj;

            var bin = SerializeBinary(wrapper);

            var msg = Server.CreateMessage();

            msg.Write((int)PacketType.NativeTick);
            msg.Write(bin.Length);
            msg.Write(bin);

            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, GetChannelIdForConnection(player));
        }

        public void SetNativeCallOnTickForAllPlayers(string identifier, ulong hash, params object[] arguments)
        {
            var obj = new NativeData();
            obj.Hash = hash;

            obj.Arguments = ParseNativeArguments(arguments);

            var wrapper = new NativeTickCall();
            wrapper.Identifier = identifier;
            wrapper.Native = obj;

            var bin = SerializeBinary(wrapper);

            var msg = Server.CreateMessage();

            msg.Write((int)PacketType.NativeTick);
            msg.Write(bin.Length);
            msg.Write(bin);

            Server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        public void RecallNativeCallOnTickForPlayer(Client player, string identifier)
        {
            var wrapper = new NativeTickCall();
            wrapper.Identifier = identifier;

            var bin = SerializeBinary(wrapper);

            var msg = Server.CreateMessage();
            msg.Write((int)PacketType.NativeTickRecall);
            msg.Write(bin.Length);
            msg.Write(bin);

            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, GetChannelIdForConnection(player));
        }

        public void RecallNativeCallOnTickForAllPlayers(string identifier)
        {
            var wrapper = new NativeTickCall();
            wrapper.Identifier = identifier;

            var bin = SerializeBinary(wrapper);

            var msg = Server.CreateMessage();
            msg.Write((int)PacketType.NativeTickRecall);
            msg.Write(bin.Length);
            msg.Write(bin);

            Server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        public void SetNativeCallOnDisconnectForPlayer(Client player, string identifier, ulong hash, params object[] arguments)
        {
            var obj = new NativeData();
            obj.Hash = hash;
            obj.Id = identifier;
            obj.Arguments = ParseNativeArguments(arguments);

            
            var bin = SerializeBinary(obj);

            var msg = Server.CreateMessage();

            msg.Write((int)PacketType.NativeOnDisconnect);
            msg.Write(bin.Length);
            msg.Write(bin);

            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, GetChannelIdForConnection(player));
        }

        public void SetNativeCallOnDisconnectForAllPlayers(string identifier, ulong hash, params object[] arguments)
        {
            var obj = new NativeData();
            obj.Hash = hash;
            obj.Id = identifier;
            obj.Arguments = ParseNativeArguments(arguments);

            var bin = SerializeBinary(obj);

            var msg = Server.CreateMessage();

            msg.Write((int)PacketType.NativeOnDisconnect);
            msg.Write(bin.Length);
            msg.Write(bin);

            Server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        public void RecallNativeCallOnDisconnectForPlayer(Client player, string identifier)
        {
            var obj = new NativeData();
            obj.Id = identifier;

            var bin = SerializeBinary(obj);

            var msg = Server.CreateMessage();
            msg.Write((int)PacketType.NativeOnDisconnectRecall);
            msg.Write(bin.Length);
            msg.Write(bin);

            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, GetChannelIdForConnection(player));
        }

        public void RecallNativeCallOnDisconnectForAllPlayers(string identifier)
        {
            var obj = new NativeData();
            obj.Id = identifier;

            var bin = SerializeBinary(obj);

            var msg = Server.CreateMessage();
            msg.Write((int)PacketType.NativeOnDisconnectRecall);
            msg.Write(bin.Length);
            msg.Write(bin);

            Server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        private Dictionary<string, Action<object>> _callbacks = new Dictionary<string, Action<object>>();

        public void GetNativeCallFromPlayer(Client player, string salt, ulong hash, NativeArgument returnType, Action<object> callback,
            params object[] arguments)
        {
            var obj = new NativeData();
            obj.Hash = hash;
            obj.ReturnType = returnType;
            salt = Environment.TickCount.ToString() +
                   salt +
                   player.NetConnection.RemoteUniqueIdentifier.ToString() +
                   DateTime.Now.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalMilliseconds.ToString();
            obj.Id = salt;
            obj.Arguments = ParseNativeArguments(arguments);

            var bin = SerializeBinary(obj);

            var msg = Server.CreateMessage();

            msg.Write((int)PacketType.NativeCall);
            msg.Write(bin.Length);
            msg.Write(bin);

            _callbacks.Add(salt, callback);
            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, GetChannelIdForConnection(player));
        }

        // SCRIPTING

        public void SendChatMessageToAll(string message)
        {
            SendChatMessageToAll("", message);
        }

        public void SendChatMessageToAll(string sender, string message)
        {
            var chatObj = new ChatData()
            {
                Sender = sender,
                Message = message,
            };

            SendToAll(chatObj, PacketType.ChatData, true);
        }

        public void SendChatMessageToPlayer(Client player, string message)
        {
            SendChatMessageToPlayer(player, "", message);
        }

        public void SendChatMessageToPlayer(Client player, string sender, string message)
        {
            var chatObj = new ChatData()
            {
                Sender = sender,
                Message = message,
            };

            var data = SerializeBinary(chatObj);

            NetOutgoingMessage msg = Server.CreateMessage();
            msg.Write((int)PacketType.ChatData);
            msg.Write(data.Length);
            msg.Write(data);
            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, 0);
        }

        public void GivePlayerWeapon(Client player, uint weaponHash, int ammo, bool equipNow, bool ammoLoaded)
        {
            SendNativeCallToPlayer(player, 0xBF0FD6E56C964FCB, new LocalPlayerArgument(), weaponHash, ammo, equipNow, ammo);
        }

        public void KickPlayer(Client player, string reason, bool silent = false)
        {
            player.Kicked = true;player.KickReason = reason.ToString();player.Silent = silent;
            player.NetConnection.Disconnect("Kicked: " + reason);
        }

        public void DenyPlayer(Client player, string reason, bool silent = true, NetIncomingMessage msg = null, int duration = 60)
        {
            if (_gamemode != null) _gamemode.OnConnectionRefused(player, reason);
            if (_filterscripts != null) _filterscripts.ForEach(fs => fs.OnConnectionRefused(player, reason));
            player.NetConnection.Deny(reason);
            Console.ForegroundColor = ConsoleColor.DarkRed; PrintPlayerInfo(player, "Connection Denied: "+ reason + " || "); Console.ResetColor();
            if (!silent) { SendNotificationToAll(player.DisplayName + " was rejected by the server: " + reason); }
            string _ip = player.NetConnection.RemoteEndPoint.Address.ToString();
            Clients.Remove(player); if (msg != null) Server.Recycle(msg);
            BlockIP(_ip, "GTAServer Block (" + _ip + ")", duration);
        }
        /*public void DenyConnection(Client player, string reason, bool silent = true, NetIncomingMessage msg = null)
        {
            player. (reason);
            Console.ForegroundColor = ConsoleColor.DarkRed; PrintPlayerInfo(player, "Connection Denied: " + reason + " || "); Console.ResetColor();
            if (!silent) SendNotificationToAll(player.DisplayName + " was rejected by the server: " + reason);
            Clients.Remove(player);if (msg != null) Server.Recycle(msg);
        }*/

        public void SetPlayerPosition(Client player, Vector3 newPosition)
        {
            SendNativeCallToPlayer(player, 0x06843DA7060A026B, new LocalPlayerArgument(), newPosition.X, newPosition.Y, newPosition.Z, 0, 0, 0, 1);
        }

        public void GetPlayerPosition(Client player, Action<object> callback, string salt = "salt")
        {
            GetNativeCallFromPlayer(player,
                salt,
                0x3FEF770D40960D5A, new Vector3Argument(), callback, new LocalPlayerArgument(), 0);
        }

        public void HasPlayerControlBeenPressed(Client player, int controlId, Action<object> callback, string salt = "salt")
        {
            GetNativeCallFromPlayer(player, salt,
                0x580417101DDB492F, new BooleanArgument(), callback, 0, controlId);
        }

        public void SetPlayerHealth(Client player, int health)
        {
            SendNativeCallToPlayer(player, 0x6B76DC1F3AE6E6A3, new LocalPlayerArgument(), health + 100);
        }

        public void SendNotificationToPlayer(Client player, string message, bool flashing = false)
        {
            for (int i = 0; i < message.Length; i += 99)
            {
                SendNativeCallToPlayer(player, 0x202709F4C58A0424, "STRING");
                SendNativeCallToPlayer(player, 0x6C188BE134E074AA, message.Substring(i, Math.Min(99, message.Length - i)));
                SendNativeCallToPlayer(player, 0xF020C96915705B3A, flashing, true);
            }
        }

        public void SendNotificationToAll(string message, bool flashing = false)
        {
            for (int i = 0; i < message.Length; i += 99)
            {
                SendNativeCallToAllPlayers(0x202709F4C58A0424, "STRING");
                SendNativeCallToAllPlayers(0x6C188BE134E074AA, message.Substring(i, Math.Min(99, message.Length - i)));
                SendNativeCallToAllPlayers(0xF020C96915705B3A, flashing, true);
            }
        }

        public void SendPictureNotificationToPlayer(Client player, string body, NotificationPicType pic, int flash, NotificationIconType iconType, string sender, string subject)
        {
            //Crash with new LocalPlayerArgument()!
            SendNativeCallToPlayer(player, 0x202709F4C58A0424, "STRING");
            SendNativeCallToPlayer(player, 0x6C188BE134E074AA, body);
            SendNativeCallToPlayer(player, 0x1CCD9A37359072CF, pic.ToString(), pic.ToString(), flash, (int)iconType, sender, subject);
            SendNativeCallToPlayer(player, 0xF020C96915705B3A, false, true);
        }

        public void SendPictureNotificationToAll(Client player, string body, NotificationPicType pic, int flash, NotificationIconType iconType, string sender, string subject)
        {
            //Crash with new LocalPlayerArgument()!
            SendNativeCallToAllPlayers(0x202709F4C58A0424, "STRING");
            SendNativeCallToAllPlayers(0x6C188BE134E074AA, body);
            SendNativeCallToAllPlayers(0x1CCD9A37359072CF, pic.ToString(), pic.ToString(), flash, (int)iconType, sender, subject);
            SendNativeCallToAllPlayers(0xF020C96915705B3A, false, true);
        }

        public void SendPictureNotificationToAll(Client player, string body, string pic, int flash, int iconType, string sender, string subject)
        {
            //Crash with new LocalPlayerArgument()!
            SendNativeCallToAllPlayers(0x202709F4C58A0424, "STRING");
            SendNativeCallToAllPlayers(0x6C188BE134E074AA, body);
            SendNativeCallToAllPlayers(0x1CCD9A37359072CF, pic, pic, flash, iconType, sender, subject);
            SendNativeCallToAllPlayers(0xF020C96915705B3A, false, true);
        }

        public void GetPlayerHealth(Client player, Action<object> callback, string salt = "salt")
        {
            GetNativeCallFromPlayer(player, salt,
                0xEEF059FAD016D209, new IntArgument(), callback, new LocalPlayerArgument());
        }
		
	    public void ToggleNightVisionForPlayer(Client player, bool status)
        {
            SendNativeCallToPlayer(player, 0x18F621F7A5B1F85D, status);
        }
		
        public void ToggleNightVisionForAll(Client player, bool status)
        {
            SendNativeCallToAllPlayers(0x18F621F7A5B1F85D, status);
        }

        public void IsNightVisionActive(Client player, Action<object> callback, string salt = "salt")
        {
            GetNativeCallFromPlayer(player, salt, 0x2202A3F42C8E5F79, new BooleanArgument(), callback, new LocalPlayerArgument());
        }
        /*const string guidFWPolicy2 = "{E2B3C97F-6AE1-41AC-817A-F6F92166D7DD}";
        const string guidRWRule = "{2C5BC43E-3369-4C33-AB0C-BE9469677AF4}";
        public void BlockIPviaFirewall(string ip, string name = "Blocked by GTAServer", int duration = -1, string port = "4499")
        {
            Type typeFWPolicy2 = Type.GetTypeFromCLSID(new Guid(guidFWPolicy2));
            Type typeFWRule = Type.GetTypeFromCLSID(new Guid(guidRWRule));
            NetFwTypeLib.INetFwPolicy2 fwPolicy2 = (NetFwTypeLib.INetFwPolicy2)Activator.CreateInstance(typeFWPolicy2);
            NetFwTypeLib.INetFwRule newRule = (NetFwTypeLib.INetFwRule)Activator.CreateInstance(typeFWRule);
            newRule.Name = "InBound_Rule";
            newRule.Description = "Block inbound traffic from "+ip+" over TCP port "+port;
            newRule.Protocol = (int)NetFwTypeLib.NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_TCP;
            newRule.LocalPorts = port;
            newRule.RemoteAddresses = ip;
            newRule.Direction = NetFwTypeLib.NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_IN;
            newRule.Enabled = true;
            newRule.Grouping = "@firewallapi.dll,-23255";
            newRule.Profiles = fwPolicy2.CurrentProfileTypes;
            newRule.Action = NetFwTypeLib.NET_FW_ACTION_.NET_FW_ACTION_BLOCK;
            fwPolicy2.Rules.Add(newRule);
            if(duration != -1)
            {
                ThreadStart StartPointMethod = null;
                Thread t = new Thread(StartPointMethod);
                t.Join(duration * 1000);
            }
    }*/

        private void BlockIP(string ip, string name = "Blocked by GTAServer", int duration = -1, string port = "")
        {
            if (!IsAdministrator()) { LogToConsole(3, false, "Firewall", "Not blocking " + ip + " cause the app is not started as admin."); return; }
            if (string.IsNullOrEmpty(port)) { port = Port.ToString(); }
            if (duration != -1)
            {
                cmdExec(null, "advfirewall firewall add rule name=\""+name+ "\" description=\"Block inbound traffic from " + ip+" over UDP port "+port+" for "+(duration).ToString()+ " seconds.\" dir=in interface=any action=block protocol=udp localport=" + port+" remoteip="+ip, "netsh.exe", true);
                cmdExec(null, "ping 127.0.0.1 - n " + (duration + 1).ToString() + " > nul & netsh advfirewall firewall delete rule name = all dir =in protocol = udp localport = " + port + " remoteip = " + ip, "cmd.exe");
                LogToConsole(2, false, "Firewall", "Blocked " + ip + " for "+duration+ " seconds!");
            }
            else {
                cmdExec("netsh advfirewall firewall add rule name=\"" +name +"\" description=\"Block inbound traffic from "+ip+" over UDP port "+port+"\" dir=in interface=any action=block localport="+port+" remoteip="+ip);
                LogToConsole(2, false, "Firewall", "Blocked " + ip + " permanently!");
            }
        }
        public static bool IsAdministrator()
        {
            return (new WindowsPrincipal(WindowsIdentity.GetCurrent()))
                    .IsInRole(WindowsBuiltInRole.Administrator);
        }
        public static void cmdExec2(string exec, string arg = null, string app = @"C:\Windows\System32\cmd.exe", bool waitForExit = false)
        {
            var psi = new ProcessStartupInfo
            {
                Arguments = $"advfirewall firewall add rule name={name} description=Block inbound traffic from {ip} over TCP port {port} dir=in interface=any action=block localport={port} remoteip={ip} && ping 127.0.0.1 -n {duration * 1000 + 1} > nul && advfirewall firewall delete rule dir=in localport={port} remoteip={ip}",
                FileName = "netsh.exe",
            using (var process = Process.Start(psi))
            {
                process.WaitForExit();
            }
        }
        public static void cmdExec(string exec, string arg = null, string app = @"C:\Windows\System32\cmd.exe", bool waitForExit = false)
        {
            ProcessStartInfo cmdStartInfo = new ProcessStartInfo();
            cmdStartInfo.FileName = app;
            if (!string.IsNullOrEmpty(arg)) { cmdStartInfo.Arguments = arg; }
            cmdStartInfo.RedirectStandardOutput = true;
            cmdStartInfo.RedirectStandardInput = true;
            cmdStartInfo.UseShellExecute = false;
            cmdStartInfo.CreateNoWindow = true;
            Process cmdProcess = new Process();
            cmdProcess.StartInfo = cmdStartInfo;
            using (var process = cmdProcess.Start())
            {
                process.WaitForExit();
            }
            if (!string.IsNullOrEmpty(exec))
            {
                cmdProcess.StandardInput.WriteLine(exec);
            }
            if (waitForExit)
            {
                cmdProcess.WaitForExit();
            }
        }
        public static void ReadFromCMD(string exec, string arg = null, string app = @"C:\Windows\System32\cmd.exe")
        {
            ProcessStartInfo cmdStartInfo = new ProcessStartInfo();
            cmdStartInfo.FileName = app;
            if (!string.IsNullOrEmpty(arg)) { cmdStartInfo.Arguments = arg; }
            cmdStartInfo.RedirectStandardOutput = true;
            cmdStartInfo.RedirectStandardError = true;
            cmdStartInfo.RedirectStandardInput = true;
            cmdStartInfo.UseShellExecute = false;
            cmdStartInfo.CreateNoWindow = true;

            Process cmdProcess = new Process();
            cmdProcess.StartInfo = cmdStartInfo;
            cmdProcess.ErrorDataReceived += cmd_Error;
            cmdProcess.OutputDataReceived += cmd_DataReceived;
            cmdProcess.EnableRaisingEvents = true;
            cmdProcess.Start();
            cmdProcess.BeginOutputReadLine();
            cmdProcess.BeginErrorReadLine();

            if (!string.IsNullOrEmpty(exec))
            {
                cmdProcess.StandardInput.WriteLine(exec);
            }
            //cmdProcess.WaitForExit();
        }

        static void cmd_DataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine("Output from other process");
            Console.WriteLine(e.Data);
        }

            static void cmd_Error(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine("Error from other process");
            Console.WriteLine(e.Data);
        }
        }
    }
