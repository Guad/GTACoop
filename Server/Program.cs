using System;
using System.Net;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Newtonsoft.Json;

namespace GTAServer
{
    public static class Program
    {
        public static string Location { get { return AppDomain.CurrentDomain.BaseDirectory; } }
        public static GameServer ServerInstance { get; set; }
        public static string WANIP { get; private set; }
        public static string LANIP { get; private set; }
        public static bool Debug { get; internal set; }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteFile(string name);
        public class MasterServerList
        {
            public List<string> list { get; set; }
        }
        static void Main(string[] args)
        {
            try {
                //Console.WriteLine("Break");
                /*System.Diagnostics.Process cmd = new System.Diagnostics.Process();
                cmd.StartInfo.FileName = "cmd.exe";
                cmd.StartInfo.RedirectStandardInput = true;
                cmd.StartInfo.RedirectStandardOutput = true;
                cmd.StartInfo.CreateNoWindow = true;
                cmd.StartInfo.UseShellExecute = false;
                cmd.Start();
                cmd.StandardInput.WriteLine("chcp 65001");
                cmd.StandardInput.Flush();
                cmd.StandardInput.Close();
                Console.WriteLine(cmd.StandardOutput.ReadToEnd());*/
                ServerSettings settings;
                try
                {
                    if (args.Length > 0)
                    {
                        settings = ReadSettings(Program.Location + args[0]);
                    }
                    else
                    {
                        settings = ReadSettings(Program.Location + "Settings.xml");
                    }
                }
                catch (Exception) { settings = ReadSettings(Program.Location + "Settings.xml"); }
                try { Console.Write("IPs: "); } catch (Exception) { }
                try
                {
                    ServerInstance.WanIP = "";ServerInstance.LanIP = "";
                }
                catch (Exception) { }
                try
            {
                string url = "http://checkip.dyndns.org/"; //http://ip-api.com/json
                WebRequest req = WebRequest.Create(url);
                req.Timeout = 2500;
                WebResponse resp = req.GetResponse();
                StreamReader sr = new StreamReader(resp.GetResponseStream());
                string res = sr.ReadToEnd().Trim();
                string[] a = res.Split(':');
                string a2 = a[1].Substring(1);
                string[] a3 = a2.Split('<');
                ServerInstance.WanIP = a3[0];
                Console.Write(ServerInstance.WanIP + "/");
            } catch{}
            try {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        ServerInstance.LanIP = ip.ToString();
                        Console.Write(ServerInstance.LanIP + "/");break;
                    }
                }
            } catch { }
            Console.WriteLine("127.0.0.1:" + settings.Port);
            try {
                    if (!string.IsNullOrWhiteSpace(ServerInstance.WanIP)) {
                        var path = Program.Location + "geoip.mmdb";
                        try
                        {
                            using (var reader = new MaxMind.GeoIP2.DatabaseReader(path))
                            {
                                ServerInstance.geoIP = reader.Country(ServerInstance.WanIP);
                            }
                        }
                        catch (Exception ex) { Console.WriteLine("Can't set GeoIP: " + ex.Message); }
                    }
                } catch (Exception ){ }
            Console.WriteLine("Name: " + settings.Name);
            Console.WriteLine("Player Limit: " + settings.MaxPlayers);
            Console.WriteLine("Starting...");
            ServerInstance = new GameServer(settings.Port, settings.Name, settings.Gamemode);
            ServerInstance.PasswordProtected = settings.PasswordProtected;
            ServerInstance.Password = settings.Password;
            ServerInstance.AnnounceSelf = settings.Announce;
            ServerInstance.MasterServer = settings.MasterServer;
            ServerInstance.MaxPlayers = settings.MaxPlayers;
            ServerInstance.AllowDisplayNames = settings.AllowDisplayNames;
            ServerInstance.AllowOutdatedClients = settings.AllowOutdatedClients;

            ServerInstance.Start(settings.Filterscripts);

            string response = String.Empty;
            try
            {
                using (var webClient = new WebClient())
                {
                    response = webClient.DownloadString("http://46.101.1.92/");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Could not contact master server: "+ex.Message); return;
            }
            if (string.IsNullOrWhiteSpace(response)) { return; }
            var dejson = JsonConvert.DeserializeObject<MasterServerList>(response) as MasterServerList;
            if (dejson == null) return;
            Console.WriteLine("Servers returned by master server: " + dejson.list.Count().ToString());
            if (!ServerInstance.WanIP.Equals("")) {
                foreach (var server in dejson.list) {
                    var split = server.Split(':');
                    if (split.Length != 2) continue;
                    int port;
                    if (!int.TryParse(split[1], out port)) { 
                        if (split[0].Equals(ServerInstance.WanIP) && port.Equals(settings.Port)) {
                                LogToConsole(2, false, null, "We found our server on the serverlist :)"); break;
                            }
                            else if (split[0].Equals(ServerInstance.WanIP)) {
                                LogToConsole(4, false, null, "We found our server IP on the serverlist, but without the right port :|"); break;
                            }
                            else {
                                LogToConsole(5, false, null, "We can't find our server on the serverlist :("); break;
                            }
                    }
                        //Console.Write(split[0] + ":" + port + ", ");
                }
            }
            }
            catch (Exception ex){ Console.WriteLine("Can't start server: "+ex.Message); }


            Console.WriteLine("Started! Waiting for connections.");

            while (true)
            {
                ServerInstance.Tick();
                System.Threading.Thread.Sleep(10); // Reducing CPU Usage (Win7 from average 15 % to 0-1 %, Linux from 100 % to 0-2 %)
            }
        }
        static void LogToConsole(int flag, bool debug, string module, string message)
        {
            if (module == null || module.Equals("")) { module = "SERVER"; }
            if (flag == 1){
                Console.ForegroundColor = ConsoleColor.Cyan;Console.WriteLine("[" + DateTime.Now + "] (DEBUG) " + module.ToUpper() + ": " + message);
            }else if (flag == 2){
                Console.ForegroundColor = ConsoleColor.Green; Console.WriteLine("[" + DateTime.Now + "] (SUCCESS) " + module.ToUpper() + ": " + message);
            }else if (flag == 3){
                Console.ForegroundColor = ConsoleColor.DarkYellow; Console.WriteLine("[" + DateTime.Now + "] (WARNING) " + module.ToUpper() + ": " + message);
            }else if (flag == 4){
                Console.ForegroundColor = ConsoleColor.Red; Console.WriteLine("[" + DateTime.Now + "] (ERROR) " + module.ToUpper() + ": " + message);
            }else{
                Console.WriteLine("[" + DateTime.Now + "] " + module.ToUpper() + ": " + message);
            }
            Console.ForegroundColor = ConsoleColor.White;
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
            LogToConsole(1, false, "FILE", "Settings loaded from " + path);
            return settings;
        }
    }
}
