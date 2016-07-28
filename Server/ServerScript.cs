using Lidgren.Network;

namespace GTAServer
{
    public class ServerScript
    {
        public virtual string Name { get; set; }

        public virtual void Start() { }

        
        public virtual void OnIncomingConnection(Client player) { }
        public virtual bool OnPlayerConnect(Client player) { return true; }


        public virtual void OnConnectionRefused(Client player, string reason) { }

        public virtual bool OnPlayerDisconnect(Client player) { return true; }


        public virtual void OnPlayerSpawned(Client player) { }
        public virtual ChatMessage OnChatMessage(ChatMessage message) { return message; }

        public virtual void OnTick() { }
    }
}