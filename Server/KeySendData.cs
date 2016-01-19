using System.Windows.Forms;
using ProtoBuf;

namespace GTAServer
{
    [ProtoContract]
    public class KeySendData
    {
        [ProtoMember(1)]
        public Keys key;
    }
}
