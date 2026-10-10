using System;

namespace GtaSkylines.Mod.Menus
{
    /// <summary>Pause menu tabs, in tab-bar order.</summary>
    public enum PauseTab
    {
        /// <summary>Full-screen map.</summary>
        Map = 0,
        /// <summary>Objective, mission log, side jobs.</summary>
        Brief = 1,
        /// <summary>Profile counters.</summary>
        Stats = 2,
        /// <summary>Every setting, live.</summary>
        Settings = 3,
        /// <summary>The bindings table.</summary>
        Controls = 4,
        /// <summary>Resume, save, return to the city builder.</summary>
        Game = 5,
    }

    /// <summary>What a Back press did.</summary>
    public enum BackResult
    {
        /// <summary>Handled inside the menu.</summary>
        Handled,
        /// <summary>The menu should close (resume).</summary>
        Close,
    }

    /// <summary>
    /// Focus state machine of the pause menu. Focus is the tab bar or the current tab's content; on top of that a
    /// confirmation dialog (return to city builder) or a pending key rebind can be open. Unity-free.
    /// </summary>
    public sealed class PauseNav
    {
        /// <summary>Number of tabs.</summary>
        public const int TabCount = 6;

        /// <summary>Current tab.</summary>
        public PauseTab Tab { get; private set; }

        /// <summary>True when keyboard focus is inside the tab's content (rows, map cursor), false on the tab bar.</summary>
        public bool InContent { get; private set; }

        /// <summary>True while the "return to city builder?" confirmation is open.</summary>
        public bool Confirming { get; private set; }

        /// <summary>True while a key rebind waits for a key.</summary>
        public bool Rebinding { get; private set; }

        /// <summary>Back to the initial state: tab <paramref name="tab"/>, tab bar focused, no dialog.</summary>
        public void Open(PauseTab tab)
        {
            Tab = tab;
            InContent = false;
            Confirming = false;
            Rebinding = false;
        }

        /// <summary>
        /// Next (+1) or previous (−1) tab, wrapping; leaves content focus (tab bar focused). Ignored (returns false) while
        /// Confirming or Rebinding. Returns true when the tab changed.
        /// </summary>
        public bool CycleTab(int direction)
        {
            if (Confirming || Rebinding) return false;
            PauseTab old = Tab;
            Tab = (PauseTab)((((int)Tab + direction) % TabCount + TabCount) % TabCount);
            InContent = false;
            return Tab != old;
        }

        /// <summary>Selects a tab by click: switches to it and focuses its content. Ignored while Confirming or Rebinding.</summary>
        public bool ClickTab(PauseTab tab)
        {
            if (Confirming || Rebinding) return false;
            Tab = tab;
            InContent = true;
            return true;
        }

        /// <summary>Accept on the tab bar focuses the content (true); in content or a dialog it returns false (the content handles it).</summary>
        public bool Accept()
        {
            if (Confirming || Rebinding || InContent) return false;
            InContent = true;
            return true;
        }

        /// <summary>Opens the confirmation dialog (only from the Game tab; returns false otherwise or while rebinding).</summary>
        public bool BeginConfirm()
        {
            if (Tab != PauseTab.Game || Rebinding) return false;
            Confirming = true;
            return true;
        }

        /// <summary>Closes the confirmation dialog.</summary>
        public void EndConfirm()
        {
            Confirming = false;
        }

        /// <summary>Starts waiting for a key (only in the Settings tab's content; returns false otherwise or while confirming).</summary>
        public bool BeginRebind()
        {
            if (Tab != PauseTab.Settings || !InContent || Confirming) return false;
            Rebinding = true;
            return true;
        }

        /// <summary>Stops waiting for a key.</summary>
        public void EndRebind()
        {
            Rebinding = false;
        }

        /// <summary>
        /// Back (Esc / Backspace): cancels a rebind, else closes the confirmation, else leaves content for the tab bar
        /// (all Handled); on the tab bar returns Close.
        /// </summary>
        public BackResult Back()
        {
            if (Rebinding) Rebinding = false;
            else if (Confirming) Confirming = false;
            else if (InContent) InContent = false;
            else return BackResult.Close;
            return BackResult.Handled;
        }
    }
}
