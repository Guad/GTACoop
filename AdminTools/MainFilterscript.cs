using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml.Serialization;
using GTAServer;
using Lidgren.Network;

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
        public static string Location { get { return AppDomain.CurrentDomain.BaseDirectory; } }
        public override string Name { get { return "Server Administration Tools"; } }

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
            LoadAccounts(Location + "Accounts.xml");
            LoadBanlist(Location + "Banlist.xml");
            Console.WriteLine("Accounts have been loaded.");

            ServerWeather = 0;
            ServerTime = new TimeSpan(12, 0, 0);
        }

        public override bool OnPlayerConnect(Client player)
        {
            if(player.IsBanned() || player.IsIPBanned())
            {
                Program.ServerInstance.KickPlayer(player, "You are banned: " + player.GetBan().Reason);

                return false;
            }

            if (player.GetAccount(false) == null)
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

            return true;
        }

        public override bool OnChatMessage(Client sender, string message)
        {
            Account account = sender.GetAccount();

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
                var args = message.Split();
                if (args.Length <= 1)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "USAGE", "/godmode [Player Name]");
                    return false;
                }

                Client target = null;
                lock (Program.ServerInstance.Clients) target = Program.ServerInstance.Clients.FirstOrDefault(c => c.DisplayName.ToLower().StartsWith(args[1].ToLower()));

                if (target == null)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "ERROR", "No such player found: " + args[1]);
                    return false;
                }

                string salt = "inv+" + target.NetConnection.RemoteUniqueIdentifier;

                Program.ServerInstance.GetNativeCallFromPlayer(target, salt, 0xB721981B2B939E07, new BooleanArgument(),
                    (o) =>
                    {
                        bool isInvincible = (bool) o;
                        Program.ServerInstance.SendChatMessageToPlayer(sender, string.Format("Player {0} is {1}", target.DisplayName, isInvincible ? "~g~invincible." : "~r~mortal."));
                    }, new LocalGamePlayerArgument());

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

                target.Ban(args[2], sender);

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
                account = sender.GetAccount(false);
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
                    Ban = null
                };
                lock (Lists._accounts.Accounts) Lists._accounts.Accounts.Add(accObject);
                SaveAccounts(Location + "Accounts.xml");
                lock (Lists._authenticatedUsers) Lists._authenticatedUsers.Add(sender.NetConnection.RemoteUniqueIdentifier);

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

                account = sender.GetAccount(false);

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

                lock (Lists._authenticatedUsers) if (!Lists._authenticatedUsers.Contains(sender.NetConnection.RemoteUniqueIdentifier)) Lists._authenticatedUsers.Add(sender.NetConnection.RemoteUniqueIdentifier);

                Program.ServerInstance.SendChatMessageToPlayer(sender, "ACCOUNT", "Authentication successful!");
                Console.WriteLine(string.Format("ADMINTOOLS: New player logged in: {0}", account.Name + " (" + sender.DisplayName + ")"));
                return false;
            }


            if (message == "/logout")
            {
                if (sender.IsAuthenticated())
                {
                    Console.WriteLine(string.Format("ADMINTOOLS: Player has logged out: {0}", sender.Name));
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "ACCOUNT", "You have been logged out.");

                    lock (Lists._authenticatedUsers) if (Lists._authenticatedUsers.Contains(sender.NetConnection.RemoteUniqueIdentifier)) Lists._authenticatedUsers.Remove(sender.NetConnection.RemoteUniqueIdentifier);
                }
                else
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "ACCOUNT", "You are not logged in.");
                }
                return false;
            }

            if (message == "/countdown")
            {
                if (DateTime.Now.Subtract(_lastCountdown).TotalSeconds < 30)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "COUNTDOWN", "Please wait 30 seconds before starting another countdown.");
                    return false;
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
                return false;
            }

            return true;
        }

        public override bool OnPlayerDisconnect(Client player)
        {
            lock (Lists._authenticatedUsers) if (Lists._authenticatedUsers.Contains(player.NetConnection.RemoteUniqueIdentifier)) Lists._authenticatedUsers.Remove(player.NetConnection.RemoteUniqueIdentifier);

            if (player.IsBanned() || player.IsIPBanned()) return false;

            return true;
        }

        public override void OnPlayerKilled(Client player)
        {
            Program.ServerInstance.SendNativeCallToPlayer(player, 0x29B487C359E19889, _weatherNames[ServerWeather]);

            Program.ServerInstance.SendNativeCallToPlayer(player, 0x47C3B5848C3E45D8, ServerTime.Hours, ServerTime.Minutes, ServerTime.Seconds);
            Program.ServerInstance.SendNativeCallToPlayer(player, 0x4055E40BD2DBEC1D, true);
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
            if (!checkauthentication || client.IsAuthenticated()) lock (Lists._accounts.Accounts) return Lists._accounts.Accounts.FirstOrDefault(acc => acc.Name == client.Name);

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
                        client.GetAccount().Ban = b;

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
                BannedBy = IssuedBy == null ? "Server" : IssuedBy.Name,
                Reason = Reason,
                TimeIssued = DateTime.Now,
                Name = client.Name,
            };

            lock (Lists._banned.BannedIps) Lists._banned.BannedIps.Add(ban);

            client.GetAccount().Ban = ban;
        }

        public static Client GetClient(this Account account)
        {
            Client client = null;
            lock (Program.ServerInstance.Clients) Program.ServerInstance.Clients.Any(c =>
            {
                if (c.Name == account.Name)
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