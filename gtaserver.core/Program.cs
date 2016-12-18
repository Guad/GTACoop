using System;
using System.Threading;
using System.IO;
using System.Xml.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace GTAServer
{
    public class Program
    {
        public static ServerConfiguration GameServerConfiguration;
        private static ILogger _logger;

        public static void Main(string[] args)
        {
            Util.LoggerFactory = new LoggerFactory()
#if DEBUG
                .AddConsole(LogLevel.Trace)
                .AddDebug(); // this adds stuff to VS debug console
#else
                .AddConsole();
#endif

            _logger = Util.LoggerFactory.CreateLogger<Program>();
#if DEBUG
            _logger.LogWarning("Note - This build is a debug build. Please do not share this build and report any issues to Mitchell Monahan (@wolfmitchell)");
            _logger.LogWarning("Furthermore, debug builds will not announce themselves to the master server, regardless of the AnnounceSelf config option.");
            _logger.LogWarning("To help bring crashes to the attention of the server owner and make sure they are reported to me, error catching has been disabled in this build.");
#endif
            _logger.LogInformation("Reading server configuration...");
            GameServerConfiguration = LoadServerConfiguration("serverSettings.xml");
            _logger.LogInformation("Configuration loaded...");

            _logger.LogInformation("Server preparing to start...");

            var gameServer = new GameServer(GameServerConfiguration.Port, GameServerConfiguration.ServerName,
                GameServerConfiguration.GamemodeName)
            {
                Password = GameServerConfiguration.Password,
                MasterServer = GameServerConfiguration.PrimaryMasterServer,
                AnnounceSelf = GameServerConfiguration.AnnounceSelf,
                AllowNicknames = GameServerConfiguration.AllowNicknames,
                AllowOutdatedClients = GameServerConfiguration.AllowOutdatedClients,
            };

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
                    logger.LogError("Exception while ticking", e);
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
