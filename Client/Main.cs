using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Serialization;
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

        private readonly UIMenu _mainMenu;
        private readonly UIMenu _serverBrowserMenu;
        private readonly UIMenu _playersMenu;

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

        // STATS
        private static int _bytesSent = 0;
        private static int _bytesReceived = 0;

        private static int _messagesSent = 0;
        private static int _messagesReceived = 0;
        //

        public Main()
        {
            PlayerSettings = Util.ReadSettings(Program.Location + "scripts\\GTACOOPSettings.xml");
            _threadJumping = new Queue<Action>();
            _emptyVehicleMods = new Dictionary<int, int>();
            for (int i = 0; i < 50; i++)
            {
                _emptyVehicleMods.Add(i, 0);
            }

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

            _config = new NetPeerConfiguration("GTAVOnlineRaces");
            _config.Port = 8888;
            _config.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);


            #region Menu Set up
            _menuPool = new MenuPool();

            _mainMenu = new UIMenu("Co-oP", "MAIN MENU");
            var settingsMenu = new UIMenu("Co-oP", "SETTINGS");
            _serverBrowserMenu = new UIMenu("Co-oP", "SERVER BROWSER");
            _playersMenu = new UIMenu("Co-oP", "PLAYER LIST");

            _playersMenu.OnIndexChange += (sender, index) =>
            {
                var newPlayer = Opponents.FirstOrDefault(ops => ops.Value.Name == _playersMenu.MenuItems[index].Text);
                if (newPlayer.Equals(new KeyValuePair<long, SyncPed>())) return;
                Function.Call(Hash.LOCK_MINIMAP_POSITION, newPlayer.Value.Position.X, newPlayer.Value.Position.Y);
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
                if (Opponents != null) Opponents.ToList().ForEach(p => p.Value.SyncMode = GlobalSyncMode);
            };

            var spawnItem = new UIMenuCheckboxItem("Debug", false);
            spawnItem.CheckboxEvent += (item, check) =>
            {
                display = check;
                if (!display)
                {
                    if (mainPed != null) mainPed.Delete();
                    if (mainVehicle != null) mainVehicle.Delete();
                }
            };

            var portItem = new UIMenuItem("Port");
            portItem.SetRightLabel("4499");
            portItem.Activated += (menu, item) =>
            {
                string newPort = Game.GetUserInput(10);
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
                    Opponents.ToList().ForEach(pair => pair.Value.Clear());
                    Opponents.Clear();
                    Npcs.ToList().ForEach(pair => pair.Value.Clear());
                    Npcs.Clear();
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


            var browserItem = new UIMenuItem("Server Browser");
            _mainMenu.BindMenuToItem(_serverBrowserMenu, browserItem);
            browserItem.Activated += (sender, item) => RebuildServerBrowser();

            var settItem = new UIMenuItem("Settings");
            _mainMenu.BindMenuToItem(settingsMenu, settItem);

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

            settingsMenu.AddItem(modeItem);
            settingsMenu.AddItem(chatItem);
            settingsMenu.AddItem(npcItem);
            settingsMenu.AddItem(spawnItem);

            _mainMenu.RefreshIndex();
            settingsMenu.RefreshIndex();


            _menuPool.Add(_mainMenu);
            _menuPool.Add(_serverBrowserMenu);
            _menuPool.Add(settingsMenu);
            _menuPool.Add(_playersMenu);
            #endregion

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

        private int Port = 4499;

        private void RebuildServerBrowser()
        {
            _serverBrowserMenu.Clear();
            _serverBrowserMenu.RefreshIndex();
            if (string.IsNullOrEmpty(PlayerSettings.MasterServerAddress))
                return;
            string response = String.Empty;
            using (var wc = new WebClient())
            {
                response = wc.DownloadString(PlayerSettings.MasterServerAddress);
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
                _client.DiscoverKnownPeer(split[0], int.Parse(split[1]));
            }
        }

        private void RebuildPlayersList()
        {
            _playersMenu.Clear();
            var list = new List<SyncPed>(Opponents.Select(pair => pair.Value));

            foreach (var ped in list)
            {
                var newItem = new UIMenuItem(ped.Name);
                newItem.SetRightLabel(((int)(ped.Latency * 1000)) + "ms");
                newItem.Activated += (sender, item) =>
                {
                    Function.Call(Hash.SET_NEW_WAYPOINT, ped.Position.X, ped.Position.Y);
                };
                _playersMenu.AddItem(newItem);
            }

            _playersMenu.RefreshIndex();
        }

        private static Dictionary<int, int> _emptyVehicleMods;


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
                    if (!_vehMods.ContainsKey(id))
                        _vehMods.Add(id, mod);
                    else
                        _vehMods[id] = mod;
                }
            }
            _modSwitch++;
            if (_modSwitch >= 1500)
            {
                _modSwitch = 0;
            }
            return _vehMods;
        }

        public static Dictionary<int, int> CheckPlayerProps()
        {
            if (_pedSwitch%30 == 0)
            {
                var id = _pedSwitch/30;
                var mod = Function.Call<int>(Hash.GET_PED_DRAWABLE_VARIATION, Game.Player.Character.Handle, id);
                if (mod != -1)
                {
                    if (!_pedClothes.ContainsKey(id))
                        _pedClothes.Add(id, mod);
                    else
                        _pedClothes[id] = mod;
                }
            }

            _pedSwitch++;
            if (_pedSwitch >= 450)
                _pedSwitch = 0;
            return _vehMods;
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

        public void CheckExpiredNpcs()
        {
            const int threshold = 10000; // 10 second timeout

            for (int i = Main.Npcs.Count - 1; i >= 0; i--)
            {
                if (DateTime.Now.Subtract(Main.Npcs.ElementAt(i).Value.LastUpdateReceived).TotalMilliseconds > threshold)
                {
                    var key = Main.Npcs.ElementAt(i).Key;
                    Main.Npcs[key].Clear();
                    Main.Npcs.Remove(key);
                }
            }
            /*
            for (int i = Main.Opponents.Count - 1; i >= 0; i--)
            {
                if (DateTime.Now.Subtract(Main.Opponents.ElementAt(i).Value.LastUpdateReceived).TotalMilliseconds > threshold)
                {
                    var key = Main.Opponents.ElementAt(i).Key;
                    Main.Opponents[key].Clear();
                    Main.Opponents.Remove(key);
                }
            }*/
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
            }
            else
            {
                _mainMenu.MenuItems[5].Text = "Connect";
            }

            if (display)
                Debug();

            ProcessMessages();

            if (_client == null || _client.ConnectionStatus == NetConnectionStatus.Disconnected ||
                _client.ConnectionStatus == NetConnectionStatus.None) return;

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


            Function.Call(Hash.SET_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
            Function.Call(Hash.SET_RANDOM_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
            Function.Call(Hash.SET_PARKED_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);

            Function.Call(Hash.SET_PED_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
            Function.Call(Hash.SET_SCENARIO_PED_DENSITY_MULTIPLIER_THIS_FRAME, 0f, 0f);

            Function.Call((Hash)0x2F9A292AD0A3BD89);
            Function.Call((Hash)0x5F3B7749C112D552);

            Function.Call(Hash.SET_TIME_SCALE, 1f);

            #region NET

            #endregion

            string stats = string.Format("{0}Kb (D)/{1}Kb (U), {2}Msg (D)/{3}Msg (U)", _bytesReceived / 1000,
                _bytesSent / 1000, _messagesReceived, _messagesSent);

            //UI.ShowSubtitle(stats);

            if (_threadJumping.Any())
            {
                Action action = _threadJumping.Dequeue();
                if (action != null) action.Invoke();
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
                _mainMenu.Visible = !_mainMenu.Visible;
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
                    _chat.IsFocused = true;
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
            Opponents = new Dictionary<long, SyncPed>();
            Npcs = new Dictionary<string, SyncPed>();

            var msg = _client.CreateMessage();

            var obj = new ConnectionRequest();
            obj.Name = string.IsNullOrEmpty(PlayerSettings.Name) ? "Player" : PlayerSettings.Name;
            if (!string.IsNullOrEmpty(_password))
                obj.Password = _password;
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

                                if (Opponents.ContainsKey(data.Id))
                                {
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

                                    Opponents[data.Id].Siren = data.IsSirenActive;
                                }
                                else
                                {
                                    var repr = new SyncPed(data.PedModelHash, data.Position.ToVector(),
                                        data.Quaternion.ToQuaternion());
                                    Opponents.Add(data.Id, repr);
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

                                    Opponents[data.Id].Siren = data.IsSirenActive;
                                }
                            }
                            break;
                        case PacketType.PedPositionData:
                            {
                                var len = msg.ReadInt32();
                                var data = DeserializeBinary<PedData>(msg.ReadBytes(len)) as PedData;
                                if (data == null) return;

                                if (Opponents.ContainsKey(data.Id))
                                {
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

                                    Opponents[data.Id].PedProps = data.PedProps;
                                }
                                else
                                {
                                    var repr = new SyncPed(data.PedModelHash, data.Position.ToVector(),
                                        data.Quaternion.ToQuaternion());
                                    Opponents.Add(data.Id, repr);
                                    Opponents[data.Id].LastUpdateReceived = DateTime.Now;
                                    Opponents[data.Id].Name = data.Name;
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

                                    Opponents[data.Id].PedProps = data.PedProps;
                                }
                            }
                            break;
                        case PacketType.NpcVehPositionData:
                            {
                                var len = msg.ReadInt32();
                                var data = DeserializeBinary<VehicleData>(msg.ReadBytes(len)) as VehicleData;
                                if (data == null) return;

                                if (Npcs.ContainsKey(data.Name))
                                {
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
                                }
                                else
                                {
                                    var repr = new SyncPed(data.PedModelHash, data.Position.ToVector(),
                                        data.Quaternion.ToQuaternion(), false);
                                    Npcs.Add(data.Name, repr);

                                    Npcs[data.Name].LastUpdateReceived = DateTime.Now;
                                    Npcs[data.Name].Name = "";
                                    Npcs[data.Name].Host = data.Id;
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
                                }
                            }
                            break;
                        case PacketType.NpcPedPositionData:
                            {
                                var len = msg.ReadInt32();
                                var data = DeserializeBinary<PedData>(msg.ReadBytes(len)) as PedData;
                                if (data == null) return;

                                if (Npcs.ContainsKey(data.Name))
                                {

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
                                }
                                else
                                {
                                    var repr = new SyncPed(data.PedModelHash, data.Position.ToVector(),
                                        data.Quaternion.ToQuaternion(), false);
                                    Npcs.Add(data.Name, repr);

                                    Npcs[data.Name].LastUpdateReceived = DateTime.Now;
                                    Npcs[data.Name].Name = "";
                                    Npcs[data.Name].Host = data.Id;
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
                                }
                            }
                            break;
                        case PacketType.ChatData:
                            {
                                var len = msg.ReadInt32();
                                var data = DeserializeBinary<ChatData>(msg.ReadBytes(len)) as ChatData;
                                if (data != null && !string.IsNullOrEmpty(data.Message))
                                {
                                    _threadJumping.Enqueue(() =>
                                    {
                                        if (!string.IsNullOrEmpty(data.Sender))
                                            UI.Notify(data.Sender + ": " + data.Message);
                                        else
                                            UI.Notify(data.Message);
                                    });
                                }
                            }
                            break;
                        case PacketType.PlayerDisconnect:
                            {
                                var len = msg.ReadInt32();
                                var data = DeserializeBinary<PlayerDisconnect>(msg.ReadBytes(len)) as PlayerDisconnect;
                                if (data != null && Opponents.ContainsKey(data.Id))
                                {
                                    Opponents[data.Id].Clear();
                                    Opponents.Remove(data.Id);

                                    var toRem = new List<string>();
                                    foreach (var pair in Npcs.Where(p => p.Value.Host == data.Id))
                                    {
                                        pair.Value.Clear();
                                        toRem.Add(pair.Key);
                                    }

                                    foreach (var i in toRem)
                                    {
                                        Npcs.Remove(i);
                                    }
                                }
                            }
                            break;
                        case PacketType.WorldSharingStop:
                            {
                                var len = msg.ReadInt32();
                                var data = DeserializeBinary<PlayerDisconnect>(msg.ReadBytes(len)) as PlayerDisconnect;
                                if (data == null) return;
                                var list = Npcs.Where(p => p.Value.Host == data.Id).ToList();
                                foreach (var pair in list)
                                {
                                    pair.Value.Clear();
                                    Npcs.Remove(pair.Key);
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
                    }
                }
                else if (msg.MessageType == NetIncomingMessageType.StatusChanged)
                {
                    var newStatus = (NetConnectionStatus)msg.ReadByte();
                    UI.Notify("STATUS: " + newStatus);
                    switch (newStatus)
                    {
                        case NetConnectionStatus.Connected:
                            UI.Notify("Connection successful!");
                            _channel = msg.SenderConnection.RemoteHailMessage.ReadInt32();
                            break;
                        case NetConnectionStatus.Disconnected:
                            UI.Notify("You have been disconnected from the server.");
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
                    item.SetRightLabel(data.PlayerCount + "/" + data.MaxPlayers);
                    if (data.PasswordProtected)
                        item.SetLeftBadge(UIMenuItem.BadgeStyle.Lock);

                    int lastIndx = 0;
                    if (_serverBrowserMenu.Size > 0)
                        lastIndx = _serverBrowserMenu.CurrentSelection;

                    var gMsg = msg;
                    item.Activated += (sender, selectedItem) =>
                    {
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

        public void Debug()
        {
            Ped player = Game.Player.Character;
            if (display)
            {
                if (mainPed == null || !mainPed.Exists())
                {
                    Vector3 pos = player.Position;
                    mainPed = World.CreatePed(player.Model, pos, player.Heading);
                    Function.Call(Hash.SET_ENTITY_NO_COLLISION_ENTITY, mainPed.Handle, player.Handle, false);
                    Function.Call(Hash.SET_ENTITY_NO_COLLISION_ENTITY, player.Handle, mainPed.Handle, false);
                    mainPed.Alpha = 125;
                }
                else
                {
                    //Vector3 offset = player.Position - oldplayerpos;
                    //Vector3 dest = mainPed.Position + offset + player.ForwardVector*2f;
                    Vector3 dest = player.Position; // + new Vector3(5f, 0f, 0f);


                    new UIText(
                        (player.Position - oldplayerpos).Length().ToString() + "\n" +
                        GetPedSpeed(player.Position, oldplayerpos).ToString(), new Point(20, 20), 0.5f, Color.White,
                        GTA.Font.ChaletLondon, false).Draw();
                    /*
                    int speed = GetPedSpeed(player.Position, oldplayerpos);
                    if (speed == 0)
                    {
                        //mainPed.Task.ClearAllImmediately();
                        //mainPed.Task.StandStill(-1);
                        if(!mainPed.IsInRangeOf(player.Position + new Vector3(5f, 0f, 0f), 1f))
                            mainPed.Task.GoTo(player.Position + new Vector3(5f, 0f, 0f), true, 100);
                    }
                    else
                    {
                        Function.Call(Hash.TASK_GO_STRAIGHT_TO_COORD, mainPed.Handle, dest.X, dest.Y, dest.Z, (float)speed, 100, player.Heading, 0.0f);
                        //mainPed.Task.GoTo(dest, true, 100);
                    }*/

                    if (!_lastVehicle && player.IsInVehicle())
                    {
                        if (mainVehicle != null) mainVehicle.Delete();
                        mainVehicle = World.CreateVehicle(player.CurrentVehicle.Model,
                            player.CurrentVehicle.Position + player.ForwardVector * 10f, player.CurrentVehicle.Heading);
                        Function.Call(Hash.SET_ENTITY_NO_COLLISION_ENTITY, mainVehicle.Handle,
                            player.CurrentVehicle.Handle, false);
                        Function.Call(Hash.SET_ENTITY_NO_COLLISION_ENTITY, player.CurrentVehicle.Handle,
                            mainVehicle.Handle, false);
                        mainVehicle.Position = player.CurrentVehicle.Position;
                        mainVehicle.Heading = player.CurrentVehicle.Heading;
                        mainVehicle.Alpha = 125;
                        Function.Call(Hash.SET_PED_INTO_VEHICLE, mainPed.Handle, mainVehicle.Handle,
                            (int)VehicleSeat.Driver);
                        _lastVehicle = true;
                    }

                    if (_lastVehicle && !player.IsInVehicle())
                    {
                        mainPed.Task.LeaveVehicle();
                        while (mainPed.IsInVehicle()) Yield();
                        if (mainVehicle != null) mainVehicle.Delete();
                        Function.Call(Hash.SET_ENTITY_NO_COLLISION_ENTITY, mainPed.Handle, player.Handle, false);
                        Function.Call(Hash.SET_ENTITY_NO_COLLISION_ENTITY, player.Handle, mainPed.Handle, false);
                    }
                    _switch++;
                    if (player.IsInVehicle())
                    {
                        {
                            var STATIONID = Function.Call<int>(Hash.GET_PLAYER_RADIO_STATION_INDEX);
                            var STATIONNAME = Function.Call<string>(Hash.GET_RADIO_STATION_NAME, STATIONID);
                            var TRACKID = Function.Call<int>(Hash.GET_AUDIBLE_MUSIC_TRACK_TEXT_ID);
                            //var debugThing = Function.Call<string>((Hash) 0x5F43D83FD6738741);
                            UI.ShowSubtitle("{STATIONNAME}({STATIONID}) - {TRACKID}");
                        }



                        if (!mainPed.IsInVehicle() || mainVehicle == null)
                        {
                            if (mainVehicle != null) mainVehicle.Delete();
                            mainVehicle = World.CreateVehicle(player.CurrentVehicle.Model,
                                player.CurrentVehicle.Position + player.ForwardVector * 10f, player.CurrentVehicle.Heading);
                            Function.Call(Hash.SET_ENTITY_NO_COLLISION_ENTITY, mainVehicle.Handle,
                                player.CurrentVehicle.Handle, false);
                            Function.Call(Hash.SET_ENTITY_NO_COLLISION_ENTITY, player.CurrentVehicle.Handle,
                                mainVehicle.Handle, false);
                            mainVehicle.Position = player.CurrentVehicle.Position;
                            mainVehicle.Heading = player.CurrentVehicle.Heading;
                            mainVehicle.Alpha = 125;
                            Function.Call(Hash.SET_PED_INTO_VEHICLE, mainPed.Handle, mainVehicle.Handle,
                                (int)VehicleSeat.Driver);
                            return;
                        }
                        //Function.Call(Hash.TASK_VEHICLE_MISSION_COORS_TARGET, riv.Character.Handle, riv.Vehicle.Handle, race.Checkpoints[0].X, race.Checkpoints[0].Y, race.Checkpoints[0].Z, Mode, 200f, (int)DrivingStyle.AvoidTraffic, 5f, 10f, 0);
                        /*
                        if(!mainPed.IsInRangeOf(player.Position, 3f) && _switch % 100 == 0)
                            Function.Call(Hash.TASK_VEHICLE_MISSION_COORS_TARGET, mainPed.Handle, mainVehicle?.Handle, dest.X, dest.Y, dest.Z, 4, player.CurrentVehicle.Speed < 10f ? 10f : player.CurrentVehicle.Speed, (int)DrivingStyle.AvoidTraffic, 0f, 1f, 0);
                        else if (mainPed.IsInRangeOf(player.Position, 3f))
                        {
                            mainPed.Task.ClearAll();
                        }*/

                        //mainVehicle.Position = player.CurrentVehicle.Position;
                        var dir = player.CurrentVehicle.Position - mainVehicle.Position;
                        dir.Normalize();

                        mainVehicle.ApplyForce(dir);
                        if (!player.CurrentVehicle.IsInRangeOf(mainVehicle.Position, 0.8f))
                            mainVehicle.Position = player.CurrentVehicle.Position;

                        mainVehicle.Quaternion = player.CurrentVehicle.Quaternion;
                    }
                    else
                    {
                        if (mainPed.Weapons.Current != player.Weapons.Current)
                        {
                            mainPed.Weapons.Give(player.Weapons.Current.Hash, player.Weapons.Current.Ammo, true, true);
                            mainPed.Weapons.Select(player.Weapons.Current);
                        }

                        bool jumping = Function.Call<bool>(Hash.IS_PED_JUMPING, player.Handle);

                        oldplayerpos = player.Position;

                        if (!_lastJumping && jumping)
                        {
                            mainPed.Task.Jump();
                        }

                        bool aiming = Game.IsControlPressed(0, GTA.Control.Aim);
                        bool shooting = Function.Call<bool>(Hash.IS_PED_SHOOTING, player.Handle);

                        Vector3 aimCoord = new Vector3();
                        if (aiming || shooting)
                            aimCoord = RaycastEverything(new Vector2(0, 0));
                        //aimCoord = ScreenRelToWorld(GameplayCamera.Position, GameplayCamera.Rotation, new Vector2(0, 0));


                        //mainPed.Heading = player.Heading;
                        if (_lastShooting && !shooting && Game.IsControlPressed(0, GTA.Control.Attack))
                            shooting = true;

                        int threshold = 50;
                        if (aiming && !shooting && !mainPed.IsInRangeOf(player.Position, 0.5f) && _switch % threshold == 0)
                        {
                            Function.Call(Hash.TASK_GO_TO_COORD_WHILE_AIMING_AT_COORD, mainPed.Handle, dest.X, dest.Y,
                                dest.Z, aimCoord.X, aimCoord.Y, aimCoord.Z, 2f, 0, 0x3F000000, 0x40800000, 1, 512, 0,
                                (uint)FiringPattern.FullAuto);
                        }
                        else if (aiming && !shooting && mainPed.IsInRangeOf(player.Position, 0.5f))
                        {
                            mainPed.Task.AimAt(aimCoord, 100);
                        }

                        if (!mainPed.IsInRangeOf(player.Position, 0.5f) &&
                            ((shooting && !_lastShooting) || (shooting && _lastShooting && _switch % (threshold * 2) == 0)))
                        {
                            Function.Call(Hash.TASK_GO_TO_COORD_WHILE_AIMING_AT_COORD, mainPed.Handle, dest.X, dest.Y,
                                dest.Z, aimCoord.X, aimCoord.Y, aimCoord.Z, 2f, 1, 0x3F000000, 0x40800000, 1, 0, 0,
                                (uint)FiringPattern.FullAuto);
                        }
                        else if ((shooting && !_lastShooting) ||
                                 (shooting && _lastShooting && _switch % (threshold / 2) == 0))
                        {
                            Function.Call(Hash.TASK_SHOOT_AT_COORD, mainPed.Handle, aimCoord.X, aimCoord.Y,
                                aimCoord.Z, 1500, (uint)FiringPattern.FullAuto);
                        }

                        if (!aiming && !shooting && !jumping)
                        {
                            if (GlobalSyncMode == SynchronizationMode.Teleport)
                            {
                                mainPed.Position = dest - new Vector3(0, 0, 1f);
                                mainPed.Heading = player.Heading;
                            }
                            else if (!mainPed.IsInRangeOf(player.Position, 0.5f))
                            {
                                mainPed.Task.RunTo(player.Position, true, 500);
                            }
                        }

                        int speed = GetPedSpeed(player.Position, oldplayerpos);
                        switch (speed)
                        {
                            case 1:
                                Function.Call(Hash.TASK_PLAY_ANIM, mainPed.Handle, "move_m@casual@e", "walk", 8f, -8f,
                                    -1, 1, 8f, 1, 1, 1);
                                break;
                            case 2:
                                Function.Call(Hash.TASK_PLAY_ANIM, mainPed.Handle, "move_m@casual@e", "run", 8f, -8f, -1,
                                    1, 8f, 1, 1, 1);
                                break;
                        }
                        _lastJumping = jumping;
                        _lastShooting = shooting;
                        _lastAiming = aiming;
                    }
                    oldplayerpos = player.Position;
                    _lastVehicle = player.IsInVehicle();
                }
            }
        }

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
                    if (Opponents.ContainsKey(handle) && Opponents[handle].Character != null)
                        list.Add(new InputArgument(Opponents[handle].Character.Handle));
                }
                else if (arg is Vector3Argument)
                {
                    var tmp = (Vector3Argument)arg;
                    list.Add(new InputArgument(tmp.X));
                    list.Add(new InputArgument(tmp.Y));
                    list.Add(new InputArgument(tmp.Z));
                }
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
