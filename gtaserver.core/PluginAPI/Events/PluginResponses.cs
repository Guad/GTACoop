using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GTAServer.PluginAPI.Events
{
    public class PluginResponse<T>
    {
        /// <summary>
        /// If the event should continue being processed by other plugins
        /// </summary>
        public bool ContinuePluginProc;
        /// <summary>
        /// If the event should continue being processed by the server
        /// </summary>
        public bool ContinueServerProc;
        /// <summary>
        /// Data specific to the event time, usually specified in the event.
        /// </summary>
        public T Data;
    }

    public class PluginResponse
    {
        /// <summary>
        /// If the event should continue being processed by other plugins
        /// </summary>
        public bool ContinuePluginProc;
        /// <summary>
        /// If the event should continue being processed by the server
        /// </summary>
        public bool ContinueServerProc;
    }
}
