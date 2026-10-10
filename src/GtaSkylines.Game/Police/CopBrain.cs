using System;
using System.Collections.Generic;
using GtaSkylines.Game.Crime;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Physics;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Police
{
    /// <summary>What a cop on foot is doing.</summary>
    public enum CopMode
    {
        /// <summary>Seated in the unit's car (the brain does nothing; the car's driver drives).</summary>
        InCar = 0,
        /// <summary>Getting out (0.8 s, Anim ExitVehicle) then the next mode.</summary>
        Exiting = 1,
        /// <summary>1-2★, the player not hostile: run to the player and order hands up.</summary>
        Arrest = 2,
        /// <summary>Following the player who runs (sprint 6.5 m/s), no shooting.</summary>
        Chase = 3,
        /// <summary>Shooting (3★+, the player armed and aiming/firing, or a cop attacked).</summary>
        Shoot = 4,
        /// <summary>Behind the unit's car, on the side away from the player, still shooting when Shoot applies.</summary>
        Cover = 5,
        /// <summary>Standing beside a roadblock car facing the road.</summary>
        Guard = 6,
        /// <summary>Walking the search sector (unit role Search, interior search at 4★+).</summary>
        Search = 7,
        /// <summary>Walking back to the unit's car and getting in.</summary>
        ReturnToCar = 8,
    }

    /// <summary>
    /// The cop brain (F-HEAT-8), an <see cref="IPedBrain"/> stepped by G7's Peds system. One instance per cop slot,
    /// owned by <see cref="PoliceSystem"/> and reused; no allocation.
    /// <list type="bullet">
    /// <item>Shoot rule (<see cref="ShouldShoot"/>): stars ≥ 3, or the player holds a weapon other than Fists and is
    /// Aiming or fired within the last 2 s (a Gunshot event with A = player), or the player attacked any cop this
    /// episode (an AssaultOnCop or CopKilled crime is Known). Otherwise Arrest.</item>
    /// <item>Arrest: run (5 m/s; sprint 6.5 m/s as Chase while the player runs faster than 3 m/s) to 1.5 m; on reaching <see cref="OrderRange"/> queue
    /// the subtitle (speaker "Police", text <see cref="HandsUpLine"/>, 2.5 s) — at most once per <see cref="OrderCooldown"/>
    /// per cop. BUSTED when, for <see cref="ArrestHoldSeconds"/> continuously, this cop is within ArrestRange and the
    /// player (on foot) has the HandsUp flag or moves slower than 0.3 m/s, or (in a car) the car moves slower than
    /// 0.5 m/s and this cop is within ArrestRange of the driver door: sets <c>Wanted.BustedRequested</c> = true and
    /// <c>Wanted.BustedByTag</c> = this cop's tag (G6 runs the BUSTED flow). Resisting: after the order, the player
    /// moving more than <see cref="ResistDistance"/> from where they stood at the order, within 10 s of it, reports
    /// <c>CrimeType.ResistingArrest</c> through <c>CrimeSystem.ReportByCop</c> once per order.</item>
    /// <item>Shoot: sets Hostile; CurrentWeapon = Pistol; keeps 8-15 m from the player; fires through
    /// <c>ICrimePedServices.Fire</c> from the eye (feet + 1.5 m) toward the player's chest (feet + 1.2 m, or the car
    /// centre) when it can see the player, at intervals U[0.6, 1.2) s × ReactionScale, with an aim error (degrees, uniform
    /// in a cone) of (1.5 + 0.12 × distance + 0.5 × player speed) × (Easy 1.5, Normal 1, Hard 0.7).</item>
    /// <item>Cover: when the cop took damage within 1 s (LastDamageTime) or a player Gunshot happened within 30 m in
    /// the last 1 s, and its car is within 15 m: move to car centre + (away-from-player direction) × (car half width +
    /// 1 m) and stay U[3.5, 5) s (seeded; renewed while still under fire), shooting if Shoot applies.</item>
    /// <item>Not wanted, or the unit's role no longer needs cops on foot (not OnFoot, BoxIn or Roadblock): walk back to
    /// the unit's car and get in (free seat). A roadblock cop guards beside its car until the player is within 25 m.
    /// While evading with no sight of the player: Search, walking (2.5 m/s) to seeded points in the search zone.</item>
    /// <item>Random choices use <c>new Rng(Rng.Seed(police seed, cop ped id, decision counter))</c>.</item>
    /// </list>
    /// </summary>
    public sealed class CopBrain : IPedBrain
    {
        /// <summary>Busted distance, metres.</summary>
        public const float ArrestRange = 2f;
        /// <summary>Distance at which the order is shouted, metres.</summary>
        public const float OrderRange = 15f;
        /// <summary>Seconds the arrest condition must hold.</summary>
        public const float ArrestHoldSeconds = 2f;
        /// <summary>Seconds between two orders of one cop.</summary>
        public const float OrderCooldown = 6f;
        /// <summary>Fleeing farther than this after an order is resisting arrest, metres.</summary>
        public const float ResistDistance = 6f;
        /// <summary>The order subtitle.</summary>
        public const string HandsUpLine = "Police! Hands up!";
        /// <summary>Subtitle speaker for police lines.</summary>
        public const string Speaker = "Police";

        /// <summary>Creates a brain bound to the police system (its unit and crime data).</summary>
        /// <exception cref="ArgumentNullException"><paramref name="police"/> is null.</exception>
        public CopBrain(PoliceSystem police)
        {
            if (police == null) throw new ArgumentNullException("police");
            Police = police;
        }

        /// <summary>The owning system.</summary>
        public PoliceSystem Police { get; private set; }
        /// <summary>The unit this cop belongs to (unit id, 0 none).</summary>
        public int UnitId;
        /// <summary>Current mode.</summary>
        public CopMode Mode;
        /// <summary>Seconds in the current mode.</summary>
        public float ModeSeconds;
        /// <summary>Seconds the arrest condition has held.</summary>
        public float ArrestHeld;
        /// <summary>Game seconds of this cop's last hands-up order, −1 none.</summary>
        public double LastOrderTime = -1.0;
        /// <summary>Seconds until the next shot.</summary>
        public float ShotCooldown;

        private Vec3 _orderPlayerPos;
        private bool _resistReported;
        private float _coverSeconds;
        private uint _decisions;
        private Vec3 _searchPoint;
        private float _searchSeconds;

        /// <summary>Prepares the brain for a newly spawned cop of a unit (mode InCar).</summary>
        public void Reset(int unitId)
        {
            UnitId = unitId;
            Mode = CopMode.InCar;
            ModeSeconds = 0f;
            ArrestHeld = 0f;
            LastOrderTime = -1.0;
            ShotCooldown = 0f;
            _resistReported = false;
            _coverSeconds = 0f;
            _searchSeconds = 0f;
        }

        /// <summary>
        /// The shoot rule (see class) as a pure function: true when stars ≥ 3, or (armed with a non-fist weapon and
        /// (aiming or fired within 2 s)), or a cop was attacked this episode.
        /// </summary>
        public static bool ShouldShoot(int stars, bool playerArmed, bool playerAimingOrFiring, bool copAttacked)
        {
            return stars >= 3 || (playerArmed && playerAimingOrFiring) || copAttacked;
        }

        /// <summary>Aim error cone half-angle in degrees for a distance (m), player speed (m/s) and difficulty factor (see class).</summary>
        public static float AimErrorDeg(float distance, float playerSpeed, float difficultyFactor)
        {
            return (1.5f + 0.12f * distance + 0.5f * playerSpeed) * difficultyFactor;
        }

        /// <inheritdoc />
        public string Name { get { return "Cop"; } }

        /// <inheritdoc />
        public bool IsActive { get { return true; } }

        /// <inheritdoc />
        public void Step(GameWorld world, Ped ped, float dt)
        {
            ModeSeconds += dt;
            ped.Move = default(MotorInput);
            ped.Move.FacingYawDeg = ped.YawDeg;
            if (ped.VehicleId != 0)
            {
                if (Mode != CopMode.InCar) SetMode(CopMode.InCar);
                ped.Anim = PedAnim.Sit;
                return;
            }
            Ped player = world.Player;
            if (player == null) return;
            if (Mode == CopMode.InCar) SetMode(CopMode.Exiting);
            if (Mode == CopMode.Exiting)
            {
                ped.Anim = PedAnim.ExitVehicle;
                if (ModeSeconds < ExitSeconds) return;
                SetMode(CopMode.Arrest);
            }

            WantedStatus wanted = world.Wanted;
            PoliceUnit unit = UnitId != 0 ? Police.FindUnit(UnitId) : null;
            Car unitCar = unit != null && unit.CarId != 0 ? world.FindCar(unit.CarId) : null;
            if (!wanted.IsWanted || (unit != null && !NeedsCopsOnFoot(unit.Role)))
            {
                ped.Flags &= ~PedFlags.Hostile;
                ReturnToCar(world, ped, unitCar);
                return;
            }

            double now = world.Clock.GameSeconds;
            Car playerCar = Recognition.PlayerCar(world);
            Vec3 target = playerCar != null ? playerCar.Position : player.Position;
            Vec3 playerVel = playerCar != null ? playerCar.Body.Velocity : player.Motor.Velocity;
            float dist = Vec3.DistanceXZ(ped.Position, target);
            bool armed = player.CurrentWeapon != 0;
            bool firing = now - Police.LastPlayerGunshotTime <= 2.0;
            bool shoot = ShouldShoot(wanted.Stars, armed, player.Has(PedFlags.Aiming) || firing, wanted.Episode.CopAttacked);
            Vec3 eye = ped.Position + new Vec3(0f, 1.5f, 0f);
            Vec3 aim = playerCar != null ? target : player.Position + new Vec3(0f, 1.2f, 0f);
            bool sees = Recognition.Clear(world, eye, aim, ped.Tag, player.Tag, playerCar != null ? playerCar.Tag : player.Tag, 0f);

            // Cover when under fire and a car is close.
            bool underFire = (ped.LastDamageTime >= 0.0 && now - ped.LastDamageTime <= 1.0)
                || (now - Police.LastPlayerGunshotTime <= 1.0 && Vec3.DistanceXZ(Police.LastPlayerGunshotPosition, ped.Position) <= 30f);
            Car cover = unitCar ?? NearestPoliceCar(world, ped.Position, 15f);
            if (underFire && cover != null && Vec3.DistanceXZ(cover.Position, ped.Position) <= 15f)
            {
                if (Mode != CopMode.Cover)
                {
                    SetMode(CopMode.Cover);
                    _coverSeconds = Seeded.Range(Police.Seed, (uint)ped.Id, _decisions++, 3.5f, 5f);
                }
                else if (ModeSeconds > _coverSeconds - 1.5f) _coverSeconds = ModeSeconds + 1.5f;
            }
            if (Mode == CopMode.Cover && cover != null && ModeSeconds < _coverSeconds)
            {
                Vec3 away = (cover.Position - target).Flat;
                away = away.LengthSquared > 1e-4f ? away.Normalized : Vec3.Forward;
                Vec3 spot = cover.Position + away * (cover.Model.HalfExtents.X + 1f);
                MoveTo(ped, spot, RunSpeed, 0.4f);
                Face(ped, target);
                if (shoot) TryShoot(world, ped, eye, aim, dist, playerVel.LengthXZ, sees, dt);
                return;
            }

            if (unit != null && unit.Role == UnitRole.Roadblock && dist > Dispatcher.OnFootRange)
            {
                SetModeIfNot(CopMode.Guard);
                Face(ped, target);
                ped.Anim = shoot ? PedAnim.Aim : PedAnim.Idle;
                if (shoot) { ped.Flags |= PedFlags.Hostile; TryShoot(world, ped, eye, aim, dist, playerVel.LengthXZ, sees, dt); }
                return;
            }

            if (wanted.Evading && !sees)
            {
                SetModeIfNot(CopMode.Search);
                Search(world, ped, wanted, dt);
                return;
            }

            if (shoot)
            {
                SetModeIfNot(CopMode.Shoot);
                ped.Flags |= PedFlags.Hostile;
                ped.CurrentWeapon = (int)WeaponId.Pistol;
                if (dist > 15f) MoveTo(ped, target, RunSpeed, 0f);
                else if (dist < 8f) MoveTo(ped, ped.Position + (ped.Position - target).Flat.Normalized * 3f, 2.5f, 0f);
                Face(ped, target);
                ped.Anim = PedAnim.Aim;
                TryShoot(world, ped, eye, aim, dist, playerVel.LengthXZ, sees, dt);
                return;
            }

            ped.Flags &= ~PedFlags.Hostile;
            Arrest(world, ped, player, playerCar, target, dist, playerVel.LengthXZ, now, dt);
        }

        private const float ExitSeconds = 0.8f;
        private const float RunSpeed = 5f;
        private const float SprintSpeed = 6.5f;

        private static bool NeedsCopsOnFoot(UnitRole role)
        {
            return role == UnitRole.OnFoot || role == UnitRole.BoxIn || role == UnitRole.Roadblock;
        }

        private void SetMode(CopMode mode)
        {
            Mode = mode;
            ModeSeconds = 0f;
        }

        private void SetModeIfNot(CopMode mode)
        {
            if (Mode != mode) SetMode(mode);
        }

        private void Arrest(GameWorld world, Ped ped, Ped player, Car playerCar, Vec3 target, float dist, float playerSpeed, double now, float dt)
        {
            WantedStatus wanted = world.Wanted;
            Vec3 goal = playerCar != null ? world.DoorPosition(playerCar, true) : target;
            float goalDist = Vec3.DistanceXZ(ped.Position, goal);
            bool fleeing = playerSpeed > 3f && dist > 4f;
            SetModeIfNot(fleeing ? CopMode.Chase : CopMode.Arrest);
            if (goalDist > 1.5f) MoveTo(ped, goal, fleeing ? SprintSpeed : RunSpeed, 0f);
            else ped.Anim = PedAnim.Aim;
            Face(ped, target);

            if (dist <= OrderRange && (LastOrderTime < 0.0 || now - LastOrderTime >= OrderCooldown))
            {
                world.Messages.QueueSubtitle(Speaker, HandsUpLine, 2.5f);
                LastOrderTime = now;
                _orderPlayerPos = target;
                _resistReported = false;
            }

            if (LastOrderTime >= 0.0 && !_resistReported && now - LastOrderTime <= 10.0
                && Vec3.DistanceXZ(target, _orderPlayerPos) > ResistDistance)
            {
                _resistReported = true;
                Police.Crime.ReportByCop(world, CrimeType.ResistingArrest, target, ped.Tag, ped.Tag);
            }

            bool holding;
            if (playerCar != null) holding = playerSpeed < 0.5f && goalDist <= ArrestRange;
            else holding = dist <= ArrestRange && (player.Has(PedFlags.HandsUp) || playerSpeed < 0.3f);
            ArrestHeld = holding ? ArrestHeld + dt : 0f;
            if (ArrestHeld >= ArrestHoldSeconds && !wanted.BustedRequested)
            {
                wanted.BustedRequested = true;
                wanted.BustedByTag = ped.Tag;
            }
        }

        private void TryShoot(GameWorld world, Ped ped, Vec3 eye, Vec3 aim, float dist, float playerSpeed, bool sees, float dt)
        {
            ShotCooldown -= dt;
            if (ShotCooldown > 0f || !sees || dist > PistolRange) return;
            float reaction = WantedRules.ReactionScale(world.Settings.Police);
            float diff = world.Settings.Police == GtaSkylines.Game.Settings.PoliceDifficulty.Easy ? 1.5f
                : world.Settings.Police == GtaSkylines.Game.Settings.PoliceDifficulty.Hard ? 0.7f : 1f;
            float error = AimErrorDeg(dist, playerSpeed, diff);
            Vec3 dir = (aim - eye).Normalized;
            float theta = error * Seeded.Unit(Police.Seed, (uint)ped.Id, _decisions++);
            float phi = 360f * Seeded.Unit(Police.Seed, (uint)ped.Id, _decisions++);
            Vec3 side = Vec3.Cross(dir, Vec3.Up);
            side = side.LengthSquared > 1e-6f ? side.Normalized : Vec3.Right;
            Vec3 axis = Quat.AngleAxis(phi, dir) * side;
            dir = Quat.AngleAxis(theta, axis) * dir;
            Police.Crime.Services.Fire(world, ped, eye, dir);
            ped.Anim = PedAnim.Shoot;
            ShotCooldown = Seeded.Range(Police.Seed, (uint)ped.Id, _decisions++, 0.6f, 1.2f) * reaction;
        }

        private void Search(GameWorld world, Ped ped, WantedStatus wanted, float dt)
        {
            _searchSeconds -= dt;
            if (_searchSeconds <= 0f || Vec3.DistanceXZ(ped.Position, _searchPoint) < 1.5f)
            {
                float r = Math.Max(10f, wanted.SearchRadius * 0.5f);
                float a = Seeded.Range(Police.Seed, (uint)ped.Id, _decisions++, 0f, 360f);
                float d = Seeded.Range(Police.Seed, (uint)ped.Id, _decisions++, 0f, r);
                _searchPoint = wanted.SearchCentre + Vec3.FromYawDeg(a) * d;
                _searchSeconds = 6f;
            }
            MoveTo(ped, _searchPoint, 2.5f, 0f);
        }

        private void ReturnToCar(GameWorld world, Ped ped, Car car)
        {
            if (car == null || car.Destroyed)
            {
                ped.Anim = PedAnim.Idle;
                return;
            }
            SetModeIfNot(CopMode.ReturnToCar);
            int seat = -1;
            int seats = Math.Min(car.Model.Seats, Car.MaxSeats);
            for (int i = 0; i < seats; i++) if (car.Seats[i] == 0) { seat = i; break; }
            Vec3 door = world.DoorPosition(car, seat != 1);
            if (seat >= 0 && Vec3.DistanceXZ(ped.Position, door) <= 1.5f)
            {
                world.PutInCar(ped, car, seat);
                SetMode(CopMode.InCar);
                return;
            }
            MoveTo(ped, door, RunSpeed, 0f);
        }

        private const float PistolRange = 70f;

        private static Car NearestPoliceCar(GameWorld world, Vec3 at, float range)
        {
            Car best = null;
            float bestD = range;
            IList<Car> cars = world.Cars;
            for (int i = 0; i < cars.Count; i++)
            {
                Car c = cars[i];
                if (c.Owner != CarOwner.Police || c.Destroyed) continue;
                float d = Vec3.DistanceXZ(c.Position, at);
                if (d <= bestD) { bestD = d; best = c; }
            }
            return best;
        }

        private static void MoveTo(Ped ped, Vec3 goal, float speed, float stopDistance)
        {
            Vec3 d = (goal - ped.Position).Flat;
            float len = d.Length;
            if (len <= Math.Max(stopDistance, 0.05f))
            {
                ped.Move.DesiredVelocity = Vec3.Zero;
                ped.Anim = PedAnim.Idle;
                return;
            }
            float v = Math.Min(speed, len * 4f);
            ped.Move.DesiredVelocity = d * (v / len);
            ped.Move.FacingYawDeg = Vec3.YawDegOf(d);
            ped.YawDeg = ped.Move.FacingYawDeg;
            ped.Anim = v > 5.5f ? PedAnim.Sprint : v > 2f ? PedAnim.Run : PedAnim.Walk;
        }

        private static void Face(Ped ped, Vec3 target)
        {
            Vec3 d = (target - ped.Position).Flat;
            if (d.LengthSquared < 1e-4f) return;
            ped.YawDeg = Vec3.YawDegOf(d);
            ped.Move.FacingYawDeg = ped.YawDeg;
        }
    }
}
