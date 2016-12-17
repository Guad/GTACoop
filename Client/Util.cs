using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Serialization;
using GTA;
using GTA.Math;
using GTA.Native;

namespace GTAServer
{
    public static class Util
    {
        public static int GetStationId()
        {
            if (!Game.Player.Character.IsInVehicle()) return -1;
            return Function.Call<int>(Hash.GET_PLAYER_RADIO_STATION_INDEX);
        }

        public static string GetStationName(int id)
        {
            return Function.Call<string>(Hash.GET_RADIO_STATION_NAME, id);
        }

        public static int GetTrackId()
        {
            if (!Game.Player.Character.IsInVehicle()) return -1;
            return Function.Call<int>(Hash.GET_AUDIBLE_MUSIC_TRACK_TEXT_ID);
        }

        public static bool IsVehicleEmpty(Vehicle veh)
        {
            if (veh == null) return true;
            if (!veh.IsSeatFree(VehicleSeat.Driver)) return false;
            for (int i = 0; i < veh.PassengerSeats; i++)
            {
                if (!veh.IsSeatFree((VehicleSeat)i))
                    return false;
            }
            return true;
        }

        public static Dictionary<int, int> GetVehicleMods(Vehicle veh)
        {
            var dict = new Dictionary<int, int>();
            for (int i = 0; i < 50; i++)
            {
                dict.Add(i, veh.GetMod((VehicleMod)i));
            }
            return dict;
        }

        public static Dictionary<int, int> GetPlayerProps(Ped ped)
        {
            var props = new Dictionary<int, int>();
            for (int i = 0; i < 15; i++)
            {
                var mod = Function.Call<int>(Hash.GET_PED_DRAWABLE_VARIATION, ped.Handle, i);
                if (mod == -1) continue;
                props.Add(i, mod);
            }
            return props;
        }

        public static int GetPedSeat(Ped ped)
        {
            if (ped == null || !ped.IsInVehicle()) return -3;
            if (ped.CurrentVehicle.GetPedOnSeat(VehicleSeat.Driver) == ped) return (int)VehicleSeat.Driver;
            for (int i = 0; i < ped.CurrentVehicle.PassengerSeats; i++)
            {
                if (ped.CurrentVehicle.GetPedOnSeat((VehicleSeat)i) == ped)
                    return i;
            }
            return -3;
        }

        public static int GetFreePassengerSeat(Vehicle veh)
        {
            if (veh == null) return -3;
            for (int i = 0; i < veh.PassengerSeats; i++)
            {
                if (veh.IsSeatFree((VehicleSeat)i))
                    return i;
            }
            return -3;
        }

        public static PlayerSettings ReadSettings(string path)
        {
            var ser = new XmlSerializer(typeof(PlayerSettings));

            PlayerSettings settings = null;

            if (File.Exists(path))
            {
                using (var stream = File.OpenRead(path)) settings = (PlayerSettings)ser.Deserialize(stream);

                using (var stream = new FileStream(path, File.Exists(path) ? FileMode.Truncate : FileMode.Create, FileAccess.ReadWrite)) ser.Serialize(stream, settings);
            }
            else
            {
                using (var stream = File.OpenWrite(path)) ser.Serialize(stream, settings = new PlayerSettings());
            }

            return settings;
        }

        public static void SaveSettings(string path)
        {
            var ser = new XmlSerializer(typeof(PlayerSettings));
            using (var stream = new FileStream(path, File.Exists(path) ? FileMode.Truncate : FileMode.Create, FileAccess.ReadWrite)) ser.Serialize(stream, Main.PlayerSettings);
        }

        public static Vector3 GetLastWeaponImpact(Ped ped)
        {
            var coord = new OutputArgument();
            if (!Function.Call<bool>(Hash.GET_PED_LAST_WEAPON_IMPACT_COORD, ped.Handle, coord))
            {
                return new Vector3();
            }
            return coord.GetResult<Vector3>();
        }

        public static Quaternion LerpQuaternion(Quaternion start, Quaternion end, float speed)
        {
            return new Quaternion()
            {
                X = start.X + (end.X - start.X) * speed,
                Y = start.Y + (end.Y - start.Y) * speed,
                Z = start.Z + (end.Z - start.Z) * speed,
                W = start.W + (end.W - start.W) * speed,
            };
        }

        public static Vector3 LerpVector(Vector3 start, Vector3 end, float speed)
        {
            return new Vector3()
            {
                X = start.X + (end.X - start.X) * speed,
                Y = start.Y + (end.Y - start.Y) * speed,
                Z = start.Z + (end.Z - start.Z) * speed,
            };
        }

        public static Vector3 QuaternionToEuler(Quaternion quat)
        {
            //heading = atan2(2*qy*qw-2*qx*qz , 1 - 2*qy2 - 2*qz2) (yaw)
            //attitude = asin(2 * qx * qy + 2 * qz * qw) (pitch)
            //bank = atan2(2 * qx * qw - 2 * qy * qz, 1 - 2 * qx2 - 2 * qz2) (roll)

            return new Vector3()
            {
                X = (float)Math.Asin(2 * quat.X * quat.Y + 2 *quat.Z * quat.W),
                Y = (float)Math.Atan2(2 * quat.X * quat.W - 2 * quat.Y * quat.Z, 1 -  2 * quat.X*quat.X - 2 * quat.Z * quat.Z),
                Z = (float)Math.Atan2(2*quat.Y*quat.W - 2*quat.X*quat.Z, 1 - 2*quat.Y*quat.Y - 2*quat.Z * quat.Z),
            };

            /*except when qx*qy + qz*qw = 0.5 (north pole)
            which gives:
            heading = 2 * atan2(x,w)
            bank = 0

            and when qx*qy + qz*qw = -0.5 (south pole)
            which gives:
            heading = -2 * atan2(x,w)
            bank = 0 */
        }
    }
}