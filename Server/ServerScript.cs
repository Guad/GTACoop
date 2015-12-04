using Lidgren.Network;

namespace GTAServer
{
    public class ServerScript
    {
        public virtual string Name { get; set; }

        public virtual void Start()
        {
        }


        public virtual bool OnChatMessage(NetConnection sender, string message)
        {
            return true;
        }

        public virtual void OnPlayerConnect(NetConnection player)
        {
        }


        public virtual void OnPlayerDisconnect(NetConnection player)
        {
        }


        public virtual void OnConnectionRefused(NetConnection player, string reason)
        {
        }

        public virtual void OnPlayerKilled(NetConnection player)
        {
        }

        public virtual void OnTick()
        {
        }
    }
}