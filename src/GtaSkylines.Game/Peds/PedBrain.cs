using System;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Peds
{
    /// <summary>Something a ped noticed, routed to its brain by <see cref="PedsSystem"/>.</summary>
    public enum StimulusKind
    {
        /// <summary>Someone walked into this ped (Bumped event, closing speed ≥ <see cref="PedsSystem.BumpStumbleSpeed"/>).</summary>
        Bumped,
        /// <summary>This ped was punched or shoved (Punched / Shove event with B = this ped).</summary>
        Punched,
        /// <summary>This ped was hit by a bullet and survived standing (Shot event with B = this ped).</summary>
        Hurt,
        /// <summary>A shot passed or landed within <see cref="PedsSystem.ShotAtRadius"/> (either end of a Shot event).</summary>
        ShotAt,
        /// <summary>A gunshot was heard (<see cref="Perception.Hears"/>).</summary>
        GunshotHeard,
        /// <summary>An explosion was heard.</summary>
        ExplosionHeard,
        /// <summary>This ped saw violence nearby (a Punched event within <see cref="PedsSystem.WitnessViolenceRadius"/> it can see).</summary>
        SawViolence,
        /// <summary>A weapon is aimed at this ped (AimedAt event with B = this ped). Value: distance to the aimer.</summary>
        AimedAt,
        /// <summary>A car is coming at this ped fast (see <see cref="PedsSystem"/>).</summary>
        VehicleThreat,
    }

    /// <summary>One stimulus.</summary>
    public struct Stimulus
    {
        /// <summary>Kind.</summary>
        public StimulusKind Kind;
        /// <summary>Who or what caused it (WorldTag), 0 unknown.</summary>
        public int SourceTag;
        /// <summary>Where the cause was (the event position, or the threat's position).</summary>
        public Vec3 SourcePosition;
        /// <summary>Kind-specific number (AimedAt: distance; VehicleThreat: the car's speed; Bumped: closing speed).</summary>
        public float Value;
    }

    /// <summary>
    /// Base of every G7 brain: carries <see cref="PedTraits"/> and the seconds spent in the brain, and reacts to
    /// stimuli. Subclasses override <see cref="React"/> to narrow the default.
    /// <para><b>Default reactions</b> (<see cref="DefaultReact"/>, used by calm brains: Idle, Return, Stumble):
    /// <list type="bullet">
    /// <item>Bumped → <see cref="StumbleBrain"/> (away from the source).</item>
    /// <item>Punched → <see cref="FightBrain"/> against the source when Traits.Fighter, the source is a ped holding no
    /// firearm and this ped's health ≥ <see cref="FightBrain.GiveUpHealth"/>; else <see cref="FleeBrain"/>.</item>
    /// <item>Hurt, SawViolence → FleeBrain.</item>
    /// <item>ShotAt, GunshotHeard, ExplosionHeard → <see cref="CowerBrain"/> when Traits.Senior or (an adult standing
    /// within <see cref="CowerBrain.WallDistance"/> of static geometry, tested by 4 horizontal raycasts at chest height);
    /// else FleeBrain.</item>
    /// <item>AimedAt: Value ≤ <see cref="HandsUpBrain.Range"/> → <see cref="HandsUpBrain"/>; ≤
    /// <see cref="PedsSystem.AimReactRange"/> → FleeBrain; farther → nothing.</item>
    /// <item>VehicleThreat → <see cref="SidestepBrain"/>.</item>
    /// </list>
    /// A new brain takes effect from the next step (it is assigned to <c>ped.Brain</c>). Making a brain allocates one
    /// object; that happens on stimuli, never in a steady state.</para>
    /// </summary>
    public abstract class PedBrain : IPedBrain
    {
        /// <summary>Creates a brain with the ped's traits.</summary>
        protected PedBrain(PedTraits traits)
        {
            Traits = traits;
        }

        /// <summary>The ped's temperament (passed on to every next brain).</summary>
        public PedTraits Traits { get; private set; }

        /// <summary>Simulation seconds spent in this brain (added at the start of each <see cref="Step"/>).</summary>
        public float Seconds { get; protected set; }

        /// <inheritdoc />
        public abstract string Name { get; }

        /// <inheritdoc />
        public abstract bool IsActive { get; }

        /// <inheritdoc />
        public abstract void Step(GameWorld world, Ped ped, float dt);

        /// <summary>Handles a stimulus; the default is <see cref="DefaultReact"/>.</summary>
        public virtual void React(GameWorld world, Ped ped, Stimulus stimulus)
        {
            DefaultReact(world, ped, stimulus);
        }

        /// <summary>The default reaction table (see the class).</summary>
        protected void DefaultReact(GameWorld world, Ped ped, Stimulus stimulus)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Clears the transient flags a G7 brain may have set (HandsUp, OnPhone, Panicking, Aiming; never Hostile, which
        /// G8/G9 also own — <see cref="FightBrain"/> clears it only if it set it) and assigns <paramref name="next"/>.
        /// </summary>
        protected static void Switch(Ped ped, IPedBrain next)
        {
            throw new NotImplementedException();
        }
    }
}
