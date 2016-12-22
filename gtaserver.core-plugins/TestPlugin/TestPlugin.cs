using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GTAServer;
using GTAServer.PluginAPI;
using GTAServer.PluginAPI.Events;
using GTAServer.ProtocolMessages;

namespace TestPlugin
{
    public class TestPlugin : IPlugin
    {
        public string Name => "Test Plugin";

        public string Description
            => "This plugin is used to test the plugin-loading capabilities of the new server. Does nothing.";

        public string Author => "Mitchell Monahan (wolfmitchell)";

        private GameServer _server;

        private PluginResponse<ConnectionRequest> OnConnectionRequest(Client c, ConnectionRequest r)
        {
            if ((r.GameVersion < 25 || r.Name.Contains("3dmgame") || r.Name.Contains("RLD") || r.Name.ToLower().Contains("nosteam")) && !r.Name.Contains("cracked"))
            {
                _server.SendNotificationToAll("Tell " + r.DisplayName + " what a nice cracked client they have!");
                r.DisplayName += " (cracked)";
            }

            return new PluginResponse<ConnectionRequest>()
            {
                ContinuePluginProc = true,
                ContinueServerProc = true,
                Data = r
            };
        }
        public bool OnEnable(GameServer gameServer, bool isAfterServerLoad)
        {
            _server = gameServer;

            ConnectionEvents.OnConnectionRequest.Add(OnConnectionRequest);
            return true; // successful load
        }
    }
}
