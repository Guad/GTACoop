using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GTAServer.ProtocolMessages;
using Lidgren.Network;

namespace GTAServer.PluginAPI.Events
{
    public class PluginPacketHandler
    {
        /// <summary>
        /// If the packet should continue being processed by other plugins
        /// </summary>
        public bool ContinuePluginProc;
        /// <summary>
        /// If the packet should continue being processed by the server
        /// </summary>
        public bool ContinueServerProc;

        /// <summary>
        /// Message received. Passed on to the next thing in the chain (or the server)
        /// </summary>
        public NetIncomingMessage Msg;
    }
    public static class PacketEvents
    {
        /// <summary>
        /// Called when a new packet is received. Return false to cancel further processing by the server and other plugins.
        /// </summary>
        public static List<Func<Client, NetIncomingMessage, PluginPacketHandler>> OnIncomingPacket = new List<Func<Client, NetIncomingMessage, PluginPacketHandler>>();

        /// <summary>
        /// Internal method. Used to trigger OnIncomingPacket.
        /// </summary>
        /// <param name="c">Client who the packet is from.</param>
        /// <param name="msg">Packet contents</param>
        /// <returns></returns>
        public static PluginPacketHandler IncomingPacket(Client c, NetIncomingMessage msg)
        {
            var result = new PluginPacketHandler()
            {
                ContinuePluginProc = true,
                ContinueServerProc = true,
                Msg = msg
            };
            foreach (var f in OnIncomingPacket)
            {
                result = f(c, msg);
                if (!result.ContinuePluginProc) return result;
                msg = result.Msg;
            }
            return result;
        }


    }
}
