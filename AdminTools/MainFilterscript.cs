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

        public override void Start()
        {
            LoadAccounts(Location + "Accounts.xml");
            LoadBanlist(Location + "Banlist.xml");
            _authenticatedUsers = new Dictionary<long, Account>();
            Console.WriteLine("Accounts have been loaded.");
        }

        public override void OnPlayerConnect(NetConnection player)
        {
            if (_banned.BannedIps.Any(b => b.Address == player.RemoteEndPoint.Address.ToString()))
            {
                Program.ServerInstance.SendChatMessageToPlayer(player, "BANNED", "You are banned.");
                Program.ServerInstance.KickPlayer(player, "You are banned.");
                return;
            }

            var name = Program.ServerInstance.NickNames[player.RemoteUniqueIdentifier];
            var account = _accounts.Accounts.FirstOrDefault(acc => acc.Name == name);

            if (account == null)
            {
                Program.ServerInstance.SendChatMessageToPlayer(player, "ACCOUNT", "You can register an account using /register [password]");
            }
            else
            {
                Program.ServerInstance.SendChatMessageToPlayer(player, "ACCOUNT", "Please authenticate to your account using /login [password]");
            }
        }

        public override bool OnChatMessage(NetConnection sender, string message)
        {
            var account = _authenticatedUsers.ContainsKey(sender.RemoteUniqueIdentifier)
                ? _authenticatedUsers[sender.RemoteUniqueIdentifier]
                : null;


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

                var target =
                    Program.ServerInstance.Clients.FirstOrDefault(
                        c =>
                            c.RemoteUniqueIdentifier ==
                            Program.ServerInstance.NickNames.FirstOrDefault(pair => pair.Value.ToLower().StartsWith(args[1].ToLower())).Key);

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

                Console.WriteLine("ADMINTOOLS: {account.Name} has teleported to player {Program.ServerInstance.NickNames[target.RemoteUniqueIdentifier]}");

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

                var target =
                    Program.ServerInstance.Clients.FirstOrDefault(
                        c =>
                            c.RemoteUniqueIdentifier ==
                            Program.ServerInstance.NickNames.FirstOrDefault(pair => pair.Value.ToLower().StartsWith(args[1].ToLower())).Key);

                if (target == null)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "ERROR", "No such player found: " + args[1]);
                    return false;
                }

                Program.ServerInstance.SetPlayerHealth(target, -1);
                Console.WriteLine("ADMINTOOLS: {account.Name} has killed player {Program.ServerInstance.NickNames[target.RemoteUniqueIdentifier]}");
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

                var target =
                    Program.ServerInstance.Clients.FirstOrDefault(
                        c =>
                            c.RemoteUniqueIdentifier ==
                            Program.ServerInstance.NickNames.FirstOrDefault(pair => pair.Value.ToLower().StartsWith(args[1].ToLower())).Key);

                if (target == null)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "ERROR", "No such player found: " + args[1]);
                    return false;
                }

                _banned.BannedIps.Add(new Ban()
                {
                    Address = target.RemoteEndPoint.Address.ToString(),
                    BannedBy = account.Name,
                    Reason = args[2],
                    TimeIssued = DateTime.Now,
                    Name = Program.ServerInstance.NickNames[target.RemoteUniqueIdentifier],
                });

                SaveBanlist(Location + "Banlist.xml");

                Console.WriteLine("ADMINTOOLS: {account.Name} has banned player {Program.ServerInstance.NickNames[target.RemoteUniqueIdentifier]} with reason: {args[2]}");
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

                var target =
                    Program.ServerInstance.Clients.FirstOrDefault(
                        c =>
                            c.RemoteUniqueIdentifier ==
                            Program.ServerInstance.NickNames.FirstOrDefault(pair => pair.Value.ToLower().StartsWith(args[1].ToLower())).Key);

                if (target == null)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "ERROR", "No such player found: " + args[1]);
                    return false;
                }

                Program.ServerInstance.KickPlayer(target, args[2]);
                Console.WriteLine("ADMINTOOLS: {account.Name} has kickd player {Program.ServerInstance.NickNames[target.RemoteUniqueIdentifier]}");
                return false;
            }

            if (message.StartsWith("/register"))
            {
                account = _accounts.Accounts.FirstOrDefault(acc => acc.Name == Program.ServerInstance.NickNames[sender.RemoteUniqueIdentifier]);
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
                    Name = Program.ServerInstance.NickNames[sender.RemoteUniqueIdentifier],
                    Password = password,
                };
                _accounts.Accounts.Add(accObject);
                SaveAccounts(Location + "Accounts.xml");
                _authenticatedUsers.Add(sender.RemoteUniqueIdentifier, accObject);

                Program.ServerInstance.SendChatMessageToPlayer(sender, "ACCOUNT", "Your account has been created!");
                Console.WriteLine("ADMINTOOLS: New player registered: {accObject.Name}");
                return false;
            }

            if (message.StartsWith("/login"))
            {
                if (account != null)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "ERROR", "You are already authenticated.");
                    return false;
                }

                account = _accounts.Accounts.FirstOrDefault(acc => acc.Name == Program.ServerInstance.NickNames[sender.RemoteUniqueIdentifier]);

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

                _authenticatedUsers.Add(sender.RemoteUniqueIdentifier, account);
                Program.ServerInstance.SendChatMessageToPlayer(sender, "ACCOUNT", "Authentication successful!");
                Console.WriteLine("ADMINTOOLS: New player logged in: {account.Name}");
                return false;
            }


            if (message == "/logout")
            {
                if (_authenticatedUsers.ContainsKey(sender.RemoteUniqueIdentifier))
                {
                    Console.WriteLine("ADMINTOOLS: Player has logged out: {_authenticatedUsers[sender.RemoteUniqueIdentifier].Name}");
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "ACCOUNT", "You have been logged out.");
                    _authenticatedUsers.Remove(sender.RemoteUniqueIdentifier);
                }
                else
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "ACCOUNT", "You are not logged in.");
                }
                return false;
            }

            return true;
        }

        public override void OnPlayerDisconnect(NetConnection player)
        {
            if (_authenticatedUsers.ContainsKey(player.RemoteUniqueIdentifier))
                _authenticatedUsers.Remove(player.RemoteUniqueIdentifier);
        }

        private UserList _accounts;
        private Banlist _banned;
        private Dictionary<long, Account> _authenticatedUsers;

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