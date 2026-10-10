namespace GtaSkylines.Mod.Lifecycle
{
    /// <summary>
    /// The <c>simulation_rate</c> setting (1..3) as a multiplier of CS1's normal simulation speed while playing:
    /// 1 = 0.5x (DESIGN F-MODE-2 default), 2 = 0.75x, 3 = 1x. Only slowing is offered, the direction the reference
    /// project used. Unity-free.
    /// </summary>
    public static class SimRateSetting
    {
        /// <summary>Multiplier for a setting value; out-of-range values are clamped to 1..3.</summary>
        public static float Multiplier(int setting)
        {
            if (setting <= 1) return 0.5f;
            if (setting == 2) return 0.75f;
            return 1f;
        }
    }
}
