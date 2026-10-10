using System;
using System.Collections.Generic;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Settings;
using GtaSkylines.Mod.Options;
using Xunit;

namespace GtaSkylines.Mod.Tests
{
    public class BindingTextTests
    {
        [Fact]
        public void ActionName_uses_enum_names()
        {
            Assert.Equal("Sprint", BindingText.ActionName(false, (int)GameButton.Sprint));
            Assert.Equal("MenuRight", BindingText.ActionName(false, (int)GameButton.MenuRight));
            Assert.Equal("Back", BindingText.ActionName(true, (int)MoveKey.Back));
        }

        [Fact]
        public void Defaults_have_no_conflicts_in_any_slot()
        {
            var kb = new KeyBindings();
            foreach (GameButton b in Enum.GetValues(typeof(GameButton)))
                for (int s = 0; s < 2; s++)
                    Assert.Empty(BindingText.Conflicts(kb, false, (int)b, s));
            foreach (MoveKey m in Enum.GetValues(typeof(MoveKey)))
                for (int s = 0; s < 2; s++)
                    Assert.Empty(BindingText.Conflicts(kb, true, (int)m, s));
        }

        [Fact]
        public void Unbound_slot_has_no_conflicts_and_describes_as_none()
        {
            var kb = new KeyBindings();
            Assert.Empty(BindingText.Conflicts(kb, false, (int)GameButton.Sprint, 1));
            Assert.Equal("(none)", BindingText.Describe(kb, false, (int)GameButton.Sprint, 1));
        }

        [Fact]
        public void Describe_without_conflict_is_the_key_name()
        {
            Assert.Equal("LeftShift", BindingText.Describe(new KeyBindings(), false, (int)GameButton.Sprint, 0));
        }

        [Fact]
        public void Describe_with_conflict_lists_other_action()
        {
            var kb = new KeyBindings();
            kb.Set(GameButton.Sprint, 0, "Space");
            Assert.Equal("Space (also Jump)", BindingText.Describe(kb, false, (int)GameButton.Sprint, 0));
            Assert.Equal("Space (also Sprint)", BindingText.Describe(kb, false, (int)GameButton.Jump, 0));
        }

        [Fact]
        public void Conflicts_are_buttons_then_moves_each_once()
        {
            var kb = new KeyBindings();
            kb.Set(GameButton.Sprint, 0, "E");
            kb.Set(GameButton.Interact, 1, "E");
            kb.SetMove(MoveKey.Forward, 1, "E");
            kb.SetMove(MoveKey.Left, 0, "E");
            List<string> c = BindingText.Conflicts(kb, false, (int)GameButton.Sprint, 0);
            Assert.Equal(new[] { "Interact", "Forward", "Left" }, c.ToArray());
        }

        [Fact]
        public void Conflicts_ignore_the_actions_own_other_slot()
        {
            var kb = new KeyBindings();
            kb.Set(GameButton.Sprint, 1, "LeftShift");
            Assert.Empty(BindingText.Conflicts(kb, false, (int)GameButton.Sprint, 0));
        }

        [Fact]
        public void Move_key_conflicts_with_gameplay_button()
        {
            var kb = new KeyBindings();
            kb.Set(GameButton.Jump, 0, "W");
            Assert.Equal(new[] { "Jump" }, BindingText.Conflicts(kb, true, (int)MoveKey.Forward, 0).ToArray());
        }

        [Fact]
        public void Menu_and_gameplay_contexts_do_not_conflict()
        {
            var kb = new KeyBindings();
            kb.Set(GameButton.Pause, 0, "Return");
            Assert.Empty(BindingText.Conflicts(kb, false, (int)GameButton.Pause, 0));
            Assert.Empty(BindingText.Conflicts(kb, false, (int)GameButton.MenuAccept, 0));
        }

        [Fact]
        public void Menu_buttons_conflict_with_each_other_but_not_with_moves()
        {
            var kb = new KeyBindings();
            kb.Set(GameButton.MenuAccept, 0, "W");
            Assert.Equal(new[] { "MenuUp" }, BindingText.Conflicts(kb, false, (int)GameButton.MenuAccept, 0).ToArray());
        }
    }
}
