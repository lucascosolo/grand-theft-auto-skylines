using System;
using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Combat;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Physics;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Peds
{
    /// <summary>
    /// Ped brains and motors plus the citizens' reactions to the player (G7 contract F-PED). Order
    /// <see cref="SystemOrder.Peds"/> (30). Add once per session: <c>session.AddSystem(new PedsSystem())</c> and install
    /// <see cref="PedBrainFactory"/> on <c>world.Hooks.Brains</c>. Each step, in this order:
    /// <list type="number">
    /// <item><b>Events</b> (own cursor from <c>Events.NextSeq</c> at the first step; Combat's events of the previous step
    /// arrive here one step later) are turned into stimuli delivered with <see cref="PedBrain.React"/> to living Game peds
    /// whose brain is a <see cref="PedBrain"/> (other brains — G8 cops, G9 actors — get nothing; they read events
    /// themselves). The player and cops never receive stimuli.
    /// <list type="bullet">
    /// <item>Bumped (Value ≥ <see cref="BumpStumbleSpeed"/>): B is promoted if ambient (cause A); Bumped to B.</item>
    /// <item>Punched, Shove: Punched to B (if alive and not KnockedDown); SawViolence to every other ped within
    /// <see cref="WitnessViolenceRadius"/> that <see cref="Perception.CanSee(GameWorld, Ped, GtaSkylines.Game.Mathx.Vec3)"/> the victim.</item>
    /// <item>Shot: Hurt to B when B is a living, standing ped; ShotAt to every ped within <see cref="ShotAtRadius"/> of the
    /// shot's end point or within 2 m of the segment, except A and B.</item>
    /// <item>Gunshot / Explosion: GunshotHeard / ExplosionHeard to every ped that <see cref="Perception.Hears"/> it (not
    /// A); ambient snapshot citizens within <see cref="AmbientPanicRadius"/> get <c>CityEffect.PanicCitizen(instance)</c>,
    /// at most once per instance per <see cref="PanicRepeatSeconds"/> (a fixed table of 256 entries; nothing
    /// allocates).</item>
    /// <item>AimedAt: AimedAt to B with Value = distance from A.</item>
    /// <item>RecklessDriving with B a Game ped: VehicleThreat to B (source A).</item>
    /// </list></item>
    /// <item><b>Cars coming at peds</b>: for the player's car (the one the player sits in) at speed &gt;
    /// <see cref="VehicleThreatSpeed"/>: each Game ped on foot within <see cref="VehicleThreatRange"/> ahead whose
    /// time to the car's path is &lt; <see cref="VehicleThreatSeconds"/> and lateral offset &lt; car half width + 1 m gets
    /// VehicleThreat (once per ped per 2 s); ambient citizens in the same zone get PanicCitizen (same dedupe). This covers
    /// driving on the pavement.</item>
    /// <item><b>Brains and motors</b>: for every ped in <c>world.Peds</c> order (index loop over the count at the start
    /// of the step; peds added during the step wait for the next one), except the player: Dead → Move zero, skipped;
    /// Brain non-null → <c>Brain.Step(world, ped, dt)</c>; then, unless InVehicle, <c>world.MovePed(ped, dt)</c> then
    /// <c>Damage.Drown(world, ped, dt)</c>. A brain
    /// replaced during Step runs from the next step.</item>
    /// </list>
    /// Steady state (30 idle peds, no events) allocates nothing.
    /// </summary>
    public sealed class PedsSystem : IGameSystem
    {
        /// <summary>A bump at or above this closing speed (m/s) makes the bumped person stumble (1.5).</summary>
        public const float BumpStumbleSpeed = 1.5f;
        /// <summary>Peds who see a punch within this distance (m) flee (15).</summary>
        public const float WitnessViolenceRadius = 15f;
        /// <summary>Peds this close (m) to a shot's end point are shot at (3); near the segment: 2 m.</summary>
        public const float ShotAtRadius = 3f;
        /// <summary>Aiming at a ped within this distance (m) makes it react (hands up within 15 m, else flee) (25).</summary>
        public const float AimReactRange = 25f;
        /// <summary>Ambient citizens within this distance (m) of a gunshot or explosion panic (CS1 runs them) (30).</summary>
        public const float AmbientPanicRadius = 30f;
        /// <summary>Seconds before the same ambient citizen can be sent PanicCitizen again (10).</summary>
        public const float PanicRepeatSeconds = 10f;
        /// <summary>The player's car threatens peds above this speed, m/s (8).</summary>
        public const float VehicleThreatSpeed = 8f;
        /// <summary>Look-ahead distance for car threats, metres (25).</summary>
        public const float VehicleThreatRange = 25f;
        /// <summary>A ped reacts when the car would reach it within this many seconds (1.5).</summary>
        public const float VehicleThreatSeconds = 1.5f;

        /// <inheritdoc />
        public int Order { get { return SystemOrder.Peds; } }

        /// <inheritdoc />
        public void Step(GameWorld world, float dt)
        {
            _time += dt;
            if (!_started)
            {
                _cursor = world.Events.NextSeq;
                _started = true;
            }
            GameEvent e;
            while (world.Events.Read(ref _cursor, out e)) Route(world, e);
            CarThreats(world);
            IList<Ped> peds = world.Peds;
            int n = peds.Count;
            for (int i = 0; i < n && i < peds.Count; i++)
            {
                Ped p = peds[i];
                if (p.Kind == PedKind.Player) continue;
                if (p.IsDead)
                {
                    p.Move = default(MotorInput);
                    continue;
                }
                if (p.Brain != null) p.Brain.Step(world, p, dt);
                if (!p.Has(PedFlags.InVehicle) && !p.IsDead)
                {
                    world.MovePed(p, dt);
                    Damage.Drown(world, p, dt);
                }
            }
        }

        /// <summary>
        /// Delivers <paramref name="stimulus"/> to <paramref name="ped"/> now (alive, has a <see cref="PedBrain"/>, not the
        /// player); false when not delivered. Systems (G8, G9) may use it to scare a ped.
        /// </summary>
        public static bool Deliver(GameWorld world, Ped ped, Stimulus stimulus)
        {
            if (ped == null || ped.IsDead || ped.Kind == PedKind.Player || ped.Kind == PedKind.Cop) return false;
            var brain = ped.Brain as PedBrain;
            if (brain == null) return false;
            brain.React(world, ped, stimulus);
            return true;
        }

        private const int TableSize = 256;
        private const float VehicleThreatRepeatSeconds = 2f;
        private const float ShotLineRadius = 2f;

        private bool _started;
        private long _cursor;
        private float _time;
        private readonly int[] _panicKeys = new int[TableSize];
        private readonly float[] _panicTimes = new float[TableSize];
        private int _panicNext;
        private readonly int[] _threatKeys = new int[TableSize];
        private readonly float[] _threatTimes = new float[TableSize];
        private int _threatNext;

        private void Route(GameWorld world, GameEvent e)
        {
            switch (e.Type)
            {
                case GameEventType.Bumped:
                    if (e.Value < BumpStumbleSpeed) break;
                    Send(world, world.PedFromTag(e.ActorB, true, e.ActorA), StimulusKind.Bumped, e.ActorA, Source(world, e), e.Value);
                    break;
                case GameEventType.Punched:
                case GameEventType.Shove:
                    Punched(world, e);
                    break;
                case GameEventType.Shot:
                    Shot(world, e);
                    break;
                case GameEventType.Gunshot:
                case GameEventType.Explosion:
                    Noise(world, e);
                    break;
                case GameEventType.AimedAt:
                {
                    Ped target = world.PedFromTag(e.ActorB, false, 0);
                    if (target == null) break;
                    Vec3 from = Source(world, e);
                    float d = e.Value > 0f ? e.Value : Vec3.Distance(from, target.Position);
                    Send(world, target, StimulusKind.AimedAt, e.ActorA, from, d);
                    break;
                }
                case GameEventType.RecklessDriving:
                    Send(world, world.PedFromTag(e.ActorB, false, 0), StimulusKind.VehicleThreat, e.ActorA, Source(world, e), e.Value);
                    break;
            }
        }

        private static Vec3 Source(GameWorld world, GameEvent e)
        {
            Vec3 p;
            return e.ActorA != 0 && world.TryGetTagPosition(e.ActorA, out p) ? p : e.Position;
        }

        private static void Send(GameWorld world, Ped ped, StimulusKind kind, int source, Vec3 at, float value)
        {
            if (ped == null) return;
            Deliver(world, ped, new Stimulus { Kind = kind, SourceTag = source, SourcePosition = at, Value = value });
        }

        private static void Punched(GameWorld world, GameEvent e)
        {
            Ped victim = world.PedFromTag(e.ActorB, false, 0);
            Vec3 from = Source(world, e);
            if (victim != null && !victim.Has(PedFlags.KnockedDown)) Send(world, victim, StimulusKind.Punched, e.ActorA, from, 0f);
            Vec3 seen = e.Position + new Vec3(0f, Perception.TargetHeight, 0f);
            IList<Ped> peds = world.Peds;
            for (int i = 0; i < peds.Count; i++)
            {
                Ped p = peds[i];
                if (p == victim || p.Tag == e.ActorA || Vec3.Distance(p.Position, e.Position) > WitnessViolenceRadius) continue;
                if (!(p.Brain is PedBrain) || !Perception.CanSee(world, p, seen)) continue;
                Send(world, p, StimulusKind.SawViolence, e.ActorA, from, 0f);
            }
        }

        private static void Shot(GameWorld world, GameEvent e)
        {
            Ped victim = world.PedFromTag(e.ActorB, false, 0);
            if (victim != null && !victim.Has(PedFlags.KnockedDown)) Send(world, victim, StimulusKind.Hurt, e.ActorA, e.Position, e.Value);
            IList<Ped> peds = world.Peds;
            for (int i = 0; i < peds.Count; i++)
            {
                Ped p = peds[i];
                if (p == victim || p.Tag == e.ActorA || !(p.Brain is PedBrain)) continue;
                Vec3 chest = p.Position + new Vec3(0f, Perception.TargetHeight, 0f);
                if (Vec3.Distance(chest, e.Position2) <= ShotAtRadius || SegmentDistance(chest, e.Position, e.Position2) <= ShotLineRadius)
                    Send(world, p, StimulusKind.ShotAt, e.ActorA, e.Position, 0f);
            }
        }

        private void Noise(GameWorld world, GameEvent e)
        {
            StimulusKind kind = e.Type == GameEventType.Gunshot ? StimulusKind.GunshotHeard : StimulusKind.ExplosionHeard;
            IList<Ped> peds = world.Peds;
            for (int i = 0; i < peds.Count; i++)
            {
                Ped p = peds[i];
                if (p.Tag == e.ActorA || !(p.Brain is PedBrain) || !Perception.Hears(world, p, e)) continue;
                Send(world, p, kind, e.ActorA, e.Position, 0f);
            }
            if (world.Interior.IsInside) return;
            List<AmbientCitizen> cs = world.Ambient.Citizens;
            for (int i = 0; i < cs.Count; i++)
                if (Vec3.Distance(cs[i].Position, e.Position) <= AmbientPanicRadius) Panic(world, cs[i]);
        }

        private void Panic(GameWorld world, AmbientCitizen c)
        {
            if (world.FindPedByCitizen(c.Citizen) != null) return;
            if (!Allow(_panicKeys, _panicTimes, ref _panicNext, c.Instance, PanicRepeatSeconds)) return;
            world.Effects.Add(CityEffect.PanicCitizen(c.Instance));
        }

        private bool Allow(int[] keys, float[] times, ref int next, int key, float repeat)
        {
            for (int i = 0; i < TableSize; i++)
            {
                if (keys[i] != key) continue;
                if (_time - times[i] < repeat) return false;
                times[i] = _time;
                return true;
            }
            keys[next] = key;
            times[next] = _time;
            next = (next + 1) % TableSize;
            return true;
        }

        private void CarThreats(GameWorld world)
        {
            Ped player = world.Player;
            if (player == null || !player.Has(PedFlags.InVehicle)) return;
            Car car = world.FindCar(player.VehicleId);
            if (car == null) return;
            Vec3 v = car.Body.Velocity.Flat;
            float speed = v.Length;
            if (speed <= VehicleThreatSpeed) return;
            Vec3 fwd = v / speed;
            Vec3 right = new Vec3(fwd.Z, 0f, -fwd.X);
            float halfWidth = car.Model.HalfExtents.X + 1f;
            IList<Ped> peds = world.Peds;
            for (int i = 0; i < peds.Count; i++)
            {
                Ped p = peds[i];
                if (p == player || p.Has(PedFlags.InVehicle) || !(p.Brain is PedBrain)) continue;
                if (!InPath(p.Position - car.Position, fwd, right, speed, halfWidth)) continue;
                if (Allow(_threatKeys, _threatTimes, ref _threatNext, p.Id, VehicleThreatRepeatSeconds))
                    Send(world, p, StimulusKind.VehicleThreat, car.Tag, car.Position, speed);
            }
            if (world.Interior.IsInside) return;
            List<AmbientCitizen> cs = world.Ambient.Citizens;
            for (int i = 0; i < cs.Count; i++)
                if (InPath(cs[i].Position - car.Position, fwd, right, speed, halfWidth)) Panic(world, cs[i]);
        }

        private static bool InPath(Vec3 rel, Vec3 fwd, Vec3 right, float speed, float halfWidth)
        {
            rel = rel.Flat;
            float along = Vec3.Dot(rel, fwd);
            return along > 0f && along <= VehicleThreatRange && along / speed < VehicleThreatSeconds
                && Math.Abs(Vec3.Dot(rel, right)) < halfWidth;
        }

        private static float SegmentDistance(Vec3 p, Vec3 a, Vec3 b)
        {
            Vec3 ab = b - a;
            float len2 = ab.LengthSquared;
            float t = len2 > 1e-8f ? MathUtil.Clamp01(Vec3.Dot(p - a, ab) / len2) : 0f;
            return Vec3.Distance(p, a + ab * t);
        }
    }
}
