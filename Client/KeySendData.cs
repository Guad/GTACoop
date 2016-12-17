using System.Windows.Forms;
using ProtoBuf;

namespace GTACoOp
{
    [ProtoContract]
    public class KeySendData
    {
        [ProtoMember(1)]
        public Keys key;
    }
}
