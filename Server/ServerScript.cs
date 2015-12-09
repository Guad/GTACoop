using Lidgren.Network;

namespace GTAServer
{
    public class ServerScript
    {
        public virtual string Name { get; set; }

        public virtual void Start()
        {
        }


        public virtual bool OnChatMessage(Client sender, string message)
        {
            return true;
        }

        public virtual bool OnPlayerConnect(Client player)
        {
            return true;
        }

        public virtual void OnIncomingConnection(Client player)
        {
        }

        public virtual bool OnPlayerDisconnect(Client player)
        {
            return true;
        }


        public virtual void OnConnectionRefused(Client player, string reason)
        {
        }

        public virtual void OnPlayerKilled(Client player)
        {
        }

        public virtual void OnTick()
        {
        }
    }
}