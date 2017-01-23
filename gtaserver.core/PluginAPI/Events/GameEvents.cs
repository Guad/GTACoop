﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GTAServer.ProtocolMessages;
using Lidgren.Network;

namespace GTAServer.PluginAPI.Events
{
    public static class GameEvents
    {
        /// <summary>
        /// Called on every tick.
        /// </summary>
        public static List<Action<int>> OnTick = new List<Action<int>>();
        /// <summary>
        /// Internal method. Triggers OnTick.
        /// </summary>
        /// <param name="t">Current tick.</param>
        public static void Tick(int t) => OnTick.ForEach(f => f(t));

        /// <summary>
        /// Called on every chat message. For commands, add an element to the dictionary GameServer.Commands
        /// </summary>
        public static List<Func<Client, ChatData, PluginResponse<ChatData>>> OnChatMessage
                = new List<Func<Client, ChatData, PluginResponse<ChatData>>>();

        /// <summary>
        /// Internal method. Triggers OnChatMessage
        /// </summary>
        /// <param name="c">Client who sent the chat message</param>
        /// <param name="d">ChatData object</param>
        /// <returns>A PluginResponse, with the ability to rewrite the received message.</returns>
        public static PluginResponse<ChatData> ChatMessage(Client c, ChatData d)
        {
            var result = new PluginResponse<ChatData>()
            {
                ContinuePluginProc = true,
                ContinueServerProc = true,
                Data = d
            };
            foreach (var f in OnChatMessage)
            {
                result = f(c, d);
                if (!result.ContinuePluginProc) return result;
                d = result.Data;
            }
            return result;
        }

        /// <summary>
        /// Called on every vehicle update (or vehicle creation)
        /// </summary>
        public static List<Func<Client, VehicleData, PluginResponse<VehicleData>>> OnVehicleDataUpdate
               = new List<Func<Client, VehicleData, PluginResponse<VehicleData>>>();
        /// <summary>
        /// Internal method. Triggers OnVehicleDataUpdate
        /// </summary>
        /// <param name="c">Client who sent the vehicle position update</param>
        /// <param name="v">VehicleData object</param>
        /// <returns>A PluginResponse, with the ability to rewrite the received data.</returns>
        public static PluginResponse<VehicleData> VehicleDataUpdate(Client c, VehicleData v)
        {
            var result = new PluginResponse<VehicleData>
            {
                ContinuePluginProc = true,
                ContinueServerProc = true,
                Data = v
            };
            foreach (var f in OnVehicleDataUpdate)
            {
                result = f(c, v);
                if (!result.ContinuePluginProc) return result;
                v = result.Data;
            }
            return result;
        }

        /// <summary>
        /// Called on every pedestrian update/creation
        /// </summary>
        public static List<Func<Client, PedData, PluginResponse<PedData>>> OnPedDataUpdate 
                = new List<Func<Client, PedData, PluginResponse<PedData>>>();
        /// <summary>
        /// Internal method. Triggers OnPedDataUpdate
        /// </summary>
        /// <param name="c">Client who sent the update</param>
        /// <param name="p">PedData object</param>
        /// <returns>A PluginResponse, with the ability to rewrite the received data.</returns>
        public static PluginResponse<PedData> PedDataUpdate(Client c, PedData p)
        {
            var result = new PluginResponse<PedData>()
            {
                ContinuePluginProc = true,
                ContinueServerProc = true,
                Data = p
            };
            foreach (var f in OnPedDataUpdate)
            {
                result = f(c, p);
                if (!result.ContinuePluginProc) return result;
                p = result.Data;
            }
            return result;
        }

        /// <summary>
        /// Called on every NPC vehicle update/creation
        /// </summary>
        public static List<Func<Client, VehicleData, PluginResponse<VehicleData>>> OnNpcVehicleDataUpdate
                = new List<Func<Client, VehicleData, PluginResponse<VehicleData>>>();
        /// <summary>
        /// Internal method. Triggers OnNpcVehicleDataUpdate
        /// </summary>
        /// <param name="c">Client who sent the update</param>
        /// <param name="v">VehicleData object</param>
        /// <returns>A PluginResponse, with the ability to rewrite the received data.</returns>
        public static PluginResponse<VehicleData> NpcVehicleDataUpdate(Client c, VehicleData v)
        {
            var result = new PluginResponse<VehicleData>()
            {
                ContinuePluginProc = true,
                ContinueServerProc = true,
                Data = v
            };
            foreach (var f in OnNpcVehicleDataUpdate)
            {
                result = f(c, v);
                if (!result.ContinuePluginProc) return result;
                v = result.Data;
            }
            return result;
        }

        /// <summary>
        /// Called on every NPC update/creation
        /// </summary>
        public static List<Func<Client, PedData, PluginResponse<PedData>>> OnNpcPedDataUpdate
                = new List<Func<Client, PedData, PluginResponse<PedData>>>();
        /// <summary>
        /// Internal method. Trigers OnNpcPedDataUpdate
        /// </summary>
        /// <param name="c">Client who sent the update</param>
        /// <param name="p">PedData Object</param>
        /// <returns>A PluginResponse, with the ability to rewrite the received data</returns>
        public static PluginResponse<PedData> NpcPedDataUpdate(Client c, PedData p)
        {
            var result = new PluginResponse<PedData>()
            {
                ContinuePluginProc = true,
                ContinueServerProc = true,
                Data = p
            };
            foreach (var f in OnNpcPedDataUpdate)
            {
                result = f(c, p);
                if (!result.ContinuePluginProc) return result;
                p = result.Data;
            }
            return result;
        }

        /// <summary>
        /// (Non-cancellable) Called when a player stops sharing their world.
        /// </summary>
        public static List<Action<Client>> OnWorldSharingStop = new List<Action<Client>>();

        /// <summary>
        /// Internal method. Triggers OnWorldSharingStop.
        /// </summary>
        /// <param name="c">Client who stopped sharing their world.</param>
        public static void WorldSharingStop(Client c) => OnWorldSharingStop.ForEach(f => f(c));

        /// <summary>
        /// (Non-cancellable) Called when a player spawns.
        /// </summary>
        public static List<Action<Client>> OnPlayerSpawned = new List<Action<Client>>();
        /// <summary>
        /// Internal method. Triggers OnPlayerSpawned.
        /// </summary>
        /// <param name="c">Client of the player who spawned.</param>
        public static void PlayerSpawned(Client c) => OnPlayerSpawned.ForEach(f => f(c));
    }
}
