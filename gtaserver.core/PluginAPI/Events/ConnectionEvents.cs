using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GTAServer.ProtocolMessages;

namespace GTAServer.PluginAPI.Events
{
    public static class ConnectionEvents
    {
        /// <summary>
        /// Called whenever a new connection request comes in.
        /// </summary>
        public static List<Func<Client, ConnectionRequest, PluginResponse<ConnectionRequest>>> OnConnectionRequest 
         = new List<Func<Client, ConnectionRequest, PluginResponse<ConnectionRequest>>>();
        /// <summary>
        /// Internal method. Triggers OnConnectionRequest
        /// </summary>
        /// <param name="c">Client who the request is from</param>
        /// <param name="r">Connection request</param>
        /// <returns>A PluginResponse, with the ability to rewrite the received message.</returns>
        public static PluginResponse<ConnectionRequest> ConnectionRequest(Client c, ConnectionRequest r)
        {
            var result = new PluginResponse<ConnectionRequest>()
            {
                ContinueServerProc = true,
                ContinuePluginProc = true,
                Data = r
            };
            foreach (var f in OnConnectionRequest)
            {
                result = f(c, r);
                if (!result.ContinuePluginProc) return result;
                r = result.Data;
            }
            return result;
        }
    }
}
