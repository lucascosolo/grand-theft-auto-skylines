using System;
using System.Collections.Generic;

namespace GtaSkylines.Mod.Menus
{
    /// <summary>
    /// Selection state of a GTA-style vertical (or horizontal) list: wraps around at both ends, skips disabled rows, and
    /// keeps a scroll window of <see cref="VisibleRows"/> rows around the selection. Every mutator returns true when the
    /// selection changed (the caller plays the move sound). Unity-free.
    /// </summary>
    public sealed class MenuList
    {
        private readonly List<bool> _disabled = new List<bool>();

        private bool Relocate()
        {
            int old = Selected;
            if (!IsEnabled(old))
            {
                Selected = -1;
                int start = Math.Max(0, Math.Min(old, Count - 1));
                for (int k = 0; k < Count; k++)
                {
                    int row = (start + k) % Count;
                    if (IsEnabled(row)) { Selected = row; break; }
                }
            }
            EnsureVisible();
            return Selected != old;
        }

        private bool Step(int dir)
        {
            for (int k = 1; k < Count; k++)
            {
                int row = ((Selected + dir * k) % Count + Count) % Count;
                if (IsEnabled(row)) return MoveTo(row);
            }
            return false;
        }

        private bool MoveTo(int row)
        {
            if (row == Selected) return false;
            Selected = row;
            EnsureVisible();
            return true;
        }

        private void EnsureVisible()
        {
            if (VisibleRows <= 0) { First = 0; return; }
            if (Selected >= 0)
            {
                if (Selected < First) First = Selected;
                else if (Selected >= First + VisibleRows) First = Selected - VisibleRows + 1;
            }
            First = Math.Max(0, Math.Min(Math.Max(0, Count - VisibleRows), First));
        }

        /// <summary>Creates a list with <paramref name="visibleRows"/> rows on screen (≤ 0 means unlimited).</summary>
        public MenuList(int visibleRows)
        {
            VisibleRows = visibleRows;
            Selected = -1;
        }

        /// <summary>Rows visible at once (≤ 0 unlimited).</summary>
        public int VisibleRows { get; private set; }

        /// <summary>Number of rows.</summary>
        public int Count { get; private set; }

        /// <summary>Selected row, −1 when the list is empty or every row is disabled.</summary>
        public int Selected { get; private set; }

        /// <summary>First row of the scroll window (0 when unlimited or Count ≤ VisibleRows).</summary>
        public int First { get; private set; }

        /// <summary>
        /// Sets the row count. New rows are enabled; rows beyond the count are forgotten. The selection is kept when still
        /// valid and enabled, otherwise it moves to the first enabled row at or after min(old selection, Count − 1),
        /// wrapping, or −1. Returns true when the selection changed.
        /// </summary>
        public bool SetCount(int count)
        {
            while (_disabled.Count < count) _disabled.Add(false);
            if (_disabled.Count > count) _disabled.RemoveRange(count, _disabled.Count - count);
            Count = count < 0 ? 0 : count;
            return Relocate();
        }

        /// <summary>
        /// Enables or disables a row (ignored outside [0, Count)). Disabling the selected row moves the selection as
        /// <see cref="SetCount"/> does. Returns true when the selection changed.
        /// </summary>
        public bool SetEnabled(int row, bool enabled)
        {
            if (row < 0 || row >= Count) return false;
            _disabled[row] = !enabled;
            return Relocate();
        }

        /// <summary>True when <paramref name="row"/> is in range and enabled.</summary>
        public bool IsEnabled(int row)
        {
            return row >= 0 && row < Count && !_disabled[row];
        }

        /// <summary>Selects the previous enabled row, wrapping from the first to the last.</summary>
        public bool Up()
        {
            return Step(-1);
        }

        /// <summary>Selects the next enabled row, wrapping from the last to the first.</summary>
        public bool Down()
        {
            return Step(1);
        }

        /// <summary>Selects <paramref name="row"/> when it is enabled (mouse hover/click); otherwise nothing changes.</summary>
        public bool Select(int row)
        {
            if (!IsEnabled(row) || row == Selected) return false;
            Selected = row;
            EnsureVisible();
            return true;
        }

        /// <summary>
        /// Scrolls the window by <paramref name="rows"/> without moving the selection beyond keeping it visible: First is
        /// clamped to [0, max(0, Count − VisibleRows)]; if the selection leaves the window it moves to the nearest enabled
        /// row inside it. No-op when unlimited.
        /// </summary>
        public bool Scroll(int rows)
        {
            if (VisibleRows <= 0) return false;
            First = Math.Max(0, Math.Min(Math.Max(0, Count - VisibleRows), First + rows));
            if (Selected < 0) return false;
            int last = Math.Min(Count - 1, First + VisibleRows - 1);
            if (Selected >= First && Selected <= last) return false;
            int target = Selected < First ? First : last;
            for (int d = 0; d <= last - First; d++)
            {
                if (IsEnabled(target + d) && target + d <= last) return MoveTo(target + d);
                if (IsEnabled(target - d) && target - d >= First) return MoveTo(target - d);
            }
            return false;
        }

        /// <summary>Clears every row (Count 0, Selected −1, First 0).</summary>
        public void Reset()
        {
            _disabled.Clear();
            Count = 0;
            Selected = -1;
            First = 0;
        }
    }
}
