using ProtoBuf;

namespace GTACoOp
{
    [ProtoContract]
    public class VehicleSpawnData
    {
        [ProtoMember(1)]
        public uint modelHash;
        [ProtoMember(2)]
        public float x;
        [ProtoMember(3)]
        public float y;
        [ProtoMember(4)]
        public float z;
        [ProtoMember(5)]
        public float heading;
    }
}
