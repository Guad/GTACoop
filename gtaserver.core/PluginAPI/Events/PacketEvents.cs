using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GTAServer.ProtocolMessages;
using Lidgren.Network;

namespace GTAServer.PluginAPI.Events
{


    public static class PacketEvents
    {
        /// <summary>
        /// Called when a new packet is received. Return false to cancel further processing by the server and other plugins.
        /// </summary>
        public static List<Func<Client, NetIncomingMessage, PluginResponse<NetIncomingMessage>>> OnIncomingPacket = new List<Func<Client, NetIncomingMessage, PluginResponse<NetIncomingMessage>>>();

        /// <summary>
        /// Internal method. Used to trigger OnIncomingPacket.
        /// </summary>
        /// <param name="c">Client who the packet is from.</param>
        /// <param name="msg">Packet contents</param>
        /// <returns></returns>
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
        public static List<Func<Client, NetIncomingMessage,PluginResponse>> OnPing = new List<Func<Client, NetIncomingMessage, PluginResponse>>();
        /// <summary>
        /// Internal method. Used to trigger OnPing.
        /// </summary>
        /// <param name="c">Client who the packet is from</param>
        /// <param name="msg">Packet contents</param>
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
        public static List<Func<Client, NetIncomingMessage, PluginResponse>> OnQuery = new List<Func<Client, NetIncomingMessage, PluginResponse>>();
        /// <summary>
        /// Internal method. Used to trigger OnQuery.
        /// </summary>
        /// <param name="c">Client who the packet is from</param>
        /// <param name="msg">Packet contents</param>
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
        public static List<Func<Client, NetIncomingMessage, PluginResponse<NetIncomingMessage,bool>>> OnIncomingConnectionApproval = new List<Func<Client, NetIncomingMessage, PluginResponse<NetIncomingMessage,bool>>>();
        /// <summary>
        /// Internal method. Used to trigger OnIncomingPacket.
        /// </summary>
        /// <param name="c">Client who the packet is from.</param>
        /// <param name="msg">Packet contents</param>
        /// <returns></returns>
        public static PluginResponse<NetIncomingMessage,bool> IncomingConnectionApproval(Client c, NetIncomingMessage msg)
        {
            var result = new PluginResponse<NetIncomingMessage,bool>()
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


    }
}
