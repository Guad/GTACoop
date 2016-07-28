using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
//using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using GTAServer;
//using Lidgren.Network;
using Console = Colorful.Console;
using System.Drawing;
using MaxMind.GeoIP2;

namespace AdminTools
{
    public class Lists
    {
        internal static UserList _accounts = new UserList();
        internal static Banlist _banned = new Banlist();
        internal static List<long> _authenticatedUsers = new List<long>();
    }
    [Serializable]
    public class TestServerScript : ServerScript
    {
        static void LogToConsole(int flag, bool debug, string module,string message)
        {
            if (module == null || module == "") { module = "AdminTools"; }
            if(flag == 1)
            {
                Console.WriteLine("[" + DateTime.Now + "] (DEBUG) " + module.ToUpper() + ": " + message, Color.Cyan);
            }
            else if (flag == 2)
            {
                Console.WriteLine("[" + DateTime.Now + "] (SUCCESS) " + module.ToUpper() + ": " + message, Color.Green);
            } else if(flag == 3) {
                Console.WriteLine("[" + DateTime.Now + "] (WARNING) " + module.ToUpper() + ": " + message, Color.Orange);
            } else if(flag == 4) {
                Console.WriteLine("[" + DateTime.Now + "] (ERROR) " + module.ToUpper() + ": " + message, Color.Red);
            }else {
                Console.WriteLine("[" + DateTime.Now + "] " + module.ToUpper() + ": " + message);
            }
        }
        public static string Location { get { return AppDomain.CurrentDomain.BaseDirectory; } }
        public override string Name { get { return "Server Administration Tools"; } }
        public int Version { get { return 1;  } }

        public bool afk { get; private set; }
        public AdminSettings Settings = ReadSettings(Program.Location + "/filterscripts/AdminTools.xml");
        public int ServerWeather;
        public TimeSpan ServerTime;

        private DateTime _lastCountdown;

        private string[] _weatherNames = new[]
        {
            "CLEAR",
            "EXTRASUNNY",
            "CLOUDS",
            "OVERCAST",
            "RAIN",
            "CLEARING",
            "THUNDER",
            "SMOG",
            "FOGGY",
            "XMAS",
            "SNOWLIGHT",
            "BLIZZARD",
        };

        public override void Start()
        {
            //Console.WriteLine("\x1b[31mRed\x1b[0;37m"); //https://en.wikipedia.org/wiki/ANSI_escape_code
            LoadAccounts(Location + "Accounts.xml");
            LogToConsole(2, false, null, "Accounts loaded.");
            LoadBanlist(Location + "Banlist.xml");
            LogToConsole(2, false, null, "Bans loaded.");
            //Settings.MaxPing
            //Settings.MaxPing

            ServerWeather = 0;
            ServerTime = new TimeSpan(12, 0, 0);
        }
        public class MasterServerList
        {
            public List<string> list { get; set; }
        }
        public int readableVersion(ScriptVersion version)
        {
            string Version = version.ToString();
            Version = Regex.Replace(Version, "VERSION_", "", RegexOptions.IgnoreCase);
            Version = Regex.Replace(Version, "_", "", RegexOptions.IgnoreCase);
            return Int32.Parse(Version);
        }
        public override void OnTick()
        {
            if((DateTime.Now.ToString("ss:fff").Equals("20:001")) || (DateTime.Now.ToString("ss:fff").Equals("40:001"))) {
                if (Settings.MaxPing > 0 && Program.ServerInstance.Clients.Count > 0)
                {
                    for (var i = 0; i < Program.ServerInstance.Clients.Count; i++) {
                        Console.WriteLine(string.Format("{0} Current Ping: \"{1}\" / Max Ping: \"{2}\"",Program.ServerInstance.Clients[i].DisplayName, Math.Round(Program.ServerInstance.Clients[i].Latency * 1000, MidpointRounding.AwayFromZero).ToString(), Settings.MaxPing));
                        if (Math.Round(Program.ServerInstance.Clients[i].Latency * 1000, MidpointRounding.AwayFromZero) > Settings.MaxPing)
                        {
                            Program.ServerInstance.SendChatMessageToAll("SERVER", string.Format("Kicking {0} for Ping {1} too high! Max: {2}", Program.ServerInstance.Clients[i].DisplayName.ToString(), (Program.ServerInstance.Clients[i].Latency * 1000).ToString(), Settings.MaxPing.ToString()));
                            Program.ServerInstance.KickPlayer(Program.ServerInstance.Clients[i], string.Format("Ping {0} too high! Max: {1}", (Program.ServerInstance.Clients[i].Latency * 1000).ToString(), Settings.MaxPing.ToString()));
                        }
                    }

                }
            }
        }
        public override void OnIncomingConnection(Client player)
        {
            if (!Settings.ColoredNicknames)
            {
                player.DisplayName = Regex.Replace(player.DisplayName, "~.~", "", RegexOptions.IgnoreCase);
            }
            if (player.DisplayName.ToLower().Contains("server") || player.DisplayName.ToLower().Contains("owner") || player.DisplayName.ToLower().Contains("admin") || player.DisplayName.ToLower().Contains("moderator") || player.DisplayName.ToLower().Contains("vip") || player.DisplayName.ToLower().Contains("user") || player.DisplayName.ToLower().Contains("guest"))
            {
                Program.ServerInstance.SendChatMessageToAll("SERVER", string.Format("Kicking {0} for impersonating.", player.DisplayName.ToString()));
                Program.ServerInstance.DenyPlayer(player, "Change your nickname to a proper one!", true, null, 30); return;
            }
            if (Settings.KickOnDefaultNickName)
            {
                if (player.DisplayName.StartsWith("RLD!") || player.DisplayName.StartsWith("Player") || player.DisplayName.StartsWith("nosTEAM") || player.DisplayName.ToString() == "3dmgame1")
                {
                    //Program.ServerInstance.SendChatMessageToAll("SERVER", string.Format("Kicking {0} for default nickname.", player.DisplayName.ToString()));
                    Program.ServerInstance.DenyPlayer(player, "~r~Change your nickname!~w~ (~h~F9~h~->~h~Settings~h~->~h~Nickname~h~)", true, null, 10); return;
                }
            }
            if (Settings.KickOnDifferentScript == true)
            {
                //Console.WriteLine(string.Format("[Script Version Check] Got: {0} | Expected: {1}", player.RemoteScriptVersion.ToString(), Settings.NeededScriptVersion.ToString()));
                //Console.WriteLine((ScriptVersion)player.RemoteScriptVersion);
                //Console.WriteLine((ScriptVersion)Settings.NeededScriptVersion);
                //if (readableVersion(player.RemoteScriptVersion) < Int32.Parse(Settings.MinScriptVersion))
                if ((ScriptVersion)player.RemoteScriptVersion != (ScriptVersion)Settings.NeededScriptVersion)
                {
                    //Program.ServerInstance.SendChatMessageToAll("SERVER", string.Format("Kicking {0} for outdated mod.", player.DisplayName.ToString()));
                    string version = Regex.Replace(Settings.NeededScriptVersion.ToString(), "VERSION_", "", RegexOptions.IgnoreCase);
                    version = Regex.Replace(version, "_", ".", RegexOptions.IgnoreCase);
                    Program.ServerInstance.DenyPlayer(player, string.Format("You need GTACoop Mod v~g~{0}~w~ to play on this server.", version), true, null, 120); return;
                }
            }
            if (Settings.KickOnOutdatedGame)
            {
                //Console.WriteLine(string.Format("[Game Version Check] Got: {0} | Expected: {1}", player.GameVersion.ToString(), Settings.MinGameVersion.ToString()));
                if (player.GameVersion < Settings.MinGameVersion)
                {
                    Program.ServerInstance.SendChatMessageToAll("SERVER", string.Format("Kicking {0} for outdated game.", player.DisplayName.ToString()));
                    Program.ServerInstance.DenyPlayer(player, "Update your GTA V to the newest version!", false, null, 120); return;
                }
            }
            if (Settings.SocialClubOnly)
            {
                // Console.WriteLine(player.Name);
                if (player.Name.ToString() == "RLD!" || player.Name.ToString() == "nosTEAM" || player.Name.ToString() == "Player" || player.Name.ToString() == "3dmgame1")
                {
                    Program.ServerInstance.SendChatMessageToAll("SERVER", string.Format("Kicking {0} for cracked game.", player.DisplayName.ToString()));
                    Program.ServerInstance.DenyPlayer(player, "Buy the game and sign in through social club!"); return;
                }
            }
            if (Settings.KickOnNameDifference)
            {
                if (!player.DisplayName.Equals(player.Name))
                {
                    //Program.ServerInstance.SendChatMessageToAll("SERVER", string.Format("Kicking {0} for nickname differs from account name.", player.DisplayName.ToString()));
                    Program.ServerInstance.DenyPlayer(player, string.Format("Change your nickname to {0}", player.Name), true, null, 15); return;
                }
            }
            if (player.IsBanned() || player.IsIPBanned())
            {
                Program.ServerInstance.SendChatMessageToAll("SERVER", string.Format("{0} is banned for {1}", player.DisplayName.ToString(), player.GetBan().Reason));
                Program.ServerInstance.DenyPlayer(player, "You are banned: " + player.GetBan().Reason, false, null, 120); return;
            }
            if (Settings.OnlyAsciiNickName)
            {
                if (Encoding.UTF8.GetByteCount(player.DisplayName) != player.DisplayName.Length)
                {
                    //Program.ServerInstance.SendChatMessageToAll("SERVER", string.Format("{0} was kicked for non-ascii chars in his nickname.", player.DisplayName.ToString()));
                    Program.ServerInstance.DenyPlayer(player, "Remove all non-ascii characters from your nickname.", true, null, 15); return;
                }
            }
            if (Settings.OnlyAsciiUserName)
            {
                if (Encoding.UTF8.GetByteCount(player.Name) != player.Name.Length)
                {
                    //Program.ServerInstance.SendChatMessageToAll("SERVER", string.Format("{0} was kicked for non-ascii chars in his username.", player.DisplayName.ToString()));
                    Program.ServerInstance.DenyPlayer(player, "Remove all non-ascii characters from your Social Club username.", true, null, 120); return;
                }
            }
            if (Settings.LimitNickNames)
            {
                if (player.DisplayName.Length < 3 || player.DisplayName.Length > 100 || Regex.Replace(player.DisplayName, "~.~", "", RegexOptions.IgnoreCase).Length > 20)
                {
                    Program.ServerInstance.DenyPlayer(player, "Your nickname has to be between 3 and 20 chars long. (Max 100 with colors)", true, null, 20); return;
                }
            }
            if (Settings.AntiClones)
            {
                int count = 0;
                for (var i = 0; i < Program.ServerInstance.Clients.Count; i++)
                {
                    if (player.NetConnection.RemoteEndPoint.Address.Equals(Program.ServerInstance.Clients[i].NetConnection.RemoteEndPoint.Address) && player.DisplayName.Contains(Program.ServerInstance.Clients[i].DisplayName))
                    {
                        count++;Console.WriteLine("Player: "+ player.DisplayName+" | Client: "+ Program.ServerInstance.Clients[i].DisplayName);
                        if (count > 1)
                        {
                            Program.ServerInstance.KickPlayer(Program.ServerInstance.Clients[i], "Clone detected!");
                            player.DisplayName = Program.ServerInstance.Clients[i].DisplayName; return;
                            //Program.ServerInstance.SendChatMessageToAll("SERVER", string.Format("Kicking {0} for clone detected!", Program.ServerInstance.Clients[i].DisplayName.ToString()));
                        }
                    }
                }
            }
            if (player.DisplayName.Trim().StartsWith("[") || player.DisplayName.Trim().EndsWith(")"))
            {
                Program.ServerInstance.SendChatMessageToAll("SERVER", string.Format("Kicking {0} for impersonating.", player.DisplayName.ToString()));
                Program.ServerInstance.DenyPlayer(player, "Remove the () and [] from your nickname.", false, null, 15); return;
            }
        }
        public override bool OnPlayerConnect(Client player)
        {
            //try
            //{
            //    Console.Write("Nickname: " + player.DisplayName.ToString() + " | ");
            //    Console.Write("Realname: " + player.Name.ToString() + " | ");
            //    Console.Write("IP: " + player.NetConnection.RemoteEndPoint.Address.ToString() + " | ");
            //    Console.Write("Game Version: " + player.GameVersion.ToString() + " | ");
            //    Console.Write("Script Version: " + player.RemoteScriptVersion.ToString() + "\n");
            //}
            //catch (Exception e) { }
            //string response = String.Empty;
            //try
            //{
            //    using (var webClient = new System.Net.WebClient())
            //    {
            //        response = webClient.DownloadString("http://ip-api.com/json/"+player.NetConnection.RemoteEndPoint.Address);
            //    }
            //}
            //catch (Exception e)
            //{
            //    Console.WriteLine("Could not contact IP API.");
            //}
            //if (!string.IsNullOrWhiteSpace(response)) {
            //    IPInfo dejson = JsonConvert.DeserializeObject<IPInfo>(response);
            //    if (dejson.list != null)
            //    {
            //        if (dejson.status.Equals("success"))
            //        {
            //            string country = dejson.countryCode;
            //            Console.WriteLine("Country Code: "+country);
            //        }else
            //        {
            //            Console.WriteLine("Could not query IP infos from API.");
            //        }
            //    }

            //}

            //Program.ServerInstance.SendChatMessageToPlayer(player, "INFO", "Current Server Flags: ");

            Program.ServerInstance.SendChatMessageToPlayer(player, "SERVER", Settings.MOTD);
            //Program.ServerInstance.SendChatMessageToPlayer(player, "SERVER", string.Format("Welcome to {0}", GTAServer.ServerSettings));
            //var settings = ReadSettings(Program.Location + "Settings.xml");

            if (player.GetAccount(false) == null)
            {
                Program.ServerInstance.SendChatMessageToPlayer(player, "SERVER", "You can register an account using /register [password]");
            }
            else
            {
                Program.ServerInstance.SendChatMessageToPlayer(player, "SERVER", "Please authenticate to your account using /login [password]");
            }

            Program.ServerInstance.SendNativeCallToPlayer(player, 0x29B487C359E19889, _weatherNames[ServerWeather]);

            Program.ServerInstance.SendNativeCallToPlayer(player, 0x47C3B5848C3E45D8, ServerTime.Hours, ServerTime.Minutes, ServerTime.Seconds);
            Program.ServerInstance.SendNativeCallToPlayer(player, 0x4055E40BD2DBEC1D, true);
            return true;
        }

        public override ChatMessage OnChatMessage(ChatMessage message)
        {
            try { 
            Account account = message.Sender.GetAccount();
            if (message.Message.ToLower().Equals("/help"))
            {
                if (account != null)
                {
                    switch ((int)account.Level)
                    {
                        case 0:
                            Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Available commands: /help, /register, /q"); message.Supress = true; return message;
                        case 1:
                            Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Available commands: /help, /logout, /q, /afk, /back"); message.Supress = true; return message;
                        case 2:
                            Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Available commands: /help, /logout, /q, /afk, /back"); message.Supress = true; return message;
                        case 3:
                            Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Available commands: /help, /logout, /q, /afk, /back, /kick, /ban, /tp"); message.Supress = true; return message;
                        case 4:
                            Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Available commands: /help, /logout, /q, /afk, /back, /kick, /ban, /tp, /godmode, /info, /kill, /weather, /time, /nick"); message.Supress = true; return message;
                        case 5:
                            Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Available commands: /help, /logout, /q, /afk, /back, /kick, /ban, /tp, /godmode, /info, /kill, /weather, /time, /nick, /stop, /l"); message.Supress = true; return message;
                        default:
                            break;
                    }
                }
                else
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Available commands: /help, /register, /q"); message.Supress = true; return message;
                }
            }
            if (message.Message.ToLower().Equals("/q"))
            {
                Program.ServerInstance.KickPlayer(message.Sender, "You left the server.");
            }
            if (message.Message.ToLower().Equals("/afk"))
            {
                if (account == null || (int)account.Level < 1)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Register to use this command."); message.Supress = true; return message;
                }
                if (message.Sender.afk) { Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "You are already AFK."); message.Supress = true; return message; }
                message.Sender.afk = true;
                Program.ServerInstance.SendChatMessageToAll(message.Sender.DisplayName, "has gone AFK.");message.Supress = true; return message;
            }
            if (message.Message.ToLower().Equals("/back"))
            {
                if (account == null || (int)account.Level < 1)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Register to use this command."); message.Supress = true; return message;
                }
                if (!message.Sender.afk) { Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "You are not AFK."); message.Supress = true; return message; }
                Program.ServerInstance.SendChatMessageToAll(message.Sender.DisplayName, "is now back.");message.Sender.afk = false; message.Supress = true; return message;

            }
            if (message.Message.ToLower().Equals("/l"))
            {
                if (account == null || (int)account.Level < 5)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Insufficent privileges."); message.Supress = true; return message;
                }
                for (var i = 0; i < Program.ServerInstance.Clients.Count; i++)
                {
                    try {
                        Client target = Program.ServerInstance.Clients[i];
                        Console.WriteLine(string.Format("" +
                        "Nickname: {0} | " +
                        "Realname: {1} |" +
                        "Ping: {2}ms | " +
                        "IP: {3} | " +
                        "Game Version: {4} | " +
                        "Script Version: {5} | " +
                        "Vehicle Health: {6} | " +
                        "Last Position: {7} | ",
                        target.DisplayName.ToString(),
                        target.Name.ToString(),
                        Math.Round(target.Latency * 1000, MidpointRounding.AwayFromZero).ToString(),
                        target.NetConnection.RemoteEndPoint.Address.ToString(),
                        target.GameVersion.ToString(),
                        target.RemoteScriptVersion.ToString(),
                        target.VehicleHealth.ToString(),
                        target.LastKnownPosition.ToString()));
                    }catch {}
                }
                Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Printed playerlist to console."); message.Supress = true; return message;
            }
            if (message.Message.ToLower().StartsWith("/info"))
            {
                if (account == null || (int)account.Level < 4)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Insufficent privileges."); message.Supress = true; return message;
                }
                var args = message.Message.Split();
                if (args.Length <= 1)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "USAGE", "/info [Player Name]"); message.Supress = true; return message;
                }
                Client target = null;
                lock (Program.ServerInstance.Clients) target = Program.ServerInstance.Clients.FirstOrDefault(c => c.DisplayName.ToLower().StartsWith(args[1].ToLower()));

                if (target == null)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "ERROR", "No such player found: " + args[1]);
                    message.Supress = true; return message;
                }
                Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "1/2", string.Format("" +
                    "Nickname: {0}\n" +
                    "Realname: {1}\n" +
                    "Ping: {2}ms\n" +
                    "IP: {3}",
                    target.DisplayName.ToString(),
                    target.Name.ToString(),
                    Math.Round(target.Latency * 1000, MidpointRounding.AwayFromZero).ToString(),
                    target.NetConnection.RemoteEndPoint.Address.ToString()));
                Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "2/2", string.Format("" +
                    "Game Version: {0}\n" +
                    "Script Version: {1}\n" +
                    "Vehicle Health: {2}\n" +
                    "Last Position: {3}\n",
                    target.GameVersion.ToString(),
                    target.RemoteScriptVersion.ToString(),
                    target.VehicleHealth.ToString(),
                    target.LastKnownPosition.ToString()));
                message.Supress = true; return message;
            }
            if (message.Message.ToLower().StartsWith("/nick"))
            {
                if (account == null || (int)account.Level < 4)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Insufficent privileges."); message.Supress = true; return message;
                }
                var args = message.Message.Split();
                if (args.Length <= 1)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "USAGE", "/tp [Player Name]"); message.Supress = true; return message;
                }
                message.Sender.DisplayName = args[1];message.Supress = true; return message;
            }
            if (message.Message.ToLower().StartsWith("/stop"))
            {
                if (account == null || (int)account.Level < 5)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Insufficent privileges."); message.Supress = true; return message;
                }
                Program.ServerInstance.SendChatMessageToAll("SERVER", "This server will stop now!");
                Environment.Exit(-1);message.Supress = true; return message;
            }
            /*if (message.Message.StartsWith("/restart"))
            {
                if (account == null || (int)account.Level < 5)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Insufficent privileges.");
                    message.Supress = true; return message;
                }
                Program.ServerInstance.SendChatMessageToAll("SERVER", "~p~This server will restart now. Please reconnect!~p~");
                try
                {
                    //process = System.Diagnostics.Process[] GetProcessesByName("GTAServer.exe";
                    //Process[] processes = Process.GetProcessesByName("GTAServer.exe");
                    //processes[0].WaitForExit(1000);
                    Environment.Exit(-1);
                }
                catch (ArgumentException ex) { Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Could not restart."); }
                Process.Start("GTAServer.exe", ""); message.Supress = true; return message;
            }*/
            if (message.Message.ToLower().StartsWith("/tp"))
            {
                if (account == null || (int)account.Level < 3)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Insufficent privileges.");
                    message.Supress = true; return message;
                }

                var args = message.Message.Split();
                if (args.Length <= 1)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "USAGE", "/tp [Player Name]");
                    message.Supress = true; return message;
                }

                Client target = null;
                lock (Program.ServerInstance.Clients) target = Program.ServerInstance.Clients.FirstOrDefault(c => c.DisplayName.ToLower().StartsWith(args[1].ToLower()));

                if (target == null)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "ERROR", "No such player found: " + args[1]);
                    message.Supress = true; return message;
                }

                Program.ServerInstance.GetPlayerPosition(target, o =>
                {
                    var newPos = (Vector3)o;
                    Program.ServerInstance.SetPlayerPosition(message.Sender, newPos);
                });

                Console.WriteLine(string.Format("ADMINTOOLS: {0} has teleported to player {1}", account.Name + " (" + message.Sender.DisplayName + ")", target.Name + " (" + target.DisplayName + ")"));

                message.Supress = true; return message;
            }

            if (message.Message.ToLower().StartsWith("/godmode"))
            {
                var args = message.Message.Split();
                if (args.Length <= 1)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "USAGE", "/godmode [Player Name]");
                    message.Supress = true; return message;
                }

                Client target = null;
                lock (Program.ServerInstance.Clients) target = Program.ServerInstance.Clients.FirstOrDefault(c => c.DisplayName.ToLower().StartsWith(args[1].ToLower()));

                if (target == null)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "ERROR", "No such player found: " + args[1]);
                    message.Supress = true; return message;
                }

                string salt = "inv+" + target.NetConnection.RemoteUniqueIdentifier;

                Program.ServerInstance.GetNativeCallFromPlayer(target, salt, 0xB721981B2B939E07, new BooleanArgument(),
                    (o) =>
                    {
                        bool isInvincible = (bool) o;
                        Program.ServerInstance.SendChatMessageToPlayer(message.Sender, string.Format("Player {0} is {1}", target.DisplayName, isInvincible ? "~g~invincible." : "~r~mortal."));
                    }, new LocalGamePlayerArgument());

                message.Supress = true; return message;
            }

            if (message.Message.ToLower().StartsWith("/weather"))
            {
                if (account == null || (int)account.Level < 4)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Insufficent privileges.");
                    message.Supress = true; return message;
                }

                var args = message.Message.Split();
                if (args.Length <= 1)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "USAGE", "/weather [Weather ID]");
                    message.Supress = true; return message;
                }

                int newWeather;
                if (!int.TryParse(args[1], out newWeather))
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "USAGE", "/weather [Weather ID]");
                    message.Supress = true; return message;
                }

                if (newWeather < 0 || newWeather >= _weatherNames.Length)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "USAGE", "Weather ID must be between 0 and " + (_weatherNames.Length-1));
                    message.Supress = true; return message;
                }

                ServerWeather = newWeather;
                Program.ServerInstance.SendNativeCallToAllPlayers(0x29B487C359E19889, _weatherNames[ServerWeather]);

                Console.WriteLine(string.Format("ADMINTOOLS: {0} has changed the weather to {1}", account.Name + " (" + message.Sender.DisplayName + ")", ServerWeather));

                message.Supress = true; return message;
            }

            if (message.Message.ToLower().StartsWith("/time"))
            {
                if (account == null || (int)account.Level < 4)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Insufficent privileges.");
                    message.Supress = true; return message;
                }

                var args = message.Message.Split();
                if (args.Length <= 1)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "USAGE", "/time [hours]:[minutes]");
                    message.Supress = true; return message;
                }

                int hours;
                int minutes;
                var timeSplit = args[1].Split(':');

                if (timeSplit.Length < 2 || !int.TryParse(timeSplit[0], out hours) || !int.TryParse(timeSplit[1], out minutes))
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "USAGE", "/time [hours]:[minutes]");
                    message.Supress = true; return message;
                }

                if (hours < 0 || hours > 24 || minutes < 0 || minutes > 60)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "USAGE", "/time [hours]:[minutes]");
                    message.Supress = true; return message;
                }

                ServerTime = new TimeSpan(hours, minutes, 0);

                Program.ServerInstance.SendNativeCallToAllPlayers(0x47C3B5848C3E45D8, ServerTime.Hours, ServerTime.Minutes, ServerTime.Seconds);
                Program.ServerInstance.SendNativeCallToAllPlayers(0x4055E40BD2DBEC1D, true);

                Console.WriteLine(string.Format("ADMINTOOLS: {0} has changed the time to {1}", account.Name + " (" + message.Sender.DisplayName + ")", ServerTime));

                message.Supress = true; return message;
            }

            if (message.Message.ToLower().StartsWith("/kill"))
            {
                if (account == null || (int)account.Level < 4)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Insufficent privileges.");
                    message.Supress = true; return message;
                }

                var args = message.Message.Split();
                if (args.Length <= 1)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "USAGE", "/kill [Player Name]");
                    message.Supress = true; return message;
                }

                Client target = null;
                lock (Program.ServerInstance.Clients) target = Program.ServerInstance.Clients.FirstOrDefault(c => c.DisplayName.ToLower().StartsWith(args[1].ToLower()));

                if (target == null)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "ERROR", "No such player found: " + args[1]);
                    message.Supress = true; return message;
                }

                Program.ServerInstance.SetPlayerHealth(target, -1);
                Console.WriteLine(string.Format("ADMINTOOLS: {0} has killed player {1}", account.Name + " (" + message.Sender.DisplayName + ")", target.Name + " (" + target.DisplayName + ")"));
                message.Supress = true; return message;
            }

            if (message.Message.ToLower().StartsWith("/ban"))
            {
                if (account == null || (int)account.Level < 3)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Insufficent privileges.");
                    message.Supress = true; return message;
                }

                var args = message.Message.Split();
                if (args.Length <= 2)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "USAGE", "/ban [Player Name] [Reason]");
                    message.Supress = true; return message;
                }

                Client target = null;
                lock (Program.ServerInstance.Clients) target = Program.ServerInstance.Clients.FirstOrDefault(c => c.DisplayName.ToLower().StartsWith(args[1].ToLower()));

                if (target == null)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "ERROR", "No such player found: " + args[1]);
                    message.Supress = true; return message;
                }

                target.Ban(args[2], message.Sender);

                SaveBanlist(Location + "Banlist.xml");

                Console.WriteLine(string.Format("ADMINTOOLS: {0} has banned player {1} with reason: {2}", account.Name + " (" + message.Sender.DisplayName + ")", target.Name + " (" + target.DisplayName + ")", args[2]));
                Program.ServerInstance.KickPlayer(target, "You have been banned: " + args[2]);
                message.Supress = true; return message;
            }

            if (message.Message.ToLower().StartsWith("/kick"))
            {
                if (account == null || (int)account.Level < 3)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Insufficent privileges.");
                    message.Supress = true; return message;
                }

                var args = message.Message.Split();
                if (args.Length <= 2)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "USAGE", "/kick [Player Name] [Reason]");
                    message.Supress = true; return message;
                }

                Client target = null;
                lock (Program.ServerInstance.Clients) target = Program.ServerInstance.Clients.FirstOrDefault(c => c.DisplayName.ToLower().StartsWith(args[1].ToLower()));

                if (target == null)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "ERROR", "No such player found: " + args[1]);
                    message.Supress = true; return message;
                }

                Program.ServerInstance.KickPlayer(target, args[2]);
                Console.WriteLine(string.Format("SERVER: {0} has kicked player {1}", account.Name + " (" + message.Sender.DisplayName + ")", target.Name + " (" + target.DisplayName + ")"));
                message.Supress = true; return message;
            }

            if (message.Message.ToLower().StartsWith("/register"))
            {
                account = message.Sender.GetAccount(false);
                if (account != null)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "You already have an account.");
                    message.Supress = true; return message;
                }

                var args = message.Message.Split();
                if (args.Length <= 1)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "USAGE", "/register [Password]");
                    message.Supress = true; return message;
                }

                var password = GetHashSha256(args[1]);
                var accObject = new Account()
                {
                    Level = Privilege.User,
                    Name = message.Sender.DisplayName,
                    Password = password,
                    Ban = null
                };
                lock (Lists._accounts.Accounts) Lists._accounts.Accounts.Add(accObject);
                SaveAccounts(Location + "Accounts.xml");
                lock (Lists._authenticatedUsers) Lists._authenticatedUsers.Add(message.Sender.NetConnection.RemoteUniqueIdentifier);

                Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Your account has been created!");
                    Program.ServerInstance.PrintPlayerInfo(message.Sender, "New Player registered: ");
                message.Supress = true; return message;
            }

            if (message.Message.ToLower().StartsWith("/login"))
            {
                if (account != null)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "You are already authenticated.");
                    message.Supress = true; return message;
                }

                account = message.Sender.GetAccount(false);

                if (account == null)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "No accounts have been found with your name.");
                    message.Supress = true; return message;
                }

                var args = message.Message.Split();
                if (args.Length <= 1)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "USAGE", "/login [Password]");
                    message.Supress = true; return message;
                }

                var password = GetHashSha256(args[1]);

                if (password != account.Password)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Wrong password.");
                    message.Supress = true; return message;
                }

                lock (Lists._authenticatedUsers) if (!Lists._authenticatedUsers.Contains(message.Sender.NetConnection.RemoteUniqueIdentifier)) Lists._authenticatedUsers.Add(message.Sender.NetConnection.RemoteUniqueIdentifier);

                Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Authentication successful!");
                    Program.ServerInstance.PrintPlayerInfo(message.Sender, account.Level.ToString()+" logged in: ");
                    //LogToConsole(2, false, "Accounting", string.Format("{0} \"{1}\" logged in.", account.Level.ToString(), message.Sender.DisplayName));
            }

            if (message.Message.ToLower() == "/logout")
            {
                if (message.Sender.IsAuthenticated())
                {
                    Console.WriteLine(string.Format("SERVER: Player has logged out: {0}", message.Sender.Name));
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "You have been logged out.");

                    lock (Lists._authenticatedUsers) if (Lists._authenticatedUsers.Contains(message.Sender.NetConnection.RemoteUniqueIdentifier)) Lists._authenticatedUsers.Remove(message.Sender.NetConnection.RemoteUniqueIdentifier);
                }
                else
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "You are not logged in.");
                }
                message.Supress = true; return message;
            }

            if (message.Message.ToLower() == "/countdown")
            {
                if (DateTime.Now.Subtract(_lastCountdown).TotalSeconds < 30)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(message.Sender, "COUNTDOWN", "Please wait 30 seconds before starting another countdown.");
                    message.Supress = true; return message;
                }

                _lastCountdown = DateTime.Now;

                var cdThread = new Thread((ThreadStart) delegate
                {
                    for (int i = 3; i >= 0; i--)
                    {
                        Program.ServerInstance.SendChatMessageToAll("COUNTDOWN", i == 0 ? "Go!" : i.ToString());
                        Thread.Sleep(1000);
                    }
                });
                cdThread.Start();
                message.Supress = true; return message;
            }
            try { message.Prefix = message.Sender.geoIP.Country.IsoCode.ToString(); } catch(Exception ex) { LogToConsole(3, false, "GeoIP", ex.Message); }
                try { message.Suffix = account.Level.ToString(); } catch { message.Suffix = "Guest"; }
            if (message.Message.ToLower().Contains("login") || message.Message.ToLower().Contains("register") || message.Message.ToLower().Equals("urtle")) { message.Supress = true; return message; }
            return message;
        } catch(Exception ex) { LogToConsole(4, false, "Chat", "Can't handle message: "+ex.Message); return null; }
}

        public override bool OnPlayerDisconnect(Client player)
        {
            lock (Lists._authenticatedUsers) if (Lists._authenticatedUsers.Contains(player.NetConnection.RemoteUniqueIdentifier)) Lists._authenticatedUsers.Remove(player.NetConnection.RemoteUniqueIdentifier);

            if (player.IsBanned() || player.IsIPBanned()) return false;

            return true;
        }

        public override void OnPlayerSpawned(Client player)
        {
            try { Program.ServerInstance.SendNativeCallToPlayer(player, 0x29B487C359E19889, _weatherNames[ServerWeather]); } catch { }
            try { Program.ServerInstance.SendNativeCallToPlayer(player, 0x47C3B5848C3E45D8, ServerTime.Hours, ServerTime.Minutes, ServerTime.Seconds); } catch { }
            try { Program.ServerInstance.SendNativeCallToPlayer(player, 0x4055E40BD2DBEC1D, true); } catch { }
        }

        static AdminSettings ReadSettings(string path)
        {
            var ser = new XmlSerializer(typeof(AdminSettings));

            AdminSettings settings = null;

            if (File.Exists(path))
            {
                using (var stream = File.OpenRead(path)) settings = (AdminSettings)ser.Deserialize(stream);

                using (var stream = new FileStream(path, File.Exists(path) ? FileMode.Truncate : FileMode.Create, FileAccess.ReadWrite)) ser.Serialize(stream, settings);
            }
            else
            {
                using (var stream = File.OpenWrite(path)) ser.Serialize(stream, settings = new AdminSettings());
            }

            return settings;
        }

        private void LoadAccounts(string path)
        {
            XmlSerializer ser = new XmlSerializer(typeof(UserList));
            if (File.Exists(path))
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite))
                {
                    Lists._accounts = (UserList)ser.Deserialize(stream);
                }
            }
            else
            {
                Lists._accounts = new UserList();
                Lists._accounts.Accounts = new List<Account>();
                SaveAccounts(path);
            }
        }

        private void SaveAccounts(string path)
        {
            XmlSerializer ser = new XmlSerializer(typeof(UserList));
            using (var stream = new FileStream(path, File.Exists(path) ? FileMode.Truncate : FileMode.Create, FileAccess.ReadWrite))
            {
                ser.Serialize(stream, Lists._accounts);
            }
        }

        private void LoadBanlist(string path)
        {
            XmlSerializer ser = new XmlSerializer(typeof(Banlist));
            if (File.Exists(path))
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite))
                {
                    Lists._banned = (Banlist)ser.Deserialize(stream);
                }

                Lists._accounts.Accounts.ForEach(acc=>
                {
                    acc.Ban = null;
                    Lists._banned.BannedIps.Any(b =>
                    {
                        if (b.Name == acc.Name)
                        {
                            acc.Ban = b;

                            return true;
                        }

                        return false;
                    });
                });
            }
            else
            {
                Lists._banned = new Banlist();
                Lists._banned.BannedIps = new List<Ban>();
                SaveBanlist(path);
            }
        }

        private void SaveBanlist(string path)
        {
            XmlSerializer ser = new XmlSerializer(typeof(Banlist));
            using (var stream = new FileStream(path, File.Exists(path) ? FileMode.Truncate : FileMode.Create, FileAccess.ReadWrite))
            {
                ser.Serialize(stream, Lists._banned);
            }
        }

        private string GetHashSha256(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            SHA256Managed hashstring = new SHA256Managed();
            byte[] hash = hashstring.ComputeHash(bytes);

            StringBuilder hashString = new StringBuilder();
            foreach (byte x in hash)
            {
                hashString.Append(string.Format("{0:x2}", x));
            }
            return hashString.ToString();
        }
        public string SanitizeString(string input)
        {
            input = Regex.Replace(input, "~.~", "", RegexOptions.IgnoreCase);
            return input;
        }
        public string FormatString(string input)
        {
            input = Regex.Replace(input, "~b~", "<unicode for blue>", RegexOptions.IgnoreCase);
            input = Regex.Replace(input, "~r~", "<Unicode for reset>", RegexOptions.IgnoreCase);
            return input;
        }
    }

    public class IPInfo
    {
        public string countryCode { get; internal set; }
        public List<string> list { get; set; }
        public object status { get; internal set; }
    }

    public enum Privilege
    {
        Guest = 0,
        User = 1,
        VIP = 2,
        Moderator = 3,
        Administrator = 4,
        Owner = 5,
    }

    public class UserList
    {
        public List<Account> Accounts { get; set; }
    }

    public class Account
    {
        public string Name { get; set; }
        public string Password { get; set; }
        public Privilege Level { get; set; }
        public Ban Ban { get; set; }
    }

    public static class Accounts
    {
        public static bool IsAuthenticated(this Client client)
        {
            lock (Lists._authenticatedUsers) return Lists._authenticatedUsers.Contains(client.NetConnection.RemoteUniqueIdentifier);
        }

        public static Account GetAccount(this Client client, bool checkauthentication = true)
        {
            if (!checkauthentication || client.IsAuthenticated()) lock (Lists._accounts.Accounts) return Lists._accounts.Accounts.FirstOrDefault(acc => acc.Name == client.DisplayName);

            return null;
        }

        public static bool IsIPBanned(this Client client)
        {
            bool banned = false;
            lock (Lists._banned.BannedIps)
            {
                banned = Lists._banned.BannedIps.Any(b =>
                {
                    if(b.Address == client.NetConnection.RemoteEndPoint.Address.ToString())
                    {
                        try
                        {
                            client.GetAccount().Ban = b;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(string.Format("Check for ban of player \"{0}\" failed: {1}", client.DisplayName, ex.Message));
                        }
                        return true;
                    }

                    return false;
                });
            }

            return banned;
        }

        public static bool IsBanned(this Client client)
        {
            return client.GetBan() != null;
        }

        public static Ban GetBan(this Client client)
        {
            Account account = client.GetAccount();

            return account == null ? null : account.Ban;
        }

        public static void Ban(this Client client, string Reason, Client IssuedBy = null)
        {
            Ban ban = new Ban()
            {
                Address = client.NetConnection.RemoteEndPoint.Address.ToString(),
                BannedBy = IssuedBy == null ? "Server" : IssuedBy.DisplayName,
                Reason = Reason,
                TimeIssued = DateTime.Now,
                Name = client.DisplayName
            };

            lock (Lists._banned.BannedIps) Lists._banned.BannedIps.Add(ban);
            try {
                client.GetAccount().Ban = ban;
            } catch (Exception ex) {
                Console.WriteLine(string.Format("Ban of player \"{0}\" failed: {1}", client.DisplayName, ex.ToString() ));
            }
        }

        public static Client GetClient(this Account account)
        {
            Client client = null;
            lock (Program.ServerInstance.Clients) Program.ServerInstance.Clients.Any(c =>
            {
                if (c.DisplayName == account.Name)
                {
                    client = c;

                    return true;
                }

                return false;
            });

            return client;
        }
    }

    public class Banlist
    {
        public List<Ban> BannedIps { get; set; }
    }

    public class Ban
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public string Reason { get; set; }
        public DateTime TimeIssued { get; set; }
        public string BannedBy { get; set; }
    }
}