using System;
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
        /// Called on every chat message. Currently the only way to implement a command (a better way is coming, I promise!)
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
    }
}
