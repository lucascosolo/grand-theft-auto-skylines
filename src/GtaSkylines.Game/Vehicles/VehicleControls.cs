namespace GtaSkylines.Game.Vehicles
{
    /// <summary>
    /// One fixed step's driver input, from the keyboard or from AI (<c>RouteFollower</c>). Raw input: the body clamps every
    /// value into its range and treats a non-finite value as 0; steering is rate-limited inside the body, never by callers.
    /// <para>
    /// "S brakes, then reverses" (GTA convention), decided by <see cref="VehicleBody.Speed"/> at the start of the step:
    /// <list type="bullet">
    /// <item>Speed &gt; <see cref="VehicleTuning.ReverseEngageSpeed"/> (rolling forward): <see cref="Throttle"/> drives
    /// forward and <see cref="Brake"/> brakes.</item>
    /// <item>Speed &lt; −<see cref="VehicleTuning.ReverseEngageSpeed"/> (rolling backward): <see cref="Brake"/> drives in
    /// reverse and <see cref="Throttle"/> brakes.</item>
    /// <item>Otherwise (nearly stopped): whichever of Throttle and Brake is larger drives, forward or in reverse
    /// respectively, at that strength; equal values (both 0 included) hold the car with the brakes.</item>
    /// </list>
    /// </para>
    /// </summary>
    public struct VehicleControls
    {
        /// <summary>Accelerator, 0..1 (W).</summary>
        public float Throttle;

        /// <summary>Brake, or reverse throttle when stopped or reversing, 0..1 (S).</summary>
        public float Brake;

        /// <summary>Steering, −1 (full left) .. +1 (full right). +1 turns the front wheels toward the body's +x.</summary>
        public float Steer;

        /// <summary>Handbrake (Space): locks the rear wheels and cuts rear lateral grip to <see cref="VehicleTuning.HandbrakeGripFactor"/>.</summary>
        public bool Handbrake;

        /// <summary>No input: throttle, brake and steer 0, handbrake off.</summary>
        public static readonly VehicleControls None = default(VehicleControls);
    }
}
