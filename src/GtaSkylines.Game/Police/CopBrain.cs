using System;
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
    /// <item>Arrest: run (5 m/s) to within <see cref="ArrestRange"/>; on first reaching <see cref="OrderRange"/> queue
    /// the subtitle (speaker "Police", text <see cref="HandsUpLine"/>, 2.5 s) — at most once per <see cref="OrderCooldown"/>
    /// per cop. BUSTED when, for <see cref="ArrestHoldSeconds"/> continuously, this cop is within ArrestRange and the
    /// player (on foot) has the HandsUp flag or moves slower than 0.3 m/s, or (in a car) the car moves slower than
    /// 0.5 m/s and this cop is within ArrestRange of the driver door: sets <c>Wanted.BustedRequested</c> = true and
    /// <c>Wanted.BustedByTag</c> = this cop's tag (G6 runs the BUSTED flow). Resisting: after the order, the player
    /// getting farther than <see cref="ResistDistance"/> from this cop within 10 s of it reports
    /// <c>CrimeType.ResistingArrest</c> through <c>CrimeSystem.ReportByCop</c> once per order.</item>
    /// <item>Shoot: sets Hostile; CurrentWeapon = Pistol; keeps 8-15 m from the player; fires through
    /// <c>ICrimePedServices.Fire</c> from the eye (feet + 1.5 m) toward the player's chest (feet + 1.2 m, or the car
    /// centre) when it can see the player, at intervals U[0.6, 1.2) s × ReactionScale, with an aim error (degrees, uniform
    /// in a cone) of (1.5 + 0.12 × distance + 0.5 × player speed) × (Easy 1.5, Normal 1, Hard 0.7).</item>
    /// <item>Cover: when the cop took damage within 1 s (LastDamageTime) or a player Gunshot happened within 30 m in
    /// the last 1 s, and its car is within 15 m: move to car centre + (away-from-player direction) × (car half width +
    /// 1 m) and stay 2-4 s (seeded), shooting if Shoot applies.</item>
    /// <item>Random choices use <c>new Rng(Rng.Seed(police seed, cop ped id, decision counter))</c>.</item>
    /// </list>
    /// </summary>
    public sealed class CopBrain : IPedBrain
    {
        /// <summary>Busted distance, metres.</summary>
        public const float ArrestRange = 2f;
        /// <summary>Distance at which the order is shouted, metres.</summary>
        public const float OrderRange = 8f;
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

        /// <summary>
        /// The shoot rule (see class) as a pure function: true when stars ≥ 3, or (armed with a non-fist weapon and
        /// (aiming or fired within 2 s)), or a cop was attacked this episode.
        /// </summary>
        public static bool ShouldShoot(int stars, bool playerArmed, bool playerAimingOrFiring, bool copAttacked)
        {
            throw new NotImplementedException();
        }

        /// <summary>Aim error cone half-angle in degrees for a distance (m), player speed (m/s) and difficulty factor (see class).</summary>
        public static float AimErrorDeg(float distance, float playerSpeed, float difficultyFactor)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public string Name { get { return "Cop"; } }

        /// <inheritdoc />
        public bool IsActive { get { return true; } }

        /// <inheritdoc />
        public void Step(GameWorld world, Ped ped, float dt)
        {
            throw new NotImplementedException();
        }
    }
}
