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

        public Client(NetConnection nc)
        {
            NetConnection = nc;
        }
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

        public readonly ScriptVersion ServerVersion = ScriptVersion.VERSION_0_6_1;

        private ServerScript _gamemode { get; set; }
        private List<ServerScript> _filterscripts;
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

                try
                {
                    try
                    {
                        Program.DeleteFile(Program.Location + "filterscripts" + Path.DirectorySeparatorChar + GamemodeName + ".dll:Zone.Identifier");
                    }
                    catch
                    {
                    }

                    var fsAsm = Assembly.LoadFrom(Program.Location + "filterscripts" + Path.DirectorySeparatorChar + path + ".dll");
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

        public void Tick()
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
                            Console.WriteLine("INFO: ping received from " + msg.SenderEndPoint.Address.ToString());
                            var pong = Server.CreateMessage();
                            pong.Write("pong");
                            Server.SendMessage(pong, client.NetConnection, NetDeliveryMethod.ReliableOrdered);
                        }
                        break;
                    case NetIncomingMessageType.VerboseDebugMessage:
                    case NetIncomingMessageType.DebugMessage:
                    case NetIncomingMessageType.WarningMessage:
                    case NetIncomingMessageType.ErrorMessage:
                        Console.WriteLine(msg.ReadString());
                        break;
                    case NetIncomingMessageType.ConnectionLatencyUpdated:
                        client.Latency = msg.ReadFloat();
                        break;
                    case NetIncomingMessageType.ConnectionApproval:
                        var type = msg.ReadInt32();
                        var leng = msg.ReadInt32();
                        var connReq = DeserializeBinary<ConnectionRequest>(msg.ReadBytes(leng)) as ConnectionRequest;
                        if (connReq == null)
                        {
                            client.NetConnection.Deny("Connection Object is null");
                            Server.Recycle(msg);
                            continue;
                        }

                        int clients = 0;
                        lock (Clients) clients = Clients.Count;
                        if (clients < MaxPlayers)
                        {
                            if (PasswordProtected && !string.IsNullOrWhiteSpace(Password))
                            {
                                if (Password != connReq.Password)
                                {
                                    client.NetConnection.Deny("Wrong password.");
                                    Console.WriteLine("Player connection refused: wrong password.");

                                    if (_gamemode != null) _gamemode.OnConnectionRefused(client, "Wrong password");
                                    if (_filterscripts != null) _filterscripts.ForEach(fs => fs.OnConnectionRefused(client, "Wrong password"));

                                    Server.Recycle(msg);

                                    continue;
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

                            var channelHail = Server.CreateMessage();
                            channelHail.Write(GetChannelIdForConnection(client));
                            client.NetConnection.Approve(channelHail);

                            bool sendMsg = true;

                            if (_gamemode != null) sendMsg = sendMsg && _gamemode.OnPlayerConnect(client);
                            if (_filterscripts != null) _filterscripts.ForEach(fs => sendMsg = sendMsg && fs.OnPlayerConnect(client));

                            if (sendMsg)
                            {
                                var chatObj = new ChatData()
                                {
                                    Sender = "SERVER",
                                    Message =
                                        "Player ~h~" + client.DisplayName +
                                        "~h~ has connected.",
                                };

                                SendToAll(chatObj, PacketType.ChatData, 0);
                            }

                            Console.WriteLine("New player connected: " + client.Name + " (" + client.DisplayName + ")");
                        }
                        else
                        {
                            client.NetConnection.Deny("No available player slots.");
                            Console.WriteLine("Player connection refused: server full.");
                            if (_gamemode != null) _gamemode.OnConnectionRefused(client, "Server is full");
                            if (_filterscripts != null) _filterscripts.ForEach(fs => fs.OnConnectionRefused(client, "Server is full"));
                        }
                        break;
                    case NetIncomingMessageType.StatusChanged:
                        var newStatus = (NetConnectionStatus)msg.ReadByte();
                        if (newStatus == NetConnectionStatus.Disconnected)
                        {
                            lock (Clients)
                            {
                                if (Clients.Contains(client))
                                {
                                    var sendMsg = true;

                                    if (_gamemode != null) sendMsg = sendMsg && _gamemode.OnPlayerDisconnect(client);
                                    if (_filterscripts != null) _filterscripts.ForEach(fs => sendMsg = sendMsg && fs.OnPlayerDisconnect(client));

                                    if (sendMsg)
                                    {
                                        var chatObj = new ChatData()
                                        {
                                            Sender = "SERVER",
                                            Message =
                                                "Player ~h~" + client.DisplayName +
                                                "~h~ has disconnected.",
                                        };

                                        SendToAll(chatObj, PacketType.ChatData, 0);
                                    }

                                    var dcObj = new PlayerDisconnect()
                                    {
                                        Id = client.NetConnection.RemoteUniqueIdentifier,
                                    };

                                    SendToAll(dcObj, PacketType.PlayerDisconnect, 0);

                                    Console.WriteLine("Player disconnected: " + client.Name + " (" + client.DisplayName + ")");
                                    
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
                        lock (Clients) obj.PlayerCount = Clients.Count;
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
                                            if (_gamemode != null) pass = _gamemode.OnChatMessage(client, data.Message);

                                            if (_filterscripts != null) _filterscripts.ForEach(fs => pass = pass && fs.OnChatMessage(client, data.Message));

                                            if (pass)
                                            {
                                                data.Id = client.NetConnection.RemoteUniqueIdentifier;
                                                data.Sender = client.DisplayName;
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

                                            SendToAll(data, PacketType.VehiclePositionData, GetChannelIdForConnection(client), client.NetConnection.RemoteUniqueIdentifier);
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

                                            SendToAll(data, PacketType.PedPositionData, GetChannelIdForConnection(client), client.NetConnection.RemoteUniqueIdentifier);
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
                                            SendToAll(data, PacketType.NpcVehPositionData, GetChannelIdForConnection(client), client.NetConnection.RemoteUniqueIdentifier);
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
                                            SendToAll(data, PacketType.NpcPedPositionData, GetChannelIdForConnection(client), client.NetConnection.RemoteUniqueIdentifier);
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
                                    if (_gamemode != null) _gamemode.OnPlayerKilled(client);
                                    if (_filterscripts != null) _filterscripts.ForEach(fs => fs.OnPlayerKilled(client));
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
            lock (Clients)
            {
                foreach (var client in Clients)
                {
                    if (client.NetConnection.RemoteUniqueIdentifier == exclude) continue;
                    NetOutgoingMessage msg = Server.CreateMessage();
                    msg.Write((int)packetType);
                    msg.Write(data.Length);
                    msg.Write(data);
                    client.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, channel);
                }
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

        public int GetChannelIdForConnection(Client conn)
        {
            lock (Clients) return (Clients.IndexOf(conn) % 31) + 1;
        }

        public void SendNativeCallToPlayer(Client player, ulong hash, params object[] arguments)
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
                else if (o is LocalGamePlayerArgument)
                {
                    list.Add((LocalGamePlayerArgument)o);
                }
            }

            obj.Arguments = list.ToList();

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
                else if (o is LocalGamePlayerArgument)
                {
                    list.Add((LocalGamePlayerArgument)o);
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

            Server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        public void SetNativeCallOnTickForPlayer(Client player, string identifier, ulong hash, params object[] arguments)
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
                else if (o is LocalGamePlayerArgument)
                {
                    list.Add((LocalGamePlayerArgument)o);
                }
            }
            obj.Arguments = list.ToList();

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
                else if (o is LocalGamePlayerArgument)
                {
                    list.Add((LocalGamePlayerArgument)o);
                }
            }
            obj.Arguments = list.ToList();

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
                else if (o is LocalGamePlayerArgument)
                {
                    list.Add((LocalGamePlayerArgument)o);
                }
            }

            obj.Arguments = list.ToList();

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

            SendToAll(chatObj, PacketType.ChatData, 0);
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

        public void KickPlayer(Client player, string reason)
        {
            player.NetConnection.Disconnect("Kicked: " + reason);
        }

        public void TeleportPlayer(Client player, Vector3 newPosition)
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

        public void GetPlayerHealth(Client player, Action<object> callback, string salt = "salt")
        {
            GetNativeCallFromPlayer(player, salt,
                0xEEF059FAD016D209, new IntArgument(), callback, new LocalPlayerArgument());
        }
    }
}