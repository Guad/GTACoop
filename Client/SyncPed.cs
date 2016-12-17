using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
using NativeUI;
using Font = GTA.Font;

namespace GTAServer
{
    public enum SynchronizationMode
    {
        Tasks,
        Teleport,
    }

    public class SyncPed
    {
        public SynchronizationMode SyncMode;
        public long Host;
        public Ped Character;
        public Vector3 Position;
        public Quaternion Rotation;
        public bool IsInVehicle;
        public bool IsJumping;
        public int ModelHash;
        public int CurrentWeapon;
        public bool IsShooting;
        public bool IsAiming;
        public Vector3 AimCoords;
        public float Latency;
        public bool IsHornPressed;
        public Vehicle MainVehicle { get; set; }

        public int VehicleSeat;
        public int PedHealth;

        public int VehicleHealth;
        public int VehicleHash;
        public Quaternion VehicleRotation;
        public int VehiclePrimaryColor;
        public int VehicleSecondaryColor;
        public string Name;
        public bool Siren;

        public float Speed
        {
            get { return _speed; }
            set
            {
                _lastSpeed = _speed;
                _speed = value;
            }
        }

        public bool IsParachuteOpen;

        public double AverageLatency
        {
            get { return _latencyAverager.Average(); }
        }

        public DateTime LastUpdateReceived
        {
            get { return _lastUpdateReceived; }
            set
            {
                _secondToLastUpdateReceived = _lastUpdateReceived;
                _lastUpdateReceived = value;

                if (_secondToLastUpdateReceived != null && _lastUpdateReceived != null)
                {
                    _latencyAverager.Enqueue(_lastUpdateReceived.Subtract(_secondToLastUpdateReceived).TotalMilliseconds);
                    if (_latencyAverager.Count >= 10)
                        _latencyAverager.Dequeue();
                }
            }
        }

        public Dictionary<int, int> VehicleMods
        {
            get { return _vehicleMods; }
            set
            {
                if (value == null) return;
                _vehicleMods = value;
            }
        }

        public Dictionary<int, int> PedProps
        {
            get { return _pedProps; }
            set
            {
                if (value == null) return;
                _pedProps = value;
            }
        }

        private Vector3 _lastVehiclePos;
        public Vector3 VehiclePosition
        {
            get { return _vehiclePosition; }
            set
            {
                _lastVehiclePos = _vehiclePosition;
                _vehiclePosition = value;
            }
        }

        private bool _lastVehicle;
        private uint _switch;
        private bool _lastAiming;
        private float _lastSpeed;
        private DateTime _secondToLastUpdateReceived;
        private bool _lastShooting;
        private bool _lastJumping;
        private bool _blip;
        private bool _justEnteredVeh;
        private DateTime _lastHornPress = DateTime.Now;
        private int _relGroup;
        private DateTime _enterVehicleStarted;
        private Vector3 _vehiclePosition;
        private Dictionary<int, int> _vehicleMods;
        private Dictionary<int, int> _pedProps;

        private Queue<double> _latencyAverager;

        private int _playerSeat;
        private bool _isStreamedIn;
        private Blip _mainBlip;
        private bool _lastHorn;
        private Prop _parachuteProp;

        public SyncPed(int hash, Vector3 pos, Quaternion rot, bool blip = true)
        {
            Position = pos;
            Rotation = rot;
            ModelHash = hash;
            _blip = blip;

            _latencyAverager = new Queue<double>();

            _relGroup = World.AddRelationshipGroup("SYNCPED");
            World.SetRelationshipBetweenGroups(Relationship.Neutral, _relGroup, Game.Player.Character.RelationshipGroup);
            World.SetRelationshipBetweenGroups(Relationship.Neutral, Game.Player.Character.RelationshipGroup, _relGroup);
        }

        public void SetBlipNameFromTextFile(Blip blip, string text)
        {
            Function.Call(Hash._0xF9113A30DE5C6670, "STRING");
            Function.Call(Hash._ADD_TEXT_COMPONENT_STRING, text);
            Function.Call(Hash._0xBC38B49BCB83BC9B, blip);
        }

        private int _modSwitch = 0;
        private int _clothSwitch = 0;
        private DateTime _lastUpdateReceived;
        private float _speed;

        public void DisplayLocally()
        {
            const float hRange = 200f;
            var gPos = IsInVehicle ? VehiclePosition : Position;
            var inRange = Game.Player.Character.IsInRangeOf(gPos, hRange);
            
            if (inRange && !_isStreamedIn)
            {
                _isStreamedIn = true;
                if (_mainBlip != null)
                {
                    _mainBlip.Remove();
                    _mainBlip = null;
                }
            }
            else if(!inRange && _isStreamedIn)
            {
                Clear();
                _isStreamedIn = false;
            }

            if (!inRange)
            {
                if (_mainBlip == null && _blip)
                {
                    _mainBlip = World.CreateBlip(gPos);
                    _mainBlip.Color = BlipColor.White;
                    _mainBlip.Scale = 0.8f;
                    SetBlipNameFromTextFile(_mainBlip, Name == null ? "<nameless>" : Name);
                }
                if(_blip && _mainBlip != null)
                    _mainBlip.Position = gPos;
                return;
            }

            
            if (Character == null || !Character.Exists() || !Character.IsInRangeOf(gPos, hRange) || Character.Model.Hash != ModelHash || (Character.IsDead && PedHealth > 0))
            {
                if (Character != null) Character.Delete();

                Character = World.CreatePed(new Model(ModelHash), gPos, Rotation.Z);
                if (Character == null) return;

                Character.BlockPermanentEvents = true;
                Character.IsInvincible = true;
                Character.CanRagdoll = false;
                Character.RelationshipGroup = _relGroup;
                if (_blip)
                {
                    Character.AddBlip();
                    if (Character.CurrentBlip == null) return;
                    Character.CurrentBlip.Color = BlipColor.White;
                    Character.CurrentBlip.Scale = 0.8f;
                    SetBlipNameFromTextFile(Character.CurrentBlip, Name);
                }
                return;
            }

            if (!Character.IsOccluded && Character.IsInRangeOf(Game.Player.Character.Position, 20f))
            {
                var oldPos = UI.WorldToScreen(Character.Position + new Vector3(0, 0, 1.5f));
                if (oldPos.X != 0 && oldPos.Y != 0)
                {
                    var res = UIMenu.GetScreenResolutionMantainRatio();
                    var pos = new Point((int)((oldPos.X / (float)UI.WIDTH) * res.Width),
                        (int)((oldPos.Y / (float)UI.HEIGHT) * res.Height));


                    new UIResText(Name == null ? "<nameless>" : Name, pos, 0.3f, Color.WhiteSmoke, Font.ChaletLondon, UIResText.Alignment.Centered)
                    {
                        Outline = true,
                    }.Draw();
                }
            }

            if ((!_lastVehicle && IsInVehicle && VehicleHash != 0) || (_lastVehicle && IsInVehicle && (MainVehicle == null || !Character.IsInVehicle(MainVehicle) || MainVehicle.Model.Hash != VehicleHash || VehicleSeat != Util.GetPedSeat(Character))))
            {
                if (MainVehicle != null && Util.IsVehicleEmpty(MainVehicle))
                    MainVehicle.Delete();

                var vehs = World.GetAllVehicles().OrderBy(v =>
                {
                    if (v == null) return float.MaxValue;
                    return (v.Position - Character.Position).Length();
                }).ToList();


                if (vehs.Any() && vehs[0].Model.Hash == VehicleHash && vehs[0].IsInRangeOf(gPos, 3f))
                {
                    MainVehicle = vehs[0];
                    if (Game.Player.Character.IsInVehicle(MainVehicle) &&
                        VehicleSeat == Util.GetPedSeat(Game.Player.Character))
                    {
                        Game.Player.Character.Task.WarpOutOfVehicle(MainVehicle);
                        UI.Notify("~r~Car jacked!");
                    }
                }
                else
                {
                    MainVehicle = World.CreateVehicle(new Model(VehicleHash), gPos, 0);
                }

                if (MainVehicle != null)
                {
                    MainVehicle.PrimaryColor = (VehicleColor)VehiclePrimaryColor;
                    MainVehicle.SecondaryColor = (VehicleColor)VehicleSecondaryColor;
                    MainVehicle.Quaternion = VehicleRotation;
                    MainVehicle.IsInvincible = true;
                    Character.Task.WarpIntoVehicle(MainVehicle, (VehicleSeat)VehicleSeat);

                    /*if (_playerSeat != -2 && !Game.Player.Character.IsInVehicle(_mainVehicle))
                    { // TODO: Fix me.
                        Game.Player.Character.Task.WarpIntoVehicle(_mainVehicle, (VehicleSeat)_playerSeat);
                    }*/
                }

                _lastVehicle = true;
                _justEnteredVeh = true;
                _enterVehicleStarted = DateTime.Now;
                return;
            }
           
            if (_lastVehicle && _justEnteredVeh && IsInVehicle && !Character.IsInVehicle(MainVehicle) && DateTime.Now.Subtract(_enterVehicleStarted).TotalSeconds <= 4)
            {
                return;
            }
            _justEnteredVeh = false;

            if (_lastVehicle && !IsInVehicle && MainVehicle != null)
            {
                if (Character != null) Character.Task.LeaveVehicle(MainVehicle, true);
            }

            Character.Health = PedHealth;

            _switch++;
            if (IsInVehicle)
            {
                if (VehicleSeat == (int) GTA.VehicleSeat.Driver ||
                    MainVehicle.GetPedOnSeat(GTA.VehicleSeat.Driver) == null)
                {
                    MainVehicle.Health = VehicleHealth;
                    if (MainVehicle.Health <= 0)
                    {
                        MainVehicle.IsInvincible = false;
                        //_mainVehicle.Explode();
                    }
                    else
                    {
                        MainVehicle.IsInvincible = true;
                        if (MainVehicle.IsDead)
                            MainVehicle.Repair();
                    }

                    MainVehicle.PrimaryColor = (VehicleColor) VehiclePrimaryColor;
                    MainVehicle.SecondaryColor = (VehicleColor) VehicleSecondaryColor;

                    if (VehicleMods != null && _modSwitch%50 == 0 &&
                        Game.Player.Character.IsInRangeOf(VehiclePosition, 30f))
                    {
                        var id = _modSwitch/50;

                        if (VehicleMods.ContainsKey(id) && VehicleMods[id] != MainVehicle.GetMod((VehicleMod) id))
                        {
                            Function.Call(Hash.SET_VEHICLE_MOD_KIT, MainVehicle.Handle, 0);
                            MainVehicle.SetMod((VehicleMod) id, VehicleMods[id], false);
                            Function.Call(Hash.RELEASE_PRELOAD_MODS, id);
                        }
                    }
                    _modSwitch++;

                    if (_modSwitch >= 2500)
                        _modSwitch = 0;

                    if (IsHornPressed && !_lastHorn)
                    {
                        _lastHorn = true;
                        MainVehicle.SoundHorn(99999);
                    }

                    if (!IsHornPressed && _lastHorn)
                    {
                        _lastHorn = false;
                        MainVehicle.SoundHorn(1);
                    }

                    if (MainVehicle.SirenActive && !Siren)
                        MainVehicle.SirenActive = Siren;
                    else if (!MainVehicle.SirenActive && Siren)
                        MainVehicle.SirenActive = Siren;

                    var dir = VehiclePosition - _lastVehiclePos;

                    dir.Normalize();

                    var range = Math.Max(20f, Speed*Math.Ceiling(DateTime.Now.Subtract(LastUpdateReceived).TotalSeconds));

                    if (MainVehicle.IsInRangeOf(VehiclePosition, (float) range))
                    {
                        var timeElapsed = (float) DateTime.Now.Subtract(LastUpdateReceived).TotalSeconds;
                        var acceleration = Speed - _lastSpeed;
                        MainVehicle.Position = _lastVehiclePos + dir*(Speed*timeElapsed) +
                                               dir*(0.5f*acceleration*(float) Math.Pow(timeElapsed, 2));
                    }
                    else
                    {
                        MainVehicle.Position = VehiclePosition;
                        _lastVehiclePos = VehiclePosition - (dir*0.5f);
                    }
                    #if DEBUG
                    if (MainVehicle.Heading < 270)
                        MainVehicle.Quaternion = Util.LerpQuaternion(MainVehicle.Quaternion, VehicleRotation, 0.1f);
                    else if (MainVehicle.Heading >= 270)
                        MainVehicle.Quaternion = Util.LerpQuaternion(VehicleRotation, MainVehicle.Quaternion, 0.1f);
                    #else
                    MainVehicle.Quaternion = VehicleRotation;
                    #endif
                }
            }
                else
                {
                    if (PedProps != null && _clothSwitch%50 == 0 && Game.Player.Character.IsInRangeOf(Position, 30f))
                    {
                        var id = _clothSwitch/50;

                        if (PedProps.ContainsKey(id) &&
                            PedProps[id] != Function.Call<int>(Hash.GET_PED_DRAWABLE_VARIATION, Character.Handle, id))
                        {
                            Function.Call(Hash.SET_PED_COMPONENT_VARIATION, Character.Handle, id, PedProps[id], 0, 0);
                        }
                    }

                    _clothSwitch++;
                    if (_clothSwitch >= 750)
                        _clothSwitch = 0;

                    if (Character.Weapons.Current.Hash != (WeaponHash) CurrentWeapon)
                    {
                        var wep = Character.Weapons.Give((WeaponHash) CurrentWeapon, 9999, true, true);
                        Character.Weapons.Select(wep);
                    }

                    if (!_lastJumping && IsJumping)
                    {
                        Character.Task.Jump();
                    }

                    if (IsParachuteOpen)
                    {
                        if (_parachuteProp == null)
                        {
                            _parachuteProp = World.CreateProp(new Model(1740193300), Character.Position,
                                Character.Rotation, false, false);
                            _parachuteProp.FreezePosition = true;
                            Function.Call(Hash.SET_ENTITY_COLLISION, _parachuteProp.Handle, false, 0);
                        }
                        Character.FreezePosition = true;
                        Character.Position = Position - new Vector3(0, 0, 1);
                        Character.Quaternion = Rotation;
                        _parachuteProp.Position = Character.Position + new Vector3(0, 0, 3.7f);
                        _parachuteProp.Quaternion = Character.Quaternion;

                        Character.Task.PlayAnimation("skydive@parachute@first_person", "chute_idle_right", 8f, 5000,
                            false, 8f);
                    }
                    else
                    {
                        var dest = Position;
                        Character.FreezePosition = false;

                        if (_parachuteProp != null)
                        {
                            _parachuteProp.Delete();
                            _parachuteProp = null;
                        }

                        const int threshold = 50;
                        if (IsAiming && !IsShooting && !Character.IsInRangeOf(Position, 0.5f) && _switch%threshold == 0)
                        {
                            Function.Call(Hash.TASK_GO_TO_COORD_WHILE_AIMING_AT_COORD, Character.Handle, dest.X, dest.Y,
                                dest.Z, AimCoords.X, AimCoords.Y, AimCoords.Z, 2f, 0, 0x3F000000, 0x40800000, 1, 512, 0,
                                (uint) FiringPattern.FullAuto);
                        }
                        else if (IsAiming && !IsShooting && Character.IsInRangeOf(Position, 0.5f))
                        {
                            Character.Task.AimAt(AimCoords, 100);
                        }

                        if (!Character.IsInRangeOf(Position, 0.5f) &&
                            ((IsShooting && !_lastShooting) ||
                             (IsShooting && _lastShooting && _switch%(threshold*2) == 0)))
                        {
                            Function.Call(Hash.TASK_GO_TO_COORD_WHILE_AIMING_AT_COORD, Character.Handle, dest.X, dest.Y,
                                dest.Z, AimCoords.X, AimCoords.Y, AimCoords.Z, 2f, 1, 0x3F000000, 0x40800000, 1, 0, 0,
                                (uint) FiringPattern.FullAuto);
                        }
                        else if ((IsShooting && !_lastShooting) ||
                                 (IsShooting && _lastShooting && _switch%(threshold/2) == 0))
                        {
                            Function.Call(Hash.TASK_SHOOT_AT_COORD, Character.Handle, AimCoords.X, AimCoords.Y,
                                AimCoords.Z, 1500, (uint) FiringPattern.FullAuto);
                        }

                        if (!IsAiming && !IsShooting && !IsJumping)
                        {
                            switch (SyncMode)
                            {
                                case SynchronizationMode.Tasks:
                                    if (!Character.IsInRangeOf(Position, 0.5f))
                                    {
                                        Character.Task.RunTo(Position, true, 500);
                                        //var targetAngle = Rotation.Z/Math.Sqrt(1 - Rotation.W*Rotation.W);
                                        //Function.Call(Hash.TASK_GO_STRAIGHT_TO_COORD, Character.Handle, Position.X, Position.Y, Position.Z, 5f, 3000, targetAngle, 0);
                                    }
                                    if (!Character.IsInRangeOf(Position, 5f))
                                    {
                                        Character.Position = dest - new Vector3(0, 0, 1f);
                                        Character.Quaternion = Rotation;
                                    }
                                    break;
                                case SynchronizationMode.Teleport:
                                    Character.Position = dest - new Vector3(0, 0, 1f);
                                    Character.Quaternion = Rotation;
                                    break;
                            }
                        }
                    }
                    _lastJumping = IsJumping;
                    _lastShooting = IsShooting;
                    _lastAiming = IsAiming;
                }
                _lastVehicle = IsInVehicle;
        }

        public void Clear()
        {
            /*if (_mainVehicle != null && Character.IsInVehicle(_mainVehicle) && Game.Player.Character.IsInVehicle(_mainVehicle))
            {
                _playerSeat = Util.GetPedSeat(Game.Player.Character);
            }
            else
            {
                _playerSeat = -2;
            }*/

            if (Character != null)
            {
                Character.Model.MarkAsNoLongerNeeded();
                Character.Delete();
            }
            if (_mainBlip != null)
            {
                _mainBlip.Remove();
                _mainBlip = null;
            }
            if (MainVehicle != null && Util.IsVehicleEmpty(MainVehicle))
            {
                MainVehicle.Model.MarkAsNoLongerNeeded();
                MainVehicle.Delete();
            }
            if (_parachuteProp != null)
            {
                _parachuteProp.Delete();
                _parachuteProp = null;
            }
        }
    }
}