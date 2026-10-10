using System;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Combat
{
    /// <summary>What <see cref="WeaponUser.TryAttack"/> did.</summary>
    public enum AttackOutcome
    {
        /// <summary>A shot was fired or a swing made.</summary>
        Attacked,
        /// <summary>Still within 1 / rate of the last attack.</summary>
        Cooldown,
        /// <summary>A reload is running.</summary>
        Reloading,
        /// <summary>The magazine was empty: a reload started (if any reserve), nothing fired.</summary>
        Empty,
    }

    /// <summary>
    /// Rate of fire, magazine and reload timing for one ped's weapon in hand (the player's lives in
    /// <see cref="CombatSystem"/>; G8 cop brains and G7's FightBrain own one each). The magazine is
    /// <c>Ped.MagazineAmmo</c>; the weapon is <c>Ped.CurrentWeapon</c>.
    /// <para>
    /// Ammo model: with a profile (the player), <c>Profile.GetAmmo(w)</c> is every round carried, the magazine included;
    /// invariant <c>MagazineAmmo ≤ GetAmmo(current)</c> (enforced at the start of <see cref="Step"/>); each shot removes 1
    /// round from both (one shell for the shotgun's 8 pellets); a reload sets MagazineAmmo = min(Magazine, GetAmmo). The
    /// HUD's reserve is therefore GetAmmo − MagazineAmmo. Without a profile (NPCs) the reserve is unlimited.
    /// </para>
    /// <para>
    /// Timing: <see cref="Cooldown"/> counts down by dt and an attack sets it to 1 / rate; so with Attack held from
    /// t = 0 a 12/s SMG fires at t = 0, 1/12, 2/12 ... (an attack is allowed when Cooldown ≤ 0; excess carries so that the
    /// long-run rate is exact at 60 Hz). A reload lasts <c>ReloadSeconds</c> and fills the magazine when it ends; an empty
    /// magazine starts a reload automatically (after the last shot, or on an attack attempt) when reserve remains.
    /// </para>
    /// </summary>
    public sealed class WeaponUser
    {
        /// <summary>A user drawing ammo from <paramref name="profile"/> (null = unlimited reserve).</summary>
        public WeaponUser(PlayerProfile profile)
        {
            Profile = profile;
        }

        /// <summary>Ammo source, or null for unlimited.</summary>
        public PlayerProfile Profile { get; private set; }

        /// <summary>Seconds until the next attack is allowed (≤ 0: ready).</summary>
        public float Cooldown { get; private set; }

        /// <summary>True while reloading.</summary>
        public bool IsReloading { get { return ReloadRemaining > 0f; } }

        /// <summary>Seconds left in the reload, 0 when not reloading.</summary>
        public float ReloadRemaining { get; private set; }

        /// <summary>
        /// Advances timers by <paramref name="dt"/>: clamps the magazine to the carried ammo, counts the cooldown down,
        /// finishes a reload when its time runs out (MagazineAmmo = min(Magazine, carried)).
        /// </summary>
        public void Step(Ped ped, float dt)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Puts <paramref name="weapon"/> in hand: cancels any reload, Cooldown 0, <c>CurrentWeapon</c> = weapon,
        /// MagazineAmmo = min(Magazine, carried) (0 for melee). No-op when it is already in hand.
        /// </summary>
        public void Equip(Ped ped, WeaponId weapon)
        {
            throw new NotImplementedException();
        }

        /// <summary>Starts a reload when the weapon is a firearm, not reloading, the magazine is not full and carried &gt; MagazineAmmo. True when started.</summary>
        public bool StartReload(Ped ped)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Attacks with the weapon in hand if allowed: melee → <see cref="Weapons.Melee"/>; firearm with MagazineAmmo &gt; 0 →
        /// <see cref="Weapons.Fire(GameWorld, Ped, WeaponId, Vec3, Vec3, float)"/>(origin, direction,
        /// <paramref name="spreadDeg"/>), one round removed from the magazine and the profile, and an automatic reload when
        /// that emptied the magazine. <paramref name="shot"/>/<paramref name="melee"/> carry the attack's result.
        /// </summary>
        public AttackOutcome TryAttack(GameWorld world, Ped ped, Vec3 origin, Vec3 direction, float spreadDeg,
            out ShotResult shot, out MeleeResult melee)
        {
            throw new NotImplementedException();
        }
    }
}
