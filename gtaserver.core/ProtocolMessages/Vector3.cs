using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ProtoBuf;

namespace GTAServer.ProtocolMessages
{
    [ProtoContract]
    public class Vector3
    {
        public Vector3()
        {
            X = 0f;
            Y = 0f;
            Z = 0f;
        }

        public Vector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
        /// <summary>
        /// X
        /// </summary>
        [ProtoMember(1)]
        public float X { get; set; }
        /// <summary>
        /// Y
        /// </summary>
        [ProtoMember(2)]
        public float Y { get; set; }
        /// <summary>
        /// Z
        /// </summary>
        [ProtoMember(3)]
        public float Z { get; set; }
    }
}
