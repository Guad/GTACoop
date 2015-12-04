using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Xml.Serialization;
using Lidgren.Network;
using ProtoBuf;

namespace GTAServer
{
    public class GameServer
    {
        public GameServer(int port, string name, string gamemodeName)
        {
            Clients = new List<NetConnection>();
            NickNames = new Dictionary<long, string>();
            MaxPlayers = 32;
            Port = port;
            GamemodeName = gamemodeName;

            Name = name;
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
            NetPeerConfiguration config = new NetPeerConfiguration("GTAVOnlineRaces");
            config.Port = port;
            config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            config.EnableMessageType(NetIncomingMessageType.DiscoveryRequest);
            config.EnableMessageType(NetIncomingMessageType.UnconnectedData);
            config.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);
            Server = new NetServer(config);
            KnownLatencies = new Dictionary<long, float>();
        }

        public NetServer Server;

        public int MaxPlayers { get; set; }
        public int Port { get; set; }
        public List<NetConnection> Clients { get; set; }
        public Dictionary<long, float> KnownLatencies { get; set; }
        public Dictionary<long, string> NickNames;
        public string Name { get; set; }
        public string Password { get; set; }
        public bool PasswordProtected { get; set; }
        public string GamemodeName { get; set; }
        public string MasterServer { get; set; }
        public bool AnnounceSelf { get; set; }

        private ServerScript _gamemode { get; set; }
        private List<ServerScript> _filterscripts;

        public void Start(string[] filterscripts)
        {
            Server.Start();

            if (AnnounceSelf)
            {
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
                        Program.DeleteFile(Program.Location + "gamemodes\\" + GamemodeName + ".dll:Zone.Identifier");
                    }
                    catch
                    {
                    }

                    var asm = Assembly.LoadFrom(Program.Location + "gamemodes\\" + GamemodeName + ".dll");
                    var types = asm.GetExportedTypes();
                    var validTypes = types.Where(t =>
                        !t.IsInterface &&
                        !t.IsAbstract)
                        .Where(t => typeof(ServerScript).IsAssignableFrom(t));
                    if (!validTypes.Any())
                    {
                        Console.WriteLine("ERROR: No classes that inherit from {nameof(_gamemode)} have been found in the assembly. Starting freeroam.");
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

                try
                {
                    try
                    {
                        Program.DeleteFile(Program.Location + "filterscripts\\" + GamemodeName + ".dll:Zone.Identifier");
                    }
                    catch
                    {
                    }

                    var fsAsm = Assembly.LoadFrom(Program.Location + "filterscripts\\" + path + ".dll");
                    var fsObj = InstantiateScripts(fsAsm);
                    list.AddRange(fsObj);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to load filterscript \"" + path + "\", error: " + ex.Message);
                }
            }

            list.ForEach(fs =>
            {
                fs.Start();
                Console.WriteLine("Starting filterscript " + fs.Name + "...");
            });
            _filterscripts = list;
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

        private int _lastDay = DateTime.UtcNow.Day;


        public void Tick()
        {
            if (DateTime.UtcNow.Day != _lastDay)
            {
                _lastDay = DateTime.UtcNow.Day;
                if (AnnounceSelf)
                    AnnounceSelfToMaster();
            }

            NetIncomingMessage msg;
            while ((msg = Server.ReadMessage()) != null)
            {
                switch (msg.MessageType)
                {
                    case NetIncomingMessageType.UnconnectedData:
                        var isPing = msg.ReadString();
                        if (isPing == "ping")
                        {
                            Console.WriteLine("INFO: ping received from " + msg.SenderEndPoint.Address.ToString());
                            var pong = Server.CreateMessage();
                            pong.Write("pong");
                            Server.SendMessage(pong, msg.SenderConnection, NetDeliveryMethod.ReliableOrdered);
                        }
                        break;
                    case NetIncomingMessageType.VerboseDebugMessage:
                    case NetIncomingMessageType.DebugMessage:
                    case NetIncomingMessageType.WarningMessage:
                    case NetIncomingMessageType.ErrorMessage:
                        Console.WriteLine(msg.ReadString());
                        break;
                    case NetIncomingMessageType.ConnectionLatencyUpdated:
                        var latency = msg.ReadFloat();
                        if (KnownLatencies.ContainsKey(msg.SenderConnection.RemoteUniqueIdentifier))
                            KnownLatencies[msg.SenderConnection.RemoteUniqueIdentifier] = latency;
                        else
                            KnownLatencies.Add(msg.SenderConnection.RemoteUniqueIdentifier, latency);
                        break;
                    case NetIncomingMessageType.ConnectionApproval:
                        var type = msg.ReadInt32();
                        var leng = msg.ReadInt32();
                        var connReq = DeserializeBinary<ConnectionRequest>(msg.ReadBytes(leng)) as ConnectionRequest;
                        if (connReq == null)
                        {
                            msg.SenderConnection.Deny("Connection Object is null");
                            Server.Recycle(msg);
                            continue;
                        }

                        if (Clients.Count < MaxPlayers)
                        {
                            if (PasswordProtected && !string.IsNullOrWhiteSpace(Password))
                            {
                                if (Password != connReq.Password)
                                {
                                    msg.SenderConnection.Deny("Wrong password.");
                                    Console.WriteLine("Player connection refused: wrong password.");
                                    if (_gamemode != null) _gamemode.OnConnectionRefused(msg.SenderConnection, "Wrong password");
                                    if (_filterscripts != null) _filterscripts.ForEach(fs => fs.OnConnectionRefused(msg.SenderConnection, "Wrong password"));
                                    Server.Recycle(msg);
                                    continue;
                                }
                            }

                            Clients.Add(msg.SenderConnection);
                            if (NickNames.ContainsKey(msg.SenderConnection.RemoteUniqueIdentifier))
                                NickNames[msg.SenderConnection.RemoteUniqueIdentifier] = connReq.Name;
                            else
                                NickNames.Add(msg.SenderConnection.RemoteUniqueIdentifier, connReq.Name);

                            var channelHail = Server.CreateMessage();
                            channelHail.Write(GetChannelIdForConnection(msg.SenderConnection));
                            msg.SenderConnection.Approve(channelHail);

                            var chatObj = new ChatData()
                            {
                                Sender = "SERVER",
                                Message =
                                    "Player ~h~" + NickNames[msg.SenderConnection.RemoteUniqueIdentifier] +
                                    "~h~ has connected.",
                            };

                            SendToAll(chatObj, PacketType.ChatData, 0);

                            Console.WriteLine("New player connected: " + NickNames[msg.SenderConnection.RemoteUniqueIdentifier]);

                            if (_gamemode != null) _gamemode.OnPlayerConnect(msg.SenderConnection);
                            if (_filterscripts != null) _filterscripts.ForEach(fs => fs.OnPlayerConnect(msg.SenderConnection));
                        }
                        else
                        {
                            msg.SenderConnection.Deny("No available player slots.");
                            Console.WriteLine("Player connection refused: server full.");
                            if (_gamemode != null) _gamemode.OnConnectionRefused(msg.SenderConnection, "Server is full");
                            if (_filterscripts != null) _filterscripts.ForEach(fs => fs.OnConnectionRefused(msg.SenderConnection, "Server is full"));
                        }
                        break;
                    case NetIncomingMessageType.StatusChanged:
                        var newStatus = (NetConnectionStatus)msg.ReadByte();
                        if (newStatus == NetConnectionStatus.Disconnected && Clients.Contains(msg.SenderConnection))
                        {
                            var name = "";
                            if (NickNames.ContainsKey(msg.SenderConnection.RemoteUniqueIdentifier))
                                name = NickNames[msg.SenderConnection.RemoteUniqueIdentifier];

                            var chatObj = new ChatData()
                            {
                                Sender = "SERVER",
                                Message =
                                    "Player ~h~" + name +
                                    "~h~ has disconnected.",
                            };

                            SendToAll(chatObj, PacketType.ChatData, 0);

                            var dcObj = new PlayerDisconnect()
                            {
                                Id = msg.SenderConnection.RemoteUniqueIdentifier,
                            };

                            SendToAll(dcObj, PacketType.PlayerDisconnect, 0);

                            Console.WriteLine("Player disconnected: " + name);

                            if (_gamemode != null) _gamemode.OnPlayerDisconnect(msg.SenderConnection);
                            if (_filterscripts != null) _filterscripts.ForEach(fs => fs.OnPlayerDisconnect(msg.SenderConnection));

                            Clients.Remove(msg.SenderConnection);
                            NickNames.Remove(msg.SenderConnection.RemoteUniqueIdentifier);
                        }
                        break;
                    case NetIncomingMessageType.DiscoveryRequest:
                        NetOutgoingMessage response = Server.CreateMessage();
                        var obj = new DiscoveryResponse();
                        obj.ServerName = Name;
                        obj.MaxPlayers = MaxPlayers;
                        obj.PasswordProtected = PasswordProtected;
                        obj.PlayerCount = Clients.Count;
                        obj.Port = Port;

                        var bin = SerializeBinary(obj);

                        response.Write((int)PacketType.DiscoveryResponse);
                        response.Write(bin.Length);
                        response.Write(bin);

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
                                            var pass = true;
                                            if (_gamemode != null)
                                            {
                                                pass = _gamemode.OnChatMessage(msg.SenderConnection, data.Message);
                                            }

                                            if (_filterscripts != null) _filterscripts.ForEach(fs => pass = pass && fs.OnChatMessage(msg.SenderConnection, data.Message));

                                            if (pass)
                                            {
                                                data.Id = msg.SenderConnection.RemoteUniqueIdentifier;
                                                data.Sender = NickNames[msg.SenderConnection.RemoteUniqueIdentifier];
                                                SendToAll(data, PacketType.ChatData, 0);
                                                Console.WriteLine(data.Sender + ": " + data.Message);
                                            }
                                        }
                                    }
                                    catch (IndexOutOfRangeException)
                                    { }
                                }
                                break;
                            case PacketType.VehiclePositionData:
                                {
                                    try
                                    {
                                        var name = "";
                                        if (NickNames.ContainsKey(msg.SenderConnection.RemoteUniqueIdentifier))
                                            name = NickNames[msg.SenderConnection.RemoteUniqueIdentifier];

                                        var len = msg.ReadInt32();
                                        var data =
                                            DeserializeBinary<VehicleData>(msg.ReadBytes(len)) as
                                                VehicleData;
                                        if (data != null)
                                        {
                                            data.Id = msg.SenderConnection.RemoteUniqueIdentifier;
                                            data.Name = name;
                                            if (KnownLatencies.ContainsKey(msg.SenderConnection.RemoteUniqueIdentifier))
                                                data.Latency =
                                                    KnownLatencies[msg.SenderConnection.RemoteUniqueIdentifier];

                                            SendToAll(data, PacketType.VehiclePositionData, GetChannelIdForConnection(msg.SenderConnection), msg.SenderConnection.RemoteUniqueIdentifier);
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
                                        var name = "";
                                        if (NickNames.ContainsKey(msg.SenderConnection.RemoteUniqueIdentifier))
                                            name = NickNames[msg.SenderConnection.RemoteUniqueIdentifier];
                                        var len = msg.ReadInt32();
                                        var data =
                                            DeserializeBinary<PedData>(msg.ReadBytes(len)) as PedData;
                                        if (data != null)
                                        {
                                            data.Id = msg.SenderConnection.RemoteUniqueIdentifier;
                                            data.Name = name;
                                            if (KnownLatencies.ContainsKey(msg.SenderConnection.RemoteUniqueIdentifier))
                                                data.Latency =
                                                    KnownLatencies[msg.SenderConnection.RemoteUniqueIdentifier];
                                            SendToAll(data, PacketType.PedPositionData, GetChannelIdForConnection(msg.SenderConnection), msg.SenderConnection.RemoteUniqueIdentifier);
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
                                            data.Id = msg.SenderConnection.RemoteUniqueIdentifier;
                                            SendToAll(data, PacketType.NpcVehPositionData, GetChannelIdForConnection(msg.SenderConnection), msg.SenderConnection.RemoteUniqueIdentifier);
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
                                            SendToAll(data, PacketType.NpcPedPositionData, GetChannelIdForConnection(msg.SenderConnection), msg.SenderConnection.RemoteUniqueIdentifier);
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
                                        Id = msg.SenderConnection.RemoteUniqueIdentifier,
                                    };
                                    SendToAll(dcObj, PacketType.WorldSharingStop, 0);
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
                            case PacketType.PlayerKilled:
                                {
                                    if (_gamemode != null) _gamemode.OnPlayerKilled(msg.SenderConnection);
                                    if (_filterscripts != null) _filterscripts.ForEach(fs => fs.OnPlayerKilled(msg.SenderConnection));
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
        }

        public void SendToAll(object newData, PacketType packetType, int channel, long exclude = 0)
        {
            var data = SerializeBinary(newData);
            foreach (var client in Clients)
            {
                if (client.RemoteUniqueIdentifier == exclude) continue;
                NetOutgoingMessage msg = Server.CreateMessage();
                msg.Write((int)packetType);
                msg.Write(data.Length);
                msg.Write(data);
                client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, channel);
            }
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

        public int GetChannelIdForConnection(NetConnection conn)
        {
            return (Clients.IndexOf(conn) % 31) + 1;
        }

        public void SendNativeCallToPlayer(NetConnection player, ulong hash, params object[] arguments)
        {
            var obj = new NativeData();
            obj.Hash = hash;

            var list = new List<NativeArgument>();
            foreach (var o in arguments)
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
            }

            obj.Arguments = list.ToList();

            var bin = SerializeBinary(obj);

            var msg = Server.CreateMessage();

            msg.Write((int)PacketType.NativeCall);
            msg.Write(bin.Length);
            msg.Write(bin);

            player.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, GetChannelIdForConnection(player));
        }

        public void SendNativeCallToAllPlayers(ulong hash, params object[] arguments)
        {
            var obj = new NativeData();
            obj.Hash = hash;

            var list = new List<NativeArgument>();
            foreach (var o in arguments)
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
                else if (o is LocalPlayerArgument)
                {
                    list.Add((LocalPlayerArgument)o);
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
                else if (o is OpponentPedHandleArgument)
                {
                    list.Add((OpponentPedHandleArgument)o);
                }
            }

            obj.Arguments = list.ToList();
            obj.ReturnType = null;
            obj.Id = null;

            var bin = SerializeBinary(obj);

            var msg = Server.CreateMessage();

            msg.Write((int)PacketType.NativeCall);
            msg.Write(bin.Length);
            msg.Write(bin);

            Clients.ForEach(c => c.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, GetChannelIdForConnection(c)));
        }

        private Dictionary<string, Action<object>> _callbacks = new Dictionary<string, Action<object>>();
        public void GetNativeCallFromPlayer(NetConnection player, string identifier, ulong hash, NativeArgument returnType, Action<object> callback,
            params object[] arguments)
        {
            var obj = new NativeData();
            obj.Hash = hash;
            obj.ReturnType = returnType;
            obj.Id = identifier;

            var list = new List<NativeArgument>();
            foreach (var o in arguments)
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
                    list.Add(new LocalPlayerArgument());
                }
                else if (o is OpponentPedHandleArgument)
                {
                    list.Add((OpponentPedHandleArgument)o);
                }
            }

            obj.Arguments = list.ToList();

            var bin = SerializeBinary(obj);

            var msg = Server.CreateMessage();

            msg.Write((int)PacketType.NativeCall);
            msg.Write(bin.Length);
            msg.Write(bin);

            _callbacks.Add(identifier, callback);
            player.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, GetChannelIdForConnection(player));
        }

        // SCRIPTING

        public void SendChatMessageToAll(string sender, string message)
        {
            var chatObj = new ChatData()
            {
                Sender = sender,
                Message = message,
            };

            SendToAll(chatObj, PacketType.ChatData, 0);
        }

        public void SendChatMessageToPlayer(NetConnection player, string sender, string message)
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
            player.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, 0);
        }

        public void GivePlayerWeapon(NetConnection player, uint weaponHash, int ammo, bool equipNow, bool ammoLoaded)
        {
            SendNativeCallToPlayer(player, 0xBF0FD6E56C964FCB, new LocalPlayerArgument(), weaponHash, ammo, equipNow, ammo);
        }

        public void KickPlayer(NetConnection player, string reason)
        {
            player.Disconnect("Kicked: " + reason);
        }

        public void TeleportPlayer(NetConnection player, Vector3 newPosition)
        {
            SendNativeCallToPlayer(player, 0x06843DA7060A026B, new LocalPlayerArgument(), newPosition.X, newPosition.Y, newPosition.Z, 0, 0, 0, 1);
        }

        public void GetPlayerPosition(NetConnection player, Action<object> callback, string salt = "salt")
        {
            GetNativeCallFromPlayer(player,
                Environment.TickCount.ToString() +
                salt +
                player.RemoteUniqueIdentifier.ToString() +
                DateTime.Now.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalMilliseconds.ToString(),
                0x3FEF770D40960D5A, new Vector3Argument(), callback, new LocalPlayerArgument(), 0);
        }

        public void HasPlayerControlBeenPressed(NetConnection player, int controlId, Action<object> callback, string salt = "salt")
        {
            GetNativeCallFromPlayer(player,
                Environment.TickCount.ToString() +
                salt +
                player.RemoteUniqueIdentifier.ToString() +
                DateTime.Now.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalMilliseconds.ToString(),
                0x580417101DDB492F, new BooleanArgument(), callback, 0, controlId);
        }

        public void SetPlayerHealth(NetConnection player, int health)
        {
            SendNativeCallToPlayer(player, 0x6B76DC1F3AE6E6A3, new LocalPlayerArgument(), health + 100);
        }

        public void GetPlayerHealth(NetConnection player, Action<object> callback, string salt = "salt")
        {
            GetNativeCallFromPlayer(player,
                Environment.TickCount.ToString() +
                salt +
                player.RemoteUniqueIdentifier.ToString() +
                DateTime.Now.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalMilliseconds.ToString(),
                0xEEF059FAD016D209, new IntArgument(), callback, new LocalPlayerArgument());
        }
    }
}