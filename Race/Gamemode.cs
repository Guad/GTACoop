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
        public Opponent(Client c)
        {
            Client = c;
            CheckpointsPassed = 0;
        }

        public Client Client { get; set; }
        public int CheckpointsPassed { get; set; }
        public bool HasFinished { get; set; }
        public bool HasStarted { get; set; }
        public int Vehicle { get; set; }
        public int Blip { get; set; }
    }

    public class Gamemode : ServerScript
    {
        public bool IsRaceOngoing { get; set; }
        public List<Opponent> Opponents { get; set; }
        public Race CurrentRace { get; set; }
        public List<Race> AvailableRaces { get; set; }
        public List<Vector3> CurrentRaceCheckpoints { get; set; }
        public Dictionary<long, int> RememberedBlips { get; set; }
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
            RememberedBlips = new Dictionary<long, int>();
            CurrentRaceCheckpoints = new List<Vector3>();
            LoadRaces();

            Console.WriteLine("Race gamemode started! Loaded " + AvailableRaces.Count + " races.");

            StartVote();
        }

        public override void OnTick()
        {
            if (!IsRaceOngoing) return;

            lock (Opponents)
            {
                lock (CurrentRaceCheckpoints)
                foreach (var opponent in Opponents)
                {
                    if (opponent.HasFinished || !opponent.HasStarted) continue;
                    if (opponent.Client.LastKnownPosition.IsInRangeOf(CurrentRaceCheckpoints[opponent.CheckpointsPassed], 10f))
                    {
                        opponent.CheckpointsPassed++;
                        if (opponent.CheckpointsPassed >= CurrentRaceCheckpoints.Count)
                        {
                            if (Opponents.All(op => !op.HasFinished))
                            {
                                var t = new Thread((ThreadStart) delegate
                                {
                                    Thread.Sleep(10000);
                                    Program.ServerInstance.SendChatMessageToAll("Vote for next map will start in 60 seconds!");
                                    Thread.Sleep(30000);
                                    Program.ServerInstance.SendChatMessageToAll("Vote for next map will start in 30 seconds!");
                                    Thread.Sleep(30000);
                                    if (DateTime.UtcNow.Subtract(VoteStart).TotalSeconds > 60)
                                        StartVote();
                                });
                                t.Start();
                            }

                            opponent.HasFinished = true;
                            var pos = Opponents.Count(o => o.HasFinished);
                            var suffix = pos.ToString().EndsWith("1")
                                ? "st"
                                : pos.ToString().EndsWith("2") ? "nd" : pos.ToString().EndsWith("3") ? "rd" : "th";
                            Program.ServerInstance.SendChatMessageToAll("~h~" + opponent.Client.DisplayName + "~h~ has finished " + pos + suffix);
                            Program.ServerInstance.SendNativeCallToPlayer(opponent.Client, 0x45FF974EEE1C8734, opponent.Blip, 0);
                            Program.ServerInstance.RecallNativeCallOnTickForPlayer(opponent.Client, "RACE_CHECKPOINT_MARKER");
                            Program.ServerInstance.RecallNativeCallOnTickForPlayer(opponent.Client, "RACE_CHECKPOINT_MARKER_DIR");
                            continue;
                        }

                        Program.ServerInstance.SendNativeCallToPlayer(opponent.Client, 0xAE2AF67E9D9AF65D, opponent.Blip,
                            CurrentRaceCheckpoints[opponent.CheckpointsPassed].X,
                            CurrentRaceCheckpoints[opponent.CheckpointsPassed].Y,
                            CurrentRaceCheckpoints[opponent.CheckpointsPassed].Z);

                        Program.ServerInstance.SetNativeCallOnTickForPlayer(opponent.Client, "RACE_CHECKPOINT_MARKER",
                        0x28477EC23D892089, 1, CurrentRaceCheckpoints[opponent.CheckpointsPassed], new Vector3(), new Vector3(),
                        new Vector3() { X = 10f, Y = 10f, Z = 2f }, 241, 247, 57, 180, false, false, 2, false, false,
                        false, false);

                        if (CurrentRaceCheckpoints.Count > opponent.CheckpointsPassed+1)
                        {
                            var nextCp = CurrentRaceCheckpoints[opponent.CheckpointsPassed + 1];
                            var curCp = CurrentRaceCheckpoints[opponent.CheckpointsPassed];

                            if (nextCp != null && curCp != null)
                            {
                                Vector3 dir = nextCp.Subtract(curCp);
                                dir = dir.Normalize();

                                Program.ServerInstance.SetNativeCallOnTickForPlayer(opponent.Client,
                                    "RACE_CHECKPOINT_MARKER_DIR",
                                    0x28477EC23D892089, 20, curCp.Subtract(new Vector3() {X = 0f, Y = 0f, Z = -2f}), dir,
                                    new Vector3() {X = 60f, Y = 0f, Z = 0f},
                                    new Vector3() {X = 4f, Y = 4f, Z = 4f}, 87, 193, 250, 200, false, false, 2, false,
                                    false,
                                    false, false);
                            }
                        }
                        else
                        {
                            Program.ServerInstance.RecallNativeCallOnTickForPlayer(opponent.Client, "RACE_CHECKPOINT_MARKER_DIR");
                        }
                    }
                }
            }

        }

        public override bool OnPlayerDisconnect(Client player)
        {
            Opponent curOp = Opponents.FirstOrDefault(op => op.Client == player);
            if (curOp == null) return true;

            if (RememberedBlips.ContainsKey(player.NetConnection.RemoteUniqueIdentifier))
                RememberedBlips[player.NetConnection.RemoteUniqueIdentifier] = curOp.Blip;
            else
                RememberedBlips.Add(player.NetConnection.RemoteUniqueIdentifier, curOp.Blip);

            if (curOp.Vehicle != 0)
            {
                Program.ServerInstance.SendNativeCallToPlayer(player, 0xAD738C3085FE7E11, curOp.Vehicle, true, false);
                Program.ServerInstance.SendNativeCallToPlayer(player, 0xAE3CBE5BF394C9C9, curOp.Vehicle);
            }

            if (curOp.Blip != 0)
            {
                Program.ServerInstance.SendNativeCallToPlayer(player, 0x45FF974EEE1C8734, curOp.Blip, 0);
            }

            lock (Opponents) Opponents.Remove(curOp);
            return true;
        }

        public override bool OnChatMessage(Client sender, string message)
        {
            if (message == "/votemap" && DateTime.Now.Subtract(VoteStart).TotalSeconds > 60 && (!IsRaceOngoing || DateTime.UtcNow.Subtract(RaceStart).TotalSeconds > 60))
            {
                StartVote();
                return false;
            }
            else if (message.StartsWith("/vote"))
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
                if (!int.TryParse(args[1], out choice) || choice <= 0 || choice > AvailableChoices.Count)
                {
                    Program.ServerInstance.SendChatMessageToPlayer(sender, "USAGE", "/vote [id]");
                    return false;
                }

                Votes[choice]++;
                Program.ServerInstance.SendChatMessageToPlayer(sender, "You have voted for " + AvailableChoices[choice].Name); 
                Voters.Add(sender);
                return false;
            }
            else if (message == "/q")
            {
                Opponent curOp = Opponents.FirstOrDefault(op => op.Client == sender);

                if (curOp == null)
                {
                    
                }

                if (curOp != null)
                {
                    if (curOp.Blip != 0)
                    {
                        Program.ServerInstance.SendNativeCallToPlayer(sender, 0x45FF974EEE1C8734, curOp.Blip, 0);
                    }

                    lock (Opponents) Opponents.Remove(curOp);
                }

                Program.ServerInstance.KickPlayer(sender, "requested");
            }
            return true;
        }

        public override bool OnPlayerConnect(Client player)
        {
            Program.ServerInstance.SetNativeCallOnTickForPlayer(player, "RACE_DISABLE_VEHICLE_EXIT", 0xFE99B66D079CF6BC, 0, 75, true);
            Program.ServerInstance.SendChatMessageToPlayer(player, "~r~IMPORTANT~w~", "Quit the server using the ~h~/q~h~ command to remove the blip.");

            if (IsRaceOngoing)
            {
                SetUpPlayerForRace(player, CurrentRace, false, 0);
            }

            if (DateTime.Now.Subtract(VoteStart).TotalSeconds < 60)
            {
                Program.ServerInstance.SendChatMessageToPlayer(player, GetVoteHelpString());
            }

            if (RememberedBlips.ContainsKey(player.NetConnection.RemoteUniqueIdentifier))
            {
                Opponents.Add(new Opponent(player) {Blip = RememberedBlips[player.NetConnection.RemoteUniqueIdentifier] });
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

            Opponents.ForEach(op =>
            {
                op.HasFinished = false;
                op.CheckpointsPassed = 0;
            });
            
            
            lock (Program.ServerInstance.Clients)
                for (int i = 0; i < Program.ServerInstance.Clients.Count; i++)
                {
                    SetUpPlayerForRace(Program.ServerInstance.Clients[i], CurrentRace, true, i);
                }

            CurrentRaceCheckpoints = race.Checkpoints.ToList();
            RaceStart = DateTime.UtcNow;
            
            Console.WriteLine("RACE: Starting race " + race.Name);

            var t = new Thread((ThreadStart) delegate
            {
                Thread.Sleep(10000);
                Program.ServerInstance.SendChatMessageToAll("3"); // I should probably automate this
                Thread.Sleep(1000);
                Program.ServerInstance.SendChatMessageToAll("2");
                Thread.Sleep(1000);
                Program.ServerInstance.SendChatMessageToAll("1");
                Thread.Sleep(1000);
                Program.ServerInstance.SendChatMessageToAll("Go!");
                IsRaceOngoing = true;


                lock (Opponents)
                foreach (var opponent in Opponents)
                {
                    Program.ServerInstance.SendNativeCallToPlayer(opponent.Client, 0x428CA6DBD1094446, opponent.Vehicle, false);
                    opponent.HasStarted = true;
                }
            });
            t.Start();
        }

        private void EndRace()
        {
            IsRaceOngoing = false;
            CurrentRace = null;

            foreach (var opponent in Opponents)
            {
                if (opponent.Blip != 0)
                {
                    Program.ServerInstance.SendNativeCallToPlayer(opponent.Client, 0x45FF974EEE1C8734, opponent.Blip, 0);
                }
            }

            Program.ServerInstance.RecallNativeCallOnTickForAllPlayers("RACE_CHECKPOINT_MARKER");
            Program.ServerInstance.RecallNativeCallOnTickForAllPlayers("RACE_CHECKPOINT_MARKER_DIR");

            CurrentRaceCheckpoints.Clear();
        }

        private Random randGen = new Random();
        private void SetUpPlayerForRace(Client client, Race race, bool freeze, int spawnpoint)
        {
            if (race == null) return;

            var selectedModel = unchecked((int)((uint)race.AvailableVehicles[randGen.Next(race.AvailableVehicles.Length)]));
            var position = race.SpawnPoints[spawnpoint % race.SpawnPoints.Length].Position;
            var heading = race.SpawnPoints[spawnpoint % race.SpawnPoints.Length].Heading;
            Program.ServerInstance.SendNativeCallToPlayer(client, 0x06843DA7060A026B, new LocalPlayerArgument(),
                position.X, position.Y, position.Z, 0, 0, 0, 1);
            
            if (race.Checkpoints.Length >= 2)
            {
                Vector3 dir = race.Checkpoints[1].Subtract(race.Checkpoints[0]);
                dir = dir.Normalize();

                Program.ServerInstance.SetNativeCallOnTickForPlayer(client, "RACE_CHECKPOINT_MARKER_DIR",
                0x28477EC23D892089, 20, race.Checkpoints[0].Subtract(new Vector3() { X = 0f, Y = 0f, Z = -2f }), dir, new Vector3() { X = 60f, Y = 0f, Z = 0f },
                new Vector3() { X = 4f, Y = 4f, Z = 4f }, 87, 193, 250, 200, false, false, 2, false, false,
                false, false);
            }


            Program.ServerInstance.SetNativeCallOnTickForPlayer(client, "RACE_CHECKPOINT_MARKER",
                0x28477EC23D892089, 1, race.Checkpoints[0], new Vector3(), new Vector3(),
                new Vector3() { X = 10f, Y = 10f, Z = 2f }, 241, 247, 57, 180, false, false, 2, false, false,
                false, false);


            var nt = new Thread((ThreadStart)delegate
            {
                SetPlayerInVehicle(client, selectedModel, position, heading, freeze);
            });
            nt.Start();

            Opponent curOp = Opponents.FirstOrDefault(op => op.Client == client);
            if (curOp == null || curOp.Blip == 0)
            {
                Program.ServerInstance.GetNativeCallFromPlayer(client, "start_blip", 0x5A039BB0BCA604B6,
                    new IntArgument(), // ADD_BLIP_FOR_COORD
                    delegate (object o)
                    {
                        lock (Opponents)
                        {
                            Opponent secOp = Opponents.FirstOrDefault(op => op.Client == client);

                            if (secOp != null)
                            {
                                secOp.Blip = (int)o;
                            }
                            else
                                Opponents.Add(new Opponent(client) { Blip = (int)o });
                        }

                    }, race.Checkpoints[0].X, race.Checkpoints[0].Y, race.Checkpoints[0].Z);
            }
            else
            {
                Program.ServerInstance.SendNativeCallToPlayer(client, 0x45FF974EEE1C8734, curOp.Blip, 255);
                Program.ServerInstance.SendNativeCallToPlayer(client, 0xAE2AF67E9D9AF65D, curOp.Blip, race.Checkpoints[0].X, race.Checkpoints[0].Y, race.Checkpoints[0].Z);
            }
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

        private void SetPlayerInVehicle(Client player, int model, Vector3 pos, float heading, bool freeze)
        {
            Program.ServerInstance.SetNativeCallOnTickForPlayer(player, "RACE_REQUEST_MODEL", 0x963D27A58DF860AC, model);
            Thread.Sleep(5000);
            Program.ServerInstance.RecallNativeCallOnTickForPlayer(player, "RACE_REQUEST_MODEL");

            Program.ServerInstance.GetNativeCallFromPlayer(player, "spawn", 0xAF35D0D2583051B0, new IntArgument(),
                delegate (object o)
                {
                    Program.ServerInstance.SendNativeCallToPlayer(player, 0xF75B0D629E1C063D, new LocalPlayerArgument(), (int)o, -1);
                    if (freeze)
                        Program.ServerInstance.SendNativeCallToPlayer(player, 0x428CA6DBD1094446, (int)o, true);

                    Opponent inOp = Opponents.FirstOrDefault(op => op.Client == player);

                    lock (Opponents)
                    {
                        if (inOp != null)
                        {
                            inOp.Vehicle = (int)o;
                            inOp.HasStarted = true;
                        }
                        else
                            Opponents.Add(new Opponent(player) { Vehicle = (int)o, HasStarted = true});
                    }

                    Program.ServerInstance.SendNativeCallToPlayer(player, 0xE532F5D78798DAAB, model);
                }, model, pos.X, pos.Y, pos.Z, heading, false, false);
        }

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
                build.Append("\n" + counter + ": " + race.Name);
                Votes.Add(counter, 0);
                AvailableChoices.Add(counter, race);
                counter++;
            }

            VoteStart = DateTime.Now;
            Program.ServerInstance.SendChatMessageToAll(build.ToString());

            var t = new Thread((ThreadStart)delegate
            {
                Thread.Sleep(60*1000);
                EndRace();
                var raceWon = AvailableChoices[Votes.OrderByDescending(pair => pair.Value).ToList()[0].Key];
                Program.ServerInstance.SendChatMessageToAll(raceWon.Name + " has won the vote!");

                Thread.Sleep(1000);
                StartRace(raceWon);
            });
            t.Start();
        }

        private string GetVoteHelpString()
        {
            if (DateTime.Now.Subtract(VoteStart).TotalSeconds > 60)
                return null;

            var build = new StringBuilder();
            build.Append("Type /vote [id] to vote for the next race! The options are:");

            foreach (var race in AvailableChoices)
            {
                build.Append("\n" + race.Key + ": " + race.Value.Name);
            }

            return build.ToString();
        }
    }

    public static class RangeExtension
    {
        public static bool IsInRangeOf(this Vector3 center, Vector3 dest, float radius)
        {
            return center.Subtract(dest).Length() < radius;
        }

        public static Vector3 Subtract(this Vector3 left, Vector3 right)
        {
            return new Vector3()
            {
                X = left.X - right.X,
                Y = left.Y - right.Y,
                Z = left.Z - right.Z,
            };
        }

        public static float Length(this Vector3 vect)
        {
            return (float) Math.Sqrt((vect.X*vect.X) + (vect.Y*vect.Y) + (vect.Z*vect.Z));
        }

        public static Vector3 Normalize(this Vector3 vect)
        {
            float length = vect.Length();
            if (length == 0) return vect;

            float num = 1/length;

            return new Vector3()
            {
                X = vect.X * num,
                Y = vect.Y * num,
                Z = vect.Z * num,
            };
        }
    }
}
