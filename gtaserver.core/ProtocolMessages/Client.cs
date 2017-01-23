using Lidgren.Network;

namespace GTAServer.ProtocolMessages
{

    public class Client
    {
        public NetConnection NetConnection { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public float Latency { get; set; }
        public ScriptVersion RemoteScriptVersion { get; set; }
        public int GameVersion { get; set; }
        public Vector3 LastKnownPosition { get; set; }
        public int Health { get; set; }
        public int VehicleHealth { get; set; }
        public bool IsInVehicle { get; internal set; }
        public bool IsAfk { get; set; }
        public bool Kicked { get; set; }
        public string KickReason { get; set; }
        public Client KickedBy { get; set; }
        public bool Silent { get; set; }

        public Client(NetConnection nc)
        {
            NetConnection = nc;
        }

        public void ApplyConnectionRequest(ConnectionRequest cr)
        {
            Name = cr.Name;
            DisplayName = cr.DisplayName;
            RemoteScriptVersion = (ScriptVersion)cr.ScriptVersion;
            GameVersion = cr.GameVersion;
        }
    }
}
