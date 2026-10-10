using System;
using System.Collections.Generic;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Settings;
using Xunit;

namespace GtaSkylines.Game.Tests.Settings
{
    public class KeyBindingsTests
    {
        // DESIGN.md "Controls (defaults; all rebindable)" table, one row per action: primary, secondary.
        public static IEnumerable<object[]> DesignTable()
        {
            yield return new object[] { GameButton.Sprint, "LeftShift", "" };
            yield return new object[] { GameButton.Jump, "Space", "" };
            yield return new object[] { GameButton.Sneak, "LeftControl", "" };
            yield return new object[] { GameButton.EnterExit, "F", "" };
            yield return new object[] { GameButton.Interact, "E", "" };
            yield return new object[] { GameButton.Attack, "Mouse0", "" };
            yield return new object[] { GameButton.Aim, "Mouse1", "" };
            yield return new object[] { GameButton.Reload, "R", "" };
            yield return new object[] { GameButton.WeaponWheel, "Tab", "" };
            yield return new object[] { GameButton.NextWeapon, "WheelUp", "" };
            yield return new object[] { GameButton.PrevWeapon, "WheelDown", "" };
            yield return new object[] { GameButton.HandsUp, "X", "" };
            yield return new object[] { GameButton.CameraView, "V", "" };
            yield return new object[] { GameButton.LookBehind, "C", "" };
            yield return new object[] { GameButton.Headlights, "H", "" };
            yield return new object[] { GameButton.Phone, "UpArrow", "" };
            yield return new object[] { GameButton.InteractionMenu, "M", "" };
            yield return new object[] { GameButton.Pause, "Escape", "P" };
            yield return new object[] { GameButton.BigMap, "Z", "" };
            yield return new object[] { GameButton.MenuAccept, "Return", "" };
            yield return new object[] { GameButton.MenuBack, "Backspace", "" };
            yield return new object[] { GameButton.MenuUp, "UpArrow", "W" };
            yield return new object[] { GameButton.MenuDown, "DownArrow", "S" };
            yield return new object[] { GameButton.MenuLeft, "LeftArrow", "A" };
            yield return new object[] { GameButton.MenuRight, "RightArrow", "D" };
        }

        [Theory]
        [MemberData(nameof(DesignTable))]
        public void Default_binding_matches_design_table(GameButton button, string primary, string secondary)
        {
            var k = new KeyBindings();
            Assert.Equal(primary, k.Get(button, KeyBindings.Primary));
            Assert.Equal(secondary, k.Get(button, KeyBindings.Secondary));
        }

        [Fact]
        public void Design_table_covers_every_game_button()
        {
            Assert.Equal(Enum.GetValues(typeof(GameButton)).Length, new List<object[]>(DesignTable()).Count);
        }

        [Theory]
        [InlineData(MoveKey.Forward, "W")]
        [InlineData(MoveKey.Back, "S")]
        [InlineData(MoveKey.Left, "A")]
        [InlineData(MoveKey.Right, "D")]
        public void Default_movement_keys_are_WASD(MoveKey key, string expected)
        {
            var k = new KeyBindings();
            Assert.Equal(expected, k.GetMove(key, KeyBindings.Primary));
            Assert.Equal("", k.GetMove(key, KeyBindings.Secondary));
        }

        [Fact]
        public void New_bindings_are_default()
        {
            Assert.True(new KeyBindings().IsDefault);
        }

        [Fact]
        public void Out_of_range_slot_reads_empty_and_cannot_be_set()
        {
            var k = new KeyBindings();
            Assert.Equal("", k.Get(GameButton.Jump, 2));
            Assert.Equal("", k.Get(GameButton.Jump, -1));
            Assert.Equal("", k.GetMove(MoveKey.Forward, 5));
            Assert.False(k.Set(GameButton.Jump, 2, "Q"));
            Assert.False(k.SetMove(MoveKey.Forward, -1, "Q"));
            Assert.True(k.IsDefault);
        }

        [Fact]
        public void Set_binds_a_slot_and_breaks_IsDefault()
        {
            var k = new KeyBindings();
            Assert.True(k.Set(GameButton.Jump, KeyBindings.Secondary, "Alpha1"));
            Assert.Equal("Alpha1", k.Get(GameButton.Jump, KeyBindings.Secondary));
            Assert.Equal("Space", k.Get(GameButton.Jump, KeyBindings.Primary));
            Assert.False(k.IsDefault);
        }

        [Fact]
        public void Empty_name_unbinds_a_slot()
        {
            var k = new KeyBindings();
            Assert.True(k.Set(GameButton.Pause, KeyBindings.Primary, ""));
            Assert.Equal("", k.Get(GameButton.Pause, KeyBindings.Primary));
            Assert.Equal("P", k.Get(GameButton.Pause, KeyBindings.Secondary));
            Assert.True(k.SetMove(MoveKey.Left, KeyBindings.Primary, ""));
            Assert.Equal("", k.GetMove(MoveKey.Left, KeyBindings.Primary));
        }

        [Theory]
        [InlineData("w")]
        [InlineData("Shift")]
        [InlineData("Mouse3")]
        [InlineData("F13")]
        [InlineData("Alpha10")]
        [InlineData(" W")]
        public void Invalid_key_name_is_rejected_and_changes_nothing(string name)
        {
            var k = new KeyBindings();
            Assert.False(k.Set(GameButton.Jump, 0, name));
            Assert.False(k.SetMove(MoveKey.Forward, 0, name));
            Assert.True(k.IsDefault);
        }

        [Fact]
        public void Null_key_name_is_rejected_without_throwing()
        {
            var k = new KeyBindings();
            Assert.False(k.Set(GameButton.Jump, 0, null));
            Assert.True(k.IsDefault);
        }

        [Fact]
        public void Same_key_in_both_slots_of_a_button_is_allowed()
        {
            var k = new KeyBindings();
            Assert.True(k.Set(GameButton.Jump, 1, "Space"));
            Assert.Equal("Space", k.Get(GameButton.Jump, 1));
        }

        [Fact]
        public void Reset_restores_one_button_and_ResetToDefaults_restores_all()
        {
            var k = new KeyBindings();
            k.Set(GameButton.Jump, 0, "Q");
            k.Set(GameButton.Jump, 1, "T");
            k.Set(GameButton.Sprint, 0, "G");
            k.SetMove(MoveKey.Forward, 0, "UpArrow");
            k.Reset(GameButton.Jump);
            Assert.Equal("Space", k.Get(GameButton.Jump, 0));
            Assert.Equal("", k.Get(GameButton.Jump, 1));
            Assert.Equal("G", k.Get(GameButton.Sprint, 0));
            k.ResetToDefaults();
            Assert.True(k.IsDefault);
            Assert.Equal("W", k.GetMove(MoveKey.Forward, 0));
        }

        [Fact]
        public void IsDefault_is_false_after_a_movement_rebind()
        {
            var k = new KeyBindings();
            k.SetMove(MoveKey.Back, 1, "DownArrow");
            Assert.False(k.IsDefault);
        }

        [Fact]
        public void FindConflicts_lists_buttons_sharing_a_key_in_button_order()
        {
            var k = new KeyBindings();
            Assert.Equal(new List<GameButton> { GameButton.Phone, GameButton.MenuUp }, k.FindConflicts("UpArrow"));
        }

        [Fact]
        public void FindConflicts_checks_both_slots_and_ignores_movement_keys()
        {
            var k = new KeyBindings();
            Assert.Equal(new List<GameButton> { GameButton.Pause }, k.FindConflicts("P"));
            Assert.Equal(new List<GameButton> { GameButton.MenuUp }, k.FindConflicts("W")); // MoveKey.Forward not listed
            Assert.Equal(new List<GameButton> { GameButton.Attack }, k.FindConflicts("Mouse0"));
        }

        [Fact]
        public void FindConflicts_for_free_unknown_or_empty_key_is_empty()
        {
            var k = new KeyBindings();
            Assert.Empty(k.FindConflicts("Q"));
            Assert.Empty(k.FindConflicts("nonsense"));
            Assert.Empty(k.FindConflicts(""));
        }

        [Fact]
        public void FindConflicts_sees_a_new_binding_and_a_cleared_one()
        {
            var k = new KeyBindings();
            k.Set(GameButton.Reload, 1, "Q");
            k.Set(GameButton.Aim, 0, "Q");
            Assert.Equal(new List<GameButton> { GameButton.Aim, GameButton.Reload }, k.FindConflicts("Q"));
            k.Set(GameButton.Aim, 0, "");
            Assert.Equal(new List<GameButton> { GameButton.Reload }, k.FindConflicts("Q"));
            Assert.Empty(k.FindConflicts("Mouse1"));
        }

        [Fact]
        public void FindConflicts_reports_a_button_once_even_if_both_slots_match()
        {
            var k = new KeyBindings();
            k.Set(GameButton.Jump, 1, "Space");
            Assert.Equal(new List<GameButton> { GameButton.Jump }, k.FindConflicts("Space"));
        }

        [Theory]
        [InlineData("A", true)]
        [InlineData("Z", true)]
        [InlineData("Alpha0", true)]
        [InlineData("Alpha9", true)]
        [InlineData("Keypad0", true)]
        [InlineData("Keypad9", true)]
        [InlineData("F1", true)]
        [InlineData("F12", true)]
        [InlineData("Space", true)]
        [InlineData("Tab", true)]
        [InlineData("Return", true)]
        [InlineData("Escape", true)]
        [InlineData("Backspace", true)]
        [InlineData("LeftShift", true)]
        [InlineData("RightShift", true)]
        [InlineData("LeftControl", true)]
        [InlineData("RightControl", true)]
        [InlineData("LeftAlt", true)]
        [InlineData("RightAlt", true)]
        [InlineData("UpArrow", true)]
        [InlineData("DownArrow", true)]
        [InlineData("LeftArrow", true)]
        [InlineData("RightArrow", true)]
        [InlineData("Mouse0", true)]
        [InlineData("Mouse2", true)]
        [InlineData("WheelUp", true)]
        [InlineData("WheelDown", true)]
        [InlineData("", false)]
        [InlineData(null, false)]
        [InlineData("a", false)]
        [InlineData("space", false)]
        [InlineData("F0", false)]
        [InlineData("F13", false)]
        [InlineData("Mouse3", false)]
        [InlineData("Keypad10", false)]
        [InlineData("Enter", false)]
        public void IsValidKeyName_follows_the_documented_set(string name, bool valid)
        {
            Assert.Equal(valid, KeyBindings.IsValidKeyName(name));
        }
    }
}
