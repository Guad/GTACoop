using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;
using GTAServer;
using Lidgren.Network;

namespace AdminTools
{
    [Serializable]
    public class TestServerScript : ServerScript
    {
        public static string Location { get { return AppDomain.CurrentDomain.BaseDirectory; } }
        public override string Name { get { return "Server Administration Tools"; } }

        public int ServerWeather;
        public TimeSpan ServerTime;
        public bool IsGodmodeDisabled;

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
            LoadAccounts(Location + "Accounts.xml");
            LoadBanlist(Location + "Banlist.xml");
            _authenticatedUsers = new List<long>();
            Console.WriteLine("Accounts have been loaded.");

            ServerWeather = 0;
            ServerTime = new TimeSpan(12, 0, 0);
        }

        public override bool OnPlayerConnect(Client player)
        {
            lock (_banned.BannedIps)
            {
                if (_banned.BannedIps.Any(b => b.Address == player.NetConnection.RemoteEndPoint.Address.ToString()))
                {
                    Program.ServerInstance.KickPlayer(player,
                        "You are banned: " +
                        _banned.BannedIps.First(b => b.Address == player.NetConnection.RemoteEndPoint.Address.ToString())
                            .Reason);
                    return false;
                }
            }

            Account account = null;
            lock (_accounts.Accounts) account = _accounts.Accounts.FirstOrDefault(acc => acc.Name == player.Name);

            if (account == null)
            {
                Program.ServerInstance.SendChatMessageToPlayer(player, "ACCOUNT", "You can register an account using /register [password]");
            }
            else
            {
                Program.ServerInstance.SendChatMessageToPlayer(player, "ACCOUNT", "Please authenticate to your account using /login [password]");
            }

            Program.ServerInstance.SendNativeCallToPlayer(player, 0x29B487C359E19889, _weatherNames[ServerWeather]);

            Program.ServerInstance.SendNativeCallToPlayer(player, 0x47C3B5848C3E45D8, ServerTime.Hours, ServerTime.Minutes, ServerTime.Seconds);
            Program.ServerInstance.SendNativeCallToPlayer(player, 0x4055E40BD2DBEC1D, true);

            if (IsGodmodeDisabled)
            {
                Program.ServerInstance.SetNativeCallOnTickForPlayer(player, "GODMODE", 0x239528EACDC3E7DE, new LocalGamePlayerArgument(), false);
            }

            return true;
        }

        public override bool OnChatMessage(Client sender, string message)
        {
            bool authenticated = false;
            lock (_authenticatedUsers) authenticated = _authenticatedUsers.Contains(sender.NetConnection.RemoteUniqueIdentifier);

            Account account = null;
            lock (_accounts.Accounts) if (authenticated) account = _accounts.Accounts.FirstOrDefault(acc => acc.Name == sender.Name);

            if (message.StartsWith("/tp"))
            {
                if (account == null || (int)account.Level < 1)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "ACCESS DENIED", "Insufficent privileges.");
                    return false;
                }

                var args = message.Split();
                if (args.Length <= 1)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "USAGE", "/tp [Player Name]");
                    return false;
                }

                Client target = null;
                lock (Program.ServerInstance.Clients) target = Program.ServerInstance.Clients.FirstOrDefault(c => c.DisplayName.ToLower().StartsWith(args[1].ToLower()));

                if (target == null)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "ERROR", "No such player found: " + args[1]);
                    return false;
                }

                Program.ServerInstance.GetPlayerPosition(target, o =>
                {
                    var newPos = (Vector3)o;
                    Program.ServerInstance.TeleportPlayer(sender, newPos);
                });

                Console.WriteLine(string.Format("ADMINTOOLS: {0} has teleported to player {1}", account.Name + " (" + sender.DisplayName + ")", target.Name + " (" + target.DisplayName + ")"));

                return false;
            }

            if (message.StartsWith("/godmode"))
            {
                if (account == null || (int)account.Level < 1)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "ACCESS DENIED", "Insufficent privileges.");
                    return false;
                }

                var args = message.Split();
                if (args.Length <= 1)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "USAGE", "/godmode [true/false]");
                    return false;
                }

                bool newValue;
                if (!bool.TryParse(args[1], out newValue))
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "USAGE", "/godmode [true/false]");
                    return false;
                }
                IsGodmodeDisabled = !newValue;

                if (IsGodmodeDisabled)
                {
                    Program.ServerInstance.SetNativeCallOnTickForAllPlayers("GODMODE", 0x239528EACDC3E7DE, new LocalGamePlayerArgument(), false);
                }
                else
                {
                    Program.ServerInstance.RecallNativeCallOnTickForAllPlayers("GODMODE");
                }

                Console.WriteLine(string.Format("ADMINTOOLS: {0} has set global god mode disabling to {1}", account.Name + " (" + sender.DisplayName + ")", !newValue));

                return false;
            }

            if (message.StartsWith("/weather"))
            {
                if (account == null || (int)account.Level < 1)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "ACCESS DENIED", "Insufficent privileges.");
                    return false;
                }

                var args = message.Split();
                if (args.Length <= 1)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "USAGE", "/weather [Weather ID]");
                    return false;
                }

                int newWeather;
                if (!int.TryParse(args[1], out newWeather))
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "USAGE", "/weather [Weather ID]");
                    return false;
                }

                if (newWeather < 0 || newWeather >= _weatherNames.Length)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "USAGE", "Weather ID must be between 0 and " + (_weatherNames.Length-1));
                    return false;
                }

                ServerWeather = newWeather;
                Program.ServerInstance.SendNativeCallToAllPlayers(0x29B487C359E19889, _weatherNames[ServerWeather]);

                Console.WriteLine(string.Format("ADMINTOOLS: {0} has changed the weather to {1}", account.Name + " (" + sender.DisplayName + ")", ServerWeather));

                return false;
            }

            if (message.StartsWith("/time"))
            {
                if (account == null || (int)account.Level < 1)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "ACCESS DENIED", "Insufficent privileges.");
                    return false;
                }

                var args = message.Split();
                if (args.Length <= 1)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "USAGE", "/time [hours]:[minutes]");
                    return false;
                }

                int hours;
                int minutes;
                var timeSplit = args[1].Split(':');

                if (timeSplit.Length < 2 || !int.TryParse(timeSplit[0], out hours) || !int.TryParse(timeSplit[1], out minutes))
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "USAGE", "/time [hours]:[minutes]");
                    return false;
                }

                if (hours < 0 || hours > 24 || minutes < 0 || minutes > 60)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "USAGE", "/time [hours]:[minutes]");
                    return false;
                }

                ServerTime = new TimeSpan(hours, minutes, 0);

                Program.ServerInstance.SendNativeCallToAllPlayers(0x47C3B5848C3E45D8, ServerTime.Hours, ServerTime.Minutes, ServerTime.Seconds);
                Program.ServerInstance.SendNativeCallToAllPlayers(0x4055E40BD2DBEC1D, true);

                Console.WriteLine(string.Format("ADMINTOOLS: {0} has changed the time to {1}", account.Name + " (" + sender.DisplayName + ")", ServerTime));

                return false;
            }

            if (message.StartsWith("/kill"))
            {
                if (account == null || (int)account.Level < 1)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "ACCESS DENIED", "Insufficent privileges.");
                    return false;
                }

                var args = message.Split();
                if (args.Length <= 1)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "USAGE", "/kill [Player Name]");
                    return false;
                }

                Client target = null;
                lock (Program.ServerInstance.Clients) target = Program.ServerInstance.Clients.FirstOrDefault(c => c.DisplayName.ToLower().StartsWith(args[1].ToLower()));

                if (target == null)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "ERROR", "No such player found: " + args[1]);
                    return false;
                }

                Program.ServerInstance.SetPlayerHealth(target, -1);
                Console.WriteLine(string.Format("ADMINTOOLS: {0} has killed player {1}", account.Name + " (" + sender.DisplayName + ")", target.Name + " (" + target.DisplayName + ")"));
                return false;
            }

            if (message.StartsWith("/ban"))
            {
                if (account == null || (int)account.Level < 2)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "ACCESS DENIED", "Insufficent privileges.");
                    return false;
                }

                var args = message.Split();
                if (args.Length <= 2)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "USAGE", "/ban [Player Name] [Reason]");
                    return false;
                }

                Client target = null;
                lock (Program.ServerInstance.Clients) target = Program.ServerInstance.Clients.FirstOrDefault(c => c.DisplayName.ToLower().StartsWith(args[1].ToLower()));

                if (target == null)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "ERROR", "No such player found: " + args[1]);
                    return false;
                }

                lock (_banned.BannedIps)
                {
                    _banned.BannedIps.Add(new Ban()
                    {
                        Address = target.NetConnection.RemoteEndPoint.Address.ToString(),
                        BannedBy = account.Name,
                        Reason = args[2],
                        TimeIssued = DateTime.Now,
                        Name = target.Name,
                    });
                }

                SaveBanlist(Location + "Banlist.xml");

                Console.WriteLine(string.Format("ADMINTOOLS: {0} has banned player {1} with reason: {2}", account.Name + " (" + sender.DisplayName + ")", target.Name + " (" + target.DisplayName + ")", args[2]));
                Program.ServerInstance.KickPlayer(target, "You have been banned: " + args[2]);
                return false;
            }

            if (message.StartsWith("/kick"))
            {
                if (account == null || (int)account.Level < 1)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "ACCESS DENIED", "Insufficent privileges.");
                    return false;
                }

                var args = message.Split();
                if (args.Length <= 2)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "USAGE", "/kick [Player Name] [Reason]");
                    return false;
                }

                Client target = null;
                lock (Program.ServerInstance.Clients) target = Program.ServerInstance.Clients.FirstOrDefault(c => c.DisplayName.ToLower().StartsWith(args[1].ToLower()));

                if (target == null)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "ERROR", "No such player found: " + args[1]);
                    return false;
                }

                Program.ServerInstance.KickPlayer(target, args[2]);
                Console.WriteLine(string.Format("ADMINTOOLS: {0} has kicked player {1}", account.Name + " (" + sender.DisplayName + ")", target.Name + " (" + target.DisplayName + ")"));
                return false;
            }

            if (message.StartsWith("/register"))
            {
                lock (_accounts.Accounts) account = _accounts.Accounts.FirstOrDefault(acc => acc.Name == sender.Name);
                if (account != null)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "ERROR", "You already have an account.");
                    return false;
                }

                var args = message.Split();
                if (args.Length <= 1)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "USAGE", "/register [Password]");
                    return false;
                }

                var password = GetHashSha256(args[1]);
                var accObject = new Account()
                {
                    Level = Privilege.User,
                    Name = sender.Name,
                    Password = password,
                };
                lock (_accounts.Accounts) _accounts.Accounts.Add(accObject);
                SaveAccounts(Location + "Accounts.xml");
                lock (_authenticatedUsers) _authenticatedUsers.Add(sender.NetConnection.RemoteUniqueIdentifier);

                Program.ServerInstance.SendChatMessageToPlayer(sender, "ACCOUNT", "Your account has been created!");
                Console.WriteLine(string.Format("ADMINTOOLS: New player registered: {0}", accObject.Name));
                return false;
            }

            if (message.StartsWith("/login"))
            {
                if (account != null)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "ERROR", "You are already authenticated.");
                    return false;
                }

                lock (_accounts.Accounts) account = _accounts.Accounts.FirstOrDefault(acc => acc.Name == sender.Name);

                if (account == null)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "ERROR", "No accounts have been found with your name.");
                    return false;
                }

                var args = message.Split();
                if (args.Length <= 1)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "USAGE", "/login [Password]");
                    return false;
                }

                var password = GetHashSha256(args[1]);

                if (password != account.Password)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "ERROR", "Wrong password.");
                    return false;
                }

                lock (_authenticatedUsers) _authenticatedUsers.Add(sender.NetConnection.RemoteUniqueIdentifier);

                Program.ServerInstance.SendChatMessageToPlayer(sender, "ACCOUNT", "Authentication successful!");
                Console.WriteLine(string.Format("ADMINTOOLS: New player logged in: {0}", account.Name + " (" + sender.DisplayName + ")"));
                return false;
            }


            if (message == "/logout")
            {
                if (authenticated)
                {
                    Console.WriteLine(string.Format("ADMINTOOLS: Player has logged out: {0}", sender.Name));
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "ACCOUNT", "You have been logged out.");

                    lock (_authenticatedUsers) _authenticatedUsers.Remove(sender.NetConnection.RemoteUniqueIdentifier);
                }
                else
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "ACCOUNT", "You are not logged in.");
                }
                return false;
            }

            return true;
        }

        public override bool OnPlayerDisconnect(Client player)
        {
            lock (_authenticatedUsers) if (_authenticatedUsers.Contains(player.NetConnection.RemoteUniqueIdentifier)) _authenticatedUsers.Remove(player.NetConnection.RemoteUniqueIdentifier);
            lock (_banned)
            {
                if (_banned.BannedIps.Any(b => b.Address == player.NetConnection.RemoteEndPoint.Address.ToString()))
                    return false;
            }

            return true;
        }

        private UserList _accounts;
        private Banlist _banned;
        private List<long> _authenticatedUsers;

        private void LoadAccounts(string path)
        {
            XmlSerializer ser = new XmlSerializer(typeof(UserList));
            if (File.Exists(path))
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite))
                {
                    _accounts = (UserList)ser.Deserialize(stream);
                }
            }
            else
            {
                _accounts = new UserList();
                _accounts.Accounts = new List<Account>();
                SaveAccounts(path);
            }
        }

        private void SaveAccounts(string path)
        {
            XmlSerializer ser = new XmlSerializer(typeof(UserList));
            using (var stream = new FileStream(path, File.Exists(path) ? FileMode.Truncate : FileMode.Create, FileAccess.ReadWrite))
            {
                ser.Serialize(stream, _accounts);
            }
        }

        private void LoadBanlist(string path)
        {
            XmlSerializer ser = new XmlSerializer(typeof(Banlist));
            if (File.Exists(path))
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite))
                {
                    _banned = (Banlist)ser.Deserialize(stream);
                }
            }
            else
            {
                _banned = new Banlist();
                _banned.BannedIps = new List<Ban>();
                SaveBanlist(path);
            }
        }

        private void SaveBanlist(string path)
        {
            XmlSerializer ser = new XmlSerializer(typeof(Banlist));
            using (var stream = new FileStream(path, File.Exists(path) ? FileMode.Truncate : FileMode.Create, FileAccess.ReadWrite))
            {
                ser.Serialize(stream, _banned);
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
    }

    public enum Privilege
    {
        User = 0,
        Moderator = 1,
        Administrator = 2,
        Owner = 3,
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