using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;
using System.Windows.Forms;

namespace GTAServer
{
    [ProtoContract]
    class KeySendData {
        [ProtoMember(1)]
        public Keys key;
    }
}
