using ProtoBuf;

namespace GTAServer.ProtocolMessages
{
    [ProtoContract]
    public class PlayerDisconnect
    {
        [ProtoMember(1)]
        public long Id { get; set; }
    }
}
