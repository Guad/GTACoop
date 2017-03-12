using System.IO;
using Microsoft.Extensions.Logging;
using ProtoBuf;

namespace GTAServer
{
    public class Util
    {
        public static T DeserializeBinary<T>(byte[] data)
        {
            using (var stream = new MemoryStream(data))
            {
                return Serializer.Deserialize<T>(stream);
            }
        }

        public static byte[] SerializeBinary(object data)
        {
            using (var stream = new MemoryStream())
            {
                Serializer.Serialize(stream, data);
                return stream.ToArray();
            }
        }

        public static ILoggerFactory LoggerFactory;
    }
}
