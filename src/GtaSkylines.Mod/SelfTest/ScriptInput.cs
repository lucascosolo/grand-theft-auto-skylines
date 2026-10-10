using GtaSkylines.Game.Input;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Roads;

// Unity-free part of the in-game self-test, compiled into GtaSkylines.Mod.Tests as well.
namespace GtaSkylines.Mod.SelfTest
{
    /// <summary>Builds the scripted <see cref="InputFrame"/>s the self-test feeds the play session.</summary>
    internal static class ScriptInput
    {
        public static InputFrame Press(GameButton b)
        {
            uint bit = InputFrame.Bit(b);
            return new InputFrame { Pressed = bit, Held = bit };
        }

        public static InputFrame Hold(GameButton b)
        {
            return new InputFrame { Held = InputFrame.Bit(b) };
        }

        public static float YawToward(Vec3 from, Vec3 to)
        {
            return Vec3.YawDegOf((to - from).Flat);
        }

        /// <summary>Turns the view toward <paramref name="to"/> and walks forward; stands still within 0.5 m.</summary>
        public static InputFrame Walk(float viewYawDeg, Vec3 from, Vec3 to, bool sprint)
        {
            var f = new InputFrame();
            if (sprint) f.Held = InputFrame.Bit(GameButton.Sprint);
            if (Vec3.DistanceXZ(from, to) < 0.5f) return f;
            f.LookYawDeg = MathUtil.DeltaAngleDeg(viewYawDeg, YawToward(from, to));
            f.MoveY = 1f;
            return f;
        }

        /// <summary>A route follower's command as player input: steer, throttle minus brake; handbrake once arrived or stopped.</summary>
        public static InputFrame Drive(DriveCommand cmd)
        {
            var f = new InputFrame { MoveX = MathUtil.Clamp(cmd.Steer, -1f, 1f), MoveY = MathUtil.Clamp(cmd.Throttle - cmd.Brake, -1f, 1f) };
            if (cmd.Arrived || cmd.AtStopPoint)
            {
                f.MoveY = 0f;
                f.Held = InputFrame.Bit(GameButton.Jump);
            }
            return f;
        }

        /// <summary>Axes from <paramref name="b"/> where non-zero, else from <paramref name="a"/>; buttons OR-ed.</summary>
        public static InputFrame Merge(InputFrame a, InputFrame b)
        {
            return new InputFrame
            {
                MoveX = b.MoveX != 0f ? b.MoveX : a.MoveX,
                MoveY = b.MoveY != 0f ? b.MoveY : a.MoveY,
                LookYawDeg = b.LookYawDeg != 0f ? b.LookYawDeg : a.LookYawDeg,
                LookPitchDeg = b.LookPitchDeg != 0f ? b.LookPitchDeg : a.LookPitchDeg,
                PointerX = b.PointerX != 0f ? b.PointerX : a.PointerX,
                PointerY = b.PointerY != 0f ? b.PointerY : a.PointerY,
                Held = a.Held | b.Held,
                Pressed = a.Pressed | b.Pressed,
                Released = a.Released | b.Released,
            };
        }
    }
}
