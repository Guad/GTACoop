﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;
using Lidgren.Network;
using NativeUI;
using Newtonsoft.Json;
using ProtoBuf;
using Control = GTA.Control;
//using GTAServer;
using System.Text.RegularExpressions;
using System.Globalization;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Model;

namespace GTACoOp
{
    public class Main : Script
    {
        public static PlayerSettings PlayerSettings;
        public static ServerSettings ServerSettings;
        public static ScriptVersion LocalScriptVersion = ScriptVersion.VERSION_0_9_3;

        private readonly UIMenu _mainMenu;
        public UIMenu _serverBrowserMenu;
        private readonly UIMenu _playersMenu;
        //private readonly UIMenu _playerMenu;
        private readonly UIMenu _settingsMenu;
        private readonly UIMenu _serverMenu;
        private readonly MenuPool _menuPool;

        private string _clientIp;
        private string _masterIP;
        private readonly Chat _chat;

        private static NetClient _client;
        private static NetPeerConfiguration _config;

        public static SynchronizationMode GlobalSyncMode;

        private static int _channel;

        private readonly Queue<Action> _threadJumping;
        private string _password;
        private bool _lastDead;
        private bool _wasTyping;
        private bool _serverRunning = false;
        private DebugWindow _debug;

        // STATS
        private static int _bytesSent = 0;
        private static int _bytesReceived = 0;

        private static int _messagesSent = 0;
        private static int _messagesReceived = 0;
        private string _lastIP = "";
        private int _lastPort = 4499;
        //

        // Debug stuff
        private bool display;
        private Ped mainPed;
        private Vehicle mainVehicle;

        private GTA.Math.Vector3 oldplayerpos;
        private bool _lastJumping;
        private bool _lastShooting;
        private bool _lastAiming;
        private uint _switch;
        private bool _lastVehicle;
        private bool _isGoingToCar;
        //

        public static Dictionary<long, SyncPed> Opponents;
        public static Dictionary<string, SyncPed> Npcs;
        public static float Latency;
        private int Port;

        private static Dictionary<int, int> _emptyVehicleMods;
        private Dictionary<string, NativeData> _tickNatives;
        private Dictionary<string, NativeData> _dcNatives;
        private List<int> _entityCleanup;
        private List<int> _blipCleanup;

        private static int _modSwitch = 0;
        private static int _pedSwitch = 0;
        private static Dictionary<int, int> _vehMods = new Dictionary<int, int>();
        private static Dictionary<int, int> _pedClothes = new Dictionary<int, int>();

        private enum NativeType
        {
            Unknown = 0,
            ReturnsBlip = 1,
            ReturnsEntity = 2,
            ReturnsEntityNeedsModel1 = 3,
            ReturnsEntityNeedsModel2 = 4,
            ReturnsEntityNeedsModel3 = 5,
        }
        public string ReadableScriptVersion()
        {
             return Regex.Replace(Regex.Replace(LocalScriptVersion.ToString(), "VERSION_", "", RegexOptions.IgnoreCase), "_", ".", RegexOptions.IgnoreCase);
        }
        public Main()
        {
            PlayerSettings = Util.ReadSettings(Program.Location + Path.DirectorySeparatorChar + "ClientSettings.xml");
            ServerSettings = Util.ReadServerSettings(Program.Location + Path.DirectorySeparatorChar + "ServerSettings.xml");
            //ServerSettings = new ServerSettings();
            _threadJumping = new Queue<Action>();

            Opponents = new Dictionary<long, SyncPed>();
            Npcs = new Dictionary<string, SyncPed>();
            _tickNatives = new Dictionary<string, NativeData>();
            _dcNatives = new Dictionary<string, NativeData>();

            _entityCleanup = new List<int>();
            _blipCleanup = new List<int>();

            _emptyVehicleMods = new Dictionary<int, int>();
            for (int i = 0; i < 50; i++) _emptyVehicleMods.Add(i, 0);

            _chat = new Chat();
            _chat.OnComplete += (sender, args) =>
            {
                var message = _chat.CurrentInput;
                if (!string.IsNullOrWhiteSpace(message))
                {
                    var obj = new ChatData()
                    {
                        Message = message,
                    };
                    var data = SerializeBinary(obj);

                    var msg = _client.CreateMessage();
                    msg.Write((int)PacketType.ChatData);
                    msg.Write(data.Length);
                    msg.Write(data);
                    _client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, 1);
                }
                _chat.IsFocused = false;
            };

            Tick += OnTick;
            KeyDown += OnKeyDown;

            KeyUp += (sender, args) =>
            {
                if (args.KeyCode == Keys.Escape && _wasTyping)
                {
                    _wasTyping = false;
                }
            };

            _config = new NetPeerConfiguration("GTAVOnlineRaces");
            _config.Port = 8888;
            _config.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);


            #region Menu Set up
            // #warning Affects performance when open, drops from 80~100 on a GTX 980 to high 30s ~ 60
            _menuPool = new MenuPool();

            _mainMenu = new UIMenu("Co-oP", "MAIN MENU");
            _settingsMenu = new UIMenu("Co-oP", "CLIENT SETTINGS");
            _serverBrowserMenu = new UIMenu("Co-oP", "SERVER BROWSER");
            _playersMenu = new UIMenu("Co-oP", "PLAYER LIST");
            //_serverMenu = new UIMenu("Co-oP", "SERVER SETTINGS");
            //_playerMenu = new UIMenu("Co-oP", "PLAYER OPTIONS");

            var browserItem = new UIMenuItem("Server Browser");
            _mainMenu.BindMenuToItem(_serverBrowserMenu, browserItem);
            browserItem.Activated += (sender, item) => RebuildServerBrowser();
            _serverBrowserMenu.SetMenuWidthOffset(300);

            var listenItem = new UIMenuItem("Server IP");
            listenItem.SetRightLabel(PlayerSettings.LastIP);
            listenItem.Activated += (menu, item) =>
            {
                _clientIp = Game.GetUserInput(255);
                if (!string.IsNullOrWhiteSpace(_clientIp))
                {
                    PlayerSettings.LastIP = _clientIp;
                    Util.SaveSettings(null);
                    listenItem.SetRightLabel(PlayerSettings.LastIP);
                }
            };

            var portItem = new UIMenuItem("Port");
            portItem.SetRightLabel(PlayerSettings.LastPort.ToString());
            portItem.Activated += (menu, item) =>
            {
                string newPort = Game.GetUserInput(5);
                int nPort; bool success = int.TryParse(newPort, out nPort);
                if (!success)
                {
                    UI.Notify("Wrong port format.");
                    return;
                }
                Port = nPort;
                PlayerSettings.LastPort = nPort;
                Util.SaveSettings(null);
                portItem.SetRightLabel(nPort.ToString());
            };
            
            var passItem = new UIMenuItem("Password");
            if (PlayerSettings.HidePasswords)
            {
                passItem.SetRightLabel(new String('*', PlayerSettings.LastPassword.Length));
            }
            else
            {
                passItem.SetRightLabel(PlayerSettings.LastPassword.ToString());
            }
            passItem.Activated += (menu, item) =>
            {
                string _LastPassword = Game.GetUserInput(255);
                if (!string.IsNullOrEmpty(_LastPassword))
                {
                    PlayerSettings.DisplayName = _LastPassword;
                    Util.SaveSettings(null);
                    if (PlayerSettings.HidePasswords)
                    {
                        passItem.SetRightLabel(new String('*', PlayerSettings.LastPassword.Length));
                    }
                    else
                    {
                        passItem.SetRightLabel(PlayerSettings.LastPassword.ToString());
                    }
                }
            };

            var connectItem = new UIMenuItem("Connect");
            _clientIp = PlayerSettings.LastIP;
            connectItem.Activated += (sender, item) =>
            {
                if (!IsOnServer())
                {
                    if (string.IsNullOrEmpty(_clientIp))
                    {
                        UI.Notify("No IP adress specified.");
                        return;
                    }

                    ConnectToServer(_clientIp, Port);
                }
                else
                {
                    if (_client != null) _client.Disconnect("Connection closed by peer.");
                }
            };

            var settItem = new UIMenuItem("Client Settings");
            _mainMenu.BindMenuToItem(_settingsMenu, settItem);

            //var serverItem = new UIMenuItem("Server Settings");
            //_mainMenu.BindMenuToItem(_serverMenu, serverItem);

            var playersItem = new UIMenuItem("Player List");
            _mainMenu.BindMenuToItem(_playersMenu, playersItem);
            playersItem.Activated += (sender, item) => RebuildPlayersList();

            var aboutItem = new UIMenuItem("~g~GTA V~w~ Coop mod v" + ReadableScriptVersion() + " by ~b~Bluscream~w~.")
            {
                Enabled = true
            };
            aboutItem.Activated += (sender, item) =>
            {
                UI.Notify("GTA V Coop mod by Guad, temporary continued by Bluscream and wolfmitchell.");
                UI.Notify("Mod Version: " + ReadableScriptVersion());
                UI.Notify("https://pydio.studiowolfree.com/public/gtacoop");
            };

            _mainMenu.AddItem(browserItem);
            _mainMenu.AddItem(connectItem);
            _mainMenu.AddItem(listenItem);
            _mainMenu.AddItem(portItem);
            _mainMenu.AddItem(passItem);
            _mainMenu.AddItem(settItem);
            //_mainMenu.AddItem(serverItem);
            _mainMenu.AddItem(playersItem);
            _mainMenu.AddItem(aboutItem);


            var nameItem = new UIMenuItem("Display Name");
            nameItem.SetRightLabel(PlayerSettings.DisplayName);
            nameItem.Activated += (menu, item) =>
            {
                string _DisplayName = Game.GetUserInput(32);
                if (!string.IsNullOrWhiteSpace(_DisplayName))
                {
                    PlayerSettings.DisplayName = _DisplayName;
                    Util.SaveSettings(null);
                    nameItem.SetRightLabel(PlayerSettings.DisplayName);
                }
            };

            var masterItem = new UIMenuItem("Master Server");
            masterItem.SetRightLabel(PlayerSettings.MasterServerAddress);
            masterItem.Activated += (menu, item) =>
            {
                _masterIP = Game.GetUserInput(255);
                if (!string.IsNullOrWhiteSpace(_masterIP))
                {
                    PlayerSettings.MasterServerAddress = _masterIP;
                    Util.SaveSettings(null);
                    masterItem.SetRightLabel(PlayerSettings.MasterServerAddress);
                }
            };

            var backupMasterItem = new UIMenuItem("Backup Master Server");
            backupMasterItem.SetRightLabel(PlayerSettings.BackupMasterServerAddress);
            backupMasterItem.Activated += (menu, item) =>
            {
                var _input = Game.GetUserInput(255);
                if (!string.IsNullOrWhiteSpace(_input))
                {
                    PlayerSettings.BackupMasterServerAddress = _input;
                    Util.SaveSettings(null);
                    backupMasterItem.SetRightLabel(PlayerSettings.BackupMasterServerAddress);
                }
            };


            var chatItem = new UIMenuCheckboxItem("Use Old Chat Input", PlayerSettings.OldChat);
            chatItem.CheckboxEvent += (item, check) =>
            {
                PlayerSettings.OldChat = check;
                Util.SaveSettings(null);
            };

            var chatLogItem = new UIMenuCheckboxItem("Log Chats", PlayerSettings.ChatLog);
            chatLogItem.CheckboxEvent += (item, check) =>
            {
                PlayerSettings.ChatLog = check;
                Util.SaveSettings(null);
            };

            var hidePasswordsItem = new UIMenuCheckboxItem("Hide Passwords (Restart required)", PlayerSettings.HidePasswords);
            hidePasswordsItem.CheckboxEvent += (item, check) =>
            {
                PlayerSettings.HidePasswords = check;
                //_mainMenu.RefreshIndex();_settingsMenu.RefreshIndex();_serverMenu.RefreshIndex();
                Util.SaveSettings(null);
            };

            var npcItem = new UIMenuCheckboxItem("Share NPC's With Players", PlayerSettings.SyncWorld);
            npcItem.CheckboxEvent += (item, check) =>
            {
                if (!check && _client != null)
                {
                    var msg = _client.CreateMessage();
                    var obj = new PlayerDisconnect();
                    obj.Id = _client.UniqueIdentifier;
                    var bin = SerializeBinary(obj);

                    msg.Write((int)PacketType.WorldSharingStop);
                    msg.Write(bin.Length);
                    msg.Write(bin);

                    _client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, 3);
                }
                PlayerSettings.SyncWorld = check;
                Util.SaveSettings(null);
            };

            /*var trafficItem = new UIMenuCheckboxItem("Enable Traffic When Sharing", PlayerSettings.SyncTraffic, "May affect performance.");
            trafficItem.CheckboxEvent += (item, check) =>
            {
                PlayerSettings.SyncTraffic = check;
                Util.SaveSettings(null);
            };*/

            var trafficItem = new UIMenuListItem("Share Traffic With Players", new List<dynamic>(Enum.GetNames(typeof(TrafficMode))), 0);
            trafficItem.OnListChanged += (item, index) =>
            {
                PlayerSettings.SyncTraffic = Enum.Parse(typeof(TrafficMode), item.IndexToItem(index).ToString());
                Util.SaveSettings(null);
            };

            var autoConnectItem = new UIMenuCheckboxItem("Auto Connect On Startup", PlayerSettings.AutoConnect);
            autoConnectItem.CheckboxEvent += (item, check) =>
            {
                PlayerSettings.AutoConnect = check;
                Util.SaveSettings(null);
            };

            var autoReconnectItem = new UIMenuCheckboxItem("Auto Reconnect", PlayerSettings.AutoReconnect);
            autoReconnectItem.CheckboxEvent += (item, check) =>
            {
                PlayerSettings.AutoReconnect = check;
                Util.SaveSettings(null);
            };
            var autoLoginItem = new UIMenuItem("Auto Login");
            if (PlayerSettings.HidePasswords)
            {
                autoLoginItem.SetRightLabel(new String('*', PlayerSettings.AutoLogin.Length));
            }
            else
            {
                autoLoginItem.SetRightLabel(PlayerSettings.AutoLogin.ToString());
            }
            autoLoginItem.Activated += (menu, item) =>
            {
                string _AutoLogin = Game.GetUserInput(255);
                if (!string.IsNullOrEmpty(_AutoLogin))
                {
                    PlayerSettings.AutoLogin = _AutoLogin;
                    Util.SaveSettings(null);
                    if (PlayerSettings.HidePasswords)
                    {
                        autoLoginItem.SetRightLabel(new String('*', PlayerSettings.AutoLogin.Length));
                    }
                    else
                    {
                        autoLoginItem.SetRightLabel(PlayerSettings.AutoLogin.ToString());
                    }
                }
            };

            var autoRegisterItem = new UIMenuCheckboxItem("Auto Register", PlayerSettings.AutoRegister);
            autoRegisterItem.CheckboxEvent += (item, check) =>
            {
                PlayerSettings.AutoRegister = check;
                Util.SaveSettings(null);
            };


            var modeItem = new UIMenuListItem("Sync Mode", new List<dynamic>(Enum.GetNames(typeof(SynchronizationMode))), 0);
            modeItem.OnListChanged += (item, index) =>
            {
                GlobalSyncMode = Enum.Parse(typeof(SynchronizationMode), item.IndexToItem(index).ToString());
                lock (Opponents) if (Opponents != null) Opponents.ToList().ForEach(p => p.Value.SyncMode = GlobalSyncMode);
            };

            var versionItem = new UIMenuListItem("Version", new List<dynamic>(Enum.GetNames(typeof(ScriptVersion))), 0);
            versionItem.OnListChanged += (item, index) =>
            {
                LocalScriptVersion = Enum.Parse(typeof(ScriptVersion), item.IndexToItem(index).ToString());
                //_mainMenu.Clear();_mainMenu.RefreshIndex();
            };

            var spawnItem = new UIMenuCheckboxItem("Debug", false);
            spawnItem.CheckboxEvent += (item, check) =>
            {
                display = check;
                if (!display)
                {
                    if (mainPed != null) mainPed.Delete();
                    if (mainVehicle != null) mainVehicle.Delete();
                    if (_debugSyncPed != null)
                    {
                        _debugSyncPed.Clear();
                        _debugSyncPed = null;
                    }
                }
            };

            _settingsMenu.AddItem(nameItem);
            _settingsMenu.AddItem(npcItem);
            _settingsMenu.AddItem(trafficItem);
            _settingsMenu.AddItem(chatItem);
            _settingsMenu.AddItem(chatLogItem);
            _settingsMenu.AddItem(hidePasswordsItem);
            _settingsMenu.AddItem(autoConnectItem);
            _settingsMenu.AddItem(autoReconnectItem);
            _settingsMenu.AddItem(autoLoginItem);
            _settingsMenu.AddItem(autoRegisterItem);
            _settingsMenu.AddItem(masterItem);
            _settingsMenu.AddItem(versionItem);
            _settingsMenu.AddItem(modeItem);
            _settingsMenu.AddItem(spawnItem);


            var serverNameItem = new UIMenuItem("Server Name");
            serverNameItem.SetRightLabel(ServerSettings.Name);
            serverNameItem.Activated += (menu, item) =>
            {
                string _Name = Game.GetUserInput(32);
                if (!string.IsNullOrWhiteSpace(_Name)) {
                    ServerSettings.Name = _Name;
                    serverNameItem.SetRightLabel(ServerSettings.Name);
                    Util.SaveServerSettings(Program.Location + "ServerSettings.xml");
                }
            };

            var serverMaxPlayersItem = new UIMenuItem("Server Max Players");
            serverMaxPlayersItem.SetRightLabel(ServerSettings.MaxPlayers.ToString());
            serverMaxPlayersItem.Activated += (menu, item) =>
            {
                string newPlayers = Game.GetUserInput(2);
                int nPlayers;
                bool success = int.TryParse(newPlayers, out nPlayers);
                if (!success)
                {
                    UI.Notify("Wrong player number format");
                    return;
                }
                ServerSettings.MaxPlayers = nPlayers;
                serverMaxPlayersItem.SetRightLabel(ServerSettings.MaxPlayers.ToString());
                Util.SaveServerSettings(Program.Location + "ServerSettings.xml");
            };

            var serverPortItem = new UIMenuItem("Server Port");
            serverPortItem.SetRightLabel(ServerSettings.Port.ToString());
            serverPortItem.Activated += (menu, item) =>
            {
                string newPort = Game.GetUserInput(4);
                int nPort;
                bool success = int.TryParse(newPort, out nPort);
                if (!success)
                {
                    UI.Notify("Wrong port format");
                    return;
                }
                ServerSettings.Port = nPort;
                serverPortItem.SetRightLabel(ServerSettings.Port.ToString());
                Util.SaveServerSettings(Program.Location + "ServerSettings.xml");
            };

            var serverPasswordEnabledItem = new UIMenuCheckboxItem("Password enabled", ServerSettings.PasswordProtected);
            serverPasswordEnabledItem.CheckboxEvent += (item, check) =>
            {
                ServerSettings.PasswordProtected = check;
            };

            var serverPasswordTextItem = new UIMenuItem("Password");
            serverPasswordTextItem.Activated += (menu, item) =>
            {
                string _input = Game.GetUserInput(255);
                if (!string.IsNullOrEmpty(_input))
                {
                    ServerSettings.Name = _input;
                    if (PlayerSettings.HidePasswords)
                    {
                        passItem.SetRightLabel(new String('*', ServerSettings.Password.Length));
                    }
                    else
                    {
                        passItem.SetRightLabel(ServerSettings.Password.ToString());
                    }
                    Util.SaveServerSettings(Program.Location + "ServerSettings.xml");
                }
            };

            var serverAnnounceItem = new UIMenuCheckboxItem("Announce to Master Server", ServerSettings.Announce);
            serverAnnounceItem.CheckboxEvent += (item, check) =>
            {
                ServerSettings.Announce = check;
                Util.SaveServerSettings(Program.Location + "ServerSettings.xml");
            };

            var serverMasterItem = new UIMenuItem("Master Server Adress");
            serverMasterItem.SetRightLabel(ServerSettings.MasterServer);
            serverMasterItem.Activated += (menu, item) =>
            {
                var _input = Game.GetUserInput(255);
                if (!string.IsNullOrWhiteSpace(_masterIP))
                {
                    PlayerSettings.MasterServerAddress = _input;
                    Util.SaveSettings(null);
                    serverMasterItem.SetRightLabel(ServerSettings.MasterServer);
                }
            };

            var serverBackupMasterItem = new UIMenuItem("Backup Master Server");
            serverBackupMasterItem.SetRightLabel(ServerSettings.BackupMasterServer);
            serverBackupMasterItem.Activated += (menu, item) =>
            {
                var _input = Game.GetUserInput(255);
                if (!string.IsNullOrWhiteSpace(_input))
                {
                    PlayerSettings.BackupMasterServerAddress = _input;
                    Util.SaveSettings(null);
                    serverBackupMasterItem.SetRightLabel(ServerSettings.BackupMasterServer);
                }
            };

            var serverAllowDisplayNamesItem = new UIMenuCheckboxItem("Allow Nicknames", ServerSettings.AllowNickNames);
            serverAllowDisplayNamesItem.CheckboxEvent += (item, check) =>
            {
                ServerSettings.AllowNickNames = check;
                Util.SaveServerSettings(Program.Location + "ServerSettings.xml");
            };
            var serverAutoStartItem = new UIMenuCheckboxItem("Auto Start Server", PlayerSettings.AutoStartServer);
            serverAutoStartItem.CheckboxEvent += (item, check) =>
            {
                PlayerSettings.AutoStartServer = check;
                Util.SaveSettings(null);
            };
            var serverStartItem = new UIMenuItem("Start Server");
            serverStartItem.Activated += (menu, item) =>
            {
                if (!_serverRunning)
                {
                    //Program.ServerInstance = new GameServer(ServerSettings.Port, ServerSettings.Name, "freeroam");
                    /*Program.ServerInstance = new GameServer
                    {
                        Port = ServerSettings.Port,
                        Name = ServerSettings.Name,
                        GamemodeName = ServerSettings.Gamemode,
                        PasswordProtected = ServerSettings.PasswordProtected,
                        Password = ServerSettings.Password,
                        AnnounceSelf = ServerSettings.Announce,
                        MasterServer = ServerSettings.MasterServer,
                        BackupMasterServer = ServerSettings.BackupMasterServer,
                        MaxPlayers = ServerSettings.MaxPlayers,
                        AllowNickNames = ServerSettings.AllowNickNames
                    };*/

                    if (IsOnServer())
                    {
                        if (_client != null) _client.Disconnect("Connecting to local server...");
                    }
                    _serverRunning = true;
                    //string[] filterscripts = new string[] { };
                    //try { Program.ServerInstance.Start(ServerSettings.Filterscripts); } catch(Exception ex) { UI.Notify("Can't start server: " +ex.Message); }
                    try { ConnectToServer("localhost", ServerSettings.Port); } catch (Exception ex) { UI.Notify("Can't connect to local server: " + ex.Message); }
                    UI.Notify("For others to access the server, you may have to port forward.");
                }
                else
                {
                    //Program.ServerInstance.Stop();
                    _serverRunning = false;
                }

            };
            /*
            _serverMenu.AddItem(serverNameItem);
            _serverMenu.AddItem(serverMaxPlayersItem);
            _serverMenu.AddItem(serverPortItem);
            _serverMenu.AddItem(serverPasswordEnabledItem);
            _serverMenu.AddItem(serverPasswordTextItem);
            _serverMenu.AddItem(serverAnnounceItem);
            _serverMenu.AddItem(serverMasterItem);
            _serverMenu.AddItem(serverBackupMasterItem);
            _serverMenu.AddItem(serverAllowDisplayNamesItem);
            _serverMenu.AddItem(serverAutoStartItem);
            _serverMenu.AddItem(serverStartItem);
            */


            _playersMenu.OnIndexChange += (sender, index) =>
            {
                KeyValuePair<long, SyncPed> newPlayer = new KeyValuePair<long, SyncPed>();
                lock (Opponents) newPlayer = Opponents.FirstOrDefault(ops => ops.Value.Name == _playersMenu.MenuItems[index].Text);
                if (newPlayer.Equals(new KeyValuePair<long, SyncPed>())) return;
                var pos = newPlayer.Value.IsInVehicle ? newPlayer.Value.VehiclePosition : newPlayer.Value.Position;
                Function.Call(Hash.LOCK_MINIMAP_POSITION, pos.X, pos.Y);
                Function.Call(Hash.SET_RADAR_ZOOM, 0);
            };

            _playersMenu.OnMenuClose += sender =>
            {
                Function.Call(Hash.UNLOCK_MINIMAP_POSITION);
                Function.Call(Hash.SET_RADAR_ZOOM, 200);
            };

            _mainMenu.RefreshIndex();
            _settingsMenu.RefreshIndex();
            //_serverMenu.RefreshIndex();

            _menuPool.Add(_mainMenu);
            _menuPool.Add(_serverBrowserMenu);
            _menuPool.Add(_settingsMenu);
            _menuPool.Add(_playersMenu);
            //_menuPool.Add(_serverMenu);
            #endregion

            _debug = new DebugWindow();
            UI.Notify("~g~GTA V Coop mod v" + ReadableScriptVersion() + " by Guad, Bluscream and wolfmitchell loaded successfully.~w~");
            if (PlayerSettings.AutoConnect && !String.IsNullOrWhiteSpace(PlayerSettings.LastIP) && PlayerSettings.LastPort != -1 && PlayerSettings.LastPort != 0) { 
                ConnectToServer(PlayerSettings.LastIP.ToString(), PlayerSettings.LastPort);
            }else if(PlayerSettings.AutoStartServer){
                /*Program.ServerInstance = new GameServer
                {
                    Port = ServerSettings.Port,
                    Name = ServerSettings.Name,
                    GamemodeName = ServerSettings.Gamemode,
                    PasswordProtected = ServerSettings.PasswordProtected,
                    Password = ServerSettings.Password,
                    AnnounceSelf = ServerSettings.Announce,
                    MasterServer = ServerSettings.MasterServer,
                    MaxPlayers = ServerSettings.MaxPlayers,
                    AllowNickNames = ServerSettings.AllowNickNames
                };*/

                if (IsOnServer())
                {
                    if (_client != null) _client.Disconnect("Connecting to local server...");
                }
                _serverRunning = true;
                //string[] filterscripts = new string[] { };
                /*Program.ServerInstance.Start(ServerSettings.Filterscripts);
                ConnectToServer("localhost", ServerSettings.Port);
                UI.Notify("For others to access the server, you may have to port forward.");*/
            }
        }

        private void RebuildServerBrowser()
        {
            _serverBrowserMenu.Clear();
            _serverBrowserMenu.RefreshIndex();
            if (string.IsNullOrEmpty(PlayerSettings.MasterServerAddress))
                return;
            string response = String.Empty;
            try
            {
                using (var wc = new WebClient())
                {
                    response = wc.DownloadString(PlayerSettings.MasterServerAddress);
                }
            }
            catch (Exception e)
            {
                UI.Notify("~r~~h~ERROR~h~~w~~n~Could not contact master server. Trying Fallback Server.");
                if (Main.PlayerSettings.Logging)
                {
                    var logOutput = "===== EXCEPTION CONTACTING MASTER SERVER @ " + DateTime.UtcNow + " ======\n";
                    logOutput += "Message: " + e.Message;
                    logOutput += "\nData: " + e.Data;
                    logOutput += "\nStack: " + e.StackTrace;
                    logOutput += "\nSource: " + e.Source;
                    logOutput += "\nTarget: " + e.TargetSite;
                    if (e.InnerException != null)
                        logOutput += "\nInnerException: " + e.InnerException.Message;
                    logOutput += "\n";
                    File.AppendAllText("scripts\\GTACOOP.log", logOutput);
                }
                try
                {
                    using (var wc = new WebClient())
                    {
                        response = wc.DownloadString(PlayerSettings.BackupMasterServerAddress);
                    }
                }
                catch (Exception ex)
                {
                    UI.Notify("~r~~h~ERROR~h~~w~~n~Could not contact backup master server. Please retry later...");
                    if (Main.PlayerSettings.Logging)
                    {
                        var logOutput = "===== EXCEPTION CONTACTING BACKUP MASTER SERVER @ " + DateTime.UtcNow + " ======\n";
                        logOutput += "Message: " + ex.Message;
                        logOutput += "\nData: " + ex.Data;
                        logOutput += "\nStack: " + ex.StackTrace;
                        logOutput += "\nSource: " + ex.Source;
                        logOutput += "\nTarget: " + ex.TargetSite;
                        if (e.InnerException != null)
                            logOutput += "\nInnerException: " + e.InnerException.Message;
                        logOutput += "\n";
                        File.AppendAllText("scripts\\GTACOOP.log", logOutput);
                    }
                }
            }
            if (string.IsNullOrWhiteSpace(response))
                return;

            var dejson = JsonConvert.DeserializeObject<MasterServerList>(response) as MasterServerList;

            if (dejson == null) return;

            Console.WriteLine("Servers returned by master server: " + dejson.list.Count().ToString());
            _serverBrowserMenu.Subtitle.Caption = "Servers listed: ~g~~h~" + dejson.list.Count().ToString();
            var item = new UIMenuItem(dejson.list.Count().ToString() + " Servers listed.");
            item.SetLeftBadge(UIMenuItem.BadgeStyle.Star);
            if (_client == null)
            {
                var port = GetOpenUdpPort();
                if (port == 0)
                {
                    UI.Notify("No available UDP port was found.");
                    return;
                }
                _config.Port = port;
                _client = new NetClient(_config);
                _client.Start();
            }

            foreach (var server in dejson.list)
            {
                var split = server.Split(':');
                if (split.Length != 2) continue;
                int port;
                if (!int.TryParse(split[1], out port))
                    continue;
                _client.DiscoverKnownPeer(split[0], port);
            }
        }

        private void RebuildPlayersList()
        {
            _playersMenu.Clear();
            List<SyncPed> list = null;
            lock (Opponents)
            {
                if (Opponents == null) return;

                list = new List<SyncPed>(Opponents.Select(pair => pair.Value));
            }

            /*var setWaypointItem = new UIMenuItem("Set Waypoint");
            setWaypointItem.Activated += (sender, item) =>
            {
                //var pos = ped.IsInVehicle ? ped.VehiclePosition : ped.Position;
                //Function.Call(Hash.SET_NEW_WAYPOINT, pos.X, pos.Y);
                UI.Notify("Clicked");
            };*/

            var meItem = new UIMenuItem(PlayerSettings.DisplayName);
            meItem.SetRightLabel(((int)(Latency * 1000)) + "ms");
            _playersMenu.AddItem(meItem);
            /*meItem.Activated += (sender, item) =>
            {

            };*/
            //_playerMenu.AddItem(setWaypointItem);

            foreach (var ped in list)
            {
                var newItem = new UIMenuItem(ped.Name == null ? "" : ped.Name);
                newItem.SetRightLabel(((int)(ped.Latency * 1000)) + "ms");

                newItem.Description = "Real latency: " + ped.AverageLatency;

                newItem.Activated += (sender, item) =>
                {
                    var pos = ped.IsInVehicle ? ped.VehiclePosition : ped.Position;
                    Function.Call(Hash.SET_NEW_WAYPOINT, pos.X, pos.Y);
                };
                _playersMenu.AddItem(newItem);
            }

            _playersMenu.RefreshIndex();
        }

        public static Dictionary<int, int> CheckPlayerVehicleMods()
        {
            if (!Game.Player.Character.IsInVehicle()) return null;

            if (_modSwitch % 30 == 0)
            {
                var id = _modSwitch/30;
                var mod = Game.Player.Character.CurrentVehicle.GetMod((VehicleMod) id);
                if (mod != -1)
                {
                    lock (_vehMods)
                    {
                        if (!_vehMods.ContainsKey(id)) _vehMods.Add(id, mod);

                        _vehMods[id] = mod;
                    }
                }
            }

            _modSwitch++;

            if (_modSwitch >= 1500) _modSwitch = 0;

            return _vehMods;
        }

        public static Dictionary<int, int> CheckPlayerProps()
        {
            if (_pedSwitch % 30 == 0)
            {
                var id = _pedSwitch / 30;
                var mod = Function.Call<int>(Hash.GET_PED_DRAWABLE_VARIATION, Game.Player.Character.Handle, id);
                if (mod != -1)
                {
                    lock (_pedClothes)
                    {
                        if (!_pedClothes.ContainsKey(id)) _pedClothes.Add(id, mod);

                        _pedClothes[id] = mod;
                    }
                }
            }

            _pedSwitch++;

            if (_pedSwitch >= 450) _pedSwitch = 0;

            return _pedClothes;
        }

        public static void SendPlayerData()
        {
            var player = Game.Player.Character;
            if (player.IsInVehicle())
            {
                var veh = player.CurrentVehicle;

                var obj = new VehicleData();
                obj.Position = veh.Position.ToLVector();
                obj.Quaternion = veh.Quaternion.ToLQuaternion();
                obj.PedModelHash = player.Model.Hash;
                obj.VehicleModelHash = veh.Model.Hash;
                obj.PrimaryColor = (int)veh.PrimaryColor;
                obj.SecondaryColor = (int)veh.SecondaryColor;
                obj.PlayerHealth = player.Health;
                obj.VehicleHealth = veh.Health;
                obj.VehicleSeat = Util.GetPedSeat(player);
                obj.IsPressingHorn = Game.Player.IsPressingHorn;
                obj.IsSirenActive = veh.SirenActive;
                obj.VehicleMods = CheckPlayerVehicleMods();
                obj.Speed = veh.Speed;

                var bin = SerializeBinary(obj);

                var msg = _client.CreateMessage();
                msg.Write((int)PacketType.VehiclePositionData);
                msg.Write(bin.Length);
                msg.Write(bin);

                _client.SendMessage(msg, NetDeliveryMethod.UnreliableSequenced, _channel);

                _bytesSent += bin.Length;
                _messagesSent++;
            }
            else
            {
                bool aiming = Game.IsControlPressed(0, GTA.Control.Aim);
                bool shooting = Function.Call<bool>(Hash.IS_PED_SHOOTING, player.Handle);

                GTA.Math.Vector3 aimCoord = new GTA.Math.Vector3();
                if (aiming || shooting)
                    aimCoord = ScreenRelToWorld(GameplayCamera.Position, GameplayCamera.Rotation,
                        new Vector2(0, 0));

                var obj = new PedData();
                obj.AimCoords = aimCoord.ToLVector();
                obj.Position = player.Position.ToLVector();
                obj.Quaternion = player.Quaternion.ToLQuaternion();
                obj.PedModelHash = player.Model.Hash;
                obj.WeaponHash = (int)player.Weapons.Current.Hash;
                obj.PlayerHealth = player.Health;
                obj.IsAiming = aiming;
                obj.IsShooting = shooting;
                obj.IsJumping = Function.Call<bool>(Hash.IS_PED_JUMPING, player.Handle);
                obj.IsParachuteOpen = Function.Call<int>(Hash.GET_PED_PARACHUTE_STATE, Game.Player.Character.Handle) == 2;

                obj.PedProps = CheckPlayerProps();

                var bin = SerializeBinary(obj);

                var msg = _client.CreateMessage();

                msg.Write((int)PacketType.PedPositionData);
                msg.Write(bin.Length);
                msg.Write(bin);

                _client.SendMessage(msg, NetDeliveryMethod.UnreliableSequenced, _channel);

                _bytesSent += bin.Length;
                _messagesSent++;
            }
        }

        public static void SendPedData(Ped ped)
        {
            if (ped.IsInVehicle())
            {
                var veh = ped.CurrentVehicle;

                var obj = new VehicleData();
                obj.Position = veh.Position.ToLVector();
                obj.Quaternion = veh.Quaternion.ToLQuaternion();
                obj.PedModelHash = ped.Model.Hash;
                obj.VehicleModelHash = veh.Model.Hash;
                obj.PrimaryColor = (int)veh.PrimaryColor;
                obj.SecondaryColor = (int)veh.SecondaryColor;
                obj.PlayerHealth = ped.Health;
                obj.VehicleHealth = veh.Health;
                obj.VehicleSeat = Util.GetPedSeat(ped);
                obj.Name = ped.Handle.ToString();
                obj.Speed = veh.Speed;
                obj.IsSirenActive = veh.SirenActive;

                var bin = SerializeBinary(obj);

                var msg = _client.CreateMessage();
                msg.Write((int)PacketType.NpcVehPositionData);
                msg.Write(bin.Length);
                msg.Write(bin);

                _client.SendMessage(msg, NetDeliveryMethod.Unreliable, _channel);

                _bytesSent += bin.Length;
                _messagesSent++;
            }
            else
            {
                bool shooting = Function.Call<bool>(Hash.IS_PED_SHOOTING, ped.Handle);

                GTA.Math.Vector3 aimCoord = new GTA.Math.Vector3();
                if (shooting)
                    aimCoord = Util.GetLastWeaponImpact(ped);

                var obj = new PedData();
                obj.AimCoords = aimCoord.ToLVector();
                obj.Position = ped.Position.ToLVector();
                obj.Quaternion = ped.Quaternion.ToLQuaternion();
                obj.PedModelHash = ped.Model.Hash;
                obj.WeaponHash = (int)ped.Weapons.Current.Hash;
                obj.PlayerHealth = ped.Health;
                obj.Name = ped.Handle.ToString();
                obj.IsAiming = false;
                obj.IsShooting = shooting;
                obj.IsJumping = Function.Call<bool>(Hash.IS_PED_JUMPING, ped.Handle);
                obj.IsParachuteOpen = Function.Call<int>(Hash.GET_PED_PARACHUTE_STATE, ped.Handle) == 2;

                var bin = SerializeBinary(obj);

                var msg = _client.CreateMessage();

                msg.Write((int)PacketType.NpcPedPositionData);
                msg.Write(bin.Length);
                msg.Write(bin);

                _client.SendMessage(msg, NetDeliveryMethod.Unreliable, _channel);

                _bytesSent += bin.Length;
                _messagesSent++;
            }
        }

        public void OnTick(object sender, EventArgs e)
        {
            try
            {
                Ped player = Game.Player.Character;
                _menuPool.ProcessMenus();
                _chat.Tick();

                if (_serverRunning) 

                if (_isGoingToCar && Game.IsControlJustPressed(0, Control.PhoneCancel))
                {
                    Game.Player.Character.Task.ClearAll();
                    _isGoingToCar = false;
                }

                if (IsOnServer())
                {
                    if (!_mainMenu.MenuItems[1].Text.Equals("Disconnect"))
                    {
                        _mainMenu.MenuItems[1].Text = "Disconnect";
                    }
                    if (!_mainMenu.MenuItems[7].Enabled)
                    {
                        _mainMenu.MenuItems[7].Enabled = true;
                    }
                    if (_settingsMenu.MenuItems[0].Enabled)
                    {
                        _settingsMenu.MenuItems[0].Enabled = false;
                    }
                }
                else
                {
                    if (_mainMenu.MenuItems[1].Text.Equals("Disconnect"))
                    {
                        _mainMenu.MenuItems[1].Text = "Connect";
                    }
                    if (_mainMenu.MenuItems[7].Enabled)
                    {
                        _mainMenu.MenuItems[7].Enabled = false;
                    }
                    if (!_settingsMenu.MenuItems[0].Enabled)
                    {
                        _settingsMenu.MenuItems[0].Enabled = true;
                    }
                }

                if (_serverRunning)
                {
                    //Program.ServerInstance.Tick();
                    //if(!_serverMenu.MenuItems[8].Text.Equals("Stop Server"))
                        //_serverMenu.MenuItems[8].Text = "Stop Server";
                }
                else
                {
                    //if (!_serverMenu.MenuItems[8].Text.Equals("Start Server"))
                        //_serverMenu.MenuItems[8].Text = "Start Server";
                }
                if (display)
                {
                    Debug();
                    _debug.Visible = true;
                    _debug.Draw();
                }
                ProcessMessages();

                if (_client == null || _client.ConnectionStatus == NetConnectionStatus.Disconnected ||
                    _client.ConnectionStatus == NetConnectionStatus.None) return;

                if (_wasTyping)
                    Game.DisableControlThisFrame(0, Control.FrontendPauseAlternate);

                int time = 1000;
                if ((time = Function.Call<int>(Hash.GET_TIME_SINCE_LAST_DEATH)) < 50 && !_lastDead)
                {
                    _lastDead = true;
                    var msg = _client.CreateMessage();
                    msg.Write((int)PacketType.PlayerKilled);
                    _client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, 0);
                }

                if (time > 50 && _lastDead)
                    _lastDead = false;

                if (!PlayerSettings.SyncWorld)
                {
                    Function.Call(Hash.SET_PED_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
                    Function.Call(Hash.SET_SCENARIO_PED_DENSITY_MULTIPLIER_THIS_FRAME, 0f, 0f);
                }
                switch (PlayerSettings.SyncTraffic)
                {
                    case TrafficMode.Parked:
                        Function.Call(Hash.SET_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
                        Function.Call(Hash.SET_RANDOM_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
                        break;
                    case TrafficMode.None:
                        Function.Call(Hash.SET_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
                        Function.Call(Hash.SET_RANDOM_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
                        Function.Call(Hash.SET_PARKED_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
                        break;
                    case TrafficMode.All:
                        break;
                    default:
                        LogToConsole(3, false, "SYNC", "Unknown Traffic Sync mode.");
                        break;
                }
                if(PlayerSettings.SyncWorld || PlayerSettings.SyncTraffic != TrafficMode.All)
                {
                    Function.Call((Hash)0x2F9A292AD0A3BD89);
                    Function.Call((Hash)0x5F3B7749C112D552);
                }
                Function.Call(Hash.SET_TIME_SCALE, 1f);

                /*string stats = string.Format("{0}Kb (D)/{1}Kb (U), {2}Msg (D)/{3}Msg (U)", _bytesReceived / 1000,
                    _bytesSent / 1000, _messagesReceived, _messagesSent);
                    */
                //UI.ShowSubtitle(stats);

                lock (_threadJumping)
                {
                    if (_threadJumping.Any())
                    {
                        Action action = _threadJumping.Dequeue();
                        if (action != null) action.Invoke();
                    }
                }

                Dictionary<string, NativeData> tickNatives = null;
                lock (_tickNatives) tickNatives = new Dictionary<string, NativeData>(_tickNatives);

                for (int i = 0; i < tickNatives.Count; i++) DecodeNativeCall(tickNatives.ElementAt(i).Value);
            }catch(Exception ex) {
                UI.Notify("<ERROR> Could not handle this tick:"); UI.Notify(ex.Message);
                if(Main.PlayerSettings.Logging)
                    File.AppendAllText("scripts\\GTACOOP.log", "[" + DateTime.UtcNow + "] <ERROR> Could not handle this tick: " + ex.Message+"\n");
            }
        }

        public static bool IsOnServer()
        {
            return _client != null && _client.ConnectionStatus == NetConnectionStatus.Connected;
        }

        public void OnKeyDown(object sender, KeyEventArgs e)
        {
            _chat.OnKeyDown(e.KeyCode);
            if (e.KeyCode == PlayerSettings.ActivationKey && !_chat.IsFocused)
            {
                if (_menuPool.IsAnyMenuOpen())
                {
                    _menuPool.CloseAllMenus();
                }
                else
                {
                    _mainMenu.Visible = true;
                }
            }

            if (e.KeyCode == Keys.G && !Game.Player.Character.IsInVehicle() && IsOnServer())
            {
                var vehs = World.GetAllVehicles().OrderBy(v => (v.Position - Game.Player.Character.Position).Length()).Take(1).ToList();
                if (vehs.Any() && Game.Player.Character.IsInRangeOf(vehs[0].Position, 5f))
                {
                    Game.Player.Character.Task.EnterVehicle(vehs[0], (VehicleSeat)Util.GetFreePassengerSeat(vehs[0]));
                    _isGoingToCar = true;
                }
            }

            if (e.KeyCode == Keys.T && IsOnServer())
            {
                if (!PlayerSettings.OldChat)
                {
                    _chat.IsFocused = true;
                    _wasTyping = true;
                }
                else
                {
                    //Game.DisableAllControlsThisFrame(2);
                    foreach (GTA.Control control in Enum.GetValues(typeof(GTA.Control)))
                    {
                        Game.DisableControlThisFrame(2, control);
                    }
                    var message = Game.GetUserInput(255);
                    foreach (GTA.Control control in Enum.GetValues(typeof(GTA.Control)))
                    {
                        Game.EnableControl(2, control);
                    }
                    //Game.EnableAllControlsThisFrame(2);
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        var obj = new ChatData()
                        {
                            Message = message,
                        };
                        var data = SerializeBinary(obj);

                        var msg = _client.CreateMessage();
                        msg.Write((int)PacketType.ChatData);
                        msg.Write(data.Length);
                        msg.Write(data);
                        _client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, 0);
                    }
                }
            }
        }

        public void ConnectToServer(string ip, int port = 4499)
        {
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

            _chat.Init();

            if (_client == null)
            {
                var cport = GetOpenUdpPort();
                if (cport == 0)
                {
                    UI.Notify("No available UDP port was found.");
                    return;
                }
                _config.Port = cport;
                _client = new NetClient(_config);
                _client.Start();
            }

            lock (Opponents) Opponents = new Dictionary<long, SyncPed>();
            lock (Npcs) Npcs = new Dictionary<string, SyncPed>();
            lock (_tickNatives) _tickNatives = new Dictionary<string, NativeData>();

            var msg = _client.CreateMessage();

            var obj = new ConnectionRequest();
            obj.Name = string.IsNullOrWhiteSpace(Game.Player.Name) ? "Player" : Game.Player.Name; // To be used as identifiers in server files
            obj.DisplayName = string.IsNullOrWhiteSpace(PlayerSettings.DisplayName) ? obj.Name : PlayerSettings.DisplayName.Trim();
            if (!string.IsNullOrEmpty(_password)) obj.Password = _password;
            obj.ScriptVersion = (byte)LocalScriptVersion;
            obj.GameVersion = (int)Game.Version;

            var bin = SerializeBinary(obj);

            msg.Write((int)PacketType.ConnectionRequest);
            msg.Write(bin.Length);
            msg.Write(bin);

            _client.Connect(ip, port == 0 ? Port : port, msg);

            var pos = Game.Player.Character.Position;
            Function.Call(Hash.CLEAR_AREA_OF_PEDS, pos.X, pos.Y, pos.Z, 100f, 0);
            Function.Call(Hash.CLEAR_AREA_OF_VEHICLES, pos.X, pos.Y, pos.Z, 100f, 0);

            Function.Call(Hash.SET_GARBAGE_TRUCKS, 0);
            Function.Call(Hash.SET_RANDOM_BOATS, 0);
            Function.Call(Hash.SET_RANDOM_TRAINS, 0);
            _lastIP = ip; _lastPort = port;
        }

        public void ProcessMessages()
        {
            NetIncomingMessage msg;
            while (_client != null && (msg = _client.ReadMessage()) != null)
            {
                _messagesReceived++;
                _bytesReceived += msg.LengthBytes;

                if (msg.MessageType == NetIncomingMessageType.Data)
                {
                    var type = (PacketType)msg.ReadInt32();
                    switch (type)
                    {
                        case PacketType.VehiclePositionData:
                            {
                                var len = msg.ReadInt32();
                                var data = DeserializeBinary<VehicleData>(msg.ReadBytes(len)) as VehicleData;
                                if (data == null) return;

                                lock (Opponents)
                                {
                                    if (!Opponents.ContainsKey(data.Id))
                                    {
                                        var repr = new SyncPed(data.PedModelHash, data.Position.ToVector(),
                                            data.Quaternion.ToQuaternion());
                                        Opponents.Add(data.Id, repr);
                                    }

                                    Opponents[data.Id].Name = data.Name;
                                    Opponents[data.Id].LastUpdateReceived = DateTime.Now;
                                    Opponents[data.Id].VehiclePosition =
                                        data.Position.ToVector();
                                    Opponents[data.Id].ModelHash = data.PedModelHash;
                                    Opponents[data.Id].VehicleHash =
                                        data.VehicleModelHash;
                                    Opponents[data.Id].VehicleRotation =
                                        data.Quaternion.ToQuaternion();
                                    Opponents[data.Id].PedHealth = data.PlayerHealth;
                                    Opponents[data.Id].VehicleHealth = data.VehicleHealth;
                                    Opponents[data.Id].VehiclePrimaryColor = data.PrimaryColor;
                                    Opponents[data.Id].VehicleSecondaryColor = data.SecondaryColor;
                                    Opponents[data.Id].VehicleSeat = data.VehicleSeat;
                                    Opponents[data.Id].IsInVehicle = true;
                                    Opponents[data.Id].Latency = data.Latency;

                                    Opponents[data.Id].VehicleMods = data.VehicleMods;
                                    Opponents[data.Id].IsHornPressed = data.IsPressingHorn;
                                    Opponents[data.Id].Speed = data.Speed;
                                    Opponents[data.Id].Siren = data.IsSirenActive;
                                }
                            }
                            break;
                        case PacketType.PedPositionData:
                            {
                                var len = msg.ReadInt32();
                                var data = DeserializeBinary<PedData>(msg.ReadBytes(len)) as PedData;
                                if (data == null) return;

                                lock (Opponents)
                                {
                                    if (!Opponents.ContainsKey(data.Id))
                                    {
                                        var repr = new SyncPed(data.PedModelHash, data.Position.ToVector(),
                                            data.Quaternion.ToQuaternion());
                                        Opponents.Add(data.Id, repr);
                                    }

                                    Opponents[data.Id].Name = data.Name;
                                    Opponents[data.Id].LastUpdateReceived = DateTime.Now;
                                    Opponents[data.Id].Position = data.Position.ToVector();
                                    Opponents[data.Id].ModelHash = data.PedModelHash;
                                    Opponents[data.Id].Rotation = data.Quaternion.ToQuaternion();
                                    Opponents[data.Id].PedHealth = data.PlayerHealth;
                                    Opponents[data.Id].IsInVehicle = false;
                                    Opponents[data.Id].AimCoords = data.AimCoords.ToVector();
                                    Opponents[data.Id].CurrentWeapon = data.WeaponHash;
                                    Opponents[data.Id].IsAiming = data.IsAiming;
                                    Opponents[data.Id].IsJumping = data.IsJumping;
                                    Opponents[data.Id].IsShooting = data.IsShooting;
                                    Opponents[data.Id].Latency = data.Latency;
                                    Opponents[data.Id].IsParachuteOpen = data.IsParachuteOpen;
                                    Opponents[data.Id].PedProps = data.PedProps;
                                }
                            }
                            break;
                        case PacketType.NpcVehPositionData:
                            {
                                var len = msg.ReadInt32();
                                var data = DeserializeBinary<VehicleData>(msg.ReadBytes(len)) as VehicleData;
                                if (data == null) return;

                                lock (Npcs)
                                {
                                    if (!Npcs.ContainsKey(data.Name))
                                    {
                                        var repr = new SyncPed(data.PedModelHash, data.Position.ToVector(),
                                            data.Quaternion.ToQuaternion(), false);
                                        Npcs.Add(data.Name, repr);
                                        Npcs[data.Name].Name = "";
                                        Npcs[data.Name].Host = data.Id;
                                    }

                                    Npcs[data.Name].LastUpdateReceived = DateTime.Now;
                                    Npcs[data.Name].VehiclePosition =
                                        data.Position.ToVector();
                                    Npcs[data.Name].ModelHash = data.PedModelHash;
                                    Npcs[data.Name].VehicleHash =
                                        data.VehicleModelHash;
                                    Npcs[data.Name].VehicleRotation =
                                        data.Quaternion.ToQuaternion();
                                    Npcs[data.Name].PedHealth = data.PlayerHealth;
                                    Npcs[data.Name].VehicleHealth = data.VehicleHealth;
                                    Npcs[data.Name].VehiclePrimaryColor = data.PrimaryColor;
                                    Npcs[data.Name].VehicleSecondaryColor = data.SecondaryColor;
                                    Npcs[data.Name].VehicleSeat = data.VehicleSeat;
                                    Npcs[data.Name].IsInVehicle = true;

                                    Npcs[data.Name].IsHornPressed = data.IsPressingHorn;
                                    Npcs[data.Name].Speed = data.Speed;
                                    Npcs[data.Name].Siren = data.IsSirenActive;
                                }
                            }
                            break;
                        case PacketType.NpcPedPositionData:
                            {
                                var len = msg.ReadInt32();
                                var data = DeserializeBinary<PedData>(msg.ReadBytes(len)) as PedData;
                                if (data == null) return;

                                lock (Npcs)
                                {
                                    if (!Npcs.ContainsKey(data.Name))
                                    {
                                        var repr = new SyncPed(data.PedModelHash, data.Position.ToVector(),
                                            data.Quaternion.ToQuaternion(), false);
                                        Npcs.Add(data.Name, repr);
                                        Npcs[data.Name].Name = "";
                                        Npcs[data.Name].Host = data.Id;
                                    }

                                    Npcs[data.Name].LastUpdateReceived = DateTime.Now;
                                    Npcs[data.Name].Position = data.Position.ToVector();
                                    Npcs[data.Name].ModelHash = data.PedModelHash;
                                    Npcs[data.Name].Rotation = data.Quaternion.ToQuaternion();
                                    Npcs[data.Name].PedHealth = data.PlayerHealth;
                                    Npcs[data.Name].IsInVehicle = false;
                                    Npcs[data.Name].AimCoords = data.AimCoords.ToVector();
                                    Npcs[data.Name].CurrentWeapon = data.WeaponHash;
                                    Npcs[data.Name].IsAiming = data.IsAiming;
                                    Npcs[data.Name].IsJumping = data.IsJumping;
                                    Npcs[data.Name].IsShooting = data.IsShooting;
                                    Npcs[data.Name].IsParachuteOpen = data.IsParachuteOpen;
                                }
                            }
                            break;
                        case PacketType.ChatData:
                            {
                                var len = msg.ReadInt32();
                                var data = DeserializeBinary<ChatData>(msg.ReadBytes(len)) as ChatData;
                                if (data != null && !string.IsNullOrEmpty(data.Message))
                                {
                                    var sender = string.IsNullOrEmpty(data.Sender) ? "SERVER" : data.Sender;
                                    if (data.Message.ToString().Equals("Please authenticate to your account using /login [password]"))
                                    {
                                        if (!String.IsNullOrWhiteSpace(PlayerSettings.AutoLogin))
                                        {
                                            var obj = new ChatData()
                                            {
                                                Message = "/login " + PlayerSettings.AutoLogin,
                                            };
                                            var Data = SerializeBinary(obj);

                                            var Msg = _client.CreateMessage();
                                            Msg.Write((int)PacketType.ChatData);
                                            Msg.Write(Data.Length);
                                            Msg.Write(Data);
                                            _client.SendMessage(Msg, NetDeliveryMethod.ReliableOrdered, 0);
                                            return;
                                        }
                                    }
                                    if (data.Message.ToString().Equals("You can register an account using /register [password]"))
                                    {
                                        if (!String.IsNullOrWhiteSpace(PlayerSettings.AutoLogin) && PlayerSettings.AutoRegister)
                                        {
                                            var obj = new ChatData()
                                            {
                                                Message = "/register " + PlayerSettings.AutoLogin,
                                            };
                                            var Data = SerializeBinary(obj);

                                            var Msg = _client.CreateMessage();
                                            Msg.Write((int)PacketType.ChatData);
                                            Msg.Write(Data.Length);
                                            Msg.Write(Data);
                                            _client.SendMessage(Msg, NetDeliveryMethod.ReliableOrdered, 0);
                                            return;
                                        }
                                    }
                                    _chat.AddMessage(sender, data.Message);
                                    /*lock (_threadJumping)
                                    {
                                        _threadJumping.Enqueue(() =>
                                        {
                                            if (!string.IsNullOrEmpty(data.Sender))
                                                for (int i = 0; i < data.Message.Length; i += 97 - data.Sender.Length)
                                                {
                                                    UI.Notify(data.Sender + ": " +
                                                              data.Message.Substring(i,
                                                                  Math.Min(97 - data.Sender.Length,
                                                                      data.Message.Length - i)));
                                                }
                                            else
                                                for (int i = 0; i < data.Message.Length; i += 99)
                                                    UI.Notify(data.Message.Substring(i,
                                                        Math.Min(99, data.Message.Length - i)));
                                        });
                                    }*/
                                }
                            }
                            break;
                        case PacketType.PlayerDisconnect:
                            {
                                var len = msg.ReadInt32();
                                var data = DeserializeBinary<PlayerDisconnect>(msg.ReadBytes(len)) as PlayerDisconnect;
                                lock (Opponents)
                                {
                                    if (data != null && Opponents.ContainsKey(data.Id))
                                    {
                                        Opponents[data.Id].Clear();
                                        Opponents.Remove(data.Id);

                                        lock (Npcs)
                                        {
                                            foreach (var pair in new Dictionary<string, SyncPed>(Npcs).Where(p => p.Value.Host == data.Id))
                                            {
                                                pair.Value.Clear();
                                                Npcs.Remove(pair.Key);
                                            }
                                        }
                                    }
                                }
                            }
                            break;
                        case PacketType.WorldSharingStop:
                            {
                                var len = msg.ReadInt32();
                                var data = DeserializeBinary<PlayerDisconnect>(msg.ReadBytes(len)) as PlayerDisconnect;
                                if (data == null) return;
                                lock (Npcs)
                                {
                                    foreach (var pair in new Dictionary<string, SyncPed>(Npcs).Where(p => p.Value.Host == data.Id).ToList())
                                    {
                                        pair.Value.Clear();
                                        Npcs.Remove(pair.Key);
                                    }
                                }
                            }
                            break;
                        case PacketType.NativeCall:
                            {
                                var len = msg.ReadInt32();
                                var data = (NativeData)DeserializeBinary<NativeData>(msg.ReadBytes(len));
                                if (data == null) return;
                                DecodeNativeCall(data);
                            }
                            break;
                        case PacketType.NativeTick:
                            {
                                var len = msg.ReadInt32();
                                var data = (NativeTickCall)DeserializeBinary<NativeTickCall>(msg.ReadBytes(len));
                                if (data == null) return;
                                lock (_tickNatives)
                                {
                                    if (!_tickNatives.ContainsKey(data.Identifier)) _tickNatives.Add(data.Identifier, data.Native);

                                    _tickNatives[data.Identifier] = data.Native;
                                }
                            }
                            break;
                        case PacketType.NativeTickRecall:
                            {
                                var len = msg.ReadInt32();
                                var data = (NativeTickCall)DeserializeBinary<NativeTickCall>(msg.ReadBytes(len));
                                if (data == null) return;
                                lock (_tickNatives) if (_tickNatives.ContainsKey(data.Identifier)) _tickNatives.Remove(data.Identifier);
                            }
                            break;
                        case PacketType.NativeOnDisconnect:
                            {
                                var len = msg.ReadInt32();
                                var data = (NativeData)DeserializeBinary<NativeData>(msg.ReadBytes(len));
                                if (data == null) return;
                                lock (_dcNatives)
                                {
                                    if (!_dcNatives.ContainsKey(data.Id)) _dcNatives.Add(data.Id, data);
                                    _dcNatives[data.Id] = data;
                                }
                            }
                            break;
                        case PacketType.NativeOnDisconnectRecall:
                            {
                                var len = msg.ReadInt32();
                                var data = (NativeData)DeserializeBinary<NativeData>(msg.ReadBytes(len));
                                if (data == null) return;
                                lock (_dcNatives) if (_dcNatives.ContainsKey(data.Id)) _dcNatives.Remove(data.Id);
                            }
                            break;
                    }
                }
                else if (msg.MessageType == NetIncomingMessageType.ConnectionLatencyUpdated)
                {
                    Latency = msg.ReadFloat();
                }
                else if (msg.MessageType == NetIncomingMessageType.StatusChanged)
                {
                    var newStatus = (NetConnectionStatus)msg.ReadByte();
                    switch (newStatus)
                    {
                        case NetConnectionStatus.InitiatedConnect:
                            UI.Notify("Connecting...");
                            break;
                        case NetConnectionStatus.Connected:
                            UI.Notify("Connection successful!");
                            _channel = msg.SenderConnection.RemoteHailMessage.ReadInt32();
                            //File.AppendAllText("scripts\\GTACOOP_chat.log", "[" + DateTime.UtcNow + "] New server: " + msg + "\n");
                            break;
                        case NetConnectionStatus.Disconnected:
                            var reason = msg.ReadString();
                            UI.Notify("You have been disconnected" + (string.IsNullOrEmpty(reason) ? " from the server." : ": " + reason));

                            lock (Opponents)
                            {
                                if (Opponents != null)
                                {
                                    Opponents.ToList().ForEach(pair => pair.Value.Clear());
                                    Opponents.Clear();
                                }
                            }

                            lock (Npcs)
                            {
                                if (Npcs != null)
                                {
                                    Npcs.ToList().ForEach(pair => pair.Value.Clear());
                                    Npcs.Clear();
                                }
                            }
                            
                            lock (_dcNatives)
                                if (_dcNatives != null && _dcNatives.Any())
                                {
                                    _dcNatives.ToList().ForEach(pair => DecodeNativeCall(pair.Value));
                                    _dcNatives.Clear();
                                }

                            lock (_tickNatives) if (_tickNatives != null) _tickNatives.Clear();

                            lock (_entityCleanup)
                            {
                                _entityCleanup.ForEach(ent => new Prop(ent).Delete());
                                _entityCleanup.Clear();
                            }
                            lock (_blipCleanup)
                            {
                                _blipCleanup.ForEach(blip => new Blip(blip).Remove());
                                _blipCleanup.Clear();
                            }
                            //if (PlayerSettings.AutoReconnect && !reason.StartsWith("KICKED: ") && !reason.Equals("Connection closed by peer.") && !reason.Equals("Stopping Server") && !reason.Equals("Switching servers."))
                            if (PlayerSettings.AutoConnect && (reason.Equals("Connection timed out") || reason.StartsWith("Could not connect to remote host:")))
                            {
                                ConnectToServer(_lastIP, _lastPort);
                            }
                            break;
                    }
                }
                else if (msg.MessageType == NetIncomingMessageType.DiscoveryResponse)
                {
                    var type = msg.ReadInt32();
                    var len = msg.ReadInt32();
                    var bin = msg.ReadBytes(len);
                    var data = DeserializeBinary<DiscoveryResponse>(bin) as DiscoveryResponse;
                    if (data == null) return;
                    MaxMind.GeoIP2.Responses.CountryResponse geoIP; string _description;
                    try
                    {
                        using (var reader = new DatabaseReader(Program.Location + Path.DirectorySeparatorChar + "geoip.mmdb"))
                        {
                            geoIP = reader.Country(msg.SenderEndPoint.Address.ToString());
                            data.ServerName = "["+geoIP.Continent.Code+"/"+geoIP.Country.IsoCode+"] "+data.ServerName;
                            _description = "[" + geoIP.Continent.Name + "/" + geoIP.Country.Name + "] " + msg.SenderEndPoint.Address.ToString() + ":" + data.Port;
                        }
                    }
                    catch (Exception ex)
                    {
                        UI.Notify("GeoIP: "+ex.Message);
                        LogToConsole(3, false, "GeoIP", ex.Message);
                        _description = msg.SenderEndPoint.Address.ToString() + ":" + data.Port;
                    }
                    var item = new UIMenuItem(data.ServerName);
                    TextInfo _gamemode = new System.Globalization.CultureInfo("en-US", false).TextInfo;
                    string __gamemode = _gamemode.ToTitleCase(data.Gamemode.ToString());
                    var gamemode = data.Gamemode == null ? "Unknown" : __gamemode;

                    item.SetRightLabel(gamemode + " | " + data.PlayerCount + "/" + data.MaxPlayers);
                    item.Description = _description;

                    if (data.PasswordProtected)
                        item.SetLeftBadge(UIMenuItem.BadgeStyle.Lock);

                    int lastIndx = 0;
                    if (_serverBrowserMenu.Size > 0)
                        lastIndx = _serverBrowserMenu.CurrentSelection;

                    var gMsg = msg;
                    item.Activated += (sender, selectedItem) =>
                    {
                        if (IsOnServer())
                        {
                            _client.Disconnect("Switching servers.");

                            if (Opponents != null)
                            {
                                Opponents.ToList().ForEach(pair => pair.Value.Clear());
                                Opponents.Clear();
                            }

                            if (Npcs != null)
                            {
                                Npcs.ToList().ForEach(pair => pair.Value.Clear());
                                Npcs.Clear();
                            }

                            while (IsOnServer()) Script.Yield();
                        }

                        if (data.PasswordProtected)
                        {
                            _password = Game.GetUserInput(256);
                        }
                        ConnectToServer(gMsg.SenderEndPoint.Address.ToString(), data.Port);
                        _serverBrowserMenu.Visible = false;
                    };

                    _serverBrowserMenu.AddItem(item);
                    _serverBrowserMenu.CurrentSelection = lastIndx;
                    //_serverBrowserMenu.Subtitle.Caption = "Servers listed: ~g~~h~" + dejson.list.Count().ToString();
                }
            }
        }


        static void LogToConsole(int flag, bool debug, string module, string message)
        {
            if (module == null || module.Equals("")) { module = "CLIENT"; }
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


        #region debug stuff

        private DateTime _artificialLagCounter = DateTime.MinValue;
        private bool _debugStarted;
        private SyncPed _debugSyncPed;

        private void Debug()
        {
            var player = Game.Player.Character;

            var debugText = "";

            debugText +=
            player.CurrentVehicle.Rotation.X + ", " + player.CurrentVehicle.Rotation.Y + ", "+ player.CurrentVehicle.Rotation.Z + "\n";
            var converted = Util.QuaternionToEuler(player.CurrentVehicle.Quaternion);
            debugText += converted.X + ", " + converted.Y + ", " + converted.Z;

            new UIResText(debugText, new Point(10, 10), 0.5f).Draw();


            if (_debugSyncPed == null)
            {
                _debugSyncPed = new SyncPed(player.Model.Hash, player.Position, player.Quaternion, false);
            }

            if (DateTime.Now.Subtract(_artificialLagCounter).TotalMilliseconds >= 300)
            {
                _artificialLagCounter = DateTime.Now;
                if (player.IsInVehicle())
                {
                    var veh = player.CurrentVehicle;

                    _debugSyncPed.VehiclePosition = veh.Position;
                    _debugSyncPed.VehicleRotation = veh.Quaternion;
                    _debugSyncPed.ModelHash = player.Model.Hash;
                    _debugSyncPed.VehicleHash = veh.Model.Hash;
                    _debugSyncPed.VehiclePrimaryColor = (int)veh.PrimaryColor;
                    _debugSyncPed.VehicleSecondaryColor = (int)veh.SecondaryColor;
                    _debugSyncPed.PedHealth = player.Health;
                    _debugSyncPed.VehicleHealth = veh.Health;
                    _debugSyncPed.VehicleSeat = Util.GetPedSeat(player);
                    _debugSyncPed.IsHornPressed = Game.Player.IsPressingHorn;
                    _debugSyncPed.Siren = veh.SirenActive;
                    _debugSyncPed.VehicleMods = CheckPlayerVehicleMods();
                    _debugSyncPed.Speed = veh.Speed;
                    _debugSyncPed.IsInVehicle = true;
                    _debugSyncPed.LastUpdateReceived = DateTime.Now;
                }
                else
                {
                    bool aiming = Game.IsControlPressed(0, GTA.Control.Aim);
                    bool shooting = Function.Call<bool>(Hash.IS_PED_SHOOTING, player.Handle);

                    GTA.Math.Vector3 aimCoord = new GTA.Math.Vector3();
                    if (aiming || shooting)
                        aimCoord = ScreenRelToWorld(GameplayCamera.Position, GameplayCamera.Rotation,
                            new Vector2(0, 0));

                    _debugSyncPed.AimCoords = aimCoord;
                    _debugSyncPed.Position = player.Position;
                    _debugSyncPed.Rotation = player.Quaternion;
                    _debugSyncPed.ModelHash = player.Model.Hash;
                    _debugSyncPed.CurrentWeapon = (int)player.Weapons.Current.Hash;
                    _debugSyncPed.PedHealth = player.Health;
                    _debugSyncPed.IsAiming = aiming;
                    _debugSyncPed.IsShooting = shooting;
                    _debugSyncPed.IsJumping = Function.Call<bool>(Hash.IS_PED_JUMPING, player.Handle);
                    _debugSyncPed.IsParachuteOpen = Function.Call<int>(Hash.GET_PED_PARACHUTE_STATE, Game.Player.Character.Handle) == 2;
                    _debugSyncPed.IsInVehicle = false;
                    _debugSyncPed.PedProps = CheckPlayerProps();
                    _debugSyncPed.LastUpdateReceived = DateTime.Now;
                }
            }

            _debugSyncPed.DisplayLocally();

            if (_debugSyncPed.Character != null)
            {
                Function.Call(Hash.SET_ENTITY_NO_COLLISION_ENTITY, _debugSyncPed.Character.Handle, player.Handle, false);
                Function.Call(Hash.SET_ENTITY_NO_COLLISION_ENTITY, player.Handle, _debugSyncPed.Character.Handle, false);
            }


            if (_debugSyncPed.MainVehicle != null && player.IsInVehicle())
            {
                Function.Call(Hash.SET_ENTITY_NO_COLLISION_ENTITY, _debugSyncPed.MainVehicle.Handle, player.CurrentVehicle.Handle, false);
                Function.Call(Hash.SET_ENTITY_NO_COLLISION_ENTITY, player.CurrentVehicle.Handle, _debugSyncPed.MainVehicle.Handle, false);
            }

        }
        
        #endregion

        public void DecodeNativeCall(NativeData obj)
        {
            var list = new List<InputArgument>();

            foreach (var arg in obj.Arguments)
            {
                if (arg is IntArgument)
                {
                    list.Add(new InputArgument(((IntArgument)arg).Data));
                }
                else if (arg is UIntArgument)
                {
                    list.Add(new InputArgument(((UIntArgument)arg).Data));
                }
                else if (arg is StringArgument)
                {
                    list.Add(new InputArgument(((StringArgument)arg).Data));
                }
                else if (arg is FloatArgument)
                {
                    list.Add(new InputArgument(((FloatArgument)arg).Data));
                }
                else if (arg is BooleanArgument)
                {
                    list.Add(new InputArgument(((BooleanArgument)arg).Data));
                }
                else if (arg is LocalPlayerArgument)
                {
                    list.Add(new InputArgument(Game.Player.Character.Handle));
                }
                else if (arg is OpponentPedHandleArgument)
                {
                    var handle = ((OpponentPedHandleArgument)arg).Data;
                    lock (Opponents) if (Opponents.ContainsKey(handle) && Opponents[handle].Character != null) list.Add(new InputArgument(Opponents[handle].Character.Handle));
                }
                else if (arg is Vector3Argument)
                {
                    var tmp = (Vector3Argument)arg;
                    list.Add(new InputArgument(tmp.X));
                    list.Add(new InputArgument(tmp.Y));
                    list.Add(new InputArgument(tmp.Z));
                }
                else if (arg is LocalGamePlayerArgument)
                {
                    list.Add(new InputArgument(Game.Player.Handle));
                }
            }

            var nativeType = CheckNativeHash(obj.Hash);

            if ((int)nativeType >= 2)
            {
                if ((int) nativeType >= 3)
                {
                    var modelObj = obj.Arguments[(int) nativeType - 3];
                    int modelHash = 0;

                    if (modelObj is UIntArgument)
                    {
                        modelHash = unchecked((int) ((UIntArgument) modelObj).Data);
                    }
                    else if (modelObj is IntArgument)
                    {
                        modelHash = ((IntArgument) modelObj).Data;
                    }
                    var model = new Model(modelHash);

                    if (model.IsValid)
                    {
                        model.Request(10000);
                    }
                }

                var entId = Function.Call<int>((Hash) obj.Hash, list.ToArray());
                lock(_entityCleanup) _entityCleanup.Add(entId);
                if (obj.ReturnType is IntArgument)
                {
                    SendNativeCallResponse(obj.Id, entId);
                }
                return;
            }

            if (nativeType == NativeType.ReturnsBlip)
            {
                var blipId = Function.Call<int>((Hash)obj.Hash, list.ToArray());
                lock (_blipCleanup) _blipCleanup.Add(blipId);
                if (obj.ReturnType is IntArgument)
                {
                    SendNativeCallResponse(obj.Id, blipId);
                }
                return;
            }

            if (obj.ReturnType == null)
            {
                Function.Call((Hash)obj.Hash, list.ToArray());
            }
            else
            {
                if (obj.ReturnType is IntArgument)
                {
                    SendNativeCallResponse(obj.Id, Function.Call<int>((Hash)obj.Hash, list.ToArray()));
                }
                else if (obj.ReturnType is UIntArgument)
                {
                    SendNativeCallResponse(obj.Id, Function.Call<uint>((Hash)obj.Hash, list.ToArray()));
                }
                else if (obj.ReturnType is StringArgument)
                {
                    SendNativeCallResponse(obj.Id, Function.Call<string>((Hash)obj.Hash, list.ToArray()));
                }
                else if (obj.ReturnType is FloatArgument)
                {
                    SendNativeCallResponse(obj.Id, Function.Call<float>((Hash)obj.Hash, list.ToArray()));
                }
                else if (obj.ReturnType is BooleanArgument)
                {
                    SendNativeCallResponse(obj.Id, Function.Call<bool>((Hash)obj.Hash, list.ToArray()));
                }
                else if (obj.ReturnType is Vector3Argument)
                {
                    SendNativeCallResponse(obj.Id, Function.Call<GTA.Math.Vector3>((Hash)obj.Hash, list.ToArray()));
                }
            }
        }

        public void SendNativeCallResponse(string id, object response)
        {
            var obj = new NativeResponse();
            obj.Id = id;

            if (response is int)
            {
                obj.Response = new IntArgument() { Data = ((int)response) };
            }
            else if (response is uint)
            {
                obj.Response = new UIntArgument() { Data = ((uint)response) };
            }
            else if (response is string)
            {
                obj.Response = new StringArgument() { Data = ((string)response) };
            }
            else if (response is float)
            {
                obj.Response = new FloatArgument() { Data = ((float)response) };
            }
            else if (response is bool)
            {
                obj.Response = new BooleanArgument() { Data = ((bool)response) };
            }
            else if (response is GTA.Math.Vector3)
            {
                var tmp = (GTA.Math.Vector3)response;
                obj.Response = new Vector3Argument()
                {
                    X = tmp.X,
                    Y = tmp.Y,
                    Z = tmp.Z,
                };
            }

            var msg = _client.CreateMessage();
            var bin = SerializeBinary(obj);
            msg.Write((int)PacketType.NativeResponse);
            msg.Write(bin.Length);
            msg.Write(bin);
            _client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, 0);
        }
        


        private NativeType CheckNativeHash(ulong hash)
        {
            switch (hash)
            {
                default:
                    return NativeType.Unknown;
                case 0xD49F9B0955C367DE:
                    return NativeType.ReturnsEntityNeedsModel2;
                case 0x7DD959874C1FD534:
                    return NativeType.ReturnsEntityNeedsModel3;
                case 0xAF35D0D2583051B0:
                case 0x509D5878EB39E842:
                case 0x9A294B2138ABB884:
                    return NativeType.ReturnsEntityNeedsModel1;
                case 0xEF29A16337FACADB:
                case 0xB4AC7D0CF06BFE8F:
                case 0x9B62392B474F44A0:
                case 0x63C6CCA8E68AE8C8:
                    return NativeType.ReturnsEntity;
                case 0x46818D79B1F7499A:
                case 0x5CDE92C702A8FCE7:
                case 0xBE339365C863BD36:
                case 0x5A039BB0BCA604B6:
                    return NativeType.ReturnsBlip;
            }
        }

        public static int GetPedSpeed(GTA.Math.Vector3 firstVector, GTA.Math.Vector3 secondVector)
        {
            float speed = (firstVector - secondVector).Length();
            if (speed < 0.02f)
            {
                return 0;
            }
            else if (speed >= 0.02f && speed < 0.05f)
            {
                return 1;
            }
            else if (speed >= 0.05f && speed < 0.12f)
            {
                return 2;
            }
            else if (speed >= 0.12f)
                return 3;
            return 0;
        }

        public static bool WorldToScreenRel(GTA.Math.Vector3 worldCoords, out Vector2 screenCoords)
        {
            var num1 = new OutputArgument();
            var num2 = new OutputArgument();

            if (!Function.Call<bool>(Hash._WORLD3D_TO_SCREEN2D, worldCoords.X, worldCoords.Y, worldCoords.Z, num1, num2))
            {
                screenCoords = new Vector2();
                return false;
            }
            screenCoords = new Vector2((num1.GetResult<float>() - 0.5f) * 2, (num2.GetResult<float>() - 0.5f) * 2);
            return true;
        }

        public static GTA.Math.Vector3 ScreenRelToWorld(GTA.Math.Vector3 camPos, GTA.Math.Vector3 camRot, Vector2 coord)
        {
            var camForward = RotationToDirection(camRot);
            var rotUp = camRot + new GTA.Math.Vector3(10, 0, 0);
            var rotDown = camRot + new GTA.Math.Vector3(-10, 0, 0);
            var rotLeft = camRot + new GTA.Math.Vector3(0, 0, -10);
            var rotRight = camRot + new GTA.Math.Vector3(0, 0, 10);

            var camRight = RotationToDirection(rotRight) - RotationToDirection(rotLeft);
            var camUp = RotationToDirection(rotUp) - RotationToDirection(rotDown);

            var rollRad = -DegToRad(camRot.Y);

            var camRightRoll = camRight * (float)Math.Cos(rollRad) - camUp * (float)Math.Sin(rollRad);
            var camUpRoll = camRight * (float)Math.Sin(rollRad) + camUp * (float)Math.Cos(rollRad);

            var point3D = camPos + camForward * 10.0f + camRightRoll + camUpRoll;
            Vector2 point2D;
            if (!WorldToScreenRel(point3D, out point2D)) return camPos + camForward * 10.0f;
            var point3DZero = camPos + camForward * 10.0f;
            Vector2 point2DZero;
            if (!WorldToScreenRel(point3DZero, out point2DZero)) return camPos + camForward * 10.0f;

            const double eps = 0.001;
            if (Math.Abs(point2D.X - point2DZero.X) < eps || Math.Abs(point2D.Y - point2DZero.Y) < eps) return camPos + camForward * 10.0f;
            var scaleX = (coord.X - point2DZero.X) / (point2D.X - point2DZero.X);
            var scaleY = (coord.Y - point2DZero.Y) / (point2D.Y - point2DZero.Y);
            var point3Dret = camPos + camForward * 10.0f + camRightRoll * scaleX + camUpRoll * scaleY;
            return point3Dret;
        }

        public static GTA.Math.Vector3 RotationToDirection(GTA.Math.Vector3 rotation)
        {
            var z = DegToRad(rotation.Z);
            var x = DegToRad(rotation.X);
            var num = Math.Abs(Math.Cos(x));
            return new GTA.Math.Vector3
            {
                X = (float)(-Math.Sin(z) * num),
                Y = (float)(Math.Cos(z) * num),
                Z = (float)Math.Sin(x)
            };
        }

        public static GTA.Math.Vector3 DirectionToRotation(GTA.Math.Vector3 direction)
        {
            direction.Normalize();

            var x = Math.Atan2(direction.Z, direction.Y);
            var y = 0;
            var z = -Math.Atan2(direction.X, direction.Y);

            return new GTA.Math.Vector3
            {
                X = (float)RadToDeg(x),
                Y = (float)RadToDeg(y),
                Z = (float)RadToDeg(z)
            };
        }

        public static double DegToRad(double deg)
        {
            return deg * Math.PI / 180.0;
        }

        public static double RadToDeg(double deg)
        {
            return deg * 180.0 / Math.PI;
        }

        public static double BoundRotationDeg(double angleDeg)
        {
            var twoPi = (int)(angleDeg / 360);
            var res = angleDeg - twoPi * 360;
            if (res < 0) res += 360;
            return res;
        }

        public static GTA.Math.Vector3 RaycastEverything(Vector2 screenCoord)
        {
            var camPos = GameplayCamera.Position;
            var camRot = GameplayCamera.Rotation;
            const float raycastToDist = 100.0f;
            const float raycastFromDist = 1f;

            var target3D = ScreenRelToWorld(camPos, camRot, screenCoord);
            var source3D = camPos;

            Entity ignoreEntity = Game.Player.Character;
            if (Game.Player.Character.IsInVehicle())
            {
                ignoreEntity = Game.Player.Character.CurrentVehicle;
            }

            var dir = (target3D - source3D);
            dir.Normalize();
            var raycastResults = World.Raycast(source3D + dir * raycastFromDist,
                source3D + dir * raycastToDist,
                (IntersectOptions)(1 | 16 | 256 | 2 | 4 | 8)// | peds + vehicles
                , ignoreEntity);

            if (raycastResults.DitHitAnything)
            {
                return raycastResults.HitCoords;
            }

            return camPos + dir * raycastToDist;
        }

        public static object DeserializeBinary<T>(byte[] data)
        {
            object output;
            using (var stream = new MemoryStream(data))
            {
                try
                {
                    output = Serializer.Deserialize<T>(stream);
                }
                catch (ProtoException)
                {
                    return null;
                }
            }
            return output;
        }

        public static byte[] SerializeBinary(object data)
        {
            using (var stream = new MemoryStream())
            {
                stream.SetLength(0);
                Serializer.Serialize(stream, data);
                return stream.ToArray();
            }
        }

        public int GetOpenUdpPort()
        {
            var startingAtPort = 5000;
            var maxNumberOfPortsToCheck = 500;
            var range = Enumerable.Range(startingAtPort, maxNumberOfPortsToCheck);
            var portsInUse =
                from p in range
                join used in System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners()
            on p equals used.Port
                select p;

            return range.Except(portsInUse).FirstOrDefault();
        }
    }

    public class MasterServerList
    {
        public List<string> list { get; set; }
    }
}
