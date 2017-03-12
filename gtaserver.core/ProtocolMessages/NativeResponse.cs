﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ProtoBuf;

// I'm just going to keep any natives-related code here.
namespace GTAServer.ProtocolMessages
{
    [ProtoContract]
    public class NativeResponse
    {
        [ProtoMember(1)]
        public NativeArgument Response { get; set; }
        [ProtoMember(2)]
        public string Id { get; set; }
    }

    [ProtoContract]
    public class NativeTickCall
    {
        [ProtoMember(1)]
        public NativeData Native { get; set; }
        [ProtoMember(2)]
        public string Id { get; set; }
    }

    [ProtoContract]
    public class NativeData
    {
        [ProtoMember(1)]
        public ulong Hash { get; set; }
        [ProtoMember(2)]
        public List<NativeArgument> Arguments { get; set; }
        [ProtoMember(3)]
        public NativeArgument ReturnType { get; set; }
        [ProtoMember(4)]
        public string Id { get; set; }
    }

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
        [ProtoMember(1)]
        public string Id { get; set; }
    }

    [ProtoContract]
    public class IntArgument : NativeArgument
    {
        [ProtoMember(1)]
        public int Data { get; set; }
    }

    [ProtoContract]
    public class UIntArgument : NativeArgument
    {
        [ProtoMember(1)]
        public uint Data { get; set; }
    }

    [ProtoContract]
    public class StringArgument : NativeArgument
    {
        [ProtoMember(1)]
        public string Data { get; set; }
    }

    [ProtoContract]
    public class FloatArgument : NativeArgument
    {
        [ProtoMember(1)]
        public float Data { get; set; }
    }

    [ProtoContract]
    public class BooleanArgument : NativeArgument
    {
        [ProtoMember(1)]
        public bool Data { get; set; }
    }

    [ProtoContract]
    public class Vector3Argument : NativeArgument
    {
        [ProtoMember(1)]
        public float X { get; set; }
        [ProtoMember(2)]
        public float Y { get; set; }
        [ProtoMember(3)]
        public float Z { get; set; }
    }

    [ProtoContract]
    public class LocalPlayerArgument : NativeArgument
    {        
    }

    [ProtoContract]
    public class LocalGamePlayerArgument : NativeArgument
    {
    }
}
