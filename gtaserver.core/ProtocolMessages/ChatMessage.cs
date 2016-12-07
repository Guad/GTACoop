using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GTAServer.ProtocolMessages
{
    public class ChatMessage
    {
        public Client Sender { get; set; }
        public Client Receiver { get; set; }
        public bool isPrivate { get; set; }
        public string Message { get; set; }
        public ConsoleColor Color { get; set; }
        public string Prefix { get; set; }
        public string Suffix { get; set; }
        public bool Suppress { get; set; }
    }

}
