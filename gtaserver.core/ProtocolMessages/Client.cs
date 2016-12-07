using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GTAServer.ProtocolMessages;
using Lidgren.Network;

namespace GTAServer.ProtocolMessages
{

    public class Client
    {
        private NetConnection NetConnection { get; set; }
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
        public bool Muted { get; set; }

        public Client(NetConnection nc)
        {
            NetConnection = nc;
        }
    }
}
