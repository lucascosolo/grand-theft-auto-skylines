namespace GtaSkylines.Mod.Rendering
{
    /// <summary>
    /// Render faults of one play session: the first <see cref="LoggedFaults"/> are logged, and at <see cref="ExitFaults"/>
    /// the renderer gives up so the session can exit and restore the city instead of playing on invisible. Unity-free.
    /// </summary>
    internal sealed class RenderFaultBudget
    {
        /// <summary>Faults that are logged.</summary>
        public const int LoggedFaults = 5;
        /// <summary>Faults after which play should end.</summary>
        public const int ExitFaults = 10;

        /// <summary>Faults so far.</summary>
        public int Count { get; private set; }

        /// <summary>True once <see cref="ExitFaults"/> faults have happened.</summary>
        public bool Exhausted { get { return Count >= ExitFaults; } }

        /// <summary>Counts one fault; true when it should be logged.</summary>
        public bool Record()
        {
            Count++;
            return Count <= LoggedFaults;
        }

        /// <summary>Back to no faults.</summary>
        public void Reset()
        {
            Count = 0;
        }
    }
}
