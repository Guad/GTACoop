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

        public void OnTick(object sender, EventArgs e)
        {
            if (!Main.IsOnServer()) return;

            const int npcThreshold = 5000; // 5 second timeout
            const int playerThreshold = 60000; // 60 second timeout

            Dictionary<long, SyncPed> localOpps = null;
            lock (Main.Opponents) localOpps = new Dictionary<long, SyncPed>(Main.Opponents);
            for (int i = localOpps.Count - 1; i >= 0; i--)
            {
                if (DateTime.Now.Subtract(localOpps.ElementAt(i).Value.LastUpdateReceived).TotalMilliseconds > playerThreshold)
                {
                    var key = localOpps.ElementAt(i).Key;
                    localOpps[key].Clear();
                    localOpps.Remove(key);
                }
            }

            Dictionary<string, SyncPed> localNpcs = null;
            lock (Main.Npcs) localNpcs = new Dictionary<string, SyncPed>(Main.Npcs);
            for (int i = localNpcs.Count - 1; i >= 0; i--)
            {
                if (DateTime.Now.Subtract(localNpcs.ElementAt(i).Value.LastUpdateReceived).TotalMilliseconds > npcThreshold)
                {
                    var key = localNpcs.ElementAt(i).Key;
                    localNpcs[key].Clear();
                    localNpcs.Remove(key);
                }
            }

            lock (Main.Opponents) foreach (KeyValuePair<long, SyncPed> opp in new Dictionary<long, SyncPed>(Main.Opponents)) if (!localOpps.ContainsKey(opp.Key)) Main.Opponents.Remove(opp.Key);

            lock (Main.Npcs) foreach (KeyValuePair<string, SyncPed> npc in new Dictionary<string, SyncPed>(Main.Npcs)) if (!localNpcs.ContainsKey(npc.Key)) Main.Npcs.Remove(npc.Key);

            for (int i = 0; i < localOpps.Count; i++) localOpps.ElementAt(i).Value.DisplayLocally();

            for (int i = 0; i < localNpcs.Count; i++) localNpcs.ElementAt(i).Value.DisplayLocally();

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