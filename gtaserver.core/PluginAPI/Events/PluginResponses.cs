using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GTAServer.PluginAPI.Events
{

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

    public class PluginResponse<T> : PluginResponse
    {
        /// <summary>
        /// Data specific to the event type, usually specified in the event.
        /// </summary>
        public T Data;
    }

    public class PluginResponse<T1, T2> : PluginResponse<T1>
    {
        /// <summary>
        /// Second data field specific to event
        /// </summary>
        public T2 Data2;
    }
}
