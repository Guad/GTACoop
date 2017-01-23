using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ProtoBuf;

namespace GTAServer.ProtocolMessages
{
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
}
