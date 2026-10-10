namespace GtaSkylines.Game.World
{
    /// <summary>Session time. Advanced only by <c>GameSession</c>.</summary>
    public sealed class GameClock
    {
        /// <summary>First hour counted as day (inclusive).</summary>
        public const float DayStartHour = 6f;
        /// <summary>First hour counted as night (inclusive); night runs to <see cref="DayStartHour"/>.</summary>
        public const float NightStartHour = 20f;

        /// <summary>Game seconds: the sum of every simulation step's dt (frozen while paused, scaled by the time scale).</summary>
        public double GameSeconds;
        /// <summary>Real seconds: the sum of every realDt passed to <c>GameSession.Tick</c> (pauses included).</summary>
        public double RealSeconds;
        /// <summary>Number of simulation steps run (not counting paused presentation-only steps).</summary>
        public long Steps;
        /// <summary>CS1's hour of day [0, 24), read from <c>ICity.TimeOfDayHours</c> at the start of each step.</summary>
        public float Hour;

        /// <summary>True when <see cref="Hour"/> ≥ 20 or &lt; 6.</summary>
        public bool IsNight { get { return Hour >= NightStartHour || Hour < DayStartHour; } }
    }
}
