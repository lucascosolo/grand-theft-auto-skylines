using GtaSkylines.Mod.Menus;
using Xunit;

namespace GtaSkylines.Mod.Tests.Menus
{
    public class MenuListTests
    {
        private static MenuList Make(int rows, int visible = 0)
        {
            var l = new MenuList(visible);
            l.SetCount(rows);
            return l;
        }

        [Fact]
        public void New_list_has_no_selection()
        {
            var l = new MenuList(5);
            Assert.Equal(5, l.VisibleRows);
            Assert.Equal(0, l.Count);
            Assert.Equal(-1, l.Selected);
            Assert.Equal(0, l.First);
        }

        [Fact]
        public void SetCount_selects_the_first_row_and_reports_the_change()
        {
            var l = new MenuList(0);
            Assert.True(l.SetCount(5));
            Assert.Equal(5, l.Count);
            Assert.Equal(0, l.Selected);
            Assert.True(l.IsEnabled(4));
            Assert.False(l.IsEnabled(5));
            Assert.False(l.IsEnabled(-1));
        }

        [Fact]
        public void SetCount_keeps_a_valid_selection()
        {
            var l = Make(5);
            l.Select(1);
            Assert.False(l.SetCount(8));
            Assert.Equal(1, l.Selected);
        }

        [Fact]
        public void SetCount_moves_an_out_of_range_selection_to_the_last_row()
        {
            var l = Make(5);
            l.Select(4);
            Assert.True(l.SetCount(3));
            Assert.Equal(2, l.Selected);
        }

        [Fact]
        public void SetCount_wraps_to_find_an_enabled_row()
        {
            var l = Make(5);
            l.SetEnabled(3, false);
            l.Select(4);
            l.SetCount(4); // 4 is gone, 3 is disabled, wrap to 0
            Assert.Equal(0, l.Selected);
        }

        [Fact]
        public void SetCount_zero_clears_the_selection()
        {
            var l = Make(3);
            Assert.True(l.SetCount(0));
            Assert.Equal(-1, l.Selected);
            Assert.Equal(0, l.Count);
        }

        [Fact]
        public void Disabling_the_selected_row_moves_the_selection_on()
        {
            var l = Make(3);
            Assert.True(l.SetEnabled(0, false));
            Assert.Equal(1, l.Selected);
            Assert.False(l.IsEnabled(0));
            Assert.False(l.SetEnabled(2, false));
            Assert.Equal(1, l.Selected);
        }

        [Fact]
        public void SetEnabled_outside_the_range_is_ignored()
        {
            var l = Make(3);
            Assert.False(l.SetEnabled(7, false));
            Assert.False(l.SetEnabled(-1, false));
            Assert.Equal(0, l.Selected);
        }

        [Fact]
        public void All_rows_disabled_means_no_selection()
        {
            var l = Make(3);
            l.SetEnabled(0, false);
            l.SetEnabled(1, false);
            l.SetEnabled(2, false);
            Assert.Equal(-1, l.Selected);
            Assert.False(l.Down());
            Assert.False(l.Up());
            Assert.Equal(-1, l.Selected);
        }

        [Fact]
        public void Up_and_down_wrap_around()
        {
            var l = Make(3);
            Assert.True(l.Up());
            Assert.Equal(2, l.Selected);
            Assert.True(l.Down());
            Assert.Equal(0, l.Selected);
            Assert.True(l.Down());
            Assert.Equal(1, l.Selected);
        }

        [Fact]
        public void Movement_skips_disabled_rows()
        {
            var l = Make(4);
            l.SetEnabled(1, false);
            l.SetEnabled(3, false);
            Assert.True(l.Down());
            Assert.Equal(2, l.Selected);
            Assert.True(l.Down());
            Assert.Equal(0, l.Selected);
            Assert.True(l.Up());
            Assert.Equal(2, l.Selected);
        }

        [Fact]
        public void Moving_in_a_single_row_list_changes_nothing()
        {
            var l = Make(1);
            Assert.False(l.Down());
            Assert.False(l.Up());
            Assert.Equal(0, l.Selected);
            Assert.False(Make(0).Down());
        }

        [Fact]
        public void Select_only_accepts_enabled_rows()
        {
            var l = Make(4);
            l.SetEnabled(2, false);
            Assert.True(l.Select(3));
            Assert.Equal(3, l.Selected);
            Assert.False(l.Select(2));
            Assert.Equal(3, l.Selected);
            Assert.False(l.Select(9));
            Assert.Equal(3, l.Selected);
            Assert.False(l.Select(3));
        }

        [Fact]
        public void Window_follows_the_selection()
        {
            var l = Make(10, 3);
            Assert.Equal(0, l.First);
            l.Down();
            l.Down();
            l.Down();
            Assert.Equal(3, l.Selected);
            Assert.InRange(l.Selected, l.First, l.First + 2);

            l.Up();
            l.Up();
            l.Up();
            l.Up(); // wraps to the last row
            Assert.Equal(9, l.Selected);
            Assert.Equal(7, l.First);

            l.Down(); // wraps to 0
            Assert.Equal(0, l.Selected);
            Assert.Equal(0, l.First);
        }

        [Fact]
        public void Window_is_zero_when_everything_fits_or_unlimited()
        {
            var fits = Make(3, 5);
            fits.Down();
            fits.Down();
            Assert.Equal(0, fits.First);

            var unlimited = Make(100, 0);
            unlimited.Select(50);
            Assert.Equal(0, unlimited.First);
        }

        [Fact]
        public void Scroll_moves_the_window_and_pulls_the_selection_inside()
        {
            var l = Make(10, 3);
            Assert.True(l.Scroll(2));
            Assert.Equal(2, l.First);
            Assert.Equal(2, l.Selected);
        }

        [Fact]
        public void Scroll_is_clamped_to_the_list()
        {
            var l = Make(10, 3);
            l.Scroll(100);
            Assert.Equal(7, l.First);
            Assert.Equal(7, l.Selected);
            l.Scroll(-100);
            Assert.Equal(0, l.First);
        }

        [Fact]
        public void Scroll_keeps_a_visible_selection()
        {
            var l = Make(10, 3);
            l.Select(2);
            Assert.False(l.Scroll(1));
            Assert.Equal(1, l.First);
            Assert.Equal(2, l.Selected);
        }

        [Fact]
        public void Scroll_picks_the_nearest_enabled_row_in_the_window()
        {
            var l = Make(10, 3);
            l.SetEnabled(4, false);
            Assert.True(l.Scroll(4)); // window 4..6, row 4 disabled
            Assert.Equal(4, l.First);
            Assert.Equal(5, l.Selected);
        }

        [Fact]
        public void Scroll_without_a_limit_does_nothing()
        {
            var l = Make(10, 0);
            Assert.False(l.Scroll(3));
            Assert.Equal(0, l.First);
            Assert.Equal(0, l.Selected);
        }

        [Fact]
        public void Reset_clears_everything()
        {
            var l = Make(10, 3);
            l.Scroll(5);
            l.Reset();
            Assert.Equal(0, l.Count);
            Assert.Equal(-1, l.Selected);
            Assert.Equal(0, l.First);
        }
    }
}
