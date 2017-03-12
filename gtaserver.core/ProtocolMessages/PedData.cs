using System.Collections.Generic;
using ProtoBuf;

namespace GTAServer.ProtocolMessages {
    [ProtoContract]
    public class PedData {
        [ProtoMember(1)]
        public long Id { get;set; }
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
        public Dictionary<int,int> PedProps { get; set; }
        [ProtoMember(14)]
        public bool IsParachuteOpen { get; set; }
    }
}