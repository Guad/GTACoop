using ProtoBuf;

namespace GTAServer.ProtocolMessages
{
    [ProtoContract]
    public class ChatData
    {
        [ProtoMember(1)]
        public long Id { get; set; }

        [ProtoMember(2)]
        public string Sender { get; set; }

        [ProtoMember(3)]
        public string Message { get; set; }
    }
}
