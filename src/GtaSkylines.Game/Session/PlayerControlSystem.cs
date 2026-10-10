using System;
using System.Collections.Generic;
using GtaSkylines.Game.Camera;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Interiors;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Physics;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Session
{
    /// <summary>The player on foot, interaction targeting, getting in and out of cars (order 10). Rules on <see cref="GameSession"/>.</summary>
    internal sealed class PlayerControlSystem : IGameSystem
    {
        private const float BuildingQueryRadius = 80f;
        private const float BuildingRequeryDistance = 20f;
        private const float TeleportDistance = 1f;
        private const string TalkPrompt = "Press {Interact} to talk";

        private readonly CameraRig _rig;
        private readonly InteriorSystem _interiors;
        private readonly List<ushort> _nearBuildings = new List<ushort>();
        private readonly List<Contact> _contacts = new List<Contact>();
        private readonly Dictionary<int, string> _carPrompts = new Dictionary<int, string>();
        private readonly Dictionary<ushort, string> _doorPrompts = new Dictionary<ushort, string>();
        private bool _queried;
        private Vec3 _queryAt;
        private Vec3 _lastPos;
        private bool _sprintExhausted;
        private int _targetTag;
        private Vec3 _doorTarget;
        private float _pendingDriven;

        public PlayerControlSystem(CameraRig rig, InteriorSystem interiors)
        {
            _rig = rig;
            _interiors = interiors;
        }

        public int Order { get { return SystemOrder.PlayerControl; } }

        /// <summary>True while the car the player is leaving must brake (GetOut waiting for &lt; 3 m/s).</summary>
        internal static bool WantsBrake(GameWorld w) { return w.PlayerState.Transition == VehicleTransition.GetOut; }

        public void Step(GameWorld world, float dt)
        {
            Ped p = world.Player;
            if (p == null) return;
            PlayerState ps = world.PlayerState;
            InputFrame inp = world.Input;
            bool teleported = (p.Position - _lastPos).Length > TeleportDistance;
            if (!IsInside(world) && (!_queried || teleported || (p.Position - _queryAt).Length > BuildingRequeryDistance))
            {
                world.City.BuildingsNear(p.Position, BuildingQueryRadius, _nearBuildings);
                _queried = true;
                _queryAt = p.Position;
            }

            if (!inp.IsHeld(GameButton.WeaponWheel))
            {
                ps.ViewYawDeg = MathUtil.WrapDeg(ps.ViewYawDeg + inp.LookYawDeg);
                ps.ViewPitchDeg = MathUtil.Clamp(ps.ViewPitchDeg + inp.LookPitchDeg, -CameraRig.FootPitchLimit, CameraRig.FootPitchLimit);
            }
            ps.Interaction = new InteractionTarget { Index = -1 };

            if (p.Has(PedFlags.InVehicle))
            {
                StepInCar(world, p, ps, inp, dt);
                _lastPos = p.Position;
                return;
            }

            if (ps.Life != LifeState.Alive || p.IsDead)
            {
                ps.Sprinting = false;
                p.Motor.Step(dt, new MotorInput { FacingYawDeg = p.YawDeg }, world.Collision, world.City);
                _lastPos = p.Position;
                return;
            }

            InteriorPhase phase = world.Interior.Phase;
            bool fading = phase != InteriorPhase.Outside && phase != InteriorPhase.Inside;
            bool frozen = fading || world.Menu.IsOpen;
            bool knocked = p.Has(PedFlags.KnockedDown);

            Vec3 before = p.Position;
            MotorInput mi = new MotorInput { FacingYawDeg = p.YawDeg };
            ps.Sprinting = false;
            bool aiming = false;

            if (ps.Transition != VehicleTransition.None)
            {
                StepTransition(world, p, ps, inp, dt, ref mi);
            }
            else if (!frozen && !knocked)
            {
                if (inp.WasPressed(GameButton.Sneak))
                {
                    ps.Sneaking = !ps.Sneaking;
                    if (ps.Sneaking) p.Flags |= PedFlags.Sneaking; else p.Flags &= ~PedFlags.Sneaking;
                }
                bool handsUp = inp.IsHeld(GameButton.HandsUp);
                if (handsUp && !p.Has(PedFlags.HandsUp))
                {
                    p.Flags |= PedFlags.HandsUp;
                    world.Emit(GameEventType.HandsUp, p.Position, p.Tag, p.Tag, 0f, null);
                }
                else if (!handsUp) p.Flags &= ~PedFlags.HandsUp;
                aiming = inp.IsHeld(GameButton.Aim) && !handsUp;

                float mx = inp.MoveX, my = inp.MoveY;
                float len = (float)Math.Sqrt(mx * mx + my * my);
                if (len > 1f) { mx /= len; my /= len; len = 1f; }
                if (handsUp) { mx = my = 0f; len = 0f; }

                if (inp.WasPressed(GameButton.Sprint)) _sprintExhausted = false;
                bool sprint = inp.IsHeld(GameButton.Sprint) && my > 0f && ps.Stamina > 0f && !_sprintExhausted && !aiming;
                float speed = sprint ? PlayerRules.SprintSpeed : ps.Sneaking ? PlayerRules.WalkSpeed : PlayerRules.JogSpeed;
                Vec3 fwd = Vec3.FromYawDeg(ps.ViewYawDeg), right = Vec3.FromYawDeg(ps.ViewYawDeg + 90f);
                Vec3 dir = right * mx + fwd * my;
                mi.DesiredVelocity = dir * speed;
                mi.Jump = inp.WasPressed(GameButton.Jump);
                ps.Sprinting = sprint && len > 0.01f;

                if (_rig.FootView == FootView.FirstPerson || aiming) p.YawDeg = ps.ViewYawDeg;
                else if (len > 0.01f) p.YawDeg = MathUtil.MoveTowardsAngleDeg(p.YawDeg, Vec3.YawDegOf(dir), PlayerRules.TurnRateDegPerSecond * dt);
                mi.FacingYawDeg = p.YawDeg;
            }
            if (aiming) p.Flags |= PedFlags.Aiming; else p.Flags &= ~PedFlags.Aiming;

            if (ps.Sprinting)
            {
                ps.Stamina = Math.Max(0f, ps.Stamina - PlayerRules.StaminaDrainPerSecond * dt);
                ps.SecondsSinceSprint = 0f;
                if (ps.Stamina <= 0f) _sprintExhausted = true;
            }
            else
            {
                ps.SecondsSinceSprint += dt;
                bool holdingSprint = world.Input.IsHeld(GameButton.Sprint) && _sprintExhausted;
                if (ps.SecondsSinceSprint >= PlayerRules.StaminaRefillDelay && !holdingSprint)
                    ps.Stamina = Math.Min(PlayerRules.MaxStamina, ps.Stamina + PlayerRules.StaminaRefillPerSecond * dt);
            }

            MotorStepResult r = p.Motor.Step(dt, mi, world.Collision, world.City);
            if (r.Landed && r.ImpactSpeed > PlayerRules.FallDamageMinSpeed)
            {
                float dmg = Math.Min(p.Health, (r.ImpactSpeed - PlayerRules.FallDamageMinSpeed) * PlayerRules.FallDamagePerSpeed);
                p.Health -= dmg;
                world.Emit(GameEventType.Fell, p.Position, p.Tag, 0, dmg, null);
            }
            if (!teleported) AddDistance(world, ps, Vec3.DistanceXZ(before, p.Position));
            p.Anim = AnimFor(p, ps, aiming);

            if (ps.Transition == VehicleTransition.None && !frozen && !knocked)
            {
                FindTarget(world, p, ps);
                if (ps.Interaction.Kind != InteractionTargetKind.None) world.Messages.ShowHelp(ps.Interaction.Prompt, 1, 0f);
                Use(world, p, ps, inp);
            }
            _lastPos = p.Position;
        }

        private static bool IsInside(GameWorld w) { return w.Interior.IsInside; }

        private static PedAnim AnimFor(Ped p, PlayerState ps, bool aiming)
        {
            switch (p.Motor.State)
            {
                case MotorState.Climbing: return PedAnim.Climb;
                case MotorState.Swimming: return PedAnim.Swim;
                case MotorState.Airborne: return PedAnim.Fall;
            }
            if (ps.Transition == VehicleTransition.WalkToDoor) return PedAnim.Run;
            if (ps.Transition != VehicleTransition.None) return ps.Transition == VehicleTransition.Bail ? PedAnim.ExitVehicle : PedAnim.EnterVehicle;
            if (p.Has(PedFlags.HandsUp)) return PedAnim.HandsUp;
            if (aiming) return PedAnim.Aim;
            float v = p.Motor.Velocity.Flat.Length;
            if (v < 0.2f) return PedAnim.Idle;
            if (ps.Sprinting) return PedAnim.Sprint;
            return v <= 2f ? PedAnim.Walk : PedAnim.Run;
        }

        private static void AddDistance(GameWorld w, PlayerState ps, float d)
        {
            ps.PendingDistance += d;
            if (ps.PendingDistance >= 1f)
            {
                long whole = (long)ps.PendingDistance;
                w.Profile.AddStat(PlayerRules.StatDistanceWalked, whole);
                w.Profile.AddStat(StatNames.DistanceM, whole);
                ps.PendingDistance -= whole;
            }
        }

        internal void FlushDistance(GameWorld w)
        {
            PlayerState ps = w.PlayerState;
            long walked = (long)Math.Round(ps.PendingDistance);
            if (walked > 0) { w.Profile.AddStat(PlayerRules.StatDistanceWalked, walked); w.Profile.AddStat(StatNames.DistanceM, walked); }
            ps.PendingDistance = 0f;
            long driven = (long)Math.Round(_pendingDriven);
            if (driven > 0) { w.Profile.AddStat(PlayerRules.StatDistanceDriven, driven); w.Profile.AddStat(StatNames.DistanceM, driven); }
            _pendingDriven = 0f;
        }

        // ---- targeting -------------------------------------------------------------------------------------------

        private static bool InFront(Ped p, PlayerState ps, Vec3 target, float halfAngle)
        {
            Vec3 d = (target - p.Position).Flat;
            if (d.Length < 0.3f) return true;
            float yaw = Vec3.YawDegOf(d);
            return Math.Abs(MathUtil.DeltaAngleDeg(p.YawDeg, yaw)) <= halfAngle || Math.Abs(MathUtil.DeltaAngleDeg(ps.ViewYawDeg, yaw)) <= halfAngle;
        }

        private void FindTarget(GameWorld w, Ped p, PlayerState ps)
        {
            Vec3 pos = p.Position;
            bool inside = w.Interior.IsInside;
            float best = PlayerRules.EnterVehicleReach;
            if (!inside)
            {
                List<AmbientVehicle> vs = w.Ambient.Vehicles;
                for (int i = 0; i < vs.Count; i++)
                {
                    AmbientVehicle v = vs[i];
                    if (w.IsPromotedVehicle(v.Id, v.Parked)) continue;
                    if (!v.Parked && v.Velocity.Length >= PlayerRules.EnterMovingMaxSpeed) continue;
                    VehicleModel m = w.City.GetVehicleModel(v.Model);
                    if (m == null) continue;
                    Vec3 door = GameWorld.DoorOf(m, v.Position, v.Rotation, true);
                    float d = Vec3.DistanceXZ(pos, door);
                    if (d <= best && Math.Abs(door.Y - pos.Y) < 2.5f && InFront(p, ps, door, 60f))
                    {
                        best = d;
                        SetTarget(ps, InteractionTargetKind.EnterVehicle, CarPrompt(m), WorldTag.ForAmbientVehicle(v.Id, v.Parked), door);
                    }
                }
            }
            List<Car> cars = w.CarList;
            for (int i = 0; i < cars.Count; i++)
            {
                Car c = cars[i];
                if (c.Locked || c.Destroyed) continue;
                Vec3 door = w.DoorPosition(c, true);
                float d = Vec3.DistanceXZ(pos, door);
                if (d <= best && Math.Abs(door.Y - pos.Y) < 2.5f && InFront(p, ps, door, 60f))
                {
                    best = d;
                    SetTarget(ps, InteractionTargetKind.EnterVehicle, CarPrompt(c.Model), c.Tag, door);
                }
            }
            if (ps.Interaction.Kind != InteractionTargetKind.None) return;

            if (inside)
            {
                if (w.Interior.Phase != InteriorPhase.Inside) return;
                InteriorLayout l = w.Interior.Layout;
                float bestPoint = PlayerRules.InteriorPointReach;
                for (int i = 0; i < l.Interactions.Count; i++)
                {
                    Vec3 at = w.Interior.ToWorld(l.Interactions[i].Position);
                    float d = Vec3.DistanceXZ(pos, at);
                    if (d <= bestPoint)
                    {
                        bestPoint = d;
                        SetTarget(ps, InteractionTargetKind.InteriorPoint, PointPrompt(l.Interactions[i].Kind), 0, at);
                        ps.Interaction.Index = i;
                    }
                }
                List<CustomInteraction> custom = w.Interior.CustomInteractions;
                for (int i = 0; i < custom.Count; i++)
                {
                    Vec3 at = w.Interior.ToWorld(custom[i].LocalPosition);
                    float d = Vec3.DistanceXZ(pos, at);
                    if (d <= bestPoint)
                    {
                        bestPoint = d;
                        SetTarget(ps, InteractionTargetKind.CustomInteraction, custom[i].Prompt, 0, at);
                        ps.Interaction.Index = i;
                    }
                }
            }
            else if (w.Interior.Phase == InteriorPhase.Outside)
            {
                float bestDoor = PlayerRules.BuildingDoorReach;
                for (int i = 0; i < _nearBuildings.Count; i++)
                {
                    BuildingDescriptor b;
                    if (!w.City.TryGetBuilding(_nearBuildings[i], out b) || b.Abandoned || b.Doors == null) continue;
                    for (int k = 0; k < b.Doors.Length; k++)
                    {
                        float d = Vec3.DistanceXZ(pos, b.Doors[k]);
                        if (d <= bestDoor)
                        {
                            bestDoor = d;
                            SetTarget(ps, InteractionTargetKind.BuildingDoor, DoorPrompt(b), 0, b.Doors[k]);
                            ps.Interaction.Building = b.Id;
                        }
                    }
                }
            }
            if (ps.Interaction.Kind != InteractionTargetKind.None) return;

            float bestPerson = PlayerRules.TalkReach;
            List<Ped> peds = w.PedList;
            for (int i = 0; i < peds.Count; i++)
            {
                Ped o = peds[i];
                if (o == p || (o.Flags & (PedFlags.InVehicle | PedFlags.Dead | PedFlags.Invisible)) != 0) continue;
                float d = Vec3.DistanceXZ(pos, o.Position);
                if (d <= bestPerson && Math.Abs(o.Position.Y - pos.Y) < 2f && InFront(p, ps, o.Position, 90f))
                {
                    bestPerson = d;
                    SetTarget(ps, InteractionTargetKind.Talk, TalkPrompt, o.Tag, o.Position);
                }
            }
            if (!inside)
            {
                List<AmbientCitizen> cs = w.Ambient.Citizens;
                for (int i = 0; i < cs.Count; i++)
                {
                    AmbientCitizen c = cs[i];
                    if (w.IsPromotedCitizen(c.Citizen)) continue;
                    float d = Vec3.DistanceXZ(pos, c.Position);
                    if (d <= bestPerson && Math.Abs(c.Position.Y - pos.Y) < 2f && InFront(p, ps, c.Position, 90f))
                    {
                        bestPerson = d;
                        SetTarget(ps, InteractionTargetKind.Talk, TalkPrompt, WorldTag.ForAmbientCitizen(c.Instance), c.Position);
                    }
                }
            }
        }

        private static void SetTarget(PlayerState ps, InteractionTargetKind kind, string prompt, int tag, Vec3 at)
        {
            ps.Interaction = new InteractionTarget { Kind = kind, Prompt = prompt, TargetTag = tag, Position = at, Index = -1 };
        }

        private string CarPrompt(VehicleModel m)
        {
            string s;
            if (!_carPrompts.TryGetValue(m.Id, out s)) { s = "Press {EnterExit} to enter " + m.Name; _carPrompts[m.Id] = s; }
            return s;
        }

        private string DoorPrompt(BuildingDescriptor b)
        {
            string s;
            if (!_doorPrompts.TryGetValue(b.Id, out s)) { s = "Press {Interact} to enter " + b.Name; _doorPrompts[b.Id] = s; }
            return s;
        }

        private static string PointPrompt(InteractionKind kind)
        {
            switch (kind)
            {
                case InteractionKind.ExitDoor: return "Press {Interact} to leave";
                case InteractionKind.ElevatorDoor: return "Press {Interact} to take the elevator";
                case InteractionKind.ShopCounter: return "Press {Interact} to shop";
                case InteractionKind.Bed: return "Press {Interact} to use the bed";
                case InteractionKind.Wardrobe: return "Press {Interact} to change clothes";
                case InteractionKind.HospitalDesk: return "Press {Interact} for treatment";
                case InteractionKind.PoliceDesk: return "Press {Interact} to pay off your heat";
                case InteractionKind.RespraySpot: return "Press {Interact} to respray your car";
                default: return "Press {Interact}";
            }
        }

        private void Use(GameWorld w, Ped p, PlayerState ps, InputFrame inp)
        {
            InteractionTarget t = ps.Interaction;
            if (t.Kind == InteractionTargetKind.EnterVehicle && inp.WasPressed(GameButton.EnterExit))
            {
                _targetTag = t.TargetTag;
                _doorTarget = t.Position;
                ps.Transition = VehicleTransition.WalkToDoor;
                ps.TransitionSeconds = 0f;
                ps.TransitionCarId = 0;
                return;
            }
            if (!inp.WasPressed(GameButton.Interact)) return;
            switch (t.Kind)
            {
                case InteractionTargetKind.BuildingDoor:
                    BuildingDescriptor b;
                    if (w.City.TryGetBuilding(t.Building, out b)) _interiors.RequestEnter(w, b, t.Position);
                    break;
                case InteractionTargetKind.InteriorPoint:
                    _interiors.UsePoint(w, t.Index);
                    break;
                case InteractionTargetKind.CustomInteraction:
                    w.Emit(GameEventType.CustomInteraction, t.Position, p.Tag, 0, w.Interior.CustomInteractions[t.Index].Id, null);
                    break;
                case InteractionTargetKind.Talk:
                    Talk(w, p, t.TargetTag);
                    break;
            }
        }

        private static void Talk(GameWorld w, Ped p, int tag)
        {
            Ped other = w.PedFromTag(tag, false, 0);
            uint citizen = 0;
            if (other != null) citizen = other.Citizen;
            else
            {
                AmbientCitizen c;
                if (w.TryGetAmbientCitizen((ushort)WorldTag.IdOf(tag), out c)) citizen = c.Citizen;
            }
            string line = w.Hooks.Dialogue == null ? null : w.Hooks.Dialogue.Line(w, DialogueContext.Greeting, citizen, other);
            if (line != null)
            {
                CitizenProfile prof;
                string speaker = citizen != 0 && w.City.TryGetCitizenProfile(citizen, out prof) ? prof.Name : null;
                w.Messages.QueueSubtitle(speaker, line, 3f);
            }
            Vec3 at;
            w.TryGetTagPosition(tag, out at);
            w.Emit(GameEventType.Talked, at, p.Tag, tag, 0f, line);
        }

        // ---- getting in ----------------------------------------------------------------------------------------

        private void StepTransition(GameWorld w, Ped p, PlayerState ps, InputFrame inp, float dt, ref MotorInput mi)
        {
            ps.TransitionSeconds += dt;
            switch (ps.Transition)
            {
                case VehicleTransition.WalkToDoor:
                    if (Math.Abs(inp.MoveX) > 0.1f || Math.Abs(inp.MoveY) > 0.1f) { ps.Transition = VehicleTransition.None; return; }
                    Vec3 to = (_doorTarget - p.Position).Flat;
                    float d = to.Length;
                    if (d > 0.05f)
                    {
                        p.YawDeg = Vec3.YawDegOf(to);
                        mi.DesiredVelocity = to / d * Math.Min(PlayerRules.JogSpeed, d / dt * 0.5f);
                        mi.FacingYawDeg = p.YawDeg;
                    }
                    if (d < 0.35f || ps.TransitionSeconds >= PlayerRules.WalkToDoorMaxSeconds) Take(w, p, ps);
                    break;
                case VehicleTransition.PullOut:
                    if (ps.TransitionSeconds >= PlayerRules.PullOutSeconds) { ps.Transition = VehicleTransition.GetIn; ps.TransitionSeconds = 0f; }
                    break;
                case VehicleTransition.GetIn:
                    if (ps.TransitionSeconds >= PlayerRules.GetInSeconds) SeatPlayer(w, p, ps);
                    break;
                case VehicleTransition.Bail:
                    if (ps.TransitionSeconds >= PlayerRules.BailSeconds) ps.Transition = VehicleTransition.None;
                    break;
                default:
                    ps.Transition = VehicleTransition.None;
                    break;
            }
        }

        private void Take(GameWorld w, Ped p, PlayerState ps)
        {
            ps.TransitionSeconds = 0f;
            ps.Transition = VehicleTransition.None;
            TagKind kind = WorldTag.KindOf(_targetTag);
            Car car;
            if (kind == TagKind.Car)
            {
                car = w.FindCar(WorldTag.IdOf(_targetTag));
                if (car == null || car.Locked || car.Destroyed) return;
            }
            else
            {
                AmbientVehicle v;
                if (!w.TryGetAmbientVehicle((ushort)WorldTag.IdOf(_targetTag), kind == TagKind.AmbientParkedVehicle, out v)) return;
                car = w.PromoteVehicle(v, !v.Parked, PedOrigin.CarjackedDriver, p.Tag);
                if (car == null) return;
            }
            ps.TransitionCarId = car.Id;
            Ped driver = car.DriverId != 0 ? w.FindPed(car.DriverId) : null;
            if (driver != null && driver != p)
            {
                Vec3 door = w.DoorPosition(car, true);
                Vec3 outward = Quat.Yaw(car.Rotation.YawDeg) * new Vec3(-1f, 0f, 0f);
                w.TakeOutOfCar(driver, door + outward, Vec3.YawDegOf(outward));
                driver.Brain = w.CreateBrain(driver, PedOrigin.CarjackedDriver, p.Tag, default(NpcSpot));
                w.Emit(GameEventType.CarJacked, car.Position, p.Tag, car.Tag, driver.Id, null);
                w.Profile.AddStat(StatNames.VehiclesStolen, 1);
                ps.Transition = VehicleTransition.PullOut;
                return;
            }
            if (kind != TagKind.Car || car.Owner == CarOwner.Police)
            {
                w.Emit(GameEventType.CarStolen, car.Position, p.Tag, car.Tag, car.Owner == CarOwner.Police ? 1f : 0f, null);
                w.Profile.AddStat(StatNames.VehiclesStolen, 1);
            }
            ps.Transition = VehicleTransition.GetIn;
        }

        private static void SeatPlayer(GameWorld w, Ped p, PlayerState ps)
        {
            ps.Transition = VehicleTransition.None;
            ps.TransitionSeconds = 0f;
            Car car = w.FindCar(ps.TransitionCarId);
            if (car == null || car.Seats[0] != 0) return;
            w.PutInCar(p, car, 0);
            p.Flags &= ~(PedFlags.Aiming | PedFlags.HandsUp);
            if (car.Owner != CarOwner.Mission) car.Owner = CarOwner.Player;
            car.AbandonedSeconds = 0f;
            ps.LastCarId = car.Id;
            ps.ViewYawDeg = 0f;
            w.Emit(GameEventType.PlayerEnteredCar, car.Position, p.Tag, car.Tag, 0f, null);
        }

        // ---- in a car -----------------------------------------------------------------------------------------

        private void StepInCar(GameWorld w, Ped p, PlayerState ps, InputFrame inp, float dt)
        {
            Car car = w.FindCar(p.VehicleId);
            if (car == null)
            {
                p.Flags &= ~PedFlags.InVehicle;
                p.VehicleId = 0;
                p.Seat = -1;
                return;
            }
            ps.Sprinting = false;
            ps.SecondsSinceSprint += dt;
            if (ps.SecondsSinceSprint >= PlayerRules.StaminaRefillDelay)
                ps.Stamina = Math.Min(PlayerRules.MaxStamina, ps.Stamina + PlayerRules.StaminaRefillPerSecond * dt);
            ps.LastCarId = car.Id;
            p.Anim = PedAnim.Sit;
            p.YawDeg = car.Rotation.YawDeg;
            if (ps.Life != LifeState.Alive || p.IsDead) return;
            ps.Interaction = new InteractionTarget { Kind = InteractionTargetKind.ExitVehicle, TargetTag = car.Tag, Position = car.Position, Index = -1 };
            if (p.Seat != 0) return;

            float speed = Math.Abs(car.Body.Speed);
            _pendingDriven += speed * dt;
            if (_pendingDriven >= 1f)
            {
                long whole = (long)_pendingDriven;
                w.Profile.AddStat(PlayerRules.StatDistanceDriven, whole);
                w.Profile.AddStat(StatNames.DistanceM, whole);
                _pendingDriven -= whole;
            }

            if (ps.Transition == VehicleTransition.None && inp.WasPressed(GameButton.EnterExit) && !w.Menu.IsOpen)
            {
                if (speed > PlayerRules.BailMinSpeed) { ExitCar(w, p, ps, car, true); return; }
                ps.Transition = VehicleTransition.GetOut;
                ps.TransitionSeconds = 0f;
            }
            if (ps.Transition == VehicleTransition.GetOut && speed < PlayerRules.ExitMaxSpeed)
            {
                ps.TransitionSeconds += dt;
                if (ps.TransitionSeconds >= PlayerRules.GetOutSeconds) ExitCar(w, p, ps, car, false);
            }
        }

        internal void ExitCar(GameWorld w, Ped p, PlayerState ps, Car car, bool bail)
        {
            float carYaw = car.Rotation.YawDeg;
            Vec3 door = w.DoorPosition(car, true);
            if (Blocked(w, door, car))
            {
                Vec3 right = w.DoorPosition(car, false);
                if (!Blocked(w, right, car)) door = right;
            }
            ps.ViewYawDeg = MathUtil.WrapDeg(carYaw + ps.ViewYawDeg);
            w.TakeOutOfCar(p, door, MathUtil.WrapDeg(carYaw - 90f));
            if (car.Owner == CarOwner.Player) car.Owner = CarOwner.Abandoned;
            car.AbandonedSeconds = 0f;
            ps.LastCarId = car.Id;
            ps.TransitionSeconds = 0f;
            ps.Transition = bail ? VehicleTransition.Bail : VehicleTransition.None;
            if (bail) p.Health = Math.Max(0f, p.Health - PlayerRules.BailDamage);
            _lastPos = p.Position;
            w.Emit(GameEventType.PlayerExitedCar, p.Position, p.Tag, car.Tag, bail ? 1f : 0f, null);
        }

        private bool Blocked(GameWorld w, Vec3 door, Car car)
        {
            QueryMask mask = QueryMask.All.Ignoring(car.Tag);
            return w.Collision.OverlapSphere(door + new Vec3(0f, 0.9f, 0f), 0.3f, mask, _contacts) > 0;
        }
    }
}
