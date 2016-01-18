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

namespace GTACoOp
{
    public class Main : Script
    {
        public static PlayerSettings PlayerSettings;

        public static readonly ScriptVersion LocalScriptVersion = ScriptVersion.VERSION_0_9;

        private readonly UIMenu _mainMenu;
        private readonly UIMenu _serverBrowserMenu;
        private readonly UIMenu _playersMenu;
        private readonly UIMenu _settingsMenu;

        private readonly MenuPool _menuPool;

        private string _clientIp;
        private readonly Chat _chat;

        private static NetClient _client;
        private static NetPeerConfiguration _config;

        public static SynchronizationMode GlobalSyncMode;

        public static bool SendNpcs;
        private static int _channel;

        private readonly Queue<Action> _threadJumping;
        private string _password;
        private bool _lastDead;
        private bool _wasTyping;
        private bool _isTrafficEnabled;

        private DebugWindow _debug;

        // STATS
        private static int _bytesSent = 0;
        private static int _bytesReceived = 0;

        private static int _messagesSent = 0;
        private static int _messagesReceived = 0;
        //

        public Main()
        {
            PlayerSettings = Util.ReadSettings(Program.Location + Path.DirectorySeparatorChar + "GTACOOPSettings.xml");
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
                if (!string.IsNullOrEmpty(message))
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
#warning Affects performance when open, drops from 80~100 on a GTX 980 to high 30s ~ 60
            _menuPool = new MenuPool();

            _mainMenu = new UIMenu("Co-oP", "MAIN MENU");
            _settingsMenu = new UIMenu("Co-oP", "SETTINGS");
            _serverBrowserMenu = new UIMenu("Co-oP", "SERVER BROWSER");
            _playersMenu = new UIMenu("Co-oP", "PLAYER LIST");

            _serverBrowserMenu.SetMenuWidthOffset(300);

            _playersMenu.OnIndexChange += (sender, index) =>
            {
                KeyValuePair<long, SyncPed> newPlayer = new KeyValuePair<long,SyncPed>();
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

            var modeItem = new UIMenuListItem("Sync Mode", new List<dynamic>(Enum.GetNames(typeof(SynchronizationMode))), 0);

            modeItem.OnListChanged += (item, index) =>
            {
                GlobalSyncMode = Enum.Parse(typeof(SynchronizationMode), item.IndexToItem(index).ToString());
                lock (Opponents) if (Opponents != null) Opponents.ToList().ForEach(p => p.Value.SyncMode = GlobalSyncMode);
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

            var portItem = new UIMenuItem("Port");
            portItem.SetRightLabel(Port.ToString());
            portItem.Activated += (menu, item) =>
            {
                string newPort = Game.GetUserInput(5);
                int nPort;
                bool success = int.TryParse(newPort, out nPort);
                if (!success)
                {
                    UI.Notify("Wrong port format.");
                    return;
                }
                Port = nPort;
                portItem.SetRightLabel(nPort.ToString());
            };

            var listenItem = new UIMenuItem("Server IP");
            listenItem.Activated += (menu, item) =>
            {
                _clientIp = Game.GetUserInput(255);
                listenItem.SetRightLabel(_clientIp);
            };

            var passItem = new UIMenuItem("Password");
            passItem.Activated += (menu, item) =>
            {
                _password = Game.GetUserInput(255);
                passItem.SetRightLabel(new String('*', _password.Length));
            };

            var nameItem = new UIMenuItem("Display Name");
            nameItem.SetRightLabel(PlayerSettings.DisplayName);
            nameItem.Activated += (menu, item) =>
            {
                PlayerSettings.DisplayName = Game.GetUserInput(32);
                Util.SaveSettings(Program.Location + "GTACOOPSettings.xml");
                nameItem.SetRightLabel(PlayerSettings.DisplayName);
            };

            var connectItem = new UIMenuItem("Connect");
            connectItem.Activated += (sender, item) =>
            {
                if (!IsOnServer())
                {
                    if (string.IsNullOrEmpty(_clientIp))
                    {
                        UI.Notify("No IP adress specified.");
                        return;
                    }

                    ConnectToServer(_clientIp);
                }
                else
                {
                    if (_client != null) _client.Disconnect("Connection closed by peer.");
                }
            };

            var chatItem = new UIMenuCheckboxItem("Use Old Chat Input", false);
            chatItem.CheckboxEvent += (item, check) =>
            {
                _oldChat = check;
            };

            var npcItem = new UIMenuCheckboxItem("Share World With Players", false);
            npcItem.CheckboxEvent += (item, check) =>
            {
                SendNpcs = check;
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
            };

            var trafficItem = new UIMenuCheckboxItem("Enable Traffic When Sharing", false, "May affect performance.");
            trafficItem.CheckboxEvent += (item, check) =>
            {
                _isTrafficEnabled = check;
            };


            var browserItem = new UIMenuItem("Server Browser");
            _mainMenu.BindMenuToItem(_serverBrowserMenu, browserItem);
            browserItem.Activated += (sender, item) => RebuildServerBrowser();

            var settItem = new UIMenuItem("Settings");
            _mainMenu.BindMenuToItem(_settingsMenu, settItem);

            var playersItem = new UIMenuItem("Active Players");
            _mainMenu.BindMenuToItem(_playersMenu, playersItem);
            playersItem.Activated += (sender, item) => RebuildPlayersList();


            _mainMenu.AddItem(listenItem);
            _mainMenu.AddItem(portItem);
            _mainMenu.AddItem(passItem);
            _mainMenu.AddItem(browserItem);
            _mainMenu.AddItem(settItem);
            _mainMenu.AddItem(connectItem);
            _mainMenu.AddItem(playersItem);

            _settingsMenu.AddItem(nameItem);
            _settingsMenu.AddItem(npcItem);
            _settingsMenu.AddItem(trafficItem);
            _settingsMenu.AddItem(chatItem);

            #if DEBUG
            _settingsMenu.AddItem(modeItem);
            _settingsMenu.AddItem(spawnItem);
            #endif

            _mainMenu.RefreshIndex();
            _settingsMenu.RefreshIndex();

            _menuPool.Add(_mainMenu);
            _menuPool.Add(_serverBrowserMenu);
            _menuPool.Add(_settingsMenu);
            _menuPool.Add(_playersMenu);
            #endregion

            _debug = new DebugWindow();
        }

        // Debug stuff
        private bool display;
        private Ped mainPed;
        private Vehicle mainVehicle;

        private Vector3 oldplayerpos;
        private bool _lastJumping;
        private bool _lastShooting;
        private bool _lastAiming;
        private uint _switch;
        private bool _lastVehicle;
        private bool _oldChat;
        private bool _isGoingToCar;
        //

        public static Dictionary<long, SyncPed> Opponents;
        public static Dictionary<string, SyncPed> Npcs;
        public static float Latency;
        private int Port = 4499;

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
                UI.Notify("~r~~h~ERROR~h~~w~~n~Could not contact master server. Try again later.");
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
                return;
            }

            if (string.IsNullOrWhiteSpace(response))
                return;

            var dejson = JsonConvert.DeserializeObject<MasterServerList>(response) as MasterServerList;

            if (dejson == null) return;

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

            var meItem = new UIMenuItem(PlayerSettings.DisplayName);
            meItem.SetRightLabel(((int)(Latency * 1000)) + "ms");
            _playersMenu.AddItem(meItem);

            foreach (var ped in list)
            {
                var newItem = new UIMenuItem(ped.Name == null ? "" : ped.Name);
                newItem.SetRightLabel(((int)(ped.Latency * 1000)) + "ms");

                #if DEBUG
                newItem.Description = "Real latency: " + ped.AverageLatency;
                #endif

                newItem.Activated += (sender, item) =>
                {
                    var pos = ped.IsInVehicle ? ped.VehiclePosition : ped.Position;
                    Function.Call(Hash.SET_NEW_WAYPOINT, pos.X, pos.Y);
                };
                _playersMenu.AddItem(newItem);
            }

            _playersMenu.RefreshIndex();
        }

        private static Dictionary<int, int> _emptyVehicleMods;
        private Dictionary<string, NativeData> _tickNatives;
        private Dictionary<string, NativeData> _dcNatives;
        private List<int> _entityCleanup;
        private List<int> _blipCleanup;

        private static int _modSwitch = 0;
        private static int _pedSwitch = 0;
        private static Dictionary<int, int> _vehMods = new Dictionary<int, int>();
        private static Dictionary<int, int> _pedClothes = new Dictionary<int, int>();

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

                Vector3 aimCoord = new Vector3();
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

                Vector3 aimCoord = new Vector3();
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
            Ped player = Game.Player.Character;
            _menuPool.ProcessMenus();
            _chat.Tick();

            if (_isGoingToCar && Game.IsControlJustPressed(0, Control.PhoneCancel))
            {
                Game.Player.Character.Task.ClearAll();
                _isGoingToCar = false;
            }
            
            if (IsOnServer())
            {
                _mainMenu.MenuItems[5].Text = "Disconnect";
                _mainMenu.MenuItems[6].Enabled = true;
                _settingsMenu.MenuItems[0].Enabled = false;
            }
            else
            {
                _mainMenu.MenuItems[5].Text = "Connect";
                _mainMenu.MenuItems[6].Enabled = false;
                _settingsMenu.MenuItems[0].Enabled = true;
            }

            #if DEBUG
            if (display)
            {
                Debug();
                _debug.Visible = true;
                _debug.Draw();
            }
            #endif
            ProcessMessages();

            if (_client == null || _client.ConnectionStatus == NetConnectionStatus.Disconnected ||
                _client.ConnectionStatus == NetConnectionStatus.None) return;

            if (_wasTyping)
                Game.DisableControl(0, Control.FrontendPauseAlternate);

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

            if ((!_isTrafficEnabled && SendNpcs) || !SendNpcs)
            {
                Function.Call(Hash.SET_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
                Function.Call(Hash.SET_RANDOM_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
                Function.Call(Hash.SET_PARKED_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);

                Function.Call(Hash.SET_PED_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
                Function.Call(Hash.SET_SCENARIO_PED_DENSITY_MULTIPLIER_THIS_FRAME, 0f, 0f);

                Function.Call((Hash) 0x2F9A292AD0A3BD89);
                Function.Call((Hash) 0x5F3B7749C112D552);
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
            lock (_tickNatives) tickNatives = new Dictionary<string,NativeData>(_tickNatives);

            for (int i = 0; i < tickNatives.Count; i++) DecodeNativeCall(tickNatives.ElementAt(i).Value);
        }

        public static bool IsOnServer()
        {
            return _client != null && _client.ConnectionStatus == NetConnectionStatus.Connected;
        }

        public void OnKeyDown(object sender, KeyEventArgs e)
        {
            if(e.KeyCode != PlayerSettings.ActivationKey && !_chat.IsFocused && IsOnServer() && e.KeyCode != Keys.W && e.KeyCode != Keys.S && e.KeyCode != Keys.A && e.KeyCode != Keys.D)
            {
                var obj = new KeySendData()
                {
                    key = e.KeyCode
                };
                var data = SerializeBinary(obj);

                var msg = _client.CreateMessage();
                msg.Write((int)PacketType.KeySendData);
                msg.Write(data.Length);
                msg.Write(data);
                _client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, 0);
            }
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
                if (!_oldChat)
                {
                    _chat.IsFocused = true;
                    _wasTyping = true;
                }
                else
                {
                    var message = Game.GetUserInput(255);
                    if (!string.IsNullOrEmpty(message))
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

        public void ConnectToServer(string ip, int port = 0)
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
                    var item = new UIMenuItem(data.ServerName);

                    var gamemode = data.Gamemode == null ? "Unknown" : data.Gamemode;

                    item.SetRightLabel(gamemode + " | " + data.PlayerCount + "/" + data.MaxPlayers);

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
                }
            }
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
                $"{player.CurrentVehicle.Rotation.X}, {player.CurrentVehicle.Rotation.Y}, {player.CurrentVehicle.Rotation.Z}\n";
            var converted = Util.QuaternionToEuler(player.CurrentVehicle.Quaternion);
            debugText += $"{converted.X}, {converted.Y}, {converted.Z}";

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

                    Vector3 aimCoord = new Vector3();
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
                    SendNativeCallResponse(obj.Id, Function.Call<Vector3>((Hash)obj.Hash, list.ToArray()));
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
            else if (response is Vector3)
            {
                var tmp = (Vector3)response;
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
        
        private enum NativeType
        {
            Unknown = 0,
            ReturnsBlip = 1,
            ReturnsEntity = 2,
            ReturnsEntityNeedsModel1 = 3,
            ReturnsEntityNeedsModel2 = 4,
            ReturnsEntityNeedsModel3 = 5,
        }

        private NativeType CheckNativeHash(ulong hash)
        {
            switch (hash)
            {
                default:
                    return NativeType.Unknown;
                    break;
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
                    break;
                case 0x46818D79B1F7499A:
                case 0x5CDE92C702A8FCE7:
                case 0xBE339365C863BD36:
                case 0x5A039BB0BCA604B6:
                    return NativeType.ReturnsBlip;
                    break;
            }
        }

        public static int GetPedSpeed(Vector3 firstVector, Vector3 secondVector)
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

        public static bool WorldToScreenRel(Vector3 worldCoords, out Vector2 screenCoords)
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

        public static Vector3 ScreenRelToWorld(Vector3 camPos, Vector3 camRot, Vector2 coord)
        {
            var camForward = RotationToDirection(camRot);
            var rotUp = camRot + new Vector3(10, 0, 0);
            var rotDown = camRot + new Vector3(-10, 0, 0);
            var rotLeft = camRot + new Vector3(0, 0, -10);
            var rotRight = camRot + new Vector3(0, 0, 10);

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

        public static Vector3 RotationToDirection(Vector3 rotation)
        {
            var z = DegToRad(rotation.Z);
            var x = DegToRad(rotation.X);
            var num = Math.Abs(Math.Cos(x));
            return new Vector3
            {
                X = (float)(-Math.Sin(z) * num),
                Y = (float)(Math.Cos(z) * num),
                Z = (float)Math.Sin(x)
            };
        }

        public static Vector3 DirectionToRotation(Vector3 direction)
        {
            direction.Normalize();

            var x = Math.Atan2(direction.Z, direction.Y);
            var y = 0;
            var z = -Math.Atan2(direction.X, direction.Y);

            return new Vector3
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

        public static Vector3 RaycastEverything(Vector2 screenCoord)
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
