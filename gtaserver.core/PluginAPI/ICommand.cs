using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using GTAServer.ProtocolMessages;

namespace GTAServer.PluginAPI
{
    public interface ICommand
    {
        /// <summary>
        /// Name of the command
        /// </summary>
        string CommandName { get; }
        /// <summary>
        /// What shows in the help text for the command
        /// </summary>
        string HelpText { get; }

        /// <summary>
        /// List of permissions needed to run the command
        /// </summary>
        List<string> RequiredPermissions { get; }
        /// <summary>
        /// If every permission listed in RequiredPermissions is needed.
        /// </summary>
        bool AllPermissionsRequired { get; }

        /// <summary>
        /// Called when a command is being executed.
        /// </summary>
        /// <param name="caller">Person who called the command</param>
        /// <param name="chatData">Chat data object from the message command</param>
        void OnCommandExec(Client caller, ChatData chatData);
    }
}
