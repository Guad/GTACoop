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

            ServerInstance.Start(settings.Filterscripts);

            Console.WriteLine("Started! Waiting for connections.");

            while (true)
            {
                ServerInstance.Tick();
            }
        }

        static ServerSettings ReadSettings(string path)
        {
            if (File.Exists(path))
            {
                using (var stream = File.OpenRead(path))
                {
                    var ser = new XmlSerializer(typeof(ServerSettings));
                    var settings = (ServerSettings)ser.Deserialize(stream);
                    return settings;
                }
            }
            else
            {
                var settings = new ServerSettings();
                settings.Port = 4499;
                settings.MaxPlayers = 16;
                settings.Name = "Simple GTA Server";
                settings.Password = "changeme";
                settings.PasswordProtected = false;
                settings.Gamemode = "freeroam";
                settings.Announce = true;
                settings.MasterServer = "http://46.101.1.92/";
                settings.Filterscripts = new string[] { "" };

                var ser = new XmlSerializer(typeof(ServerSettings));
                using (var stream = File.OpenWrite(path))
                {
                    ser.Serialize(stream, settings);
                }
                return settings;
            }
        }
    }
}
