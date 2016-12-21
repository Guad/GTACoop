using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace GTAServer.PluginAPI
{
    interface IGamemode : IPlugin
    {
        string GamemodeName { get; }
    }
}
