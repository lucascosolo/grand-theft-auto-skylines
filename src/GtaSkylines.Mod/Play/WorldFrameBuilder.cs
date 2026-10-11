using System.Collections.Generic;
using GtaSkylines.Game.Camera;
using GtaSkylines.Game.Combat;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Police;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Session;
using GtaSkylines.Game.Vehicles;
using GtaSkylines.Game.World;
using GtaSkylines.Mod.City;
using GtaSkylines.Mod.Rendering;
using GamePedAnim = GtaSkylines.Game.World.PedAnim;
using DrawAnim = GtaSkylines.Mod.Rendering.PedAnim;

namespace GtaSkylines.Mod.Play
{
    /// <summary>Says whether a promoted actor may be drawn yet (its CS1 original is released). Null gate = always.</summary>
    public interface IPromotionGate
    {
        /// <summary>False while the CS1 vehicle this car was taken from is still in CS1's hands.</summary>
        bool CarReady(Car car);
        /// <summary>False while the CS1 citizen instance this ped was promoted from is still in CS1's hands.</summary>
        bool PedReady(Ped ped);
    }

    /// <summary>
    /// Fills a <see cref="RenderFrame"/> from the game world once per frame (pure; no Unity). Keeps per-car wheel travel,
    /// acceleration and explosion age, and its own event cursor for effects. Steady state allocates nothing.
    /// </summary>
    public sealed class WorldFrameBuilder
    {
        /// <summary>Door marker radius, metres.</summary>
        public const float DoorMarkerRadius = 0.5f;
        /// <summary>Door marker colour (GTA yellow).</summary>
        public const int DoorMarkerColour = 0xFFD200;
        /// <summary>Alpha of every world marker.</summary>
        public const float MarkerAlpha = 0.5f;
        /// <summary>Near plane in first-person and hood views, metres.</summary>
        public const float FirstPersonNear = 0.1f;
        /// <summary>Near plane in the orbit and chase views, metres.</summary>
        public const float ThirdPersonNear = 0.3f;
        /// <summary>Seconds since the last shot before any shot was fired.</summary>
        public const float NoShotSeconds = 99f;

        private struct CarTrack
        {
            public float Wheel, Speed, SinceExplosion;
            public bool Seen;
        }

        private readonly Dictionary<int, CarTrack> _cars = new Dictionary<int, CarTrack>();
        private readonly List<int> _stale = new List<int>();
        private long _cursor = -1;
        private float _sinceShot = NoShotSeconds;
        private float _rotor;

        /// <summary>Optional promotion gate.</summary>
        public IPromotionGate Gate;

        /// <summary>Optional departing parked-car ghosts (F-VEH-7), drawn after the Game cars.</summary>
        public DepartureTracker Departures;

        /// <summary>Forgets per-car state and the event cursor (new session).</summary>
        public void Reset()
        {
            _cars.Clear();
            _cursor = -1;
            _sinceShot = NoShotSeconds;
            _rotor = 0f;
        }

        /// <summary>
        /// Clears nothing (the renderer cleared the frame) and adds: Alpha (= session interpolation) and DeltaTime;
        /// a PedDraw for every ped that is not InVehicle, not Invisible and passes the gate, except the player in the
        /// first-person foot view; a CarDraw for every car that passes the gate; the interior while
        /// <c>Interior.IsInside</c>; a Cylinder marker per mission marker and a Door marker at a BuildingDoor interaction
        /// target; shots, muzzle flashes, impacts (shots that hit an actor) and explosions from events since the last
        /// call (the first call starts at the current sequence); a parked-pose CarDraw per <see cref="Departures"/> ghost,
        /// except one the Game took whose car is drawn this frame; the first-person view model on foot; the camera from
        /// <c>World.View*</c>.
        /// </summary>
        public void Fill(RenderFrame frame, GameParts game, float dt)
        {
            GameSession s = game.Session;
            GameWorld w = s.World;
            frame.Alpha = s.Interpolation;
            frame.DeltaTime = dt;
            CameraMode mode = s.Camera.Mode;
            Ped player = w.Player;
            IList<Ped> peds = w.Peds;
            for (int i = 0; i < peds.Count; i++)
            {
                Ped p = peds[i];
                if ((p.Flags & (PedFlags.InVehicle | PedFlags.Invisible)) != 0) continue;
                if (p == player && mode == CameraMode.FootFirstPerson) continue;
                if (Gate != null && !Gate.PedReady(p)) continue;
                frame.Peds.Add(new PedDraw
                {
                    ModelId = p.Model,
                    Key = p.Id,
                    PrevPosition = p.PrevPosition,
                    Position = p.Position,
                    PrevYawDeg = p.PrevYawDeg,
                    YawDeg = p.YawDeg,
                    Velocity = p.Motor.Velocity,
                    Anim = MapAnim(p.Anim, p.Flags),
                    Visible = true,
                });
            }
            FillCars(frame, w, dt);
            FillHeli(frame, game.Police, dt);
            InteriorState interior = w.Interior;
            if (interior.IsInside && interior.Layout != null)
            {
                frame.Interior.Layout = interior.Layout;
                frame.Interior.Origin = interior.Origin;
                frame.Interior.YawDeg = interior.YawDeg;
            }
            List<WorldMarker> markers = w.Mission.Markers;
            for (int i = 0; i < markers.Count; i++)
            {
                WorldMarker m = markers[i];
                frame.Markers.Add(new MarkerDraw { Kind = MarkerKind.Cylinder, Position = m.Position, Radius = m.Radius, ColourRgb = m.ColourRgb, Alpha = MarkerAlpha });
            }
            InteractionTarget target = w.PlayerState.Interaction;
            if (target.Kind == InteractionTargetKind.BuildingDoor)
                frame.Markers.Add(new MarkerDraw { Kind = MarkerKind.Door, Position = target.Position, Radius = DoorMarkerRadius, ColourRgb = DoorMarkerColour, Alpha = MarkerAlpha });
            ReadEffects(frame, w, player, dt);
            if (player != null && mode == CameraMode.FootFirstPerson && !player.Has(PedFlags.InVehicle))
            {
                frame.ViewModel = new ViewModelDraw
                {
                    Weapon = ViewWeaponOf((WeaponId)player.CurrentWeapon),
                    SinceShotS = _sinceShot,
                    ReloadProgress = ReloadProgress(game.Combat != null ? game.Combat.PlayerWeapon : null,
                        WeaponTable.Get((WeaponId)player.CurrentWeapon)),
                    Visible = !player.IsDead,
                };
            }
            frame.Camera = new CameraDraw
            {
                Position = w.ViewPosition,
                Rotation = w.ViewRotation,
                FovDeg = w.ViewFovDeg,
                Near = IsFirstPerson(mode) ? FirstPersonNear : ThirdPersonNear,
            };
            frame.HasCamera = true;
        }

        /// <summary>
        /// Reload progress 0..1 of <paramref name="user"/> for a weapon reloading in <paramref name="stats"/>.ReloadSeconds
        /// (1 - remaining / reload time, clamped); -1 when not reloading, user is null or the reload time is not positive.
        /// </summary>
        public static float ReloadProgress(WeaponUser user, WeaponStats stats)
        {
            if (user == null || !user.IsReloading || stats == null || !(stats.ReloadSeconds > 0f)) return -1f;
            float p = 1f - user.ReloadRemaining / stats.ReloadSeconds;
            return p < 0f ? 0f : p > 1f ? 1f : p;
        }

        private void FillHeli(RenderFrame frame, PoliceSystem police, float dt)
        {
            PoliceHelicopter h = police != null ? police.Helicopter : null;
            if (h == null || !h.Active || h.ModelId < 0) return;
            _rotor += dt * HeliMath.RotorRate;
            if (_rotor > 1e5f) _rotor = 0f;
            frame.Heli = new HeliDraw
            {
                Visible = true,
                ModelId = h.ModelId,
                PrevPosition = h.Position - h.Velocity * GameSession.FixedDt,
                Position = h.Position,
                YawDeg = h.YawDeg,
                Velocity = h.Velocity,
                RotorTravel = _rotor,
                Spotlight = h.Spotlight,
                SpotlightTarget = h.SpotlightTarget,
            };
        }

        private void FillCars(RenderFrame frame, GameWorld w, float dt)
        {
            foreach (KeyValuePair<int, CarTrack> kv in _cars) _stale.Add(kv.Key);
            for (int i = 0; i < _stale.Count; i++)
            {
                CarTrack t = _cars[_stale[i]];
                t.Seen = false;
                _cars[_stale[i]] = t;
            }
            _stale.Clear();
            IList<Car> cars = w.Cars;
            for (int i = 0; i < cars.Count; i++)
            {
                Car c = cars[i];
                VehicleBody b = c.Body;
                Vec3 v = b.Velocity;
                float signed = Vec3.Dot(v, c.Rotation * Vec3.Forward);
                CarTrack t;
                bool known = _cars.TryGetValue(c.Id, out t);
                t.Wheel = VehicleMath.WheelTravel(known ? t.Wheel : 0f, signed, dt);
                float accel = known && dt > 0f ? (signed - t.Speed) / dt : 0f;
                t.Speed = signed;
                t.SinceExplosion = b.DamageState == VehicleDamageState.Exploded ? (known ? t.SinceExplosion + dt : 0f) : 0f;
                t.Seen = true;
                _cars[c.Id] = t;
                if (Gate != null && !Gate.CarReady(c)) continue;
                frame.Cars.Add(new CarDraw
                {
                    ModelId = c.Model.Id,
                    PrevCentre = c.PrevPosition,
                    Centre = c.Position,
                    PrevRotation = c.PrevRotation,
                    Rotation = c.Rotation,
                    Velocity = v,
                    Acceleration = accel,
                    ColourRgb = c.ColourRgb,
                    Headlights = c.Headlights,
                    Brake = c.Controls.Brake > 0.05f || c.Controls.Handbrake,
                    Siren = c.Siren || c.LightBar,
                    SteerRad = (float)(b.GetWheel(0).SteerDeg * MathUtil.Deg2Rad),
                    WheelTravelMetres = t.Wheel,
                    CompressionFL = b.GetWheel(0).Compression,
                    CompressionFR = b.GetWheel(1).Compression,
                    CompressionRL = b.GetWheel(2).Compression,
                    CompressionRR = b.GetWheel(3).Compression,
                    Damage = MapDamage(b.DamageState),
                    SecondsSinceExplosion = t.SinceExplosion,
                });
            }
            FillGhosts(frame, cars);
            foreach (KeyValuePair<int, CarTrack> kv in _cars) if (!kv.Value.Seen) _stale.Add(kv.Key);
            for (int i = 0; i < _stale.Count; i++) _cars.Remove(_stale[i]);
            _stale.Clear();
        }

        // Drawn as CS1 draws a parked car: still, no lights, the parked colour.
        private void FillGhosts(RenderFrame frame, IList<Car> cars)
        {
            if (Departures == null) return;
            IList<DepartureGhost> ghosts = Departures.Ghosts;
            for (int i = 0; i < ghosts.Count; i++)
            {
                DepartureGhost g = ghosts[i];
                if (!g.Elsewhere && TakenAndDrawn(g.Vehicle, cars)) continue;
                frame.Cars.Add(new CarDraw
                {
                    ModelId = g.Model,
                    PrevCentre = g.Centre,
                    Centre = g.Centre,
                    PrevRotation = g.Rotation,
                    Rotation = g.Rotation,
                    ColourRgb = g.ColourRgb,
                });
            }
        }

        private bool TakenAndDrawn(ushort vehicle, IList<Car> cars)
        {
            for (int i = 0; i < cars.Count; i++)
            {
                Car c = cars[i];
                if (c.SourceCs1Vehicle == vehicle && !c.SourceParked) return Gate == null || Gate.CarReady(c);
            }
            return false;
        }

        private void ReadEffects(RenderFrame frame, GameWorld w, Ped player, float dt)
        {
            _sinceShot += dt;
            if (_cursor < 0) _cursor = w.Events.NextSeq;
            int playerTag = player == null ? 0 : player.Tag;
            GameEvent e;
            while (w.Events.Read(ref _cursor, out e))
            {
                if (e.Type == GameEventType.Shot)
                {
                    frame.Shots.Add(new ShotDraw { From = e.Position, To = e.Position2 });
                    frame.MuzzleFlashes.Add(new FlashDraw { Position = e.Position });
                    if (e.ActorB != 0) frame.Impacts.Add(new ImpactDraw { Position = e.Position2 });
                    if (e.ActorA == playerTag) _sinceShot = 0f;
                }
                else if (e.Type == GameEventType.Explosion)
                {
                    frame.Explosions.Add(new ExplosionDraw { Position = e.Position });
                }
            }
        }

        /// <summary>True for the first-person foot and vehicle views and the hood view.</summary>
        public static bool IsFirstPerson(CameraMode mode)
        {
            return mode == CameraMode.FootFirstPerson || mode == CameraMode.VehicleFirstPerson || mode == CameraMode.Hood;
        }

        /// <summary>
        /// Game animation → CS1 pose family: Dead or KnockedDown (flags) and LieDown → Lying; Sit → Sitting; Panic or the
        /// Panicking flag → Panic; Phone or OnPhone → Phone; HandsUp (anim or flag) → HandsUp; Walk, Run, Sprint as is;
        /// Swim → Walk; everything else (Idle, Fall, Climb, enter/exit, Punch, Aim, Shoot) → Idle.
        /// </summary>
        public static DrawAnim MapAnim(GamePedAnim anim, PedFlags flags)
        {
            if ((flags & (PedFlags.Dead | PedFlags.KnockedDown)) != 0 || anim == GamePedAnim.LieDown) return DrawAnim.Lying;
            if ((flags & PedFlags.HandsUp) != 0 || anim == GamePedAnim.HandsUp) return DrawAnim.HandsUp;
            if ((flags & PedFlags.OnPhone) != 0 || anim == GamePedAnim.Phone) return DrawAnim.Phone;
            switch (anim)
            {
                case GamePedAnim.Sit: return DrawAnim.Sitting;
                case GamePedAnim.Panic: return DrawAnim.Panic;
                case GamePedAnim.Walk:
                case GamePedAnim.Swim: return DrawAnim.Walk;
                case GamePedAnim.Run: return DrawAnim.Run;
                case GamePedAnim.Sprint: return DrawAnim.Sprint;
            }
            return (flags & PedFlags.Panicking) != 0 ? DrawAnim.Panic : DrawAnim.Idle;
        }

        /// <summary>Damage state → draw damage (Intact, Smoking, Burning, Exploded).</summary>
        public static CarDamage MapDamage(VehicleDamageState state)
        {
            switch (state)
            {
                case VehicleDamageState.Smoking: return CarDamage.Smoking;
                case VehicleDamageState.OnFire: return CarDamage.Burning;
                case VehicleDamageState.Exploded: return CarDamage.Exploded;
                default: return CarDamage.Intact;
            }
        }

        /// <summary>Weapon → view model (same order; out-of-range → Fists).</summary>
        public static ViewWeapon ViewWeaponOf(WeaponId weapon)
        {
            int i = (int)weapon;
            return i < 0 || i >= WeaponInfo.Count ? ViewWeapon.Fists : (ViewWeapon)i;
        }
    }
}
