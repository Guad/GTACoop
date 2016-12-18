using System;
using System.Collections.Generic;
using ProtoBuf;

namespace GTAServer
{
    /// <summary>
    /// Server packet types
    /// </summary>
    public enum PacketType
    {
        /// <summary>
        /// VehiclePositionData
        /// </summary>
        VehiclePositionData = 0,
        /// <summary>
        /// ChatData packet
        /// </summary>
        ChatData = 1,
        /// <summary>
        /// PlayerDisconnect packet
        /// </summary>
        PlayerDisconnect = 2,
        /// <summary>
        /// PedPositionData packet
        /// </summary>
        PedPositionData = 3,
        /// <summary>
        /// NpcVehPositionData packet
        /// </summary>
        NpcVehPositionData = 4,
        /// <summary>
        /// NpcPedPositionData packet
        /// </summary>
        NpcPedPositionData = 5,
        /// <summary>
        /// WorldSharingStop packet
        /// </summary>
        WorldSharingStop = 6,
        /// <summary>
        /// DiscoveryResponse packet
        /// </summary>
        DiscoveryResponse = 7,
        /// <summary>
        /// ConnectionRequest packet
        /// </summary>
        ConnectionRequest = 8,
        /// <summary>
        /// NativeCall packet
        /// </summary>
        NativeCall = 9,
        /// <summary>
        /// NativeResponse packet
        /// </summary>
        NativeResponse = 10,
        /// <summary>
        /// PlayerSpawned packet
        /// </summary>
        PlayerSpawned = 11,
        /// <summary>
        /// NativeTick packet
        /// </summary>
        NativeTick = 12,
        /// <summary>
        /// NativeTickRecall packet
        /// </summary>
        NativeTickRecall = 13,
        /// <summary>
        /// NativeOnDisconnect packet
        /// </summary>
        NativeOnDisconnect = 14,
        /// <summary>
        /// NativeOnDisconnectRecall packet
        /// </summary>
        NativeOnDisconnectRecall = 15,
    }

    /// <summary>
    /// Script versions
    /// </summary>
    public enum ScriptVersion
    {
        /// <summary>
        /// Unknown version (Too old to send version)
        /// </summary>
        VERSION_UNKNOWN = 0,
        /// <summary>
        /// v0.6
        /// </summary>
        VERSION_0_6 = 1,
        /// <summary>
        /// v0.6.1
        /// </summary>
        VERSION_0_6_1 = 2,
        /// <summary>
        /// v0.7
        /// </summary>
        VERSION_0_7 = 3,
        /// <summary>
        /// v0.8.1
        /// </summary>
        VERSION_0_8_1 = 4,
        /// <summary>
        /// v0.9
        /// </summary>
        VERSION_0_9 = 5,
        /// <summary>
        /// v0.9.1
        /// </summary>
        VERSION_0_9_1 = 6,
        /// <summary>
        /// v0.9.2
        /// </summary>
        VERSION_0_9_2 = 7,
        /// <summary>
        /// v0.9.3
        /// </summary>
        VERSION_0_9_3 = 8,
        /// <summary>
        /// v0.9.4
        /// </summary>
        VERSION_0_9_4 = 9
    }

    /// <summary>
    /// DiscoveryResponse packet
    /// </summary>
    [ProtoContract]
    public class DiscoveryResponse
    {
        /// <summary>
        /// Server name
        /// </summary>
        [ProtoMember(1)]
        public string ServerName { get; set; }
        /// <summary>
        /// Server max players
        /// </summary>
        [ProtoMember(2)]
        public int MaxPlayers { get; set; }
        /// <summary>
        /// Server player count
        /// </summary>
        [ProtoMember(3)]
        public int PlayerCount { get; set; }
        /// <summary>
        /// If the server has a password
        /// </summary>
        [ProtoMember(4)]
        public bool PasswordProtected { get; set; }
        /// <summary>
        /// Server port
        /// </summary>
        [ProtoMember(5)]
        public int Port { get; set; }
        /// <summary>
        /// Server gamemode
        /// </summary>
        [ProtoMember(6)]
        public string Gamemode { get; set; }
    }

    /// <summary>
    /// ConnectionRequest packet
    /// </summary>
    [ProtoContract]
    public class ConnectionRequest
    {
        /// <summary>
        /// Player name
        /// </summary>
        [ProtoMember(1)]
        public string Name { get; set; }
        /// <summary>
        /// Password to connect
        /// </summary>
        [ProtoMember(2)]
        public string Password { get; set; }
        /// <summary>
        /// Player display name
        /// </summary>
        [ProtoMember(3)]
        public string DisplayName { get; set; }
        /// <summary>
        /// Player game version
        /// </summary>
        [ProtoMember(4)]
        public int GameVersion { get; set; }
        /// <summary>
        /// Player script version
        /// </summary>
        [ProtoMember(5)]
        public byte ScriptVersion { get; set; }
    }

    /// <summary>
    /// PlayerDisconnect packet
    /// </summary>
    [ProtoContract]
    public class PlayerDisconnect
    {
        /// <summary>
        /// Player ID? Don't remember.
        /// </summary>
        [ProtoMember(1)]
        public long Id { get; set; }
    }

    /// <summary>
    /// VehicleData packet
    /// </summary>
    [ProtoContract]
    public class VehicleData
    {
        /// <summary>
        /// Vehicle ID
        /// </summary>
        [ProtoMember(1)]
        public long Id { get; set; }
        /// <summary>
        /// Vehicle name
        /// </summary>
        [ProtoMember(2)]
        public string Name { get; set; }

        /// <summary>
        /// Vehicle model hash
        /// </summary>
        [ProtoMember(3)]
        public int VehicleModelHash { get; set; }
        /// <summary>
        /// Ped model hash
        /// </summary>
        [ProtoMember(4)]
        public int PedModelHash { get; set; }
        /// <summary>
        /// Primary color
        /// </summary>
        [ProtoMember(5)]
        public int PrimaryColor { get; set; }
        /// <summary>
        /// Secondary color
        /// </summary>
        [ProtoMember(6)]
        public int SecondaryColor { get; set; }

        /// <summary>
        /// Vehicle position
        /// </summary>
        [ProtoMember(7)]
        public Vector3 Position { get; set; }
        /// <summary>
        /// Vehicle rotation
        /// </summary>
        [ProtoMember(8)]
        public Quaternion Quaternion { get; set; }

        /// <summary>
        /// Vehicle seat
        /// </summary>
        [ProtoMember(9)]
        public int VehicleSeat { get; set; }
        /// <summary>
        /// Vehicle health
        /// </summary>
        [ProtoMember(10)]
        public int VehicleHealth { get; set; }
        /// <summary>
        /// Player health
        /// </summary>
        [ProtoMember(11)]
        public int PlayerHealth { get; set; }
        /// <summary>
        /// Vehicle latency
        /// </summary>
        [ProtoMember(12)]
        public float Latency { get; set; }
        /// <summary>
        /// Vehicle mods
        /// </summary>
        [ProtoMember(13)]
        public Dictionary<int, int> VehicleMods { get; set; }
        /// <summary>
        /// If the horn is being pressed
        /// </summary>
        [ProtoMember(14)]
        public bool IsPressingHorn { get; set; }
        /// <summary>
        /// If the siren is active
        /// </summary>
        [ProtoMember(15)]
        public bool IsSirenActive { get; set; }
        /// <summary>
        /// Vehicle speed
        /// </summary>
        [ProtoMember(16)]
        public float Speed { get; set; }
    }

    /// <summary>
    /// Pedestrian data
    /// </summary>
    [ProtoContract]
    public class PedData
    {
        /// <summary>
        /// Ped ID
        /// </summary>
        [ProtoMember(1)]
        public long Id { get; set; }
        /// <summary>
        /// Ped name
        /// </summary>
        [ProtoMember(2)]
        public string Name { get; set; }

        /// <summary>
        /// Ped model hash
        /// </summary>
        [ProtoMember(3)]
        public int PedModelHash { get; set; }

        /// <summary>
        /// Ped position
        /// </summary>
        [ProtoMember(4)]
        public Vector3 Position { get; set; }
        /// <summary>
        /// Ped rotation
        /// </summary>
        [ProtoMember(5)]
        public Quaternion Quaternion { get; set; }

        /// <summary>
        /// If the ped is jumping
        /// </summary>
        [ProtoMember(6)]
        public bool IsJumping { get; set; }
        /// <summary>
        /// If the ped is shooting
        /// </summary>
        [ProtoMember(7)]
        public bool IsShooting { get; set; }
        /// <summary>
        /// If the ped is aiming
        /// </summary>
        [ProtoMember(8)]
        public bool IsAiming { get; set; }
        /// <summary>
        /// Where the ped is aiming
        /// </summary>
        [ProtoMember(9)]
        public Vector3 AimCoords { get; set; }
        /// <summary>
        /// Hash of the ped's weapon
        /// </summary>
        [ProtoMember(10)]
        public int WeaponHash { get; set; }
        /// <summary>
        /// Health of the ped
        /// </summary>
        [ProtoMember(11)]
        public int PlayerHealth { get; set; }
        /// <summary>
        /// Ped latency
        /// </summary>
        [ProtoMember(12)]
        public float Latency { get; set; }
        /// <summary>
        /// Ped props
        /// </summary>
        [ProtoMember(13)]
        public Dictionary<int, int> PedProps { get; set; }
        /// <summary>
        /// If the parachute is open
        /// </summary>
        [ProtoMember(14)]
        public bool IsParachuteOpen { get; set; }
    }

    /// <summary>
    /// Vector3
    /// </summary>
    [ProtoContract]
    public class Vector3
    {
        public Vector3()
        {
            X = 0f;
            Y = 0f;
            Z = 0f;
        }

        public Vector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
        /// <summary>
        /// X
        /// </summary>
        [ProtoMember(1)]
        public float X { get; set; }
        /// <summary>
        /// Y
        /// </summary>
        [ProtoMember(2)]
        public float Y { get; set; }
        /// <summary>
        /// Z
        /// </summary>
        [ProtoMember(3)]
        public float Z { get; set; }
    }

    /// <summary>
    /// Quaternion (usually used for rotation)
    /// </summary>
    [ProtoContract]
    public class Quaternion
    {
        /// <summary>
        /// X
        /// </summary>
        [ProtoMember(1)]
        public float X { get; set; }
        /// <summary>
        /// Y
        /// </summary>
        [ProtoMember(2)]
        public float Y { get; set; }
        /// <summary>
        /// Z
        /// </summary>
        [ProtoMember(3)]
        public float Z { get; set; }
        /// <summary>
        /// W
        /// </summary>
        [ProtoMember(4)]
        public float W { get; set; }
    }
}