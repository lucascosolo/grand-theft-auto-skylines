using System.Collections.Generic;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Crime
{
    /// <summary>
    /// What G8 needs from G7 (perception, the CallPolice brain, firing). G7 implements it over
    /// <c>Perception.WitnessesOf</c>, <c>Perception.CanSee</c>, its <c>CallPolice</c> brain and <c>Combat.Fire</c>; the
    /// session wires it into <see cref="CrimeSystem"/> and the Police system. Tests use a fake. Every member must not
    /// allocate in steady state.
    /// </summary>
    public interface ICrimePedServices
    {
        /// <summary>
        /// Clears <paramref name="into"/> and adds the civilians (Game peds of kind Civilian or Npc, and ambient snapshot
        /// citizens; never cops, never the player) that could see <paramref name="e"/>: sight within 40 m by day / 25 m by
        /// night, halved when the player sneaks, interiors bounded by the room walls. Returns the count.
        /// </summary>
        int WitnessesOf(GameWorld world, GameEvent e, List<CrimeWitness> into);

        /// <summary>True when <paramref name="observer"/> can see <paramref name="point"/> (G7 cone, day/night range, line of sight).</summary>
        bool CanSee(GameWorld world, Ped observer, Vec3 point);

        /// <summary>
        /// A CallPolice brain for <paramref name="witness"/>: emits CallStarted (A = witness tag, Value = crimeId) on its
        /// first step, CallCompleted after <paramref name="callSeconds"/> on the phone, or CallInterrupted when
        /// threatened (G7 rules), then flees.
        /// </summary>
        IPedBrain CreateCallPolice(GameWorld world, Ped witness, int crimeId, float callSeconds);

        /// <summary>Hitscan shot with the shooter's current weapon (<c>Combat.Fire</c>); emits Shot and Gunshot.</summary>
        void Fire(GameWorld world, Ped shooter, Vec3 origin, Vec3 direction);
    }
}
