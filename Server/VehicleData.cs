using System;
using System.Collections.Generic;
using ProtoBuf;

namespace GTAServer
{
    public enum PacketType
    {
        VehiclePositionData = 0,
        ChatData = 1,
        PlayerDisconnect = 2,
        PedPositionData = 3,
        NpcVehPositionData = 4,
        NpcPedPositionData = 5,
        WorldSharingStop = 6,
        DiscoveryResponse = 7,
        ConnectionRequest = 8,
        NativeCall = 9,
        NativeResponse = 10,
        PlayerKilled = 11,
        NativeTick = 12,
        NativeTickRecall = 13,
    }

    public enum ScriptVersion
    {
        Unknown = 0,
        VERSION_0_6 = 1,
        VERSION_0_6_1 = 2,
        VERSION_0_7 = 3,
    }

    [ProtoContract]
    public class DiscoveryResponse
    {
        [ProtoMember(1)]
        public string ServerName { get; set; }
        [ProtoMember(2)]
        public int MaxPlayers { get; set; }
        [ProtoMember(3)]
        public int PlayerCount { get; set; }
        [ProtoMember(4)]
        public bool PasswordProtected { get; set; }
        [ProtoMember(5)]
        public int Port { get; set; }
        [ProtoMember(6)]
        public string Gamemode { get; set; }
    }

    [ProtoContract]
    public class ConnectionRequest
    {
        [ProtoMember(1)]
        public string Name { get; set; }

        [ProtoMember(2)]
        public string Password { get; set; }

        [ProtoMember(3)]
        public string DisplayName { get; set; }

        [ProtoMember(4)]
        public int GameVersion { get; set; }

        [ProtoMember(5)]
        public byte ScriptVersion { get; set; }
    }

    [ProtoContract]
    public class PlayerDisconnect
    {
        [ProtoMember(1)]
        public long Id { get; set; }
    }

    [ProtoContract]
    public class VehicleData
    {
        [ProtoMember(1)]
        public long Id { get; set; }
        [ProtoMember(2)]
        public string Name { get; set; }

        [ProtoMember(3)]
        public int VehicleModelHash { get; set; }
        [ProtoMember(4)]
        public int PedModelHash { get; set; }
        [ProtoMember(5)]
        public int PrimaryColor { get; set; }
        [ProtoMember(6)]
        public int SecondaryColor { get; set; }

        [ProtoMember(7)]
        public Vector3 Position { get; set; }
        [ProtoMember(8)]
        public Quaternion Quaternion { get; set; }

        [ProtoMember(9)]
        public int VehicleSeat { get; set; }

        [ProtoMember(10)]
        public int VehicleHealth { get; set; }

        [ProtoMember(11)]
        public int PlayerHealth { get; set; }

        [ProtoMember(12)]
        public float Latency { get; set; }

        [ProtoMember(13)]
        public Dictionary<int, int> VehicleMods { get; set; }

        [ProtoMember(14)]
        public bool IsPressingHorn { get; set; }

        [ProtoMember(15)]
        public bool IsSirenActive { get; set; }

        [ProtoMember(16)]
        public float Speed { get; set; }
    }

    [ProtoContract]
    public class PedData
    {
        [ProtoMember(1)]
        public long Id { get; set; }
        [ProtoMember(2)]
        public string Name { get; set; }

        [ProtoMember(3)]
        public int PedModelHash { get; set; }

        [ProtoMember(4)]
        public Vector3 Position { get; set; }
        [ProtoMember(5)]
        public Quaternion Quaternion { get; set; }

        [ProtoMember(6)]
        public bool IsJumping { get; set; }
        [ProtoMember(7)]
        public bool IsShooting { get; set; }
        [ProtoMember(8)]
        public bool IsAiming { get; set; }
        [ProtoMember(9)]
        public Vector3 AimCoords { get; set; }
        [ProtoMember(10)]
        public int WeaponHash { get; set; }

        [ProtoMember(11)]
        public int PlayerHealth { get; set; }

        [ProtoMember(12)]
        public float Latency { get; set; }

        [ProtoMember(13)]
        public Dictionary<int, int> PedProps { get; set; }

        [ProtoMember(14)]
        public bool IsParachuteOpen { get; set; }
    }


    [ProtoContract]
    public class Vector3
    {
        [ProtoMember(1)]
        public float X { get; set; }
        [ProtoMember(2)]
        public float Y { get; set; }
        [ProtoMember(3)]
        public float Z { get; set; }
    }

    [ProtoContract]
    public class Quaternion
    {
        [ProtoMember(1)]
        public float X { get; set; }
        [ProtoMember(2)]
        public float Y { get; set; }
        [ProtoMember(3)]
        public float Z { get; set; }
        [ProtoMember(4)]
        public float W { get; set; }
    }
}