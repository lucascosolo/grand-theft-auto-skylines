namespace GtaSkylines.Game.World
{
    /// <summary>
    /// A piece of game logic stepped by <c>GameSession</c> at the fixed 60 Hz rate, in ascending <see cref="Order"/>
    /// (systems with equal order run in the order they were added). Systems talk to each other only through
    /// <see cref="GameWorld"/>: its registries, blackboards and <see cref="GameEvents"/>.
    /// </summary>
    public interface IGameSystem
    {
        /// <summary>Position in the step (see <see cref="SystemOrder"/>). Read once, when the system is added.</summary>
        int Order { get; }

        /// <summary>
        /// Advances the system by one fixed step. <paramref name="dt"/> is <c>GameSession.FixedDt</c> × time scale (0 is
        /// possible while the weapon wheel slows time to a stop); <c>world.Input</c> is this step's input. Must not
        /// allocate in steady state.
        /// </summary>
        void Step(GameWorld world, float dt);
    }

    /// <summary>
    /// The orders of every system. Simulation systems (Order &lt; <see cref="Presentation"/>) do not run while the
    /// session is paused; presentation systems always run.
    /// </summary>
    public static class SystemOrder
    {
        /// <summary>G6: snapshot intake, collision rebuild of dynamic shapes, promotion and demotion.</summary>
        public const int Ambient = 5;
        /// <summary>G6: player on foot, interaction targeting, entering and leaving cars.</summary>
        public const int PlayerControl = 10;
        /// <summary>G6: every Game car's body and driver.</summary>
        public const int Vehicles = 20;
        /// <summary>G7: ped brains and ped motors.</summary>
        public const int Peds = 30;
        /// <summary>G6: interior transitions, occupants, menus from interactions.</summary>
        public const int Interiors = 40;
        /// <summary>G7: weapons, damage.</summary>
        public const int Combat = 50;
        /// <summary>G8: crimes, witnesses, stars, heat.</summary>
        public const int Crime = 60;
        /// <summary>G8: dispatcher and police units.</summary>
        public const int Police = 70;
        /// <summary>G9: missions, side jobs, phone, Chirper.</summary>
        public const int Missions = 80;
        /// <summary>G6: Wasted/Busted flow and respawn.</summary>
        public const int Respawn = 90;
        /// <summary>First presentation order: systems at or above it run even while paused.</summary>
        public const int Presentation = 95;
        /// <summary>G6: camera rig.</summary>
        public const int Camera = 95;
        /// <summary>G6: HUD model.</summary>
        public const int Hud = 100;
    }
}
