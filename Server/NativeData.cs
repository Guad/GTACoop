using System;
using System.Collections.Generic;
using ProtoBuf;

namespace GTAServer
{
    /// <summary>
    /// Native call response
    /// </summary>
    [ProtoContract]
    public class NativeResponse
    {
        /// <summary>
        /// Response contents
        /// </summary>
        [ProtoMember(1)]
        public NativeArgument Response { get; set; }
        /// <summary>
        /// Response ID
        /// </summary>
        [ProtoMember(2)]
        public string Id { get; set; }
    }

    /// <summary>
    /// Native tick call
    /// </summary>
    [ProtoContract]
    public class NativeTickCall
    {
        /// <summary>
        /// Native data
        /// </summary>
        [ProtoMember(1)]
        public NativeData Native { get; set; }
        /// <summary>
        /// Native identifier
        /// </summary>
        [ProtoMember(2)]
        public string Identifier { get; set; }
    }

    /// <summary>
    /// Native data
    /// </summary>
    [ProtoContract]
    public class NativeData
    {
        /// <summary>
        /// Hash to call
        /// </summary>
        [ProtoMember(1)]
        public ulong Hash { get; set; }
        /// <summary>
        /// Arguments
        /// </summary>
        [ProtoMember(2)]
        public List<NativeArgument> Arguments { get; set; }
        /// <summary>
        /// Native argument return type
        /// </summary>
        [ProtoMember(3)]
        public NativeArgument ReturnType { get; set; }
        /// <summary>
        /// Native call ID
        /// </summary>
        [ProtoMember(4)]
        public string Id { get; set; }
    }

    /// <summary>
    /// Native argument
    /// </summary>
    [ProtoContract]
    [ProtoInclude(2, typeof(IntArgument))]
    [ProtoInclude(3, typeof(UIntArgument))]
    [ProtoInclude(4, typeof(StringArgument))]
    [ProtoInclude(5, typeof(FloatArgument))]
    [ProtoInclude(6, typeof(BooleanArgument))]
    [ProtoInclude(7, typeof(LocalPlayerArgument))]
    [ProtoInclude(8, typeof(Vector3Argument))]
    [ProtoInclude(9, typeof(LocalGamePlayerArgument))]
    public class NativeArgument
    {
        /// <summary>
        /// Native argument ID
        /// </summary>
        [ProtoMember(1)]
        public string Id { get; set; }
    }

    /// <summary>
    /// Local player argument
    /// </summary>
    [ProtoContract]
    public class LocalPlayerArgument : NativeArgument
    {
    }
    /// <summary>
    /// Local game player argument
    /// </summary>
    [ProtoContract]
    public class LocalGamePlayerArgument : NativeArgument
    {
    }
    /// <summary>
    /// Opponent ped handle argument
    /// </summary>
    [ProtoContract]
    public class OpponentPedHandleArgument : NativeArgument
    {
        public OpponentPedHandleArgument(long opponentHandle)
        {
            Data = opponentHandle;
        }
        /// <summary>
        /// Ped data
        /// </summary>
        [ProtoMember(1)]
        public long Data { get; set; }
    }

    /// <summary>
    /// Int argument
    /// </summary>
    [ProtoContract]
    public class IntArgument : NativeArgument
    {
        /// <summary>
        /// Integer
        /// </summary>
        [ProtoMember(1)]
        public int Data { get; set; }
    }
    /// <summary>
    /// UInt argument
    /// </summary>
    [ProtoContract]
    public class UIntArgument : NativeArgument
    {
        /// <summary>
        /// Unsigned integer
        /// </summary>
        [ProtoMember(1)]
        public uint Data { get; set; }
    }

    /// <summary>
    /// String argument
    /// </summary>
    [ProtoContract]
    public class StringArgument : NativeArgument
    {
        /// <summary>
        /// String
        /// </summary>
        [ProtoMember(1)]
        public string Data { get; set; }
    }
    /// <summary>
    /// Float argument
    /// </summary>
    [ProtoContract]
    public class FloatArgument : NativeArgument
    {
        /// <summary>
        /// Float
        /// </summary>
        [ProtoMember(1)]
        public float Data { get; set; }
    }

    /// <summary>
    /// Boolean argument
    /// </summary>
    [ProtoContract]
    public class BooleanArgument : NativeArgument
    {
        /// <summary>
        /// Bool
        /// </summary>
        [ProtoMember(1)]
        public bool Data { get; set; }
    }

    /// <summary>
    /// Vector3
    /// </summary>
    [ProtoContract]
    public class Vector3Argument : NativeArgument
    {
        /// <summary>
        /// Vector3's X
        /// </summary>
        [ProtoMember(1)]
        public float X { get; set; }
        /// <summary>
        /// Vector3's Y
        /// </summary>
        [ProtoMember(2)]
        public float Y { get; set; }
        /// <summary>
        /// Vector3's Z
        /// </summary>
        [ProtoMember(3)]
        public float Z { get; set; }
    }
}