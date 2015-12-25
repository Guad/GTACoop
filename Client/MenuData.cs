using System;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;

namespace GTACoOp
    {
        [ProtoContract]
        public class MenuOpen
        {
        [ProtoMember(1)]
        public bool Open;
        }
    }
