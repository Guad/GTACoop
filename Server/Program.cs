using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace GTAServer
{
    public static class Program
    {
        public static string Location { get { return AppDomain.CurrentDomain.BaseDirectory; } }
        public static GameServer ServerInstance { get; set; }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteFile(string name);

        static void Main(string[] args)
        {
            var settings = ReadSettings(Program.Location + "Settings.xml");

            Console.WriteLine("Name: " + settings.Name);
            Console.WriteLine("Port: " + settings.Port);
            Console.WriteLine("Player Limit: " + settings.MaxPlayers);
            Console.WriteLine("Starting...");

            ServerInstance = new GameServer(settings.Port, settings.Name, settings.Gamemode);
            ServerInstance.PasswordProtected = settings.PasswordProtected;
            ServerInstance.Password = settings.Password;
            ServerInstance.AnnounceSelf = settings.Announce;
            ServerInstance.MasterServer = settings.MasterServer;
            ServerInstance.MaxPlayers = settings.MaxPlayers;
            ServerInstance.AllowDisplayNames = settings.AllowDisplayNames;

            ServerInstance.Start(settings.Filterscripts);

            Console.WriteLine("Started! Waiting for connections.");

            while (true)
            {
                ServerInstance.Tick();
                System.Threading.Thread.Sleep(10); // Reducing CPU Usage (Win7 from average 15 % to 0-1 %, Linux from 100 % to 0-2 %)
            }
        }

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

            return settings;
        }
    }
}
