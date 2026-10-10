using System;

namespace GtaSkylines.Game.Physics
{
    /// <summary>
    /// Tuning for <see cref="CharacterMotor"/> (DESIGN F-FOOT-1/3, PLAN G1). Defaults are the contract values. The motor
    /// reads these every step, so a change applies from the next step. Distances in metres, times in seconds, speeds in
    /// m/s, angles in degrees. All values must be positive (fractions in [0, 1]).
    /// </summary>
    public sealed class MotorSettings
    {
        /// <summary>Capsule radius.</summary>
        public float Radius = 0.35f;

        /// <summary>Capsule total height, feet to top of head (crouch/sneak keeps it).</summary>
        public float Height = 1.8f;

        /// <summary>Gap the capsule keeps from static surfaces after collide-and-slide.</summary>
        public float SkinWidth = 0.01f;

        /// <summary>Most collide-and-slide iterations per step.</summary>
        public int MaxSlideIterations = 4;

        /// <summary>Highest obstacle top above the feet that grounded walking climbs without a jump.</summary>
        public float StepHeight = 0.45f;

        /// <summary>Steepest walkable ground, degrees from horizontal; steeper ground is a slide.</summary>
        public float MaxSlopeDeg = 50f;

        /// <summary>How far below the feet a grounded motor looks for ground to stick to after moving.</summary>
        public float GroundSnapDistance = 0.5f;

        /// <summary>Gravity, m/s² downward.</summary>
        public float Gravity = 9.81f;

        /// <summary>Largest downward speed.</summary>
        public float TerminalVelocity = 55f;

        /// <summary>Height of the feet's jump apex above take-off on flat ground.</summary>
        public float JumpApexHeight = 1.1f;

        /// <summary>Horizontal acceleration toward the desired velocity on walkable ground, m/s².</summary>
        public float GroundAcceleration = 40f;

        /// <summary>Fraction of <see cref="GroundAcceleration"/> available in the air and while sliding.</summary>
        public float AirControl = 0.3f;

        /// <summary>Lowest ledge top above the feet that Jump climbs (lower ones are jumped or stepped).</summary>
        public float LedgeMinHeight = 0.6f;

        /// <summary>Highest ledge top above the feet that Jump climbs.</summary>
        public float LedgeMaxHeight = 2.0f;

        /// <summary>How far ahead of the capsule's front surface (along the facing) a ledge's front edge may be.</summary>
        public float LedgeReach = 0.6f;

        /// <summary>Duration of a ledge climb.</summary>
        public float ClimbSeconds = 0.7f;

        /// <summary>Depth of the feet below the water surface at which the motor swims, and the depth it floats at.</summary>
        public float SwimDepth = 1.2f;

        /// <summary>Largest horizontal speed while swimming.</summary>
        public float SwimSpeed = 1.5f;

        /// <summary>Largest vertical speed at which a swimmer is brought to its floating depth.</summary>
        public float FloatSpeed = 2f;

        /// <summary>Breath when full; it drains one second per second while swimming.</summary>
        public float BreathSeconds = 20f;

        /// <summary>Breath regained per second when not swimming.</summary>
        public float BreathRefillPerSecond = 4f;

        /// <summary>Take-off vertical speed that reaches <see cref="JumpApexHeight"/>: <c>sqrt(2 · Gravity · JumpApexHeight)</c>.</summary>
        public float JumpSpeed
        {
            get { return (float)Math.Sqrt(2.0 * Gravity * JumpApexHeight); }
        }
    }
}
