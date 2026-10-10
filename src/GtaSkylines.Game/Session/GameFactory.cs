using System;
using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Combat;
using GtaSkylines.Game.Crime;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Missions;
using GtaSkylines.Game.Peds;
using GtaSkylines.Game.Police;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Settings;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Session
{
    /// <summary>A complete game: the session and the systems the Mod talks to directly.</summary>
    public sealed class GameParts
    {
        /// <summary>The session (G6 systems plus everything below, registered in <see cref="SystemOrder"/>).</summary>
        public GameSession Session;
        /// <summary>G7 weapons and damage (its <c>Wheel</c> is the weapon wheel; <c>PlayerWeapon</c> the player's magazine).</summary>
        public CombatSystem Combat;
        /// <summary>G8 crimes, witnesses, stars and heat.</summary>
        public CrimeSystem Crime;
        /// <summary>G8 dispatcher and units.</summary>
        public PoliceSystem Police;
        /// <summary>G9 missions, side jobs, phone, Chirper, dialogue.</summary>
        public MissionRunner Missions;

        /// <summary>The world.</summary>
        public GameWorld World { get { return Session.World; } }
    }

    /// <summary>Builds the whole game for one play session (M7 wiring; the Mod and the headless tests use the same path).</summary>
    public static class GameFactory
    {
        /// <summary>
        /// A session with every system: G6 (constructor), G7 <see cref="CombatSystem.Install"/> (Peds 30, Combat 50, brain
        /// factory, time scale, ammo), G8 <see cref="CrimeSystem"/> (60) over <see cref="CrimePedServices"/> and
        /// <see cref="PoliceSystem"/> (70), G9 <see cref="MissionRunner"/> (80, installs <c>DialogueLines</c> as the
        /// dialogue hook; the session routes its wanted writes to G8 through <see cref="CrimeWantedControl"/>) and hostile mission
        /// actors given a G7 <see cref="HostileBrain"/>. Every system gets a seed derived from <paramref name="seed"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public static GameParts Create(ICity city, GameSettings settings, PlayerProfile profile, uint seed)
        {
            var session = new GameSession(city, settings, profile, seed);
            var parts = new GameParts { Session = session };
            parts.Combat = CombatSystem.Install(session);
            parts.Crime = new CrimeSystem(new CrimePedServices(), seed);
            parts.Police = new PoliceSystem(parts.Crime, Rng.Seed(seed, 0x70u));
            session.AddSystem(parts.Crime);
            session.AddSystem(parts.Police);
            parts.Missions = new MissionRunner(session.World, Rng.Seed(seed, 0x80u));
            parts.Missions.Context.Brains = MissionBrains;
            session.AddSystem(parts.Missions);
            return parts;
        }

        /// <summary>Hostile mission actors fight with their spec weapon; others get the G7 factory's brain for a mission ped (none).</summary>
        public static IPedBrain MissionBrains(GameWorld world, Ped ped, MissionActorSpec spec)
        {
            if (spec == null) return null;
            int target = world.Player == null ? 0 : world.Player.Tag;
            if (spec.Hostile)
            {
                ped.Flags |= PedFlags.Hostile;
                return new HostileBrain(world, PedTraits.Roll(world, ped, PedOrigin.Mission, default(Interiors.NpcSpot)), spec.Weapon, target);
            }
            return world.Hooks.Brains == null ? null : world.Hooks.Brains.Create(world, ped, PedOrigin.Mission, target, default(Interiors.NpcSpot));
        }
    }

    /// <summary>G8's view of G7: <see cref="Perception"/>, the CallPolice brain and <see cref="Weapons.Fire(GameWorld, Ped, Vec3, Vec3)"/>.</summary>
    public sealed class CrimePedServices : ICrimePedServices
    {
        private readonly List<WitnessCandidate> _candidates = new List<WitnessCandidate>(32);

        /// <inheritdoc />
        public int WitnessesOf(GameWorld world, GameEvent e, List<CrimeWitness> into)
        {
            into.Clear();
            Perception.WitnessesOf(world, e, _candidates);
            for (int i = 0; i < _candidates.Count; i++)
            {
                WitnessCandidate c = _candidates[i];
                into.Add(new CrimeWitness { Tag = c.Tag, Position = c.Position, Distance = c.Distance });
            }
            return into.Count;
        }

        /// <inheritdoc />
        public bool CanSee(GameWorld world, Ped observer, Vec3 point)
        {
            return Perception.CanSee(world, observer, point);
        }

        /// <inheritdoc />
        public IPedBrain CreateCallPolice(GameWorld world, Ped witness, int crimeId, float callSeconds)
        {
            int perp = world.Player == null ? 0 : world.Player.Tag;
            return new CallPoliceBrain(PedBrainFactory.TraitsOf(world, witness), crimeId, perp, callSeconds);
        }

        /// <inheritdoc />
        public void Fire(GameWorld world, Ped shooter, Vec3 origin, Vec3 direction)
        {
            Weapons.Fire(world, shooter, origin, direction);
        }
    }
}
