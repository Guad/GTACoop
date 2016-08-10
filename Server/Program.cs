using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Remoting;
using System.Security.Policy;
using System.Threading;
using System.Xml.Serialization;
using log4net;
using log4net.Config;

namespace GTAServer 
{
    public static class Program
    {
        /// <summary>
        /// Location of the virtual server host
        /// </summary>
        public static string ServerHostLocation => AppDomain.CurrentDomain.BaseDirectory;
        /// <summary>
        /// Dictionary of all the virtual servers currently running
        /// </summary>
        public static Dictionary<string, AppDomain> VirtualServerDomains = new Dictionary<string, AppDomain>();
        /// <summary>
        /// Dictionary of all the virtual server handles.
        /// </summary>
        public static Dictionary<string, ObjectHandle> VirtualServerHandles = new Dictionary<string, ObjectHandle>();
        /// <summary>
        /// Dictionary of all the virtual servers
        /// </summary>
        public static Dictionary<string, GameServer> VirtualServers = new Dictionary<string, GameServer>();
        /// <summary>
        /// Server debug mode
        /// </summary>
        public static bool Debug = false;
        /// <summary>
        /// If the server should allow the config option to allow old clients
        /// </summary>
        public static bool AllowOutdatedClients = false;

        /// <summary>
        /// Delete a file
        /// </summary>
        /// <param name="name">File to delete</param>
        /// <returns>If the file was deleted</returns>
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteFile(string name);

        public static readonly ILog log = LogManager.GetLogger("ServerManager");
        /// <summary>
        /// Read server settings from XML
        /// </summary>
        /// <param name="path">Server settings path</param>
        /// <returns>ServerSettings instance</returns>
        static ServerSettings ReadSettings(string path)
        {
            var ser = new XmlSerializer(typeof(ServerSettings));

            ServerSettings settings = null;

            if (File.Exists(path))
            {
                using (var stream = File.OpenRead(path)) settings = (ServerSettings)ser.Deserialize(stream);

                using (var stream = new FileStream(path, File.Exists(path) ? FileMode.Truncate : FileMode.Create, FileAccess.ReadWrite)) ser.Serialize(stream, settings);
            }
            else
            {
                using (var stream = File.OpenWrite(path)) ser.Serialize(stream, settings = new ServerSettings());
            }
            //LogToConsole(1, false, "FILE", "Settings loaded from " + path);
            return settings;
        }

        public static ServerSettings GlobalSettings;
        /// <summary>
        /// Master server list
        /// </summary>
        public class MasterServerList
        {
            public List<string> List { get; set; }
        }

        public static void Main(string[] args)
        {
            XmlConfigurator.Configure(new System.IO.FileInfo("logging.xml"));
            log.Debug("Loading settings.xml...");
            GlobalSettings = ReadSettings(Program.ServerHostLocation + ((args.Length > 0) ? args[0] : "Settings.xml"));
            StartServer(GlobalSettings);
        }

        public static void StartServer(ServerSettings settings)
        {
            log.Info("Creating new server instance: ");
            log.Info("  - Handle: " + settings.Handle);
            log.Info("  - Name: " + settings.Name);
            log.Info("  - Player Limit: " + settings.MaxPlayers);
            if (settings.AllowOutdatedClients && !AllowOutdatedClients)
            {
                log.Warn("Server config for " + settings.Handle + " is set to allow outdated clients, yet it has been disabled on the master server.");
                settings.AllowOutdatedClients = false;
            }
            VirtualServerDomains[settings.Handle]=AppDomain.CreateDomain(settings.Handle);
            var domain = VirtualServerDomains[settings.Handle];
            
            VirtualServerHandles[settings.Handle] = domain.CreateInstanceFrom(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath,
                "GTAServer.GameServer");
            var handle = VirtualServerHandles[settings.Handle];
            VirtualServers[settings.Handle] = (GameServer)handle.Unwrap();
            var curServer = VirtualServers[settings.Handle];
            curServer.Name = settings.Name;
            curServer.MaxPlayers = settings.MaxPlayers;
            curServer.Port = settings.Port;
            curServer.PasswordProtected = settings.PasswordProtected;
            curServer.Password = settings.Password;
            curServer.AnnounceSelf = settings.Announce;
            curServer.MasterServer = settings.MasterServer;
            curServer.AllowNickNames = settings.AllowDisplayNames;
            curServer.AllowOutdatedClients = settings.AllowOutdatedClients;
            curServer.GamemodeName = settings.Gamemode;
            curServer.ConfigureServer();
            log.Debug("Finished configuring server: " + settings.Handle + ", starting.");
            curServer.StartInThread();
        }
    }
}