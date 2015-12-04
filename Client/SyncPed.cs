using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
using NativeUI;
using Font = GTA.Font;

namespace GTACoOp
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

        public int VehicleSeat;
        public int PedHealth;

        public int VehicleHealth;
        public int VehicleHash;
        public Quaternion VehicleRotation;
        public int VehiclePrimaryColor;
        public int VehicleSecondaryColor;
        public DateTime LastUpdateReceived;
        public string Name;
        public bool Siren;

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
        private Vehicle _mainVehicle;
        private uint _switch;
        private bool _lastAiming;
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

        private bool _isStreamedIn;
        private Blip _mainBlip;

        public SyncPed(int hash, Vector3 pos, Quaternion rot, bool blip = true)
        {
            Position = pos;
            Rotation = rot;
            ModelHash = hash;
            _blip = blip;

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
        public void DisplayLocally()
        {
            var gPos = IsInVehicle ? VehiclePosition : Position;
            var inRange = Game.Player.Character.IsInRangeOf(gPos, 100f);

            

            if (inRange && !_isStreamedIn)
            {
                _isStreamedIn = true;
                _mainBlip?.Remove();
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
                    SetBlipNameFromTextFile(_mainBlip, Name);
                }
                if(_blip && _mainBlip != null)
                    _mainBlip.Position = gPos;
                return;
            }


            if (Character == null || !Character.Exists() || Character.Model.Hash != ModelHash || (Character.IsDead && PedHealth > 0))
            {
                if (Character != null) Character.Delete();
                Character = World.CreatePed(new Model(ModelHash), Position, Rotation.Z);
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


                    new UIResText(Name, pos, 0.3f, Color.WhiteSmoke, Font.ChaletLondon, UIResText.Alignment.Centered)
                    {
                        Outline = true,
                    }.Draw();
                } //*/
            }

            if ((!_lastVehicle && IsInVehicle && VehicleHash != 0) || _mainVehicle == null || !Character.IsInVehicle(_mainVehicle) || _mainVehicle.Model.Hash != VehicleHash || VehicleSeat != Util.GetPedSeat(Character))
            {
                if (_mainVehicle != null && Util.IsVehicleEmpty(_mainVehicle))
                    _mainVehicle.Delete();

                var vehs = World.GetAllVehicles().OrderBy(v =>
                {
                    if (v == null) return float.MaxValue;
                    return (v.Position - Character.Position).Length();
                }).ToList();


                if (vehs.Any() && vehs[0].Model.Hash == VehicleHash)
                {
                    _mainVehicle = vehs[0];
                }
                else
                {
                    _mainVehicle = World.CreateVehicle(new Model(VehicleHash), VehiclePosition, 0);
                }

                if (_mainVehicle != null)
                {
                    _mainVehicle.PrimaryColor = (VehicleColor)VehiclePrimaryColor;
                    _mainVehicle.SecondaryColor = (VehicleColor)VehicleSecondaryColor;
                    _mainVehicle.Quaternion = VehicleRotation;
                    _mainVehicle.IsInvincible = true;
                    Character.Task.WarpIntoVehicle(_mainVehicle, (VehicleSeat)VehicleSeat);
                }

                _lastVehicle = true;
                _justEnteredVeh = true;
                _enterVehicleStarted = DateTime.Now;
                return;
            }
           
            if (_lastVehicle && _justEnteredVeh && IsInVehicle && !Character.IsInVehicle(_mainVehicle) && DateTime.Now.Subtract(_enterVehicleStarted).TotalSeconds <= 4)
            {
                return;
            }
            _justEnteredVeh = false;

            if (_lastVehicle && !IsInVehicle && _mainVehicle != null)
            {
                if (Character != null) Character.Task.LeaveVehicle(_mainVehicle, true);
            }

            Character.Health = PedHealth;

            _switch++;
            if (IsInVehicle)
            {
                if (VehicleSeat == (int)GTA.VehicleSeat.Driver ||
                    _mainVehicle.GetPedOnSeat(GTA.VehicleSeat.Driver) == null)
                {
                    _mainVehicle.Health = VehicleHealth;
                    if (_mainVehicle.Health <= 0)
                    {
                        _mainVehicle.IsInvincible = false;
                        //_mainVehicle.Explode();
                    }
                    else
                    {
                        _mainVehicle.IsInvincible = true;
                        if (_mainVehicle.IsDead)
                            _mainVehicle.Repair();
                    }
                    
                    _mainVehicle.PrimaryColor = (VehicleColor)VehiclePrimaryColor;
                    _mainVehicle.SecondaryColor = (VehicleColor)VehicleSecondaryColor;
                    
                    if (VehicleMods != null && _modSwitch % 50 == 0 && Game.Player.Character.IsInRangeOf(VehiclePosition, 30f))                   
                    {
                        var id = _modSwitch/50;

                        if (VehicleMods.ContainsKey(id) && VehicleMods[id] != _mainVehicle.GetMod((VehicleMod) id))
                        {
                            Function.Call(Hash.SET_VEHICLE_MOD_KIT, _mainVehicle.Handle, 0);
                            _mainVehicle.SetMod((VehicleMod)id, VehicleMods[id], false);
                            Function.Call(Hash.RELEASE_PRELOAD_MODS, id);
                        }
                    }
                    _modSwitch++;

                    if (_modSwitch >= 2500)
                        _modSwitch = 0;
                        

                    if (IsHornPressed && DateTime.Now.Subtract(_lastHornPress).TotalMilliseconds > 1500)
                    {
                        _mainVehicle.SoundHorn(1500);
                        _lastHornPress = DateTime.Now;
                    }

                    if (_mainVehicle.SirenActive && !Siren)
                        _mainVehicle.SirenActive = Siren;
                    else if (!_mainVehicle.SirenActive && Siren)
                        _mainVehicle.SirenActive = Siren;

                    var dir = VehiclePosition - _mainVehicle.Position;
                    dir.Normalize();

                    if (!_mainVehicle.IsInRangeOf(VehiclePosition, 0.08f))
                        _mainVehicle.ApplyForce(dir);
                    if (Main.GlobalSyncMode == SynchronizationMode.Teleport && !_mainVehicle.IsInRangeOf(VehiclePosition, 0.8f))
                        _mainVehicle.Position = VehiclePosition;
                    if (Main.GlobalSyncMode == SynchronizationMode.Tasks && !_mainVehicle.IsInRangeOf(VehiclePosition, 30f))
                        _mainVehicle.Position = VehiclePosition;
                    _mainVehicle.Quaternion = VehicleRotation;
                }

            }
            else
            {
                if (PedProps != null && _clothSwitch%50 == 0 && Game.Player.Character.IsInRangeOf(Position, 30f))
                {
                    var id = _clothSwitch/50;

                    if (PedProps.ContainsKey(id) && PedProps[id] != Function.Call<int>(Hash.GET_PED_DRAWABLE_VARIATION, Character.Handle, id))
                    {
                        Function.Call(Hash.SET_PED_COMPONENT_VARIATION, Character.Handle, id, PedProps[id], 0, 0);
                    }
                }
                
                _clothSwitch++;
                if (_clothSwitch >= 750)
                    _clothSwitch = 0;
                
                if (Character.Weapons.Current.Hash != (WeaponHash)CurrentWeapon)
                {
                    var wep = Character.Weapons.Give((WeaponHash)CurrentWeapon, 9999, true, true);
                    Character.Weapons.Select(wep);
                }

                if (!_lastJumping && IsJumping)
                {
                    Character.Task.Jump();
                }

                var dest = Position;

                const int threshold = 50;
                if (IsAiming && !IsShooting && !Character.IsInRangeOf(Position, 0.5f) && _switch % threshold == 0)
                {
                    Function.Call(Hash.TASK_GO_TO_COORD_WHILE_AIMING_AT_COORD, Character.Handle, dest.X, dest.Y,
                        dest.Z, AimCoords.X, AimCoords.Y, AimCoords.Z, 2f, 0, 0x3F000000, 0x40800000, 1, 512, 0,
                        (uint)FiringPattern.FullAuto);
                }
                else if (IsAiming && !IsShooting && Character.IsInRangeOf(Position, 0.5f))
                {
                    Character.Task.AimAt(AimCoords, 100);
                }

                if (!Character.IsInRangeOf(Position, 0.5f) &&
                    ((IsShooting && !_lastShooting) || (IsShooting && _lastShooting && _switch % (threshold * 2) == 0)))
                {
                    Function.Call(Hash.TASK_GO_TO_COORD_WHILE_AIMING_AT_COORD, Character.Handle, dest.X, dest.Y,
                        dest.Z, AimCoords.X, AimCoords.Y, AimCoords.Z, 2f, 1, 0x3F000000, 0x40800000, 1, 0, 0,
                        (uint)FiringPattern.FullAuto);
                }
                else if ((IsShooting && !_lastShooting) ||
                         (IsShooting && _lastShooting && _switch % (threshold / 2) == 0))
                {
                    Function.Call(Hash.TASK_SHOOT_AT_COORD, Character.Handle, AimCoords.X, AimCoords.Y,
                                AimCoords.Z, 1500, (uint)FiringPattern.FullAuto);
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
                _lastJumping = IsJumping;
                _lastShooting = IsShooting;
                _lastAiming = IsAiming;
            }
            _lastVehicle = IsInVehicle;
        }

        public void Clear()
        {
            if (_mainVehicle != null && Util.IsVehicleEmpty(_mainVehicle))
                _mainVehicle.Delete();
            if (Character != null) Character.Delete();
            if (_mainBlip != null) _mainBlip.Remove();
        }
    }
}