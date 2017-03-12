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
using System.Drawing;
using System.Globalization;

namespace AdminTools
{
    public class Lists
    {
        internal static UserList Accounts = new UserList();
        internal static Banlist Banned = new Banlist();
        internal static List<long> AuthenticatedUsers = new List<long>();
    }
    [Serializable]
    public class AdminToolsServerScript : ServerScript
    {
        public static GameServer ServerInstance;
        static void LogToConsole(int flag, bool debug, string module, string message)
        {
            if (module == null || module.Equals("")) { module = "SERVER"; }
            if (flag == 1)
            {
                Console.ForegroundColor = ConsoleColor.Cyan; Console.WriteLine("[" + DateTime.Now + "] (DEBUG) " + module.ToUpper() + ": " + message);
            }
            else if (flag == 2)
            {
                Console.ForegroundColor = ConsoleColor.Green; Console.WriteLine("[" + DateTime.Now + "] (SUCCESS) " + module.ToUpper() + ": " + message);
            }
            else if (flag == 3)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow; Console.WriteLine("[" + DateTime.Now + "] (WARNING) " + module.ToUpper() + ": " + message);
            }
            else if (flag == 4)
            {
                Console.ForegroundColor = ConsoleColor.Red; Console.WriteLine("[" + DateTime.Now + "] (ERROR) " + module.ToUpper() + ": " + message);
            }
            else
            {
                Console.WriteLine("[" + DateTime.Now + "] " + module.ToUpper() + ": " + message);
            }
            Console.ForegroundColor = ConsoleColor.White;
        }
        public static string Location => AppDomain.CurrentDomain.BaseDirectory;
        public override string Name => "Server Administration Tools";
        public int Version => 1;

        public bool Afk { get; private set; }
        public AdminSettings Settings = ReadSettings(Location + "/filterscripts/AdminTools.xml");
        public int ServerWeather;
        public TimeSpan ServerTime;

        private DateTime _lastCountdown;

        private readonly string[] _weatherNames = new[]
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

        public override void Start(GameServer serverInstance)
        {
            ServerInstance = serverInstance;
            //Console.WriteLine("\x1b[31mRed\x1b[0;37m"); //https://en.wikipedia.org/wiki/ANSI_escape_code
            LoadAccounts(Location + "Accounts.xml");
            LogToConsole(2, false, null, "Accounts loaded.");
            LoadBanlist(Location + "Banlist.xml");
            LogToConsole(2, false, null, "Bans loaded.");

            ServerWeather = 0;
            ServerTime = new TimeSpan(12, 0, 0);
        }
        public class MasterServerList
        {
            public List<string> list { get; set; }
        }
        public int ReadableVersion(ScriptVersion version)
        {
            var Version = version.ToString();
            Version = Regex.Replace(Version, "VERSION_", "", RegexOptions.IgnoreCase);
            Version = Regex.Replace(Version, "_", "", RegexOptions.IgnoreCase);
            return Int32.Parse(Version);
        }
        public override void OnTick()
        {
            if ((DateTime.Now.ToString("ss:fff").Equals("20:001")) || (DateTime.Now.ToString("ss:fff").Equals("40:001")))
            {
                if (Settings.MaxPing > 0 && ServerInstance.Clients.Count > 0)
                {
                    for (var i = 0; i < ServerInstance.Clients.Count; i++)
                    {
                        //Console.WriteLine(string.Format("{0} Current Ping: \"{1}\" / Max Ping: \"{2}\"",ServerInstance.Clients[i].DisplayName, Math.Round(ServerInstance.Clients[i].Latency * 1000, MidpointRounding.AwayFromZero).ToString(), Settings.MaxPing));
                        if (Math.Round(ServerInstance.Clients[i].Latency * 1000, MidpointRounding.AwayFromZero) > Settings.MaxPing)
                        {
                            //ServerInstance.SendChatMessageToAll("SERVER", string.Format("Kicking {0} for Ping {1} too high! Max: {2}", ServerInstance.Clients[i].DisplayName.ToString(), Math.Round(ServerInstance.Clients[i].Latency * 1000, MidpointRounding.AwayFromZero).ToString(), Settings.MaxPing.ToString()));
                            ServerInstance.KickPlayer(ServerInstance.Clients[i],
                                $"Ping too high! {Math.Round(ServerInstance.Clients[i].Latency * 1000, MidpointRounding.AwayFromZero).ToString()}/{Settings.MaxPing.ToString()}ms");
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
                ServerInstance.SendChatMessageToAll("SERVER",
                    $"Kicking {player.DisplayName.ToString()} for impersonating.");
                ServerInstance.DenyPlayer(player, "Change your nickname to a proper one!", true, null, 30); return;
            }
            if (Settings.KickOnDefaultNickName)
            {
                if (player.DisplayName.ToLower().StartsWith("rld!") || player.DisplayName.ToLower().StartsWith("player") || player.DisplayName.ToLower().StartsWith("nosteam") || player.DisplayName.ToLower().StartsWith("3dmgame1") || player.DisplayName.StartsWith("3dm") || player.DisplayName.ToLower().StartsWith("your name"))
                {
                    //ServerInstance.SendChatMessageToAll("SERVER", string.Format("Kicking {0} for default nickname.", player.DisplayName.ToString()));
                    ServerInstance.DenyPlayer(player, "~r~Change your nickname!~w~ (~h~F9~h~->~h~Settings~h~->~h~Nickname~h~)", true, null, 10); return;
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
                    //ServerInstance.SendChatMessageToAll("SERVER", string.Format("Kicking {0} for outdated mod.", player.DisplayName.ToString()));
                    string version = Regex.Replace(Settings.NeededScriptVersion.ToString(), "VERSION_", "", RegexOptions.IgnoreCase);
                    version = Regex.Replace(version, "_", ".", RegexOptions.IgnoreCase);
                    ServerInstance.DenyPlayer(player, $"You need GTACoop Mod v~g~{version}~w~ to play on this server.", true, null, 120); return;
                }
            }
            if (Settings.KickOnOutdatedGame)
            {
                //Console.WriteLine(string.Format("[Game Version Check] Got: {0} | Expected: {1}", player.GameVersion.ToString(), Settings.MinGameVersion.ToString()));
                if (player.GameVersion < Settings.MinGameVersion)
                {
                    ServerInstance.SendChatMessageToAll("SERVER",
                        $"Kicking {player.DisplayName.ToString()} for outdated game.");
                    ServerInstance.DenyPlayer(player, "Update your GTA V to the newest version!", false, null, 120); return;
                }
            }
            if (Settings.SocialClubOnly)
            {
                // Console.WriteLine(player.Name);
                if (player.Name.ToString() == "RLD!" || player.Name.ToString() == "nosTEAM" || player.Name.ToString() == "Player" || player.Name.ToString() == "3dmgame1")
                {
                    ServerInstance.SendChatMessageToAll("SERVER",
                        $"Kicking {player.DisplayName.ToString()} for cracked game.");
                    ServerInstance.DenyPlayer(player, "Buy the game and sign in through social club!"); return;
                }
            }
            if (Settings.KickOnNameDifference)
            {
                if (!player.DisplayName.Equals(player.Name))
                {
                    //ServerInstance.SendChatMessageToAll("SERVER", string.Format("Kicking {0} for nickname differs from account name.", player.DisplayName.ToString()));
                    ServerInstance.DenyPlayer(player, $"Change your nickname to {player.Name}", true, null, 15); return;
                }
            }
            if (player.IsBanned() || player.IsIPBanned())
            {
                ServerInstance.SendChatMessageToAll("SERVER",
                    $"{player.DisplayName.ToString()} is banned for {player.GetBan().Reason}");
                ServerInstance.DenyPlayer(player, "You are banned: " + player.GetBan().Reason, false, null, 120); return;
            }
            if (Settings.OnlyAsciiNickName)
            {
                if (Encoding.UTF8.GetByteCount(player.DisplayName) != player.DisplayName.Length)
                {
                    //ServerInstance.SendChatMessageToAll("SERVER", string.Format("{0} was kicked for non-ascii chars in his nickname.", player.DisplayName.ToString()));
                    ServerInstance.DenyPlayer(player, "Remove all non-ascii characters from your nickname.", true, null, 15); return;
                }
            }
            if (Settings.OnlyAsciiUserName)
            {
                if (Encoding.UTF8.GetByteCount(player.Name) != player.Name.Length)
                {
                    //ServerInstance.SendChatMessageToAll("SERVER", string.Format("{0} was kicked for non-ascii chars in his username.", player.DisplayName.ToString()));
                    ServerInstance.DenyPlayer(player, "Remove all non-ascii characters from your Social Club username.", true, null, 120); return;
                }
            }
            if (Settings.LimitNickNames)
            {
                if (player.DisplayName.Length < 3 || player.DisplayName.Length > 100 || Regex.Replace(player.DisplayName, "~.~", "", RegexOptions.IgnoreCase).Length > 20)
                {
                    ServerInstance.DenyPlayer(player, "Your nickname has to be between 3 and 20 chars long. (Max 100 with colors)", true, null, 20); return;
                }
            }
            if (Settings.AntiClones)
            {
                var count = 0;
                foreach (var t in ServerInstance.Clients)
                {
                    if (player.NetConnection.RemoteEndPoint.Address.Equals(t.NetConnection.RemoteEndPoint.Address) && player.DisplayName.Contains(t.DisplayName))
                    {
                        var last = player; count++;
                        //Console.WriteLine("Player: "+ player.DisplayName+" | Client: "+ ServerInstance.Clients[i].DisplayName);
                        if (count <= 1) continue;
                        ServerInstance.KickPlayer(last, "Clone detected!");
                        player.DisplayName = last.DisplayName; last = null; return;
                        //ServerInstance.SendChatMessageToAll("SERVER", string.Format("Kicking {0} for clone detected!", ServerInstance.Clients[i].DisplayName.ToString()));
                    }
                }
            }
            if (player.DisplayName.Trim().StartsWith("[") || player.DisplayName.Trim().EndsWith(")"))
            {
                ServerInstance.SendChatMessageToAll("SERVER",
                    $"Kicking {player.DisplayName.ToString()} for impersonating.");
                ServerInstance.DenyPlayer(player, "Remove the () and [] from your nickname.", false, null, 15); return;
            }
            try
            {
                if (!string.IsNullOrEmpty(Settings.CountryRestriction) && !string.IsNullOrEmpty(player.geoIP.Country.Name))
                {
                    if (!Settings.CountryRestriction.Equals(player.geoIP.Country.Name))
                    {
                        ServerInstance.DenyPlayer(player, "Sorry, but only players from " + Settings.CountryRestriction + " allowed here.", false, null); return;
                    }
                }
            }
            catch { }
            if (!string.IsNullOrWhiteSpace(Settings.ProtectedNickname) && !string.IsNullOrWhiteSpace(Settings.ProtectedNicknameIP))
            {
                if (player.DisplayName.Contains(Settings.ProtectedNickname) && !player.NetConnection.RemoteEndPoint.Address.ToString().Equals(Settings.ProtectedNicknameIP))
                {
                    ServerInstance.DenyPlayer(player, "This nickname is protected! You can't use it.", false, null); return;
                }
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

            //ServerInstance.SendChatMessageToPlayer(player, "INFO", "Current Server Flags: ");

            ServerInstance.SendChatMessageToPlayer(player, "SERVER", Settings.MOTD);
            //ServerInstance.SendChatMessageToPlayer(player, "SERVER", string.Format("Welcome to {0}", GTAServer.ServerSettings));
            //var settings = ReadSettings(Program.Location + "Settings.xml");

            if (player.GetAccount(false) == null)
            {
                ServerInstance.SendChatMessageToPlayer(player, "SERVER", "You can register an account using /register [password]");
            }
            else
            {
                ServerInstance.SendChatMessageToPlayer(player, "SERVER", "Please authenticate to your account using /login [password]");
            }

            ServerInstance.SendNativeCallToPlayer(player, 0x29B487C359E19889, _weatherNames[ServerWeather]);

            ServerInstance.SendNativeCallToPlayer(player, 0x47C3B5848C3E45D8, ServerTime.Hours, ServerTime.Minutes, ServerTime.Seconds);
            ServerInstance.SendNativeCallToPlayer(player, 0x4055E40BD2DBEC1D, true);
            return true;
        }

        public override ChatMessage OnChatMessage(ChatMessage message)
        {
            try
            {
                Account account = message.Sender.GetAccount();
                if (message.Message.ToLower().Equals("/help"))
                {
                    if (account != null)
                    {
                        switch ((int)account.Level)
                        {
                            case 0:
                                ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Available commands: /help, /register, /q"); message.Supress = true; return message;
                            case 1:
                                ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Available commands: /help, /logout, /q, /afk, /back"); message.Supress = true; return message;
                            case 2:
                                ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Available commands: /help, /logout, /q, /afk, /back"); message.Supress = true; return message;
                            case 3:
                                ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Available commands: /help, /logout, /q, /afk, /back, /kick, /ban, /tp"); message.Supress = true; return message;
                            case 4:
                                ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Available commands: /help, /logout, /q, /afk, /back, /kick, /ban, /tp, /godmode, /info, /kill, /weather, /time, /nick"); message.Supress = true; return message;
                            case 5:
                                ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Available commands: /help, /logout, /q, /afk, /back, /kick, /ban, /tp, /godmode, /info, /kill, /weather, /time, /nick, /stop, /l"); message.Supress = true; return message;
                            default:
                                break;
                        }
                    }
                    else
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Available commands: /help, /register, /q"); message.Supress = true; return message;
                    }
                }
                if (message.Message.ToLower().Equals("/q"))
                {
                    ServerInstance.KickPlayer(message.Sender, "You left the server.");
                }
                if (message.Message.ToLower().Equals("/afk"))
                {
                    if (account == null || (int)account.Level < 1)
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Register to use this command."); message.Supress = true; return message;
                    }
                    if (message.Sender.afk) { ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "You are already AFK."); message.Supress = true; return message; }
                    message.Sender.afk = true;
                    ServerInstance.SendChatMessageToAll(message.Sender.DisplayName, "has gone AFK."); message.Supress = true; return message;
                }
                if (message.Message.ToLower().Equals("/back"))
                {
                    if (account == null || (int)account.Level < 1)
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Register to use this command."); message.Supress = true; return message;
                    }
                    if (!message.Sender.afk) { ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "You are not AFK."); message.Supress = true; return message; }
                    ServerInstance.SendChatMessageToAll(message.Sender.DisplayName, "is now back."); message.Sender.afk = false; message.Supress = true; return message;

                }
                if (message.Message.ToLower().Equals("/l"))
                {
                    if (account == null || (int)account.Level < 5)
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Insufficent privileges."); message.Supress = true; return message;
                    }
                    for (var i = 0; i < ServerInstance.Clients.Count; i++)
                    {
                        try
                        {
                            Client target = ServerInstance.Clients[i];
                            Console.WriteLine($"Nickname: {target.DisplayName.ToString()} | " +
                                              $"Realname: {target.Name.ToString()} |" +
                                              $"Ping: {Math.Round(target.Latency * 1000, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)}ms | " +
                                              $"IP: {target.NetConnection.RemoteEndPoint.Address.ToString()} | " +
                                              $"Game Version: {target.GameVersion.ToString()} | " +
                                              $"Script Version: {target.RemoteScriptVersion.ToString()} | " +
                                              $"Vehicle Health: {target.VehicleHealth.ToString()} | " +
                                              $"Last Position: {target.LastKnownPosition.ToString()} | ");
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                    ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Printed playerlist to console."); message.Supress = true; return message;
                }
                if (message.Message.ToLower().StartsWith("/info"))
                {
                    if (account == null || (int)account.Level < 4)
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Insufficent privileges."); message.Supress = true; return message;
                    }
                    var args = message.Message.Split();
                    if (args.Length <= 1)
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "USAGE", "/info [Player Name]"); message.Supress = true; return message;
                    }
                    Client target = null;
                    lock (ServerInstance.Clients) target = ServerInstance.Clients.FirstOrDefault(c => c.DisplayName.ToLower().StartsWith(args[1].ToLower()));

                    if (target == null)
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "ERROR", "No such player found: " + args[1]);
                        message.Supress = true; return message;
                    }
                    ServerInstance.SendChatMessageToPlayer(message.Sender, "1/2",
                        $"Nickname: {target.DisplayName.ToString()}\n" + $"Realname: {target.Name.ToString()}\n" +
                        $"Ping: {Math.Round(target.Latency * 1000, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)}ms\n" +
                        $"IP: {target.NetConnection.RemoteEndPoint.Address.ToString()}");
                    ServerInstance.SendChatMessageToPlayer(message.Sender, "2/2",
                        $"Game Version: {target.GameVersion.ToString()}\n" +
                        $"Script Version: {target.RemoteScriptVersion.ToString()}\n" +
                        $"Vehicle Health: {target.VehicleHealth.ToString()}\n" +
                        $"Last Position: {target.LastKnownPosition.ToString()}\n");
                    message.Supress = true; return message;
                }
                if (message.Message.ToLower().StartsWith("/nick"))
                {
                    if (account == null || (int)account.Level < 4)
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Insufficent privileges."); message.Supress = true; return message;
                    }
                    var args = message.Message.Split();
                    if (args.Length <= 1)
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "USAGE", "/tp [Player Name]"); message.Supress = true; return message;
                    }
                    message.Sender.DisplayName = args[1]; message.Supress = true; return message;
                }
                if (message.Message.ToLower().StartsWith("/stop"))
                {
                    if (account == null || (int)account.Level < 5)
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Insufficent privileges."); message.Supress = true; return message;
                    }
                    ServerInstance.SendChatMessageToAll("SERVER", "This server will stop now!");
                    Environment.Exit(-1); message.Supress = true; return message;
                }
                /*if (message.Message.StartsWith("/restart"))
                {
                    if (account == null || (int)account.Level < 5)
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Insufficent privileges.");
                        message.Supress = true; return message;
                    }
                    ServerInstance.SendChatMessageToAll("SERVER", "~p~This server will restart now. Please reconnect!~p~");
                    try
                    {
                        //process = System.Diagnostics.Process[] GetProcessesByName("GTAServer.exe";
                        //Process[] processes = Process.GetProcessesByName("GTAServer.exe");
                        //processes[0].WaitForExit(1000);
                        Environment.Exit(-1);
                    }
                    catch (ArgumentException ex) { ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Could not restart."); }
                    Process.Start("GTAServer.exe", ""); message.Supress = true; return message;
                }*/
                if (message.Message.ToLower().StartsWith("/tp"))
                {
                    if (account == null || (int)account.Level < 3)
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Insufficent privileges.");
                        message.Supress = true; return message;
                    }

                    var args = message.Message.Split();
                    if (args.Length <= 1)
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "USAGE", "/tp [Player Name]");
                        message.Supress = true; return message;
                    }

                    Client target = null;
                    lock (ServerInstance.Clients) target = ServerInstance.Clients.FirstOrDefault(c => c.DisplayName.ToLower().StartsWith(args[1].ToLower()));

                    if (target == null)
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "ERROR", "No such player found: " + args[1]);
                        message.Supress = true; return message;
                    }

                    ServerInstance.GetPlayerPosition(target, o =>
                    {
                        var newPos = (Vector3)o;
                        ServerInstance.SetPlayerPosition(message.Sender, newPos);
                    });

                    Console.WriteLine(
                        $"ADMINTOOLS: {account.Name + " (" + message.Sender.DisplayName + ")"} has teleported to player {target.Name + " (" + target.DisplayName + ")"}");

                    message.Supress = true; return message;
                }

                if (message.Message.ToLower().StartsWith("/godmode"))
                {
                    var args = message.Message.Split();
                    if (args.Length <= 1)
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "USAGE", "/godmode [Player Name]");
                        message.Supress = true; return message;
                    }

                    Client target = null;
                    lock (ServerInstance.Clients) target = ServerInstance.Clients.FirstOrDefault(c => c.DisplayName.ToLower().StartsWith(args[1].ToLower()));

                    if (target == null)
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "ERROR", "No such player found: " + args[1]);
                        message.Supress = true; return message;
                    }

                    string salt = "inv+" + target.NetConnection.RemoteUniqueIdentifier;

                    ServerInstance.GetNativeCallFromPlayer(target, salt, 0xB721981B2B939E07, new BooleanArgument(),
                        (o) =>
                        {
                            bool isInvincible = (bool)o;
                            ServerInstance.SendChatMessageToPlayer(message.Sender,
                                $"Player {target.DisplayName} is {(isInvincible ? "~g~invincible." : "~r~mortal.")}");
                        }, new LocalGamePlayerArgument());

                    message.Supress = true; return message;
                }

                if (message.Message.ToLower().StartsWith("/weather"))
                {
                    if (account == null || (int)account.Level < 4)
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Insufficent privileges.");
                        message.Supress = true; return message;
                    }

                    var args = message.Message.Split();
                    if (args.Length <= 1)
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "USAGE", "/weather [Weather ID]");
                        message.Supress = true; return message;
                    }

                    int newWeather;
                    if (!int.TryParse(args[1], out newWeather))
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "USAGE", "/weather [Weather ID]");
                        message.Supress = true; return message;
                    }

                    if (newWeather < 0 || newWeather >= _weatherNames.Length)
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "USAGE", "Weather ID must be between 0 and " + (_weatherNames.Length - 1));
                        message.Supress = true; return message;
                    }

                    ServerWeather = newWeather;
                    ServerInstance.SendNativeCallToAllPlayers(0x29B487C359E19889, _weatherNames[ServerWeather]);
                    Console.WriteLine(
                        $"ADMINTOOLS: {account.Name + " (" + message.Sender.DisplayName + ")"} has changed the weather to {ServerWeather}");
                    ServerInstance.SendChatMessageToAll("(" + account.Level.ToString() + ") " + message.Sender.DisplayName + " changed the weather to " + ServerWeather);

                    message.Supress = true; return message;
                }

                if (message.Message.ToLower().StartsWith("/time"))
                {
                    if (account == null || (int)account.Level < 4)
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Insufficent privileges.");
                        message.Supress = true; return message;
                    }

                    var args = message.Message.Split();
                    if (args.Length <= 1)
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "USAGE", "/time [hours]:[minutes]");
                        message.Supress = true; return message;
                    }

                    int hours;
                    int minutes;
                    var timeSplit = args[1].Split(':');

                    if (timeSplit.Length < 2 || !int.TryParse(timeSplit[0], out hours) || !int.TryParse(timeSplit[1], out minutes))
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "USAGE", "/time [hours]:[minutes]");
                        message.Supress = true; return message;
                    }

                    if (hours < 0 || hours > 24 || minutes < 0 || minutes > 60)
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "USAGE", "/time [hours]:[minutes]");
                        message.Supress = true; return message;
                    }

                    ServerTime = new TimeSpan(hours, minutes, 0);

                    ServerInstance.SendNativeCallToAllPlayers(0x47C3B5848C3E45D8, ServerTime.Hours, ServerTime.Minutes, ServerTime.Seconds);
                    ServerInstance.SendNativeCallToAllPlayers(0x4055E40BD2DBEC1D, true);

                    Console.WriteLine(
                        $"ADMINTOOLS: {account.Name + " (" + message.Sender.DisplayName + ")"} has changed the time to {ServerTime}");
                    ServerInstance.SendChatMessageToAll("(" + account.Level.ToString() + ") " + message.Sender.DisplayName + " changed the time to " + ServerTime);

                    message.Supress = true; return message;
                }

                if (message.Message.ToLower().StartsWith("/kill"))
                {
                    if (account == null || (int)account.Level < 4)
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Insufficent privileges.");
                        message.Supress = true; return message;
                    }

                    var args = message.Message.Split();
                    if (args.Length <= 1)
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "USAGE", "/kill [Player Name]");
                        message.Supress = true; return message;
                    }

                    Client target = null;
                    lock (ServerInstance.Clients) target = ServerInstance.Clients.FirstOrDefault(c => c.DisplayName.ToLower().StartsWith(args[1].ToLower()));

                    if (target == null)
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "ERROR", "No such player found: " + args[1]);
                        message.Supress = true; return message;
                    }

                    ServerInstance.SetPlayerHealth(target, -1);
                    Console.WriteLine(
                        $"ADMINTOOLS: {account.Name + " (" + message.Sender.DisplayName + ")"} has killed player {target.Name + " (" + target.DisplayName + ")"}");
                    message.Supress = true; return message;
                }

                if (message.Message.ToLower().StartsWith("/ban"))
                {
                    if (account == null || (int)account.Level < 3)
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Insufficent privileges.");
                        message.Supress = true; return message;
                    }

                    var args = message.Message.Split();
                    if (args.Length <= 2)
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "USAGE", "/ban [Player Name] [Reason]");
                        message.Supress = true; return message;
                    }

                    Client target = null;
                    lock (ServerInstance.Clients) target = ServerInstance.Clients.FirstOrDefault(c => c.DisplayName.ToLower().StartsWith(args[1].ToLower()));

                    if (target == null)
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "ERROR", "No such player found: " + args[1]);
                        message.Supress = true; return message;
                    }

                    target.Ban(args[2], message.Sender);

                    SaveBanlist(Location + "Banlist.xml");

                    Console.WriteLine(
                        $"ADMINTOOLS: {account.Name + " (" + message.Sender.DisplayName + ")"} has banned player {target.Name + " (" + target.DisplayName + ")"} with reason: {args[2]}");
                    ServerInstance.KickPlayer(target, "You have been banned: " + args[2]);
                    message.Supress = true; return message;
                }

                if (message.Message.ToLower().StartsWith("/kick"))
                {
                    if (account == null || (int)account.Level < 3)
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Insufficent privileges.");
                        message.Supress = true; return message;
                    }

                    var args = message.Message.Split();
                    if (args.Length <= 2)
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "USAGE", "/kick [Player Name] [Reason]");
                        message.Supress = true; return message;
                    }

                    Client target = null;
                    lock (ServerInstance.Clients) target = ServerInstance.Clients.FirstOrDefault(c => c.DisplayName.ToLower().StartsWith(args[1].ToLower()));

                    if (target == null)
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "ERROR", "No such player found: " + args[1]);
                        message.Supress = true; return message;
                    }

                    ServerInstance.KickPlayer(target, args[2], false, message.Sender);
                    Console.WriteLine(
                        $"SERVER: {account.Name + " (" + message.Sender.DisplayName + ")"} has kicked player {target.Name + " (" + target.DisplayName + ")"}");
                    message.Supress = true; return message;
                }

                if (message.Message.ToLower().StartsWith("/register"))
                {
                    account = message.Sender.GetAccount(false);
                    if (account != null)
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "You already have an account.");
                        message.Supress = true; return message;
                    }

                    var args = message.Message.Split();
                    if (args.Length <= 1)
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "USAGE", "/register [Password]");
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
                    lock (Lists.Accounts.Accounts) Lists.Accounts.Accounts.Add(accObject);
                    SaveAccounts(Location + "Accounts.xml");
                    lock (Lists.AuthenticatedUsers) Lists.AuthenticatedUsers.Add(message.Sender.NetConnection.RemoteUniqueIdentifier);

                    ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Your account has been created!");
                    ServerInstance.PrintPlayerInfo(message.Sender, "New Player registered: ");
                    message.Supress = true; return message;
                }

                if (message.Message.ToLower().StartsWith("/login"))
                {
                    if (account != null)
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "You are already authenticated.");
                        message.Supress = true; return message;
                    }

                    account = message.Sender.GetAccount(false);

                    if (account == null)
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "No accounts have been found with your name.");
                        message.Supress = true; return message;
                    }

                    var args = message.Message.Split();
                    if (args.Length <= 1)
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "USAGE", "/login [Password]");
                        message.Supress = true; return message;
                    }

                    var password = GetHashSha256(args[1]);

                    if (password != account.Password)
                    {
                        ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Wrong password.");
                        message.Supress = true; return message;
                    }

                    lock (Lists.AuthenticatedUsers) if (!Lists.AuthenticatedUsers.Contains(message.Sender.NetConnection.RemoteUniqueIdentifier)) Lists.AuthenticatedUsers.Add(message.Sender.NetConnection.RemoteUniqueIdentifier);

                    ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "Authentication successful!");
                    ServerInstance.PrintPlayerInfo(message.Sender, account.Level.ToString() + " logged in: ");
                    //LogToConsole(2, false, "Accounting", string.Format("{0} \"{1}\" logged in.", account.Level.ToString(), message.Sender.DisplayName));
                }

                switch (message.Message.ToLower())
                {
                    case "/logout":
                        if (message.Sender.IsAuthenticated())
                        {
                            Console.WriteLine($"SERVER: Player has logged out: {message.Sender.Name}");
                            ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "You have been logged out.");

                            lock (Lists.AuthenticatedUsers) if (Lists.AuthenticatedUsers.Contains(message.Sender.NetConnection.RemoteUniqueIdentifier)) Lists.AuthenticatedUsers.Remove(message.Sender.NetConnection.RemoteUniqueIdentifier);
                        }
                        else
                        {
                            ServerInstance.SendChatMessageToPlayer(message.Sender, "SERVER", "You are not logged in.");
                        }
                        message.Supress = true; return message;
                    case "/countdown":
                        if (DateTime.Now.Subtract(_lastCountdown).TotalSeconds < 30)
                        {
                            ServerInstance.SendChatMessageToPlayer(message.Sender, "COUNTDOWN", "Please wait 30 seconds before starting another countdown.");
                            message.Supress = true; return message;
                        }

                        _lastCountdown = DateTime.Now;

                        var cdThread = new Thread((ThreadStart)delegate
                       {
                           for (int i = 3; i >= 0; i--)
                           {
                               ServerInstance.SendChatMessageToAll("COUNTDOWN", i == 0 ? "Go!" : i.ToString());
                               Thread.Sleep(1000);
                           }
                       });
                        cdThread.Start();
                        message.Supress = true; return message;
                }

                if (!message.Sender.NetConnection.RemoteEndPoint.Address.ToString().Equals("127.0.0.1"))
                {
                    try { message.Prefix = message.Sender.geoIP.Country.IsoCode.ToString(); } catch (Exception ex) { LogToConsole(3, false, "GeoIP", ex.Message); }
                }
                try { message.Suffix = account.Level.ToString(); } catch { message.Suffix = "Guest"; }
                if (message.Message.ToLower().Contains("login") || message.Message.ToLower().Contains("register") || message.Message.ToLower().Equals("urtle") || message.Message.ToLower().Equals("turtle")) { message.Supress = true; return message; }
                return message;
            }
            catch (Exception ex) { LogToConsole(4, false, "Chat", "Can't handle message: " + ex.Message); return null; }
        }

        public override bool OnPlayerDisconnect(Client player)
        {
            lock (Lists.AuthenticatedUsers) if (Lists.AuthenticatedUsers.Contains(player.NetConnection.RemoteUniqueIdentifier)) Lists.AuthenticatedUsers.Remove(player.NetConnection.RemoteUniqueIdentifier);

            if (player.IsBanned() || player.IsIPBanned()) return false;

            return true;
        }

        public override void OnPlayerSpawned(Client player)
        {
            try { ServerInstance.SendNativeCallToPlayer(player, 0x29B487C359E19889, _weatherNames[ServerWeather]); } catch { }
            try { ServerInstance.SendNativeCallToPlayer(player, 0x47C3B5848C3E45D8, ServerTime.Hours, ServerTime.Minutes, ServerTime.Seconds); } catch { }
            try { ServerInstance.SendNativeCallToPlayer(player, 0x4055E40BD2DBEC1D, true); } catch { }
        }

        private static AdminSettings ReadSettings(string path)
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
                    Lists.Accounts = (UserList)ser.Deserialize(stream);
                }
            }
            else
            {
                Lists.Accounts = new UserList {Accounts = new List<Account>()};
                SaveAccounts(path);
            }
        }

        public void SaveAccounts(string path)
        {
            XmlSerializer ser = new XmlSerializer(typeof(UserList));
            using (var stream = new FileStream(path, File.Exists(path) ? FileMode.Truncate : FileMode.Create, FileAccess.ReadWrite))
            {
                ser.Serialize(stream, Lists.Accounts);
            }
        }

        private static void LoadBanlist(string path)
        {
            XmlSerializer ser = new XmlSerializer(typeof(Banlist));
            if (File.Exists(path))
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite))
                {
                    Lists.Banned = (Banlist)ser.Deserialize(stream);
                }

                Lists.Accounts.Accounts.ForEach(acc =>
                {
                    acc.Ban = null;
                    Lists.Banned.BannedIps.Any(b =>
                    {
                        if (b.Name != acc.Name) return false;
                        acc.Ban = b;

                        return true;
                    });
                });
            }
            else
            {
                Lists.Banned = new Banlist { BannedIps = new List<Ban>() };
                SaveBanlist(path);
            }
        }

        private static void SaveBanlist(string path)
        {
            XmlSerializer ser = new XmlSerializer(typeof(Banlist));
            using (var stream = new FileStream(path, File.Exists(path) ? FileMode.Truncate : FileMode.Create, FileAccess.ReadWrite))
            {
                ser.Serialize(stream, Lists.Banned);
            }
        }

        private static string GetHashSha256(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            SHA256Managed hashstring = new SHA256Managed();
            byte[] hash = hashstring.ComputeHash(bytes);

            StringBuilder hashString = new StringBuilder();
            foreach (byte x in hash)
            {
                hashString.Append($"{x:x2}");
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

    public class IpInfo
    {
        public string CountryCode { get; internal set; }
        public List<string> List { get; set; }
        public object Status { get; internal set; }
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
            lock (Lists.AuthenticatedUsers) return Lists.AuthenticatedUsers.Contains(client.NetConnection.RemoteUniqueIdentifier);
        }

        public static Account GetAccount(this Client client, bool checkauthentication = true)
        {
            if (!checkauthentication || client.IsAuthenticated()) lock (Lists.Accounts.Accounts) return Lists.Accounts.Accounts.FirstOrDefault(acc => acc.Name == client.DisplayName);

            return null;
        }

        public static bool IsIPBanned(this Client client)
        {
            bool banned = false;
            lock (Lists.Banned.BannedIps)
            {
                banned = Lists.Banned.BannedIps.Any(b =>
                {
                    if (b.Address == client.NetConnection.RemoteEndPoint.Address.ToString())
                    {
                        try
                        {
                            client.GetAccount().Ban = b;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Check for ban of player \"{client.DisplayName}\" failed: {ex.Message}");
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

            return account?.Ban;
        }

        public static void Ban(this Client client, string reason, Client issuedBy = null)
        {
            Ban ban = new Ban()
            {
                Address = client.NetConnection.RemoteEndPoint.Address.ToString(),
                BannedBy = issuedBy == null ? "Server" : issuedBy.DisplayName,
                Reason = reason,
                TimeIssued = DateTime.Now,
                Name = client.DisplayName
            };

            lock (Lists.Banned.BannedIps) Lists.Banned.BannedIps.Add(ban);
            try
            {
                client.GetAccount().Ban = ban;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ban of player \"{client.DisplayName}\" failed: {ex.ToString()}");
            }
        }

        public static Client GetClient(this Account account)
        {
            Client client = null;
            lock (AdminToolsServerScript.ServerInstance.Clients)
            {
                var any = AdminToolsServerScript.ServerInstance.Clients.Any(c =>
                {
                    if (c.DisplayName != account.Name) return false;
                    client = c;

                    return true;
                });
            }

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