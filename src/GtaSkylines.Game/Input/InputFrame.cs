namespace GtaSkylines.Game.Input
{
    /// <summary>Logical buttons. The Mod maps physical keys to these through the settings' bindings.</summary>
    public enum GameButton
    {
        /// <summary>Shift: sprint on foot.</summary>
        Sprint = 0,
        /// <summary>Space: jump/climb on foot, handbrake in a vehicle.</summary>
        Jump = 1,
        /// <summary>Left Ctrl: toggle sneaking.</summary>
        Sneak = 2,
        /// <summary>F: enter or exit a vehicle.</summary>
        EnterExit = 3,
        /// <summary>E: talk, doors, shops; horn or siren toggle in a vehicle.</summary>
        Interact = 4,
        /// <summary>Left mouse: attack or fire.</summary>
        Attack = 5,
        /// <summary>Right mouse: aim.</summary>
        Aim = 6,
        /// <summary>R: reload.</summary>
        Reload = 7,
        /// <summary>Tab (hold): weapon wheel.</summary>
        WeaponWheel = 8,
        /// <summary>Mouse wheel up or a key: next weapon.</summary>
        NextWeapon = 9,
        /// <summary>Mouse wheel down or a key: previous weapon.</summary>
        PrevWeapon = 10,
        /// <summary>X: hands up.</summary>
        HandsUp = 11,
        /// <summary>V: cycle camera view.</summary>
        CameraView = 12,
        /// <summary>C: look behind.</summary>
        LookBehind = 13,
        /// <summary>H: headlights.</summary>
        Headlights = 14,
        /// <summary>Up arrow: phone.</summary>
        Phone = 15,
        /// <summary>M: interaction menu.</summary>
        InteractionMenu = 16,
        /// <summary>Esc or P: pause menu.</summary>
        Pause = 17,
        /// <summary>Z: enlarge the minimap.</summary>
        BigMap = 18,
        /// <summary>Menu navigation: confirm (Enter).</summary>
        MenuAccept = 19,
        /// <summary>Menu navigation: back (Backspace).</summary>
        MenuBack = 20,
        /// <summary>Menu navigation: up.</summary>
        MenuUp = 21,
        /// <summary>Menu navigation: down.</summary>
        MenuDown = 22,
        /// <summary>Menu navigation: left.</summary>
        MenuLeft = 23,
        /// <summary>Menu navigation: right.</summary>
        MenuRight = 24,
    }

    /// <summary>
    /// One frame of player input in logical terms. Axes are already in game units; buttons are bit masks indexed by
    /// <see cref="GameButton"/>.
    /// </summary>
    public struct InputFrame
    {
        /// <summary>Strafe/steer axis, −1 (A, left) .. 1 (D, right).</summary>
        public float MoveX;
        /// <summary>Forward axis, −1 (S, back/brake) .. 1 (W, forward/throttle).</summary>
        public float MoveY;
        /// <summary>Yaw change requested by the mouse this frame, degrees (sensitivity applied; + turns right).</summary>
        public float LookYawDeg;
        /// <summary>Pitch change requested by the mouse this frame, degrees (sensitivity and invert applied; + looks up).</summary>
        public float LookPitchDeg;
        /// <summary>Mouse position on screen in pixels from the top-left (menus and the map only).</summary>
        public float PointerX, PointerY;
        /// <summary>Buttons held this frame.</summary>
        public uint Held;
        /// <summary>Buttons that went down this frame.</summary>
        public uint Pressed;
        /// <summary>Buttons that went up this frame.</summary>
        public uint Released;

        /// <summary>True while the button is held.</summary>
        public bool IsHeld(GameButton b) { return (Held & (1u << (int)b)) != 0; }

        /// <summary>True on the frame the button went down.</summary>
        public bool WasPressed(GameButton b) { return (Pressed & (1u << (int)b)) != 0; }

        /// <summary>True on the frame the button went up.</summary>
        public bool WasReleased(GameButton b) { return (Released & (1u << (int)b)) != 0; }

        /// <summary>Bit for a button.</summary>
        public static uint Bit(GameButton b) { return 1u << (int)b; }
    }
}
