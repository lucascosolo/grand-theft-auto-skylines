using GtaSkylines.Mod.Menus;
using Xunit;

namespace GtaSkylines.Mod.Tests.Menus
{
    public class PauseNavTests
    {
        private static PauseNav Opened(PauseTab tab)
        {
            var n = new PauseNav();
            n.Open(tab);
            return n;
        }

        private static PauseNav InSettingsContent()
        {
            var n = Opened(PauseTab.Settings);
            n.ClickTab(PauseTab.Settings);
            return n;
        }

        [Fact]
        public void Open_resets_to_the_tab_bar_with_no_dialog()
        {
            var n = Opened(PauseTab.Game);
            n.Accept();
            n.BeginConfirm();
            n.Open(PauseTab.Stats);
            Assert.Equal(PauseTab.Stats, n.Tab);
            Assert.False(n.InContent);
            Assert.False(n.Confirming);
            Assert.False(n.Rebinding);
        }

        [Fact]
        public void CycleTab_wraps_both_ways()
        {
            var n = Opened(PauseTab.Map);
            Assert.True(n.CycleTab(-1));
            Assert.Equal(PauseTab.Game, n.Tab);
            Assert.True(n.CycleTab(1));
            Assert.Equal(PauseTab.Map, n.Tab);
            Assert.True(n.CycleTab(1));
            Assert.Equal(PauseTab.Brief, n.Tab);
            Assert.Equal(6, PauseNav.TabCount);
        }

        [Fact]
        public void CycleTab_returns_focus_to_the_tab_bar()
        {
            var n = Opened(PauseTab.Stats);
            n.Accept();
            Assert.True(n.InContent);
            Assert.True(n.CycleTab(1));
            Assert.Equal(PauseTab.Settings, n.Tab);
            Assert.False(n.InContent);
        }

        [Fact]
        public void CycleTab_is_ignored_while_confirming()
        {
            var n = Opened(PauseTab.Game);
            Assert.True(n.BeginConfirm());
            Assert.False(n.CycleTab(1));
            Assert.Equal(PauseTab.Game, n.Tab);
        }

        [Fact]
        public void CycleTab_is_ignored_while_rebinding()
        {
            var n = InSettingsContent();
            Assert.True(n.BeginRebind());
            Assert.False(n.CycleTab(-1));
            Assert.Equal(PauseTab.Settings, n.Tab);
        }

        [Fact]
        public void ClickTab_switches_and_focuses_the_content()
        {
            var n = Opened(PauseTab.Map);
            n.ClickTab(PauseTab.Stats);
            Assert.Equal(PauseTab.Stats, n.Tab);
            Assert.True(n.InContent);
        }

        [Fact]
        public void ClickTab_is_ignored_while_a_dialog_is_open()
        {
            var n = Opened(PauseTab.Game);
            n.BeginConfirm();
            n.ClickTab(PauseTab.Map);
            Assert.Equal(PauseTab.Game, n.Tab);

            var r = InSettingsContent();
            r.BeginRebind();
            r.ClickTab(PauseTab.Map);
            Assert.Equal(PauseTab.Settings, r.Tab);
        }

        [Fact]
        public void Accept_on_the_tab_bar_focuses_the_content_once()
        {
            var n = Opened(PauseTab.Brief);
            Assert.True(n.Accept());
            Assert.True(n.InContent);
            Assert.False(n.Accept());
            Assert.True(n.InContent);
        }

        [Fact]
        public void Accept_in_a_dialog_returns_false()
        {
            var n = Opened(PauseTab.Game);
            n.BeginConfirm();
            Assert.False(n.Accept());
        }

        [Fact]
        public void BeginConfirm_works_only_on_the_game_tab()
        {
            foreach (PauseTab tab in new[] { PauseTab.Map, PauseTab.Brief, PauseTab.Stats, PauseTab.Settings, PauseTab.Controls })
            {
                var other = Opened(tab);
                Assert.False(other.BeginConfirm());
                Assert.False(other.Confirming);
            }
            var n = Opened(PauseTab.Game);
            Assert.True(n.BeginConfirm());
            Assert.True(n.Confirming);
            n.EndConfirm();
            Assert.False(n.Confirming);
        }

        [Fact]
        public void BeginRebind_works_only_in_settings_content()
        {
            var bar = Opened(PauseTab.Settings);
            Assert.False(bar.BeginRebind());
            Assert.False(bar.Rebinding);

            var controls = Opened(PauseTab.Controls);
            controls.Accept();
            Assert.False(controls.BeginRebind());

            var n = InSettingsContent();
            Assert.True(n.BeginRebind());
            Assert.True(n.Rebinding);
            n.EndRebind();
            Assert.False(n.Rebinding);
        }

        [Fact]
        public void Back_unwinds_rebind_then_content_then_closes()
        {
            var n = InSettingsContent();
            n.BeginRebind();
            Assert.Equal(BackResult.Handled, n.Back());
            Assert.False(n.Rebinding);
            Assert.True(n.InContent);
            Assert.Equal(BackResult.Handled, n.Back());
            Assert.False(n.InContent);
            Assert.Equal(BackResult.Close, n.Back());
        }

        [Fact]
        public void Back_closes_the_confirmation_before_leaving_content()
        {
            var n = Opened(PauseTab.Game);
            n.Accept();
            n.BeginConfirm();
            Assert.Equal(BackResult.Handled, n.Back());
            Assert.False(n.Confirming);
            Assert.True(n.InContent);
            Assert.Equal(BackResult.Handled, n.Back());
            Assert.False(n.InContent);
            Assert.Equal(BackResult.Close, n.Back());
        }

        [Fact]
        public void Back_on_the_tab_bar_closes()
        {
            Assert.Equal(BackResult.Close, Opened(PauseTab.Map).Back());
        }
    }
}
