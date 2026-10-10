using System;
using GtaSkylines.Game.Profile;

namespace GtaSkylines.Game.Combat
{
    /// <summary>One sector of the weapon wheel.</summary>
    public struct WeaponWheelSlot
    {
        /// <summary>The weapon (slot index = (int)weapon).</summary>
        public WeaponId Weapon;
        /// <summary>Owned (Fists always).</summary>
        public bool Owned;
        /// <summary>Rounds carried (<c>Profile.GetAmmo</c>), 0 for melee.</summary>
        public int Ammo;
        /// <summary>Can be selected: owned and (melee or Ammo &gt; 0).</summary>
        public bool Selectable;
    }

    /// <summary>
    /// The weapon wheel for the HUD (M4 draws it from this model; <see cref="CombatSystem"/> drives it).
    /// <para>
    /// Layout: <see cref="WeaponInfo.Count"/> sectors of 60°; sector i (= (int)WeaponId) is centred at i × 60° measured
    /// clockwise from straight up (Fists up, Bat upper right, Pistol lower right, Smg down, Shotgun lower left, Carbine
    /// upper left).
    /// </para>
    /// <para>
    /// Pointer: while open, the step's look deltas move a virtual stick: x += LookYawDeg × <see cref="PointerGain"/>,
    /// y += LookPitchDeg × PointerGain (y up), clamped to length 1. The highlighted sector is the one containing the stick
    /// direction when its length ≥ <see cref="DeadZone"/>; inside the dead zone the highlight stays where it was
    /// (initially the weapon in hand). Highlighting a non-selectable sector is allowed; <see cref="Close"/> selects only a
    /// selectable highlight. Nothing allocates.
    /// </para>
    /// </summary>
    public sealed class WeaponWheelModel
    {
        /// <summary>Stick units per degree of look input (1/30).</summary>
        public const float PointerGain = 1f / 30f;
        /// <summary>Stick length below which the highlight does not change (0.3).</summary>
        public const float DeadZone = 0.3f;
        /// <summary>Simulation time scale requested while the wheel is open (0.1); 1 when closed.</summary>
        public const float OpenTimeScale = 0.1f;

        private readonly WeaponWheelSlot[] _slots = new WeaponWheelSlot[WeaponInfo.Count];

        /// <summary>True while the wheel is shown (WeaponWheel held).</summary>
        public bool IsOpen { get; private set; }

        /// <summary>Highlighted sector index (= (int)WeaponId).</summary>
        public int Highlighted { get; private set; }

        /// <summary>Virtual stick x (-1..1, right positive).</summary>
        public float PointerX { get; private set; }

        /// <summary>Virtual stick y (-1..1, up positive).</summary>
        public float PointerY { get; private set; }

        /// <summary>The time scale the session should run at: <see cref="OpenTimeScale"/> while open, else 1. The Mod (or G6) copies it into <c>GameSession.TimeScale</c>.</summary>
        public float DesiredTimeScale { get { return IsOpen ? OpenTimeScale : 1f; } }

        /// <summary>Sector <paramref name="index"/> (0..5) as of the last <see cref="Refresh"/>.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Index outside 0..5.</exception>
        public WeaponWheelSlot GetSlot(int index)
        {
            if (index < 0 || index >= _slots.Length) throw new ArgumentOutOfRangeException("index");
            return _slots[index];
        }

        /// <summary>Refills the slots from the profile (ownership and ammo).</summary>
        public void Refresh(PlayerProfile profile)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                var w = (WeaponId)i;
                bool owned = profile.OwnsWeapon(w);
                int ammo = WeaponInfo.UsesAmmo(w) ? profile.GetAmmo(w) : 0;
                _slots[i] = new WeaponWheelSlot { Weapon = w, Owned = owned, Ammo = ammo, Selectable = owned && (!WeaponInfo.UsesAmmo(w) || ammo > 0) };
            }
        }

        /// <summary>Opens the wheel: stick (0, 0), highlight = <paramref name="current"/>, slots refreshed. No-op when open.</summary>
        public void Open(PlayerProfile profile, WeaponId current)
        {
            if (IsOpen) return;
            Refresh(profile);
            IsOpen = true;
            PointerX = 0f;
            PointerY = 0f;
            Highlighted = (int)current;
        }

        /// <summary>Moves the stick by look deltas (degrees) and updates the highlight (see the class). Ignored when closed.</summary>
        public void MovePointer(float lookYawDeg, float lookPitchDeg)
        {
            if (!IsOpen) return;
            float x = PointerX + lookYawDeg * PointerGain, y = PointerY + lookPitchDeg * PointerGain;
            float len = (float)Math.Sqrt(x * x + y * y);
            if (len > 1f)
            {
                x /= len;
                y /= len;
                len = 1f;
            }
            PointerX = x;
            PointerY = y;
            if (len >= DeadZone) Highlighted = SectorOf(x, y);
        }

        /// <summary>
        /// The sector containing direction (x, y) (y up, clockwise from up): index = floor(((angle + 30) mod 360) / 60)
        /// where angle = atan2(x, y) in degrees; a boundary belongs to the higher sector. Examples: (0, 1) → 0,
        /// (0.87, 0.5) (60°) → 1, (1, 0) (90°, boundary) → 2, (0, −1) → 3, (−1, 0) (270°, boundary) → 5.
        /// </summary>
        public static int SectorOf(float x, float y)
        {
            double a = Math.Atan2(x, y) * 180.0 / Math.PI + 30.0;
            a %= 360.0;
            if (a < 0.0) a += 360.0;
            int i = (int)(a / 60.0);
            return i >= WeaponInfo.Count ? WeaponInfo.Count - 1 : i;
        }

        /// <summary>Closes the wheel; returns true with the highlighted weapon when it is selectable, else false.</summary>
        public bool Close(out WeaponId selected)
        {
            bool was = IsOpen;
            IsOpen = false;
            selected = (WeaponId)Highlighted;
            return was && _slots[Highlighted].Selectable;
        }
    }
}
