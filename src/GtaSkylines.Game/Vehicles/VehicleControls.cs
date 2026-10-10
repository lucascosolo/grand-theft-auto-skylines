namespace GtaSkylines.Game.Vehicles
{
    /// <summary>
    /// One fixed step's driver input, from the keyboard or from AI (<c>RouteFollower</c>). Raw input: the body clamps every
    /// value into its range and treats a non-finite value as 0; steering is rate-limited inside the body, never by callers.
    /// <para>
    /// "S brakes, then reverses" (GTA convention). Only the larger of <see cref="Throttle"/> and <see cref="Brake"/> acts,
    /// at its own strength; what it does depends on <see cref="VehicleBody.Speed"/> at the start of the step:
    /// <list type="bullet">
    /// <item>Speed &gt; <see cref="VehicleTuning.ReverseEngageSpeed"/> (rolling forward): Throttle drives forward, Brake
    /// brakes; a tie brakes at that strength (no pedals: coasting).</item>
    /// <item>Speed &lt; −<see cref="VehicleTuning.ReverseEngageSpeed"/> (rolling backward): Brake drives in reverse,
    /// Throttle brakes; a tie brakes at that strength (no pedals: coasting).</item>
    /// <item>Otherwise (nearly stopped): Throttle drives forward, Brake drives in reverse; a tie (both 0 included) holds
    /// the car with full brakes.</item>
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
