using System;
using System.Collections.Generic;
using System.Linq;
using GTA;

namespace GTACoOp
{
    public class PedThread : Script
    {
        public PedThread()
        {
            Tick += OnTick;
        }

        public void CheckExpiredNpcs()
        {
            const int threshold = 10000; // 10 second timeout

            for (int i = Main.Npcs.Count - 1; i >= 0; i--)
            {
                if (DateTime.Now.Subtract(Main.Npcs.ElementAt(i).Value.LastUpdateReceived).TotalMilliseconds > threshold)
                {
                    var key = Main.Npcs.ElementAt(i).Key;
                    Main.Npcs[key].Clear();
                    Main.Npcs.Remove(key);
                }
            }

            for (int i = Main.Opponents.Count - 1; i >= 0; i--)
            {
                if (DateTime.Now.Subtract(Main.Opponents.ElementAt(i).Value.LastUpdateReceived).TotalMilliseconds > threshold)
                {
                    var key = Main.Opponents.ElementAt(i).Key;
                    Main.Opponents[key].Clear();
                    Main.Opponents.Remove(key);
                }
            }
        }

        public void OnTick(object sender, EventArgs e)
        {
            if (!Main.IsOnServer()) return;

            CheckExpiredNpcs();

            var localOpps = new Dictionary<long, SyncPed>(Main.Opponents);
            for (int i = 0; i < localOpps.Count; i++)
            {
                localOpps.ElementAt(i).Value.DisplayLocally();
            }

            var localNpcs = new Dictionary<string, SyncPed>(Main.Npcs);
            for (int i = 0; i < localNpcs.Count; i++)
            {
                localNpcs.ElementAt(i).Value.DisplayLocally();
            }

            if (Main.SendNpcs)
            {
                var list = new List<int>(localNpcs.Where(pair => pair.Value.Character != null).Select(pair => pair.Value.Character.Handle));
                list.AddRange(localOpps.Where(pair => pair.Value.Character != null).Select(pair => pair.Value.Character.Handle));
                list.Add(Game.Player.Character.Handle);

                foreach (Ped ped in World.GetAllPeds()
                    .OrderBy(p => (p.Position - Game.Player.Character.Position).Length())
                    .Take(Main.PlayerSettings.MaxStreamedNpcs == 0 ? 10 : Main.PlayerSettings.MaxStreamedNpcs))
                {
                    if (!list.Contains(ped.Handle))
                    {
                        Main.SendPedData(ped);
                    }
                }
            }

            Main.SendPlayerData();
        }
    }
}