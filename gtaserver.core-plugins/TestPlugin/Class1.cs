using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GTAServer;
using GTAServer.PluginAPI;

namespace TestPlugin
{
    public class TestPlugin : IPlugin
    {
        public string Name => "Test Plugin";

        public string Description
            => "This plugin is used to test the plugin-loading capabilities of the new server. Does nothing.";

        public string Author => "Mitchell Monahan (wolfmitchell)";

        public bool OnEnable(GameServer gameServer, bool isAfterServerLoad)
        {
            return true; // successful load
        }
    }
}
