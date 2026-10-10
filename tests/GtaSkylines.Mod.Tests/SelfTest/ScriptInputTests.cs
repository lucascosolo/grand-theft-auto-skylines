using System;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Roads;
using GtaSkylines.Mod.SelfTest;
using Xunit;

namespace GtaSkylines.Mod.Tests
{
    public class ScriptInputTests
    {
        // ---- Press / Hold ----

        [Fact]
        public void Press_sets_Pressed_and_Held_bits()
        {
            InputFrame f = ScriptInput.Press(GameButton.Interact);
            Assert.True(f.WasPressed(GameButton.Interact));
            Assert.True(f.IsHeld(GameButton.Interact));
            Assert.False(f.IsHeld(GameButton.Jump));
            Assert.Equal(0u, f.Released);
        }

        [Fact]
        public void Hold_sets_Held_only()
        {
            InputFrame f = ScriptInput.Hold(GameButton.Sprint);
            Assert.True(f.IsHeld(GameButton.Sprint));
            Assert.False(f.WasPressed(GameButton.Sprint));
            Assert.Equal(0u, f.Pressed);
        }

        [Fact]
        public void Press_and_Hold_leave_axes_at_zero()
        {
            InputFrame f = ScriptInput.Press(GameButton.Jump);
            Assert.Equal(0f, f.MoveX);
            Assert.Equal(0f, f.MoveY);
            Assert.Equal(0f, f.LookYawDeg);
        }

        // ---- YawToward ----

        [Theory]
        [InlineData(0f, 10f, 0f)]
        [InlineData(10f, 0f, 90f)]
        [InlineData(0f, -10f, 180f)]
        [InlineData(-10f, 0f, -90f)]
        public void YawToward_zero_is_plus_z_and_ninety_is_plus_x(float dx, float dz, float expected)
        {
            Assert.Equal(expected, ScriptInput.YawToward(Vec3.Zero, new Vec3(dx, 0, dz)), 3);
        }

        [Fact]
        public void YawToward_ignores_height_and_uses_from_as_origin()
        {
            float yaw = ScriptInput.YawToward(new Vec3(5, 100, 5), new Vec3(15, -50, 5));
            Assert.Equal(90f, yaw, 3);
        }

        // ---- Walk ----

        [Fact]
        public void Walk_moves_forward_and_turns_by_the_signed_shortest_angle()
        {
            // target is due +x (yaw 90); view faces +z (yaw 0) -> turn +90
            InputFrame f = ScriptInput.Walk(0f, Vec3.Zero, new Vec3(10, 0, 0), false);
            Assert.Equal(1f, f.MoveY);
            Assert.Equal(0f, f.MoveX);
            Assert.Equal(90f, f.LookYawDeg, 3);
            Assert.False(f.IsHeld(GameButton.Sprint));
        }

        [Fact]
        public void Walk_turn_wraps_through_the_short_side()
        {
            // view 350, target yaw 10 -> +20, not -340
            InputFrame f = ScriptInput.Walk(350f, Vec3.Zero, new Vec3((float)Math.Sin(10 * Math.PI / 180) * 10, 0, (float)Math.Cos(10 * Math.PI / 180) * 10), false);
            Assert.Equal(20f, f.LookYawDeg, 2);
        }

        [Fact]
        public void Walk_turn_is_negative_to_the_left()
        {
            InputFrame f = ScriptInput.Walk(90f, Vec3.Zero, new Vec3(0, 0, 10), false);
            Assert.Equal(-90f, f.LookYawDeg, 3);
        }

        [Fact]
        public void Walk_already_facing_target_turns_zero()
        {
            InputFrame f = ScriptInput.Walk(0f, Vec3.Zero, new Vec3(0, 0, 10), false);
            Assert.Equal(0f, f.LookYawDeg, 3);
            Assert.Equal(1f, f.MoveY);
        }

        [Fact]
        public void Walk_with_sprint_holds_sprint()
        {
            InputFrame f = ScriptInput.Walk(0f, Vec3.Zero, new Vec3(0, 0, 10), true);
            Assert.True(f.IsHeld(GameButton.Sprint));
        }

        [Fact]
        public void Walk_within_half_a_metre_stops_and_does_not_turn()
        {
            InputFrame f = ScriptInput.Walk(40f, Vec3.Zero, new Vec3(0.3f, 0, 0.3f), false);
            Assert.Equal(0f, f.MoveY);
            Assert.Equal(0f, f.LookYawDeg);
        }

        [Fact]
        public void Walk_arrival_distance_is_horizontal_only()
        {
            InputFrame f = ScriptInput.Walk(0f, Vec3.Zero, new Vec3(0.1f, 50f, 0.1f), false);
            Assert.Equal(0f, f.MoveY);
        }

        [Fact]
        public void Walk_just_beyond_half_a_metre_still_moves()
        {
            InputFrame f = ScriptInput.Walk(0f, Vec3.Zero, new Vec3(0f, 0, 0.6f), false);
            Assert.Equal(1f, f.MoveY);
        }

        // ---- Drive ----

        private static DriveCommand Cmd(float steer, float throttle, float brake, bool arrived, bool atStop)
        {
            return new DriveCommand { Steer = steer, Throttle = throttle, Brake = brake, Arrived = arrived, AtStopPoint = atStop };
        }

        [Fact]
        public void Drive_maps_steer_and_throttle()
        {
            InputFrame f = ScriptInput.Drive(Cmd(0.5f, 0.75f, 0f, false, false));
            Assert.Equal(0.5f, f.MoveX);
            Assert.Equal(0.75f, f.MoveY);
            Assert.False(f.IsHeld(GameButton.Jump));
        }

        [Fact]
        public void Drive_brake_gives_negative_forward_axis()
        {
            InputFrame f = ScriptInput.Drive(Cmd(0f, 0f, 0.6f, false, false));
            Assert.Equal(-0.6f, f.MoveY, 5);
        }

        [Fact]
        public void Drive_clamps_steer_and_forward()
        {
            InputFrame f = ScriptInput.Drive(Cmd(-3f, 5f, 0f, false, false));
            Assert.Equal(-1f, f.MoveX);
            Assert.Equal(1f, f.MoveY);
            InputFrame g = ScriptInput.Drive(Cmd(3f, 0f, 5f, false, false));
            Assert.Equal(1f, g.MoveX);
            Assert.Equal(-1f, g.MoveY);
        }

        [Fact]
        public void Drive_arrived_zeroes_forward_and_holds_handbrake()
        {
            InputFrame f = ScriptInput.Drive(Cmd(0.2f, 0.9f, 0f, true, false));
            Assert.Equal(0f, f.MoveY);
            Assert.True(f.IsHeld(GameButton.Jump));
        }

        [Fact]
        public void Drive_at_stop_point_zeroes_forward_and_holds_handbrake()
        {
            InputFrame f = ScriptInput.Drive(Cmd(0f, 0f, 1f, false, true));
            Assert.Equal(0f, f.MoveY);
            Assert.True(f.IsHeld(GameButton.Jump));
        }

        // ---- Merge ----

        [Fact]
        public void Merge_takes_axes_from_b_when_nonzero_else_a()
        {
            var a = new InputFrame { MoveX = 0.3f, MoveY = 1f, LookYawDeg = 5f, LookPitchDeg = 2f };
            var b = new InputFrame { MoveX = 0f, MoveY = -1f, LookYawDeg = 0f, LookPitchDeg = 7f };
            InputFrame m = ScriptInput.Merge(a, b);
            Assert.Equal(0.3f, m.MoveX);
            Assert.Equal(-1f, m.MoveY);
            Assert.Equal(5f, m.LookYawDeg);
            Assert.Equal(7f, m.LookPitchDeg);
        }

        [Fact]
        public void Merge_ors_all_button_masks()
        {
            InputFrame a = ScriptInput.Press(GameButton.Jump);
            InputFrame b = ScriptInput.Hold(GameButton.Sprint);
            b.Released = InputFrame.Bit(GameButton.Attack);
            InputFrame m = ScriptInput.Merge(a, b);
            Assert.True(m.IsHeld(GameButton.Jump));
            Assert.True(m.IsHeld(GameButton.Sprint));
            Assert.True(m.WasPressed(GameButton.Jump));
            Assert.False(m.WasPressed(GameButton.Sprint));
            Assert.True(m.WasReleased(GameButton.Attack));
        }

        [Fact]
        public void Merge_with_default_b_returns_a()
        {
            var a = new InputFrame { MoveX = -0.5f, MoveY = 0.25f };
            InputFrame m = ScriptInput.Merge(a, default(InputFrame));
            Assert.Equal(-0.5f, m.MoveX);
            Assert.Equal(0.25f, m.MoveY);
        }
    }
}
