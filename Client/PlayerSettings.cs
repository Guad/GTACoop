using System.Windows.Forms;

namespace GTACoOp
{
    public class PlayerSettings
    {
        public string Name { get; set; }
        public int MaxStreamedNpcs { get; set; }
        public string MasterServerAddress { get; set; }
        public Keys ActivationKey { get; set; }
    }
}