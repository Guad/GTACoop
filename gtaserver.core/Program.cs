using System;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace GTAServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Util.LoggerFactory = new LoggerFactory()
#if DEBUG
                .AddConsole(LogLevel.Trace)
#else
                .AddConsole()
#endif
                .AddDebug();
            var logger = Util.LoggerFactory.CreateLogger<Program>();
#if DEBUG
            logger.LogWarning("Note - This build is a debug build. Please do not share this build and report any issues to Mitchell Monahan (@wolfmitchell)");
            logger.LogWarning("Furthermore, debug builds will not announce themselves to the master server, regardless of the AnnounceSelf config option.");
            logger.LogWarning("To help bring crashes to the attention of the server owner and make sure they are reported to me, error catching has been disabled in this build.");
#endif
            logger.LogInformation("Server preparing to start...");

            var gameServer = new GameServer(4699, "GTAServer .NET Core Test", "freeroam");

            logger.LogInformation("Server starting...");
            gameServer.Start();

            logger.LogInformation("Starting server main loop, ready to accept connections.");
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
    }
}
