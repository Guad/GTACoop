using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Xml.Serialization;
using Microsoft.Extensions.Logging;
using GTAServer.PluginAPI;
using ProtoBuf.Meta;
using SimpleConsoleLogger;

namespace GTAServer
{
    public class ServerManager
    {
        private static ServerConfiguration _gameServerConfiguration;
        private static ILogger _logger;
        private static readonly List<IPlugin> Plugins=new List<IPlugin>();
        private static readonly string Location = System.AppContext.BaseDirectory;
        private static bool _debugMode = false;

        private static void CreateNeededFiles()
        {
            if (!Directory.Exists(Location + Path.DirectorySeparatorChar + "Plugins")) Directory.CreateDirectory(Location + Path.DirectorySeparatorChar + "Plugins");
            if (!Directory.Exists(Location + Path.DirectorySeparatorChar + "Configuration")) Directory.CreateDirectory(Location + Path.DirectorySeparatorChar + "Configuration");
        }

        private static void DoDebugWarning()
        {
            if (!_debugMode) return;
            _logger.LogWarning("Note - This build is a debug build. Please do not share this build and report any issues to Mitchell Monahan (@wolfmitchell)");
            _logger.LogWarning("Furthermore, debug builds will not announce themselves to the master server, regardless of the AnnounceSelf config option.");
            _logger.LogWarning("To help bring crashes to the attention of the server owner and make sure they are reported to me, error catching has been disabled in this build.");
        }
        public static void Main(string[] args)
        {
#if DEBUG
            _debugMode = true;
#endif
            CreateNeededFiles();

            // can't use logger here since the logger config depends on if debug mode is on or off
            Console.WriteLine("Reading server configuration...");
            _gameServerConfiguration = LoadServerConfiguration("Configuration" + Path.DirectorySeparatorChar + "serverSettings.xml");
            if (!_debugMode) _debugMode = _gameServerConfiguration.DebugMode;

            if (_debugMode)
            {

                Util.LoggerFactory = new LoggerFactory()
                    .AddSimpleConsole()
                    .AddDebug();
            }
            else
            {
                Util.LoggerFactory = new LoggerFactory()
                    .AddSimpleConsole((s, l) => (int) l >= (int) LogLevel.Information);
            }
            _logger = Util.LoggerFactory.CreateLogger<ServerManager>();
            DoDebugWarning();

            
            _logger.LogInformation("Server preparing to start...");

            var gameServer = new GameServer(_gameServerConfiguration.Port, _gameServerConfiguration.ServerName,
                _gameServerConfiguration.GamemodeName, _debugMode)
            {
                Password = _gameServerConfiguration.Password,
                MasterServer = _gameServerConfiguration.PrimaryMasterServer,
                BackupMasterServer = _gameServerConfiguration.BackupMasterServer,
                AnnounceSelf = _gameServerConfiguration.AnnounceSelf,
                AllowNicknames = _gameServerConfiguration.AllowNicknames,
                AllowOutdatedClients = _gameServerConfiguration.AllowOutdatedClients,
            };


            // Plugin Code
            _logger.LogInformation("Loading plugins");
            //Plugins = PluginLoader.LoadPlugin("TestPlugin");
            foreach (var pluginName in _gameServerConfiguration.ServerPlugins)
            {
                foreach (var loadedPlugin in PluginLoader.LoadPlugin(pluginName))
                {
                    Plugins.Add(loadedPlugin);
                }
            }

            _logger.LogInformation("Plugins loaded. Enabling plugins...");
            foreach (var plugin in Plugins)
            {
                if (!plugin.OnEnable(gameServer, false))
                {
                    _logger.LogWarning("Plugin " + plugin.Name + " returned false when enabling, marking as disabled, although it may still have hooks registered and called.");
                }
            }

            _logger.LogInformation("Server starting...");
            gameServer.Start();

            _logger.LogInformation("Starting server main loop, ready to accept connections.");
            while (true)
            {
                if (_debugMode)
                {
                    gameServer.Tick();
                }
                else
                {
                    try
                    {
                        gameServer.Tick();
                    }
                    catch (Exception e)
                    {
                        _logger.LogError("Exception while ticking", e);
                    }
                }
                Thread.Sleep(1);
            }
        }

        private static ServerConfiguration LoadServerConfiguration(string path)
        {
            var ser = new XmlSerializer(typeof(ServerConfiguration));

            ServerConfiguration cfg = null;
            if (File.Exists(path))
            {
                using (var stream = File.OpenRead(path)) cfg = (ServerConfiguration)ser.Deserialize(stream);
                using (
                    var stream = new FileStream(path, File.Exists(path) ? FileMode.Truncate : FileMode.Create,
                        FileAccess.ReadWrite)) ser.Serialize(stream, cfg);
            }
            else
            {
                Console.WriteLine("No configuration found, creating a new one.");
                using (var stream = File.OpenWrite(path)) ser.Serialize(stream, cfg = new ServerConfiguration());
            }
            return cfg;
        }
    }
}
