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
        /// <returns></returns>
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

       
    }
}
