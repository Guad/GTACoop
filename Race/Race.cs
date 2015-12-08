using GTAServer;


namespace Race
{
    public class Race
    {
        public Vector3[] Checkpoints;
        public Spawnpoint[] SpawnPoints;
        public int[] AvailableVehicles;
        public bool LapsAvailable = true;
        public Vector3 Trigger;

        public string Name;
        public string Description;

        public Race() { }

        public Race(Race copyFrom)
        {
            Checkpoints = copyFrom.Checkpoints;
            SpawnPoints = copyFrom.SpawnPoints;
            AvailableVehicles = copyFrom.AvailableVehicles;
            LapsAvailable = copyFrom.LapsAvailable;
            Trigger = copyFrom.Trigger;

            Name = copyFrom.Name;
            Description = copyFrom.Description;
        }
    }

    public class Spawnpoint
    {
        public Vector3 Position { get; set; }
        public float Heading { get; set; }
    }
}