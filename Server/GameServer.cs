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
    /// <summary>
    /// Another version of the ChatData class?
    /// TODO: Task BluScream about this...
    /// </summary>
    public class ChatMessage
    {
        /// <summary>
        /// Sender of the message
        /// </summary>
        public Client Sender { get; set; }
        /// <summary>
        /// Receiver of the message
        /// </summary>
        public Client Reciever { get; set; }
        /// <summary>
        /// If the message is private
        /// Note: ReSharper is suggesting IsPrivate as a property name
        /// </summary>
        public bool isPrivate { get; set; }
        /// <summary>
        /// Contents of message
        /// </summary>
        public string Message { get; set; }
        /// <summary>
        /// Color of the message
        /// </summary>
        public ConsoleColor Color { get; set; }
        /// <summary>
        /// Message prefix
        /// </summary>
        public string Prefix { get; set; }
        /// <summary>
        /// Message suffix
        /// </summary>
        public string Suffix { get; set; }
        /// <summary>
        /// If the message should be suppressed
        /// TODO: More detailed (where is it suppressed from? just chat? just console? both?
        /// </summary>
        public bool Supress { get; set; }
    }
    /// <summary>
    /// Class containing data for the client
    /// </summary>
    public class Client
    {
        /// <summary>
        /// Connection from the server to the client
        /// </summary>
        public NetConnection NetConnection { get; private set; }
        /// <summary>
        /// Name of the player (Usually SC name)
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Display name of the player (Usually SC name but can be changed)
        /// </summary>
        public string DisplayName { get; set; }
        /// <summary>
        /// Latency of the client
        /// </summary>
        public float Latency { get; set; }
        /// <summary>
        /// Remote client version
        /// </summary>
        public ScriptVersion RemoteScriptVersion { get; set; }
        /// <summary>
        /// Remote game version
        /// </summary>
        public int GameVersion { get; set; }
        /// <summary>
        /// Last known position of the player
        /// </summary>
        public Vector3 LastKnownPosition { get; internal set; }
        /// <summary>
        /// Health of the player
        /// </summary>
        public int Health { get; internal set; }
        /// <summary>
        /// Vehicle health of the player
        /// </summary>
        public int VehicleHealth { get; internal set; }
        /// <summary>
        /// If the player is in a vehicle
        /// </summary>
        public bool IsInVehicle { get; internal set; }
        /// <summary>
        /// If the player is AFK
        /// Note: ReSharper is suggesting 'Afk' as a method name'
        /// </summary>
        public bool afk { get; set; }
        /// <summary>
        /// If they got kicked
        /// </summary>
        public bool Kicked { get; set; }
        /// <summary>
        /// Reason the player was kicked
        /// </summary>
        public string KickReason { get; set; }
        /// <summary>
        /// Who the player was kicked by
        /// </summary>
        public Client KickedBy { get; set; }
        /// <summary>
        /// If the player is silent/muted (?)
        /// </summary>
        public bool Silent { get; set; }
        /// <summary>
        /// GeoIP response
        /// </summary>
        public MaxMind.GeoIP2.Responses.CountryResponse geoIP { get; set; }

        public Client(NetConnection nc)
        {
            NetConnection = nc;
        }
    }

    /// <summary>
    /// Notification icon types
    /// </summary>
    public enum NotificationIconType
    {
        /// <summary>
        /// Chatbox notification icon
        /// </summary>
        Chatbox = 1,
        /// <summary>
        /// Email notification icon
        /// </summary>
        Email = 2,
        /// <summary>
        /// Friend request icon
        /// </summary>
        AddFriendRequest = 3,
        /// <summary>
        /// No icon? Not sure
        /// TODO: Test this
        /// </summary>
        Nothing = 4,
        /// <summary>
        /// Right jumping arrow icon? Not sure
        /// TODO: Test this
        /// </summary>
        RightJumpingArrow = 7,
        /// <summary>
        /// RP icon
        /// </summary>
        RP_Icon = 8,
        /// <summary>
        /// Dollar sign icon
        /// </summary>
        DollarIcon = 9,
    }

    /// <summary>
    /// Notification picture type
    /// </summary>
    public enum NotificationPicType
    {
        /// <summary>
        /// Default profile pic
        /// </summary>
        CHAR_DEFAULT,
        /// <summary>
        /// Facebook icon
        /// </summary>
        CHAR_FACEBOOK,
        /// <summary>
        /// Social club star profile pic
        /// </summary>
        CHAR_SOCIAL_CLUB,
        /// <summary>
        /// Super Auto San Andreas car site
        /// </summary>
        CHAR_CARSITE2,
        /// <summary>
        /// Boat site anchor
        /// </summary>
        CHAR_BOATSITE,
        /// <summary>
        /// Maze bank logo
        /// </summary>
        CHAR_BANK_MAZE,
        /// <summary>
        /// Fleeca bank
        /// </summary>
        CHAR_BANK_FLEECA,
        /// <summary>
        /// Bank bell
        /// </summary>
        CHAR_BANK_BOL,
        /// <summary>
        /// Minotaur icon
        /// </summary>
        CHAR_MINOTAUR,
        /// <summary>
        /// Epsilon E
        /// </summary>
        CHAR_EPSILON,
        /// <summary>
        /// Warstock W
        /// </summary>
        CHAR_MILSITE,
        /// <summary>
        /// Legendary Motorsports icon
        /// </summary>
        CHAR_CARSITE,
        /// <summary>
        /// Dr. Freidlander Face
        /// </summary>
        CHAR_DR_FRIEDLANDER,
        /// <summary>
        /// P and M Logo
        /// </summary>
        CHAR_BIKESITE,
        /// <summary>
        /// Lifeinvader
        /// </summary>
        CHAR_LIFEINVADER,
        /// <summary>
        /// Plane site
        /// </summary>
        CHAR_PLANESITE,
        /// <summary>
        /// Michael's Face
        /// </summary>
        CHAR_MICHAEL,
        /// <summary>
        /// Franklin's Face
        /// </summary>
        CHAR_FRANKLIN,
        /// <summary>
        /// Trevor's Face
        /// </summary>
        CHAR_TREVOR,
        /// <summary>
        /// Simeon's Face
        /// </summary>
        CHAR_SIMEON,
        /// <summary>
        /// Ron's Face
        /// </summary>
        CHAR_RON,
        /// <summary>
        /// Jimmy's Face
        /// </summary>
        CHAR_JIMMY,
        /// <summary>
        /// Lester's Face
        /// </summary>
        CHAR_LESTER,
        /// <summary>
        /// Dave's Face
        /// </summary>
        CHAR_DAVE,
        /// <summary>
        /// Chop's Face (...Do dogs have a face?)
        /// </summary>
        CHAR_LAMAR,
        /// <summary>
        /// Devin's Face
        /// </summary>
        CHAR_DEVIN,
        /// <summary>
        /// Amanda's Face
        /// </summary>
        CHAR_AMANDA,
        /// <summary>
        /// Tracey's Face
        /// </summary>
        CHAR_TRACEY,
        /// <summary>
        /// Stretch's Face
        /// </summary>
        CHAR_STRETCH,
        /// <summary>
        /// Wade's Face
        /// </summary>
        CHAR_WADE,
        /// <summary>
        /// Martin's Face
        /// </summary>
        CHAR_MARTIN,

    }

    public class test : MarshalByRefObject
    {
    }

    /// <summary>
    /// Game server proxy class, used for appdomains
    /// </summary>
    public class GameServerMarshalObject : MarshalByRefObject
    {
        private readonly GameServer _server;
        private ServerSettings _settings;
        private Thread _thread;

        public GameServerMarshalObject()
        {
            _server = new GameServer();
        }
        public void SetConfig(ServerSettings settings)
        {
            _settings = settings;
            _server.Name = settings.Name;
            _server.MaxPlayers = settings.MaxPlayers;
            _server.Port = settings.Port;
            _server.PasswordProtected = settings.PasswordProtected;
            _server.Password = settings.Password;
            _server.AnnounceSelf = settings.Announce;
            _server.MasterServer = settings.MasterServer;
            _server.AllowNickNames = settings.AllowDisplayNames;
            _server.AllowOutdatedClients = settings.AllowOutdatedClients;
            _server.GamemodeName = settings.Gamemode;
            _server.ConfigureServer();
        }

        public void StartServerThread()
        {
            _thread = new Thread(_server.Start);
            _thread.Start();
        }
    }
    /// <summary>
    /// Game server class
    /// </summary>
    public class GameServer
    {
        /// <summary>
        /// Location of the current server instance
        /// </summary>
        public string Location => AppDomain.CurrentDomain.BaseDirectory;
        public NetPeerConfiguration Config;
        public GameServer()
        {
            Clients = new List<Client>();
            MaxPlayers = 32;
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
            Config = new NetPeerConfiguration("GTAVOnlineRaces");
            Config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            Config.EnableMessageType(NetIncomingMessageType.DiscoveryRequest);
            Config.EnableMessageType(NetIncomingMessageType.UnconnectedData);
            Config.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);
        }
        /*public GameServer(int port, string name, string gamemodeName)
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
            NetPeerConfiguration config = new NetPeerConfiguration("GTAVOnlineRaces") {Port = port};
            config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            config.EnableMessageType(NetIncomingMessageType.DiscoveryRequest);
            config.EnableMessageType(NetIncomingMessageType.UnconnectedData);
            config.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);
            Server = new NetServer(config);
        }*/
        /// <summary>
        /// Server socket
        /// </summary>
        public NetServer Server;

        /// <summary>
        /// Maximum players
        /// </summary>
        public int MaxPlayers { get; set; }
        /// <summary>
        /// Port the server is on
        /// </summary>
        public int Port { get; set; }
        /// <summary>
        /// List of clients on the server
        /// </summary>
        public List<Client> Clients { get; set; }
        /// <summary>
        /// Name of the server
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Server password
        /// </summary>
        public string Password { get; set; }
        /// <summary>
        /// Password protected
        /// </summary>
        public bool PasswordProtected { get; set; }
        /// <summary>
        /// Server gamemode
        /// </summary>
        public string GamemodeName { get; set; }
        /// <summary>
        /// Master server address
        /// </summary>
        public string MasterServer { get; set; }
        /// <summary>
        /// Backup master server address
        /// </summary>
        public string BackupMasterServer { get; set; }
        /// <summary>
        /// If the server announces itself to the master server
        /// </summary>
        public bool AnnounceSelf { get; set; }

        /// <summary>
        /// If the server allows nicknames
        /// </summary>
        public bool AllowNickNames { get; set; }
        /// <summary>
        /// If the server allows outdated clients
        /// </summary>
        public bool AllowOutdatedClients { get; set; }
        /// <summary>
        /// Server-side script version
        /// </summary>
        public readonly ScriptVersion ServerVersion = ScriptVersion.VERSION_0_9_3;
        /// <summary>
        /// Gamemode ServerScript object
        /// Note: ReSharper is suggesting we change this variable's name to 'Gamemode'
        /// </summary>
        private ServerScript _gamemode { get; set; }

        /// <summary>
        /// List of loaded filterscripts
        /// </summary>
        private List<ServerScript> _filterscripts;
        /// <summary>
        /// Public IP of the server
        /// Note: ReSharper is suggesting we change this variable's name to 'WanIp'
        /// </summary>
        public string WanIP { get; set; }
        /// <summary>
        /// Private IP of the server
        /// Note: ReSharper is suggesting we change this variable's name to 'LanIp'
        /// </summary>
        public string LanIP { get; set; }
        /// <summary>
        /// IP of the last kicked player
        /// </summary>
        public string LastKicked { get; set; }
        /// <summary>
        /// CountryResponse of the last player to join
        /// Note: ReSharper is suggesting we change this variable's name to 'GeoIp'
        /// </summary>
        public MaxMind.GeoIP2.Responses.CountryResponse geoIP { get; set; }

        /// <summary>
        /// Time since last announcing self to master
        /// </summary>
        private DateTime _lastAnnounceDateTime;

        /// <summary>
        /// Sets all the config stuff for the server.
        /// Note - You must call this after any update to the config object.
        /// </summary>
        public void ConfigureServer()
        {
            Server = new NetServer(Config);
        }

        /// <summary>
        /// Start a game server with no filterscripts loaded.
        /// </summary>
        public void Start()
        {
            Start(new string[0]);
        }

        /// <summary>
        /// Start the game server
        /// </summary>
        /// <param name="filterscripts">List of filterscritps to load</param>
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
                        Program.DeleteFile(Location + "gamemodes" + Path.DirectorySeparatorChar + GamemodeName + ".dll:Zone.Identifier");
                    }
                    catch
                    {
                    }

                    var asm = Assembly.LoadFrom(Location + "gamemodes" + Path.DirectorySeparatorChar + GamemodeName + ".dll");
                    var types = asm.GetExportedTypes();
                    var validTypes = types.Where(t =>
                        !t.IsInterface &&
                        !t.IsAbstract)
                        .Where(t => typeof(ServerScript).IsAssignableFrom(t));
                    var enumerable = validTypes as Type[] ?? validTypes.ToArray();
                    if (!enumerable.Any())
                    {
                        Console.WriteLine("ERROR: No classes that inherit from ServerScript have been found in the assembly. Starting freeroam.");
                        return;
                    }

                    _gamemode = Activator.CreateInstance(enumerable.ToArray()[0]) as ServerScript;
                    if (_gamemode == null) Console.WriteLine("Could not create gamemode: it is null.");
                    else _gamemode.Start(this);
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
                        Program.DeleteFile(Location + "filterscripts" + Path.DirectorySeparatorChar + GamemodeName + ".dll:Zone.Identifier");
                    } catch { }

                    var fsAsm = Assembly.LoadFrom(Location + "filterscripts" + Path.DirectorySeparatorChar + path + ".dll");
                    var fsObj = InstantiateScripts(fsAsm);
                    list.AddRange(fsObj);
                } catch (Exception ex) {
                    Console.WriteLine("Failed to load filterscript \"" + path + "\", error: " + ex.ToString());
                }
            }

            list.ForEach(fs =>
            {
                fs.Start(this);
                Console.WriteLine("Starting filterscript " + fs.Name + "...");
            });
            _filterscripts = list;
            PrintServerInfo(); PrintPlayerList();
        }

        /// <summary>
        /// Announce server to master
        /// </summary>
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
                    Console.WriteLine("Failed to announce self: master server is not available at this time. Using fallback server...");
                    try
                    {
                        wb.UploadData(BackupMasterServer, Encoding.UTF8.GetBytes(Port.ToString()));
                    }
                    catch (WebException)
                    {
                        Console.WriteLine("Failed to announce self: backup master server is not available at this time. Trying again later...");

                    }
                }
            }
        }

        /// <summary>
        /// Load a new .dll of server scripts into the server
        /// </summary>
        /// <param name="targetAssembly">Assembly reference to the dll</param>
        /// <returns>A list of ServerScript objects </returns>
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
        /// <summary>
        /// Log something to the console
        /// </summary>
        /// <param name="flag">Flag (TODO: Make a LogFlags enum)</param>
        /// <param name="debug">If the message is a debug mode</param>
        /// <param name="module">Module/plugin the log message is from</param>
        /// <param name="message">Message to log</param>
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
        /// <summary>
        /// Run every tick
        /// </summary>
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
                            try { Console.Write("Script Version: ["+connReq.ScriptVersion.ToString()+ "] "+(ScriptVersion)connReq.ScriptVersion+" | "); } catch (Exception) { }
                            try { Console.Write("IP: " + msg.SenderEndPoint.Address.ToString() + ":" + msg.SenderEndPoint.Port.ToString() + " | "); } catch (Exception) { }
                            Console.Write("\n");
                            if (!AllowOutdatedClients && (ScriptVersion)connReq.ScriptVersion != Enum.GetValues(typeof(ScriptVersion)).Cast<ScriptVersion>().Last())
                            {
                                var ReadableScriptVersion = Enum.GetValues(typeof(ScriptVersion)).Cast<ScriptVersion>().Last().ToString();
                                ReadableScriptVersion = Regex.Replace(ReadableScriptVersion, "VERSION_", "", RegexOptions.IgnoreCase);
                                ReadableScriptVersion = Regex.Replace(ReadableScriptVersion, "_", ".", RegexOptions.IgnoreCase);
                                LogToConsole(3, true, "Network", "Client " + connReq.DisplayName + " tried to connect with outdated scriptversion " + connReq.ScriptVersion.ToString() + " but the server requires " + Enum.GetValues(typeof(ScriptVersion)).Cast<ScriptVersion>().Last().ToString());
                                DenyPlayer(client, string.Format("Update to GTACoop v{0} from bit.ly/gtacoop", ReadableScriptVersion), true, msg); continue;
                            }else if (AllowOutdatedClients && (ScriptVersion)connReq.ScriptVersion != Enum.GetValues(typeof(ScriptVersion)).Cast<ScriptVersion>().Last())
                            {
                                SendNotificationToPlayer(client, "~r~You are using a outdated version of GTA Coop.~w~");
                                SendNotificationToPlayer(client, "~h~If you have lags or issues, update your mod!~h~", true);
                            }
                            if ((ScriptVersion)connReq.ScriptVersion == ScriptVersion.VERSION_UNKNOWN)
                            {
                                LogToConsole(3, true, "Network", "Client " + connReq.DisplayName + " tried to connect with unknown scriptversion " + connReq.ScriptVersion.ToString());
                                DenyPlayer(client, "Unknown version. Please redownload GTA Coop from bit.ly/gtacoop", true, msg); continue;
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
                                    while (AllowNickNames && Clients.Any(c => c.DisplayName == connReq.DisplayName))
                                    {
                                        duplicate++;

                                        connReq.DisplayName = displayname + " {" + duplicate + "}";
                                    }

                                    Clients.Add(client);
                                }
                                client.Name = connReq.Name;
                                client.DisplayName = AllowNickNames ? connReq.DisplayName : connReq.Name;

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
                                var path = Location + "geoip.mmdb";
                                try
                                {
                                    using (var reader = new DatabaseReader(path))
                                    {
                                        client.geoIP = reader.Country(client.NetConnection.RemoteEndPoint.Address);
                                    }
                                }
                                catch (Exception ex) { LogToConsole(3, false, "GeoIP", ex.Message); }
                                if (_gamemode != null) sendMsg = sendMsg && _gamemode.OnPlayerConnect(client);
                                _filterscripts?.ForEach(fs => sendMsg = sendMsg && fs.OnPlayerConnect(client));
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
                                        _filterscripts?.ForEach(fs => sendMsg = sendMsg && fs.OnPlayerDisconnect(client));
                                        if (client.NetConnection.RemoteEndPoint.Address.ToString().Equals(LastKicked)) { client.Silent = true; }
                                        if (sendMsg && !client.Silent)
                                            if (client.Kicked)
                                            {
                                                if (!client.KickReason.Equals(""))
                                                {
                                                    if(client.KickedBy != null)
                                                    {
                                                        SendNotificationToAll("~h~" + client.DisplayName + "~h~~w~ was kicked by "+ client.KickedBy.DisplayName +"~w~ for " + client.KickReason);
                                                    } else
                                                    {
                                                        SendNotificationToAll("~h~" + client.DisplayName + "~h~~w~ was kicked for " + client.KickReason);
                                                    }
                                                }
                                                else
                                                {
                                                    if (client.KickedBy != null)
                                                    {
                                                        SendNotificationToAll("~h~" + client.DisplayName + "~h~~w~ has been kicked by "+ client.KickedBy.DisplayName+"~w~.");
                                                    }
                                                    else
                                                    {
                                                        SendNotificationToAll("~h~" + client.DisplayName + "~h~~w~ has been kicked.");
                                                    }
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
                                        
                                        if (client.Kicked)
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

                                                _filterscripts?.ForEach(fs => Msg = fs.OnChatMessage(Msg));

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
            }catch(Exception ex) { LogToConsole(4, false, "", "Can't handle tick: "+ex.ToString()); }
        }

        /// <summary>
        /// Prints info about the server
        /// </summary>
        public void Infoscreen()
        {
            while (true)
            {
                PrintServerInfo();
                PrintPlayerList();
                Thread.Sleep(60000);
            }
        }
        /// <summary>
        /// Prints a list of players
        /// </summary>
        /// <param name="message"></param>
        public void PrintPlayerList(string message = "Online Players: ")
        {
            for (var i = 0; i < Clients.Count; i++)
            {
                PrintPlayerInfo(Clients[i], "#"+i.ToString()+ " ");
            }
        }
        /// <summary>
        /// Prints info about a player to the console
        /// </summary>
        /// <param name="client">Client to print info for</param>
        /// <param name="message">Prefix to the info</param>
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
        /// <summary>
        /// Prints server info
        /// </summary>
        /// <param name="message">Message prefix</param>
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

        /// <summary>
        /// Stop the server
        /// </summary>
        public void Stop()
        {
            foreach (Client player in Clients)
            {
                KickPlayer(player, "Server shutting down");
            }
            Server.Shutdown("Stopping server");
        }

        /// <summary>
        /// Send a packet to all players
        /// </summary>
        /// <param name="newData">Object to send</param>
        /// <param name="packetType">Packet type</param>
        /// <param name="important">If the packet is important (does it need to be sent in a specific order)</param>
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

        /// <summary>
        /// Send a packet to all but one
        /// </summary>
        /// <param name="newData">Object to send</param>
        /// <param name="packetType">Packet type</param>
        /// <param name="important">If the packet is important</param>
        /// <param name="exclude">Client to exclude from the message</param>
        public void SendToAll(object newData, PacketType packetType, bool important, Client exclude)
        {
            var data = SerializeBinary(newData);
            NetOutgoingMessage msg = Server.CreateMessage();
            msg.Write((int)packetType);
            msg.Write(data.Length);
            msg.Write(data);
            Server.SendToAll(msg, exclude.NetConnection, important ? NetDeliveryMethod.ReliableOrdered : NetDeliveryMethod.ReliableSequenced, GetChannelIdForConnection(exclude));
        }

        /// <summary>
        /// Deserialize a binary packet
        /// </summary>
        /// <typeparam name="T">Type expected from the packet</typeparam>
        /// <param name="data">Byte array of packet data</param>
        /// <returns>Deserialized object for packet</returns>
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

        /// <summary>
        /// Serialize an object into a byte array
        /// </summary>
        /// <param name="data">Object to serialize</param>
        /// <returns>What the data returns</returns>
        public byte[] SerializeBinary(object data)
        {
            using (var stream = new MemoryStream())
            {
                Serializer.Serialize(stream, data);
                return stream.ToArray();
            }
        }
        /// <summary>
        /// Get channel ID for a client connection
        /// </summary>
        /// <param name="conn">Client to get the channel ID for</param>
        /// <returns>Channel ID of client</returns>
        public int GetChannelIdForConnection(Client conn)
        {
            lock (Clients) return (Clients.IndexOf(conn) % 31) + 1;
        }

        /// <summary>
        /// Parse native arguments sent from client
        /// </summary>
        /// <param name="args">Object array of native args</param>
        /// <returns>List of NativeArguments</returns>
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

        /// <summary>
        /// Send a native call to a player
        /// </summary>
        /// <param name="player">Client to send the call to</param>
        /// <param name="hash">Native call hash</param>
        /// <param name="arguments">Arguments to native call</param>
        public void SendNativeCallToPlayer(Client player, ulong hash, params object[] arguments)
        {
            var obj = new NativeData
            {
                Hash = hash,
                Arguments = ParseNativeArguments(arguments)
            };

            var bin = SerializeBinary(obj);

            var msg = Server.CreateMessage();

            msg.Write((int)PacketType.NativeCall);
            msg.Write(bin.Length);
            msg.Write(bin);

            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, GetChannelIdForConnection(player));
        }

        /// <summary>
        /// Send a native call to all players
        /// </summary>
        /// <param name="hash">Hash of the native call</param>
        /// <param name="arguments">Arguments to the native call</param>
        public void SendNativeCallToAllPlayers(ulong hash, params object[] arguments)
        {
            var obj = new NativeData
            {
                Hash = hash,
                Arguments = ParseNativeArguments(arguments),
                ReturnType = null,
                Id = null
            };

            var bin = SerializeBinary(obj);

            var msg = Server.CreateMessage();

            msg.Write((int)PacketType.NativeCall);
            msg.Write(bin.Length);
            msg.Write(bin);

            Server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        /// <summary>
        /// Set a native call to be run on a tick
        /// </summary>
        /// <param name="player">Player to run the call</param>
        /// <param name="identifier">Unique ID for the call</param>
        /// <param name="hash">Hash of the native</param>
        /// <param name="arguments">Arguments to the native</param>
        public void SetNativeCallOnTickForPlayer(Client player, string identifier, ulong hash, params object[] arguments)
        {
            var obj = new NativeData
            {
                Hash = hash,
                Arguments = ParseNativeArguments(arguments)
            };


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

        /// <summary>
        /// Set a native call to be run on a tick for all players
        /// </summary>
        /// <param name="identifier">Unique ID for the call</param>
        /// <param name="hash">Hash of the native</param>
        /// <param name="arguments">Arguments to the native call</param>
        public void SetNativeCallOnTickForAllPlayers(string identifier, ulong hash, params object[] arguments)
        {
            var obj = new NativeData
            {
                Hash = hash,
                Arguments = ParseNativeArguments(arguments)
            };


            var wrapper = new NativeTickCall
            {
                Identifier = identifier,
                Native = obj
            };

            var bin = SerializeBinary(wrapper);

            var msg = Server.CreateMessage();

            msg.Write((int)PacketType.NativeTick);
            msg.Write(bin.Length);
            msg.Write(bin);

            Server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }
        /// <summary>
        /// Remove a native call from being run on a tick for a specific player
        /// </summary>
        /// <param name="player">Client the native was being run on</param>
        /// <param name="identifier">Identifier for the native call</param>
        public void RecallNativeCallOnTickForPlayer(Client player, string identifier)
        {
            var wrapper = new NativeTickCall {Identifier = identifier};

            var bin = SerializeBinary(wrapper);

            var msg = Server.CreateMessage();
            msg.Write((int)PacketType.NativeTickRecall);
            msg.Write(bin.Length);
            msg.Write(bin);

            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, GetChannelIdForConnection(player));
        }
        /// <summary>
        /// Remove a native call from being run on a tick for all players
        /// </summary>
        /// <param name="identifier">Identifier for the native call</param>
        public void RecallNativeCallOnTickForAllPlayers(string identifier)
        {
            var wrapper = new NativeTickCall {Identifier = identifier};

            var bin = SerializeBinary(wrapper);

            var msg = Server.CreateMessage();
            msg.Write((int)PacketType.NativeTickRecall);
            msg.Write(bin.Length);
            msg.Write(bin);

            Server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }
        /// <summary>
        /// Set a native call to be run on disconnect for a player
        /// </summary>
        /// <param name="player">Player to run the native on</param>
        /// <param name="identifier">Identifier for the native</param>
        /// <param name="hash">Hash of the native call</param>
        /// <param name="arguments">Arguments to the native call</param>
        public void SetNativeCallOnDisconnectForPlayer(Client player, string identifier, ulong hash, params object[] arguments)
        {
            var obj = new NativeData
            {
                Hash = hash,
                Id = identifier,
                Arguments = ParseNativeArguments(arguments)
            };


            var bin = SerializeBinary(obj);

            var msg = Server.CreateMessage();

            msg.Write((int)PacketType.NativeOnDisconnect);
            msg.Write(bin.Length);
            msg.Write(bin);

            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, GetChannelIdForConnection(player));
        }
        /// <summary>
        /// Set a native call to be run no disconnect for all players
        /// </summary>
        /// <param name="identifier">Identifier for the native call</param>
        /// <param name="hash">Hash for the native call</param>
        /// <param name="arguments">Arguments for the native</param>
        public void SetNativeCallOnDisconnectForAllPlayers(string identifier, ulong hash, params object[] arguments)
        {
            var obj = new NativeData
            {
                Hash = hash,
                Id = identifier,
                Arguments = ParseNativeArguments(arguments)
            };

            var bin = SerializeBinary(obj);

            var msg = Server.CreateMessage();

            msg.Write((int)PacketType.NativeOnDisconnect);
            msg.Write(bin.Length);
            msg.Write(bin);

            Server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }
        /// <summary>
        /// Remove a native call from being run on disconnect for a player
        /// </summary>
        /// <param name="player">Player to remove it from</param>
        /// <param name="identifier">Identifier for the native call</param>
        public void RecallNativeCallOnDisconnectForPlayer(Client player, string identifier)
        {
            var obj = new NativeData {Id = identifier};

            var bin = SerializeBinary(obj);

            var msg = Server.CreateMessage();
            msg.Write((int)PacketType.NativeOnDisconnectRecall);
            msg.Write(bin.Length);
            msg.Write(bin);

            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, GetChannelIdForConnection(player));
        }
        /// <summary>
        /// Remove a native call from being run on disconnect for all players
        /// </summary>
        /// <param name="identifier">Identifier for the native call</param>
        public void RecallNativeCallOnDisconnectForAllPlayers(string identifier)
        {
            var obj = new NativeData {Id = identifier};

            var bin = SerializeBinary(obj);

            var msg = Server.CreateMessage();
            msg.Write((int)PacketType.NativeOnDisconnectRecall);
            msg.Write(bin.Length);
            msg.Write(bin);

            Server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        /// <summary>
        /// List of callbacks for data returned from native calls
        /// </summary>
        private Dictionary<string, Action<object>> _callbacks = new Dictionary<string, Action<object>>();

        /// <summary>
        /// Run a native on a player, then get the response
        /// </summary>
        /// <param name="player">Player to run the native on</param>
        /// <param name="salt">Salt for the native call</param>
        /// <param name="hash">Hash of the native call</param>
        /// <param name="returnType">NativeArgument return type</param>
        /// <param name="callback">Callback to call with the native response.</param>
        /// <param name="arguments">Arguments to the native call</param>
        public void GetNativeCallFromPlayer(Client player, string salt, ulong hash, NativeArgument returnType, Action<object> callback,
            params object[] arguments)
        {
            var obj = new NativeData
            {
                Hash = hash,
                ReturnType = returnType
            };
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
        /// <summary>
        /// Send a chat message to all players
        /// </summary>
        /// <param name="message">Message to send</param>
        public void SendChatMessageToAll(string message)
        {
            SendChatMessageToAll("", message);
        }
        /// <summary>
        /// Send a chat message to all players
        /// </summary>
        /// <param name="sender">Who sent the message</param>
        /// <param name="message">Message contents</param>
        public void SendChatMessageToAll(string sender, string message)
        {
            var chatObj = new ChatData()
            {
                Sender = sender,
                Message = message,
            };

            SendToAll(chatObj, PacketType.ChatData, true);
        }
        /// <summary>
        /// Send a chat message to a player
        /// </summary>
        /// <param name="player">Player to send the chat message to</param>
        /// <param name="message">Message contents</param>
        public void SendChatMessageToPlayer(Client player, string message)
        {
            SendChatMessageToPlayer(player, "", message);
        }
        /// <summary>
        /// Send a chat message to a player
        /// </summary>
        /// <param name="player">Player to send chat message to</param>
        /// <param name="sender">Who sent the message</param>
        /// <param name="message">Message contents</param>
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
        /// <summary>
        /// Give a player a weapon
        /// </summary>
        /// <param name="player">Player to give weapon to</param>
        /// <param name="weaponHash">Hash of the weapon</param>
        /// <param name="ammo">How much ammo to give them</param>
        /// <param name="equipNow">Whether we want the player to equip the weapon now</param>
        /// <param name="ammoLoaded">Whether we want the ammo to be loaded now or not</param>
        public void GivePlayerWeapon(Client player, uint weaponHash, int ammo, bool equipNow, bool ammoLoaded)
        {
            SendNativeCallToPlayer(player, 0xBF0FD6E56C964FCB, new LocalPlayerArgument(), weaponHash, ammo, equipNow, ammo);
        }
        /// <summary>
        /// Kick a player from the server
        /// </summary>
        /// <param name="player">Player to kick from the server</param>
        /// <param name="reason">Reason for kicking the player</param>
        /// <param name="silent">Whether or not the kick is silent</param>
        /// <param name="sender"></param>
        public void KickPlayer(Client player, string reason, bool silent = false, Client sender = null)
        {
            player.Kicked = true; // Wtf Bluscream, are you allergic to newlines? This was all one line before...
            player.KickReason = reason.ToString();
            player.Silent = silent;
            player.KickedBy = sender;
            player.NetConnection.Disconnect("Kicked: " + reason);
        }
        /// <summary>
        /// Deny a player from connecting
        /// </summary>
        /// <param name="player">Player to deny from connecting</param>
        /// <param name="reason">Reason for connection denial</param>
        /// <param name="silent">Whether to silently deny the player</param>
        /// <param name="msg">Message the player was denied with (only passed for recycling)</param>
        /// <param name="duration">How long to deny the player (default: 60, currently unused)</param>
        public void DenyPlayer(Client player, string reason, bool silent = true, NetIncomingMessage msg = null, int duration = 60)
        {
            _gamemode?.OnConnectionRefused(player, reason);
            _filterscripts?.ForEach(fs => fs.OnConnectionRefused(player, reason));
            player.NetConnection.Deny(reason);

            Console.ForegroundColor = ConsoleColor.DarkRed;
            PrintPlayerInfo(player, "Connection Denied: "+ reason + " || ");
            Console.ResetColor();

            if (!silent)
                SendNotificationToAll(player.DisplayName + " was rejected by the server: " + reason);
            
            string _ip = player.NetConnection.RemoteEndPoint.Address.ToString();
            Clients.Remove(player);
            if (msg != null) Server.Recycle(msg);
            //BlockIP(_ip, "GTAServer Block (" + _ip + ")", duration);
        }

        /*public void DenyConnection(Client player, string reason, bool silent = true, NetIncomingMessage msg = null)
        {
            player. (reason);
            Console.ForegroundColor = ConsoleColor.DarkRed; PrintPlayerInfo(player, "Connection Denied: " + reason + " || "); Console.ResetColor();
            if (!silent) SendNotificationToAll(player.DisplayName + " was rejected by the server: " + reason);
            Clients.Remove(player);if (msg != null) Server.Recycle(msg);
        }*/
        /// <summary>
        /// Teleport a player
        /// </summary>
        /// <param name="player">Player to teleport</param>
        /// <param name="newPosition">New player position</param>
        public void SetPlayerPosition(Client player, Vector3 newPosition)
        {
            SendNativeCallToPlayer(player, 0x06843DA7060A026B, new LocalPlayerArgument(), newPosition.X, newPosition.Y, newPosition.Z, 0, 0, 0, 1);
        }

        /// <summary>
        /// Get a player's position
        /// </summary>
        /// <param name="player">Player to get position of</param>
        /// <param name="callback">Callback to call with the result</param>
        /// <param name="salt">Salt of the native call</param>
        public void GetPlayerPosition(Client player, Action<object> callback, string salt = "salt")
        {
            GetNativeCallFromPlayer(player,
                salt,
                0x3FEF770D40960D5A, new Vector3Argument(), callback, new LocalPlayerArgument(), 0);
        }

        /// <summary>
        /// Checks if a player control has been pressed
        /// </summary>
        /// <param name="player">Player to check if a control has been pressed on</param>
        /// <param name="controlId">Control ID</param>
        /// <param name="callback">Callback to call with the result</param>
        /// <param name="salt">Salt of the native call</param>
        public void HasPlayerControlBeenPressed(Client player, int controlId, Action<object> callback, string salt = "salt")
        {
            GetNativeCallFromPlayer(player, salt,
                0x580417101DDB492F, new BooleanArgument(), callback, 0, controlId);
        }

        /// <summary>
        /// Set a player's health
        /// </summary>
        /// <param name="player">Player</param>
        /// <param name="health">New health</param>
        public void SetPlayerHealth(Client player, int health)
        {
            SendNativeCallToPlayer(player, 0x6B76DC1F3AE6E6A3, new LocalPlayerArgument(), health + 100);
        }

        /// <summary>
        /// Send a notification to a player
        /// </summary>
        /// <param name="player">Player to send notification to</param>
        /// <param name="message">Message in the notification</param>
        /// <param name="flashing">Whether to flash the notification</param>
        public void SendNotificationToPlayer(Client player, string message, bool flashing = false)
        {
            for (int i = 0; i < message.Length; i += 99)
            {
                SendNativeCallToPlayer(player, 0x202709F4C58A0424, "STRING");
                SendNativeCallToPlayer(player, 0x6C188BE134E074AA, message.Substring(i, Math.Min(99, message.Length - i)));
                SendNativeCallToPlayer(player, 0xF020C96915705B3A, flashing, true);
            }
        }
        /// <summary>
        /// Send a notification to all players
        /// </summary>
        /// <param name="message">Message in the notification</param>
        /// <param name="flashing">Whether to flash the notification</param>
        public void SendNotificationToAll(string message, bool flashing = false)
        {
            for (int i = 0; i < message.Length; i += 99)
            {
                SendNativeCallToAllPlayers(0x202709F4C58A0424, "STRING");
                SendNativeCallToAllPlayers(0x6C188BE134E074AA, message.Substring(i, Math.Min(99, message.Length - i)));
                SendNativeCallToAllPlayers(0xF020C96915705B3A, flashing, true);
            }
        }

        /// <summary>
        /// Send a picture notification to a player
        /// </summary>
        /// <param name="player">Player to send the notification to</param>
        /// <param name="body">Body of message</param>
        /// <param name="pic">NotificationPicType for message</param>
        /// <param name="flash">Times to flash the message</param>
        /// <param name="iconType">NotificationIconType of the message</param>
        /// <param name="sender">Sender of the message</param>
        /// <param name="subject">Subject of them essage</param>
        public void SendPictureNotificationToPlayer(Client player, string body, NotificationPicType pic, int flash, NotificationIconType iconType, string sender, string subject)
        {
            //Crash with new LocalPlayerArgument()!
            SendNativeCallToPlayer(player, 0x202709F4C58A0424, "STRING");
            SendNativeCallToPlayer(player, 0x6C188BE134E074AA, body);
            SendNativeCallToPlayer(player, 0x1CCD9A37359072CF, pic.ToString(), pic.ToString(), flash, (int)iconType, sender, subject);
            SendNativeCallToPlayer(player, 0xF020C96915705B3A, false, true);
        }
        /// <summary>
        /// Send a picture notification to all players
        /// </summary>
        /// <param name="body">Body of message</param>
        /// <param name="pic">NotificationPicType for message</param>
        /// <param name="flash">Times to flash the message</param>
        /// <param name="iconType">NotificationIconType of the message</param>
        /// <param name="sender">Sender of the message</param>
        /// <param name="subject">Subject of them essage</param>
        public void SendPictureNotificationToAll(string body, NotificationPicType pic, int flash, NotificationIconType iconType, string sender, string subject)
        {
            //Crash with new LocalPlayerArgument()!
            SendNativeCallToAllPlayers(0x202709F4C58A0424, "STRING");
            SendNativeCallToAllPlayers(0x6C188BE134E074AA, body);
            SendNativeCallToAllPlayers(0x1CCD9A37359072CF, pic.ToString(), pic.ToString(), flash, (int)iconType, sender, subject);
            SendNativeCallToAllPlayers(0xF020C96915705B3A, false, true);
        }
        /// <summary>
        /// Send a picture notification to all players
        /// </summary>
        /// <param name="body">Body of message</param>
        /// <param name="pic">NotificationPicType for message</param>
        /// <param name="flash">Times to flash the message</param>
        /// <param name="iconType">NotificationIconType of the message</param>
        /// <param name="sender">Sender of the message</param>
        /// <param name="subject">Subject of them essage</param>
        public void SendPictureNotificationToAll(string body, string pic, int flash, int iconType, string sender, string subject)
        {
            //Crash with new LocalPlayerArgument()!
            SendNativeCallToAllPlayers(0x202709F4C58A0424, "STRING");
            SendNativeCallToAllPlayers(0x6C188BE134E074AA, body);
            SendNativeCallToAllPlayers(0x1CCD9A37359072CF, pic, pic, flash, iconType, sender, subject);
            SendNativeCallToAllPlayers(0xF020C96915705B3A, false, true);
        }

        /// <summary>
        /// Get a player's health
        /// </summary>
        /// <param name="player">Player to get health of</param>
        /// <param name="callback">Callback to call with result</param>
        /// <param name="salt">Salt of native call</param>
        public void GetPlayerHealth(Client player, Action<object> callback, string salt = "salt")
        {
            GetNativeCallFromPlayer(player, salt,
                0xEEF059FAD016D209, new IntArgument(), callback, new LocalPlayerArgument());
        }
		/// <summary>
        /// Set night vision status of player
        /// </summary>
        /// <param name="player">Player to set night vision of</param>
        /// <param name="status">Night vision true/false</param>
	    public void ToggleNightVisionForPlayer(Client player, bool status)
        {
            SendNativeCallToPlayer(player, 0x18F621F7A5B1F85D, status);
        }
		/// <summary>
        /// Toggle night vision for all players
        /// </summary>
        /// <param name="status">Night vision true/false</param>
        public void ToggleNightVisionForAll(bool status)
        {
            SendNativeCallToAllPlayers(0x18F621F7A5B1F85D, status);
        }
        /// <summary>
        /// Checks if night vision is active
        /// </summary>
        /// <param name="player">Player to check if night vision is active on</param>
        /// <param name="callback">Callback to call with result</param>
        /// <param name="salt">Salt for native call</param>
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

        /// <summary>
        /// Block an IP in the firewall
        /// TODO: Make this not use the firewall and instead do an IP ban on the application side, like most software.
        /// TODO: Make this work with more than just windows if we decide for some reason to keep using the firewall...
        /// </summary>
        /// <param name="ip">IP to block</param>
        /// <param name="name">Name of firewall rule</param>
        /// <param name="duration">Length to block IP</param>
        /// <param name="port">Port to block them on</param>
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
        /// <summary>
        /// Checks if server is running as admin.
        /// IMO the only reason that this should be used is to check if the user is _not_ admin when they start a server... servers running as admin is a terrible idea in general 
        /// (think: any exploits in the server? person who exploits them gets admin.)
        /// </summary>
        /// <returns>If the server is running as admin</returns>
        public static bool IsAdministrator()
        {
            return (new WindowsPrincipal(WindowsIdentity.GetCurrent()))
                    .IsInRole(WindowsBuiltInRole.Administrator);
        }
        /*public static void cmdExec2(string exec, string arg = null, string app = @"C:\Windows\System32\cmd.exe", bool waitForExit = false)
        {
            var psi = new ProcessStartupInfo
            {
                Arguments = $"advfirewall firewall add rule name={name} description=Block inbound traffic from {ip} over TCP port {port} dir=in interface=any action=block localport={port} remoteip={ip} && ping 127.0.0.1 -n {duration * 1000 + 1} > nul && advfirewall firewall delete rule dir=in localport={port} remoteip={ip}",
                FileName = "netsh.exe",
            using (var process = Process.Start(psi))
            {
                process.WaitForExit();
            }
        }*/
        /// <summary>
        /// Run a command on the system
        /// </summary>
        /// <param name="exec">Command to run</param>
        /// <param name="arg">Command arguments</param>
        /// <param name="app">Application to run the command in (default: cmd.exe)</param>
        /// <param name="waitForExit">Wait for the command to exit</param>
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
            /*using (var process = cmdProcess.Start())
            {
                process.WaitForExit();
            }*/
            if (!string.IsNullOrEmpty(exec))
            {
                cmdProcess.StandardInput.WriteLine(exec);
            }
            if (waitForExit)
            {
                cmdProcess.WaitForExit();
            }
        }
        /// <summary>
        /// Read from a command
        /// </summary>
        /// <param name="exec">Command to run</param>
        /// <param name="arg">Arguments to command</param>
        /// <param name="app">Application to run the command in (default: cmd.exe)</param>
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

        /// <summary>
        /// No idea, to be honest. Called with a DataReceivedEventArgs...
        /// </summary>
        /// <param name="sender">Unused...</param>
        /// <param name="e">DataReceivedEventArgs</param>
        static void cmd_DataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine("Output from other process");
            Console.WriteLine(e.Data);
        }

        /// <summary>
        /// No idea, to be honest. Called with a DataReceivedEventArgs...
        /// </summary>
        /// <param name="sender">Unused...</param>
        /// <param name="e">DataReceivedEventArgs</param>
        static void cmd_Error(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine("Error from other process");
            Console.WriteLine(e.Data);
        }
        }
    }
