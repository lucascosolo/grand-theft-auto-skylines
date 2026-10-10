using System;
using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Mod.Rendering
{
    /// <summary>What an actor is doing, as the renderer draws it.</summary>
    public enum PedAnim
    {
        /// <summary>Standing.</summary>
        Idle,
        /// <summary>Walking.</summary>
        Walk,
        /// <summary>Jogging.</summary>
        Run,
        /// <summary>Sprinting.</summary>
        Sprint,
        /// <summary>Panicking (fleeing or cowering).</summary>
        Panic,
        /// <summary>Seated.</summary>
        Sitting,
        /// <summary>Lying on the back (knocked down, in bed).</summary>
        Lying,
        /// <summary>Hands up (drawn as idle).</summary>
        HandsUp,
        /// <summary>On the phone (drawn as idle).</summary>
        Phone,
    }

    /// <summary>CS1 animator inputs for one actor.</summary>
    public struct PedPose
    {
        /// <summary><c>CitizenInfo.AnimationState</c> value.</summary>
        public int Cs1State;
        /// <summary>Velocity magnitude passed to <c>SetRenderParameters</c> (drives walk/sprint choice and playback speed).</summary>
        public float Speed;
        /// <summary>Draw the model rotated onto its back (<see cref="PedAnimMap.LyingRotation"/>).</summary>
        public bool LieOnBack;
    }

    /// <summary>
    /// PedAnim → CS1 animation state (CitizenInfo.cs:10-27). <c>SetRenderParameters</c> plays idle below
    /// <see cref="WalkThreshold"/>, walk below <see cref="SprintThreshold"/>, sprint above (CitizenInfo.cs:148-150, :371-384),
    /// and sets the animator speed to the magnitude (walk) or magnitude / 3.2 (sprint) (:450-461).
    /// </summary>
    public static class PedAnimMap
    {
        /// <summary><c>AnimationState.Default</c>.</summary>
        public const int StateDefault = 0;
        /// <summary><c>AnimationState.Panic</c>.</summary>
        public const int StatePanic = 1;
        /// <summary><c>AnimationState.Sitting</c>.</summary>
        public const int StateSitting = 2;
        /// <summary><c>CitizenInfo.MIN_SPEED_WALK</c>.</summary>
        public const float WalkThreshold = 0.1f;
        /// <summary><c>CitizenInfo.MIN_SPEED_SPRINT</c>.</summary>
        public const float SprintThreshold = 3.2f;

        /// <summary>The CS1 pose for an animation at a ground speed (NaN or negative counts as 0).</summary>
        public static PedPose Map(PedAnim anim, float groundSpeedMs)
        {
            float s = groundSpeedMs > 0f ? groundSpeedMs : 0f;
            switch (anim)
            {
                case PedAnim.Walk:
                    return new PedPose { Cs1State = StateDefault, Speed = Math.Max(0.5f, Math.Min(3.0f, s)) };
                case PedAnim.Run:
                case PedAnim.Sprint:
                    return new PedPose { Cs1State = StateDefault, Speed = Math.Max(3.3f, s) };
                case PedAnim.Panic:
                    return new PedPose { Cs1State = StatePanic, Speed = s };
                case PedAnim.Sitting:
                    return new PedPose { Cs1State = StateSitting };
                case PedAnim.Lying:
                    return new PedPose { Cs1State = StateDefault, LieOnBack = true };
                default:
                    return new PedPose { Cs1State = StateDefault };
            }
        }

        /// <summary>Facing <paramref name="yawDeg"/> turned onto the back: head behind the feet, face up.</summary>
        public static Quat LyingRotation(float yawDeg)
        {
            return Quat.Yaw(yawDeg) * Quat.Euler(-90f, 0f, 0f);
        }
    }

    /// <summary>
    /// <c>InstanceID</c>s for our citizen prefab instances. <c>PrefabPool</c> keys instances by the whole InstanceID
    /// (PrefabPool.cs:39-70), and CS1 obtains citizen instances with type CitizenInstance (CitizenInstance.cs:178-180).
    /// Ours use type byte <see cref="OurType"/>, which no CS1 <c>InstanceType</c> (1..23) uses, so they never collide;
    /// the index is slot + 1 so <c>InstanceID.IsEmpty</c> (index 0) is never true.
    /// </summary>
    public static class PedInstanceIds
    {
        /// <summary>Type byte of our ids.</summary>
        public const uint OurType = 0xF0;
        /// <summary>Slots available.</summary>
        public const int MaxSlots = 4096;

        /// <summary>The raw InstanceID value for a slot.</summary>
        public static uint Raw(int slot)
        {
            if (slot < 0 || slot >= MaxSlots) throw new ArgumentOutOfRangeException("slot");
            return (OurType << 24) | (uint)(slot + 1);
        }
    }
}
