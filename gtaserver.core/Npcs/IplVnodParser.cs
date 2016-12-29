namespace GTAServer.Npcs {
    public enum PathType {
        Road = 0,
        Unknown = 2,
        Pedestrian = 10,
        Interior = 14,
        Stop = 15,
        Stop2 = 16,
        Stop3 = 17,
        Pedestrian2 = 18,
        Restricted = 19
    }
    public class PathVnod {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public bool IsPrimary { get; set; }
        public bool IsLand { get; set; }
        public float Unknown1 { get; set; }
        public float Unknown2 { get; set; }
        public PathType PathType { get; set; }
        public float Unknown3 { get; set; }
        public float Unknown4 { get; set; }
        public bool IsRoad { get;set; }
        public bool IsPrimaryOrSecondary { get; set; }
        public float Unknown5 { get; set; }
        public float Unknown6 { get; set; }
        public float StopRight { get; set; }
        public float StopStraight { get;set; }
        public bool IsMajor { get; set; }
        public float StopLeft { get; set; }
        public float Unknown7 { get; set; }
        public float Unknown8 { get; set; }
        public float Unknown9 { get; set; }
        public float Unknown10 { get; set; }
        
    }
}