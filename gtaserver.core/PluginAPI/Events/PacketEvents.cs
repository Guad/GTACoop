using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GTAServer.ProtocolMessages;
using Lidgren.Network;

namespace GTAServer.PluginAPI.Events
{

    /// <summary>
    /// Warning: This API is unstable and may change at any time. Please stick to the higher-level APIs to avoid
    /// having to change plugins drastically.
    /// </summary>
    public static class PacketEvents
    {
        /// <summary>
        /// Called when a new packet is received. Return false to cancel further processing by the server and other plugins.
        /// </summary>
        public static List<Func<Client, NetIncomingMessage, PluginResponse<NetIncomingMessage>>> OnIncomingPacket = 
                  new List<Func<Client, NetIncomingMessage, PluginResponse<NetIncomingMessage>>>();

        /// <summary>
        /// Internal method. Used to trigger OnIncomingPacket.
        /// </summary>
        /// <param name="c">Client who the packet is from.</param>
        /// <param name="msg">Packet contents</param>
        /// <returns>A PluginResponse, with the ability to modify the message passed on to other plugins and the server</returns>
        public static PluginResponse<NetIncomingMessage> IncomingPacket(Client c, NetIncomingMessage msg)
        {
            var result = new PluginResponse<NetIncomingMessage>()
            {
                ContinuePluginProc = true,
                ContinueServerProc = true,
                Data = msg
            };
            foreach (var f in OnIncomingPacket)
            {
                result = f(c, msg);
                if (!result.ContinuePluginProc) return result;
                msg = result.Data;
            }
            return result;
        }

        /// <summary>
        /// Called whenever a ping packet is received.
        /// </summary>
        public static List<Func<Client, NetIncomingMessage, PluginResponse>> OnPing = 
                  new List<Func<Client, NetIncomingMessage, PluginResponse>>();
        /// <summary>
        /// Internal method. Used to trigger OnPing.
        /// </summary>
        /// <param name="c">Client who the packet is from</param>
        /// <param name="msg">Packet contents</param>
        /// <returns>A PluginResponse</returns>
        public static PluginResponse Ping(Client c, NetIncomingMessage msg)
        {
            var result = new PluginResponse()
            {
                ContinuePluginProc = true,
                ContinueServerProc = true,
            };
            foreach (var f in OnPing)
            {
                result = f(c, msg);
                if (!result.ContinuePluginProc) return result;
            }
            return result;
        }

        /// <summary>
        /// Called whenever a query packet is received.
        /// </summary>
        public static List<Func<Client, NetIncomingMessage, PluginResponse>> OnQuery = 
                  new List<Func<Client, NetIncomingMessage, PluginResponse>>();
        /// <summary>
        /// Internal method. Used to trigger OnQuery.
        /// </summary>
        /// <param name="c">Client who the packet is from</param>
        /// <param name="msg">Packet contents</param>
        /// <returns>A PluginResponse</returns>
        public static PluginResponse Query(Client c, NetIncomingMessage msg)
        {
            var result = new PluginResponse()
            {
                ContinuePluginProc = true,
                ContinueServerProc = true,
            };
            foreach (var f in OnQuery)
            {
                result = f(c, msg);
                if (!result.ContinuePluginProc) return result;
            }
            return result;
        }

        /// <summary>
        /// Called when a new connection approval packet is received.. Return false to cancel further processing by the server and other plugins.
        /// </summary>
        public static List<Func<Client, NetIncomingMessage, PluginResponse<NetIncomingMessage>>> OnIncomingConnectionApproval = 
                  new List<Func<Client, NetIncomingMessage, PluginResponse<NetIncomingMessage>>>();
        /// <summary>
        /// Internal method. Used to trigger OnIncomingPacket.
        /// </summary>
        /// <param name="c">Client who the packet is from.</param>
        /// <param name="msg">Packet contents</param>
        /// <returns>A PluginResponse, with the ability rewrite the message</returns>
        public static PluginResponse<NetIncomingMessage> IncomingConnectionApproval(Client c, NetIncomingMessage msg)
        {
            var result = new PluginResponse<NetIncomingMessage>()
            {
                ContinuePluginProc = true,
                ContinueServerProc = true,
                Data = msg
            };
            foreach (var f in OnIncomingConnectionApproval)
            {
                result = f(c, msg);
                if (!result.ContinuePluginProc) return result;
                msg = result.Data;
            }
            return result;
        }

        /// <summary>
        /// Called whene a new status change packet is received.
        /// </summary>
        public static List<Func<Client, NetIncomingMessage, PluginResponse<NetIncomingMessage>>> OnIncomingStatusChange =
                  new List<Func<Client, NetIncomingMessage, PluginResponse<NetIncomingMessage>>>();
        /// <summary>
        /// Internal method. Used to trigger OnIncomingStatusChange
        /// </summary>
        /// <param name="c">Client who sent the status change (or is affected by the change)</param>
        /// <param name="msg">Status change message for the client</param>
        /// <returns>A PluginResponse, with the ability to rewrite the emssage.</returns>
        public static PluginResponse<NetIncomingMessage> IncomingStatusChange(Client c, NetIncomingMessage msg)
        {
            var result = new PluginResponse<NetIncomingMessage>()
            {
                ContinuePluginProc = true,
                ContinueServerProc = true,
                Data = msg
            };
            foreach (var f in OnIncomingStatusChange)
            {
                result = f(c, msg);
                if (!result.ContinuePluginProc) return result;
                msg = result.Data;
            }
            return result;
        }

        /// <summary>
        /// Called when a new discovery request is received.
        /// </summary>
        public static List<Func<Client, NetIncomingMessage, PluginResponse<NetIncomingMessage>>> OnIncomingDiscoveryRequest =
                      new List<Func<Client, NetIncomingMessage, PluginResponse<NetIncomingMessage>>>();
        /// <summary>
        /// Internal method. Used to trigger OnIncomingDiscoveryRequest
        /// </summary>
        /// <param name="c">Client who sent the discovery request</param>
        /// <param name="msg">Discovery request packet received from client</param>
        /// <returns>A PluginResponse, with the ability to rewrite the message</returns>
        public static PluginResponse<NetIncomingMessage> IncomingDiscoveryRequest(Client c, NetIncomingMessage msg)
        {
            var result = new PluginResponse<NetIncomingMessage>()
            {
                ContinuePluginProc = true,
                ContinueServerProc = true,
                Data = msg
            };
            foreach (var f in OnIncomingDiscoveryRequest)
            {
                result = f(c, msg);
                if (!result.ContinuePluginProc) return result;
                msg = result.Data;
            }
            return result;
        }

        /// <summary>
        /// Called when a new data packet is received (most game events)
        /// </summary>
        public static List<Func<Client, NetIncomingMessage, PluginResponse<NetIncomingMessage>>> OnIncomingData =
                  new List<Func<Client, NetIncomingMessage, PluginResponse<NetIncomingMessage>>>();

        /// <summary>
        /// Internal method. Used to trigger OnIncomingData
        /// </summary>
        /// <param name="c">Client who sent the data</param>
        /// <param name="msg">Data packet itself</param>
        /// <returns>A PluginResponse, with the ability to rewrite the message</returns>
        public static PluginResponse<NetIncomingMessage> IncomingData(Client c, NetIncomingMessage msg)
        {
            var result = new PluginResponse<NetIncomingMessage>()
            {
                ContinueServerProc = true,
                ContinuePluginProc = true,
                Data = msg,
            };
            foreach (var f in OnIncomingData)
            {
                result = f(c, msg);
                if (!result.ContinuePluginProc) return result;
                msg = result.Data;
            }
            return result;
        }
    }
}
