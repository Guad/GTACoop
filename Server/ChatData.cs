using ProtoBuf;

namespace GTAServer
{
    /// <summary>
    /// Class containing data for each chat message.
    /// </summary>
    [ProtoContract]
    public class ChatData
    {
        /// <summary>
        /// Message ID
        /// </summary>
        [ProtoMember(1)]
        public long Id { get; set; }
        /// <summary>
        /// Message Sender
        /// </summary>
        [ProtoMember(2)]
        public string Sender { get; set; }
        /// <summary>
        /// Message Contents
        /// </summary>
        [ProtoMember(3)]
        public string Message { get; set; }
    }
}