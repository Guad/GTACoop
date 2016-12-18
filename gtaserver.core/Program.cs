using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Xml.Serialization;
using Microsoft.Extensions.Logging;
using GTAServer.PluginAPI;

namespace GTAServer
{
    public class Program
    {
        private static ServerConfiguration _gameServerConfiguration;
        private static ILogger _logger;
        private static IEnumerable<IPlugin> Plugins;
        private static void CreateNeededFiles()
        {
            if (!Directory.Exists("Plugins")) Directory.CreateDirectory("Plugins");
        }

        private static void DoDebugWarning()
        {
#if DEBUG
            _logger.LogWarning("Note - This build is a debug build. Please do not share this build and report any issues to Mitchell Monahan (@wolfmitchell)");
            _logger.LogWarning("Furthermore, debug builds will not announce themselves to the master server, regardless of the AnnounceSelf config option.");
            _logger.LogWarning("To help bring crashes to the attention of the server owner and make sure they are reported to me, error catching has been disabled in this build.");
#endif
        }
        public static void Main(string[] args)
        {
            CreateNeededFiles();
            Util.LoggerFactory = new LoggerFactory()
#if DEBUG
                .AddConsole(LogLevel.Trace)
                .AddDebug(); // this adds stuff to VS debug console
#else
                .AddConsole();
#endif

            _logger = Util.LoggerFactory.CreateLogger<Program>();
            DoDebugWarning();

            _logger.LogInformation("Reading server configuration...");
            _gameServerConfiguration = LoadServerConfiguration("serverSettings.xml");
            _logger.LogInformation("Configuration loaded...");

            _logger.LogInformation("Server preparing to start...");

            var gameServer = new GameServer(_gameServerConfiguration.Port, _gameServerConfiguration.ServerName,
                _gameServerConfiguration.GamemodeName)
            {
                Password = _gameServerConfiguration.Password,
                MasterServer = _gameServerConfiguration.PrimaryMasterServer,
                AnnounceSelf = _gameServerConfiguration.AnnounceSelf,
                AllowNicknames = _gameServerConfiguration.AllowNicknames,
                AllowOutdatedClients = _gameServerConfiguration.AllowOutdatedClients,
            };


            // Plugin Code
            _logger.LogInformation("loading test plugin");
            Plugins = PluginLoader.LoadPlugin("TestPlugin");
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
#if DEBUG
                gameServer.Tick();
#else
                try
                {
                    gameServer.Tick();
                }
                catch (Exception e)
                {
                    _logger.LogError("Exception while ticking", e);
                }
#endif
                Thread.Sleep(1);
            }
        }

        public static ServerConfiguration LoadServerConfiguration(string path)
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
                _logger.LogInformation("No config found, creating a new one");
                using (var stream = File.OpenWrite(path)) ser.Serialize(stream, cfg = new ServerConfiguration());
            }
            return cfg;
        }
    }
}
