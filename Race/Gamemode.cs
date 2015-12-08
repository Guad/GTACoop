using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using GTAServer;

namespace Race
{
    public class Opponent
    {
        public Opponent(Client c, int carHandle)
        {
            Client = c;
            CheckpointPassed = 0;
            Vehicle = carHandle;
        }

        public Client Client { get; set; }
        public int CheckpointPassed { get; set; }
        public bool HasFinished { get; set; }
        public int Vehicle { get; set; }
    }

    public class Gamemode : ServerScript
    {
        public bool IsRaceOngoing { get; set; }
        public List<Opponent> Opponents { get; set; }
        public Race CurrentRace { get; set; }
        public List<Race> AvailableRaces { get; set; }
        public List<Vector3> CurrentRaceCheckpoints { get; set; }

        public DateTime RaceStart { get; set; }


        // Voting
        public DateTime VoteStart { get; set; }
        public List<Client> Voters { get; set; }
        public Dictionary<int, int> Votes { get; set; }
        public Dictionary<int, Race> AvailableChoices { get; set; }

        public override void Start()
        {
            AvailableRaces = new List<Race>();
            Opponents = new List<Opponent>();
            LoadRaces();

            Console.WriteLine("Race gamemode started! Loaded " + AvailableRaces.Count + " races.");
        }

        public override bool OnChatMessage(Client sender, string message)
        {
            if (message.StartsWith("/vote"))
            {
                if (DateTime.Now.Subtract(VoteStart).TotalSeconds > 60)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "No current vote is in progress.");
                    return false;
                }

                var args = message.Split();

                if (args.Length <= 1)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "USAGE", "/vote [id]");
                    return false;
                }

                if (Voters.Contains(sender))
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "ERROR", "You have already voted!");
                    return false;
                }

                int choice;
                if (!int.TryParse(args[1], out choice) || choice <= 0 || choice >= 10)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "USAGE", "/vote [id]");
                    return false;
                }

                Votes[choice]++;
                Program.ServerInstance.SendChatMessageToPlayer(sender, "Vote successful!");
                Voters.Add(sender);
                return false;
            }
            return true;
        }
        
        private int LoadRaces()
        {
            int counter = 0;
            if (!Directory.Exists("races")) return 0;
            foreach (string path in Directory.GetFiles("races", "*.xml"))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(Race));
                StreamReader file = new StreamReader(path);
                var raceout = (Race)serializer.Deserialize(file);
                file.Close();
                AvailableRaces.Add(raceout);
                counter++;
            }
            return counter;
        }

        private void StartRace(Race race)
        {
            race = new Race(race);
            //Game.FadeScreenOut(500);

            IsRaceOngoing = true;
            CurrentRace = race;

            /*if (_raceSettings["Laps"] > 1)
            {
                _totalLaps = race.Checkpoints.Length;
                List<Vector3> tmpCheckpoints = new List<Vector3>();
                for (int i = 0; i < _raceSettings["Laps"]; i++)
                {
                    tmpCheckpoints.AddRange(race.Checkpoints);
                }
                _currentRace.Checkpoints = tmpCheckpoints.ToArray();
            }*/
            

            List<Spawnpoint> availalbleSpawnPoints = new List<Spawnpoint>(race.SpawnPoints);
            var randGen = new Random();

            int spawnId = randGen.Next(availalbleSpawnPoints.Count);
            var spawn = availalbleSpawnPoints[spawnId];
            availalbleSpawnPoints.RemoveAt(spawnId);

            lock(Program.ServerInstance.Clients)
            foreach (var client in Program.ServerInstance.Clients)
            {
                var spid = randGen.Next(availalbleSpawnPoints.Count);
                var selectedModel = race.AvailableVehicles[randGen.Next(race.AvailableVehicles.Length)];
                var position = availalbleSpawnPoints[spid].Position;
                var heading = availalbleSpawnPoints[spid].Heading;
                availalbleSpawnPoints.RemoveAt(spid);
                
                Program.ServerInstance.GetNativeCallFromPlayer(client, "spawn", 0xAF35D0D2583051B0, new IntArgument(),
                    delegate(object o)
                    {
                        Program.ServerInstance.SendNativeCallToPlayer(client, 0xF75B0D629E1C063D, new LocalPlayerArgument(), (int)o, -1);
                        Program.ServerInstance.SendNativeCallToPlayer(client, 0x428CA6DBD1094446, (int)o, true);
                        Opponents.Add(new Opponent(client, (int)o));
                    }, selectedModel, position.X, position.Y, position.Z, heading, 0, 1);
            }

            CurrentRaceCheckpoints = race.Checkpoints.ToList();
            RaceStart = DateTime.UtcNow;

            //Game.FadeScreenIn(500);
            
            // Start countdown
        }

        private void EndRace()
        {
            IsRaceOngoing = false;
            CurrentRace = null;

            Opponents.Clear();
            CurrentRaceCheckpoints.Clear();
        }

        /*private int CalculatePlayerPositionInRace(Client player)
        {
            int output = 0;
            int playerCheckpoint = _currentRace.Checkpoints.Length - _checkpoints.Count;

            int beforeYou = _rivalCheckpointStatus.Count(tuple => tuple.Item2 > playerCheckpoint);
            output += beforeYou;

            var samePosAsYou = _rivalCheckpointStatus.Where(tuple => tuple.Item2 == playerCheckpoint);
            output +=
                samePosAsYou.Count(
                    tuple =>
                        (_currentRace.Checkpoints[playerCheckpoint] - tuple.Item1.Vehicle.Position).Length() <
                        (_currentRace.Checkpoints[playerCheckpoint] - Game.Player.Character.Position).Length());

            return output;
        }*/


        public void StartVote()
        {
            var pickedRaces = new List<Race>();
            var racePool = new List<Race>(AvailableRaces);
            var rand = new Random();

            for (int i = 0; i < Math.Min(9, AvailableRaces.Count); i++)
            {
                var pick = rand.Next(racePool.Count);
                pickedRaces.Add(racePool[pick]);
                racePool.RemoveAt(pick);
            }

            Votes = new Dictionary<int, int>();
            Voters = new List<Client>();
            AvailableChoices = new Dictionary<int, Race>();

            var build = new StringBuilder();
            build.Append("Type /vote [id] to vote for the next race! The options are:");

            var counter = 1;
            foreach (var race in pickedRaces)
            {
                build.Append("~n~" + counter + ": " + race.Name);
                Votes.Add(counter, 0);
                AvailableChoices.Add(counter, race);
                counter++;
            }

            VoteStart = DateTime.Now;
            Program.ServerInstance.SendChatMessageToAll(build.ToString());

            var t = new Thread((ThreadStart)delegate
            {
                Thread.Sleep(60*1000);
                var raceWon = AvailableChoices[Votes.OrderByDescending(pair => pair.Value).ToList()[0].Key];
                Program.ServerInstance.SendChatMessageToAll(raceWon.Name + " has won the vote!");

                EndRace();
                Thread.Sleep(1000);
                StartRace(raceWon);
            });
            t.Start();
        }
    }
}
