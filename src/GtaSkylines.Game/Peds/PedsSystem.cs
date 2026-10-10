using System;
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
    /// Brain non-null → <c>Brain.Step(world, ped, dt)</c>; then, unless InVehicle, <c>world.MovePed(ped, dt)</c>. A brain
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// Delivers <paramref name="stimulus"/> to <paramref name="ped"/> now (alive, has a <see cref="PedBrain"/>, not the
        /// player); false when not delivered. Systems (G8, G9) may use it to scare a ped.
        /// </summary>
        public static bool Deliver(GameWorld world, Ped ped, Stimulus stimulus)
        {
            throw new NotImplementedException();
        }
    }
}
