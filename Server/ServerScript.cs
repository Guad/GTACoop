using Lidgren.Network;

namespace GTAServer
{
    /// <summary>
    /// ServerScript class used for server-side scripting.
    /// </summary>
    public class ServerScript
    {
        /// <summary>
        /// Script name
        /// </summary>
        public virtual string Name { get; set; }
        /// <summary>
        /// Init functon for script
        /// </summary>
        public virtual void Start(GameServer serverInstance) { }

        /// <summary>
        /// Called on an incoming connection.
        /// </summary>
        /// <param name="player">Player that is connecting</param>
        public virtual void OnIncomingConnection(Client player) { }
        /// <summary>
        /// Called on successful connection
        /// </summary>
        /// <param name="player">Player that connected</param>
        /// <returns>Currently discarded. To be implemented: Connection rejections</returns>
        public virtual bool OnPlayerConnect(Client player) { return true; }
        /// <summary>
        /// Called when a player is refused connection.
        /// </summary>
        /// <param name="player">Player who was refused</param>
        /// <param name="reason">Reason for being refused connection</param>
        public virtual void OnConnectionRefused(Client player, string reason) { }

        /// <summary>
        /// Called when a player disconnects
        /// </summary>
        /// <param name="player">Player who disconnected</param>
        /// <returns>... Literally thrown out. Will be changed to return void.</returns>
        public virtual bool OnPlayerDisconnect(Client player) { return true; }

        /// <summary>
        /// Called when a player is spawned.
        /// </summary>
        /// <param name="player">Player who spawned.</param>
        public virtual void OnPlayerSpawned(Client player) { }
        /// <summary>
        /// Called on a chat message
        /// </summary>
        /// <param name="message">ChatMessage object</param>
        /// <returns>A new chat message, currently discarded. TODO: Allow message rewriting.</returns>
        public virtual ChatMessage OnChatMessage(ChatMessage message) { return message; }

        /// <summary>
        /// Called every tick.
        /// </summary>
        public virtual void OnTick() { }
    }
}