using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;
using System.Runtime.CompilerServices;

namespace GTAServer
{
    [ProtoContract]
    public class RemoteMenuData
    {
       [ProtoMember(1)]
       public  _UIMenu _uiMenu;
       [ProtoMember(2)]
       public  List<_UIMenuItem> _uiMenuItem;
        [ProtoMember(3)]
        public List<_UIMenuCheckboxItem> _uiMenuCheckboxItem;
        [ProtoMember(4)]
        public List<_UIMenuListItem> _uiMenuListItem;

    }
    [ProtoContract]
    public class _UIMenu
    {
        [ProtoMember(1)]
        public string title;
        [ProtoMember(2)]
        public string subtitle;
    }
    [ProtoContract]
    public class _UIMenuItem
    {
        [ProtoMember(1)]
        public string name;
        [ProtoMember(2)]
        public string description;
    }
    [ProtoContract]
    public class _UIMenuCheckboxItem
    {
        [ProtoMember(1)]
        public string name;
        [ProtoMember(2)]
        public string description;
        [ProtoMember(3)]
        public bool check;
    }
    [ProtoContract]
    public class _UIMenuListItem
    {
        [ProtoMember(1)]
        public string name;
        [ProtoMember(2)]
        public string description;
        [ProtoMember(3)]
        public List<string> list;
        [ProtoMember(4)]
        public int index;
    }
}
