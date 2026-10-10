using System;
using System.Collections.Generic;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Settings;
using GtaSkylines.Mod.Input;
using Xunit;

namespace GtaSkylines.Mod.Tests
{
    public class ButtonMapTests
    {
        private static uint Bit(GameButton b) { return 1u << (int)b; }

        private static bool[] Down(ButtonMap map, params string[] names)
        {
            var down = new bool[map.SourceCount];
            for (int i = 0; i < map.SourceCount; i++)
                if (Array.IndexOf(names, map.SourceName(i)) >= 0) down[i] = true;
            return down;
        }

        private static List<string> Names(ButtonMap map)
        {
            var l = new List<string>();
            for (int i = 0; i < map.SourceCount; i++) l.Add(map.SourceName(i));
            return l;
        }

        [Fact]
        public void Sources_are_distinct_and_cover_every_bound_key()
        {
            var map = new ButtonMap(new KeyBindings());
            var seen = new HashSet<string>();
            for (int i = 0; i < map.SourceCount; i++) Assert.True(seen.Add(map.SourceName(i)));
            Assert.Contains("Escape", seen);
            Assert.Contains("P", seen);
            Assert.Contains("W", seen);
            Assert.Contains("D", seen);
            Assert.DoesNotContain("", seen);
        }

        [Fact]
        public void Sources_are_in_first_appearance_order()
        {
            var map = new ButtonMap(new KeyBindings());
            Assert.Equal("LeftShift", map.SourceName(0));
            Assert.Equal("Space", map.SourceName(1));
            Assert.Equal("LeftControl", map.SourceName(2));
        }

        [Fact]
        public void Escape_and_P_both_hold_Pause()
        {
            var map = new ButtonMap(new KeyBindings());
            Assert.Equal(Bit(GameButton.Pause), map.Held(Down(map, "Escape")));
            Assert.Equal(Bit(GameButton.Pause), map.Held(Down(map, "P")));
        }

        [Fact]
        public void Nothing_down_holds_nothing()
        {
            var map = new ButtonMap(new KeyBindings());
            Assert.Equal(0u, map.Held(new bool[map.SourceCount]));
        }

        [Fact]
        public void W_holds_MenuUp_and_drives_the_forward_axis()
        {
            var map = new ButtonMap(new KeyBindings());
            var down = Down(map, "W");
            Assert.Equal(Bit(GameButton.MenuUp), map.Held(down));
            Assert.Equal(1f, map.Axis(down, MoveKey.Back, MoveKey.Forward));
            Assert.Equal(-1f, map.Axis(down, MoveKey.Forward, MoveKey.Back));
        }

        [Fact]
        public void Axis_is_zero_for_both_or_neither()
        {
            var map = new ButtonMap(new KeyBindings());
            Assert.Equal(0f, map.Axis(Down(map, "W", "S"), MoveKey.Back, MoveKey.Forward));
            Assert.Equal(0f, map.Axis(Down(map), MoveKey.Back, MoveKey.Forward));
        }

        [Fact]
        public void Axis_uses_secondary_slot_too()
        {
            var kb = new KeyBindings();
            kb.SetMove(MoveKey.Left, 1, "Q");
            var map = new ButtonMap(kb);
            Assert.Equal(-1f, map.Axis(Down(map, "Q"), MoveKey.Left, MoveKey.Right));
        }

        [Fact]
        public void One_key_bound_to_two_buttons_holds_both()
        {
            var map = new ButtonMap(new KeyBindings());
            Assert.Equal(Bit(GameButton.Phone) | Bit(GameButton.MenuUp), map.Held(Down(map, "UpArrow")));
        }

        [Fact]
        public void Rebinding_to_a_mouse_button_creates_a_source_that_maps()
        {
            var kb = new KeyBindings();
            Assert.True(kb.Set(GameButton.Sprint, 0, "Mouse2"));
            var map = new ButtonMap(kb);
            Assert.Contains("Mouse2", Names(map));
            Assert.Equal(Bit(GameButton.Sprint), map.Held(Down(map, "Mouse2")));
            Assert.DoesNotContain("LeftShift", Names(map));
        }

        [Fact]
        public void Map_is_a_snapshot_of_the_bindings()
        {
            var kb = new KeyBindings();
            var map = new ButtonMap(kb);
            kb.Set(GameButton.Pause, 0, "F5");
            Assert.DoesNotContain("F5", Names(map));
            Assert.Equal(Bit(GameButton.Pause), map.Held(Down(map, "Escape")));
        }

        [Fact]
        public void Edges_split_pressed_and_released()
        {
            ButtonMap.Edges(6u, 3u, out uint pressed, out uint released);
            Assert.Equal(1u, pressed);
            Assert.Equal(4u, released);
        }

        [Fact]
        public void Look_scales_by_sensitivity_and_degrees_per_unit()
        {
            Assert.Equal(1.5f, ButtonMap.DegreesPerUnit);
            ButtonMap.Look(2f, 4f, 2f, false, out float yaw, out float pitch);
            Assert.Equal(6f, yaw, 4);
            Assert.Equal(12f, pitch, 4);
        }

        [Fact]
        public void Look_invert_negates_pitch_only()
        {
            ButtonMap.Look(2f, 4f, 2f, true, out float yaw, out float pitch);
            Assert.Equal(6f, yaw, 4);
            Assert.Equal(-12f, pitch, 4);
        }
    }
}
