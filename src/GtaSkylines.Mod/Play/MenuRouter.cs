using GtaSkylines.Game.Input;

namespace GtaSkylines.Mod.Play
{
    /// <summary>What the menu keys of one frame do.</summary>
    public enum MenuAction
    {
        /// <summary>Nothing (an open menu reads its own navigation keys).</summary>
        None,
        /// <summary>Open the pause menu.</summary>
        OpenPause,
        /// <summary>Close the pause menu.</summary>
        ClosePause,
        /// <summary>Take the phone out.</summary>
        OpenPhone,
        /// <summary>Put the phone away.</summary>
        ClosePhone,
        /// <summary>Open or close the interaction menu.</summary>
        ToggleInteraction,
        /// <summary>Enlarge or shrink the minimap.</summary>
        ToggleBigMap,
    }

    /// <summary>Which menus are open this frame.</summary>
    public struct OpenMenus
    {
        /// <summary>Pause menu.</summary>
        public bool Pause;
        /// <summary>Phone.</summary>
        public bool Phone;
        /// <summary>Interaction menu.</summary>
        public bool Interaction;
        /// <summary>A shop or other game menu (<c>world.Menu.IsOpen</c>).</summary>
        public bool Shop;
        /// <summary>The pause menu waits for a key to bind.</summary>
        public bool Rebinding;
        /// <summary>The player is alive (not Wasted or Busted).</summary>
        public bool PlayerAlive;
    }

    /// <summary>
    /// GTA-style menu keys (F-JOB-4). The phone key (Up arrow, which is also MenuUp) only ever opens the phone, and only
    /// when no menu is open and the player is alive; while the phone or any menu is open, the arrows navigate inside it
    /// and MenuBack goes back inside it (handled by the menu itself); Escape (Pause) puts the phone away. Rules, first
    /// match wins:
    /// <list type="number">
    /// <item>Rebinding → None.</item>
    /// <item>Pause pressed: pause open → ClosePause; phone open → ClosePhone; interaction or shop open → None; else
    /// OpenPause.</item>
    /// <item>Pause open → None.</item>
    /// <item>Phone pressed, nothing open and the player alive → OpenPhone.</item>
    /// <item>InteractionMenu pressed and no shop open → ToggleInteraction.</item>
    /// <item>BigMap pressed → ToggleBigMap.</item>
    /// <item>Otherwise None.</item>
    /// </list>
    /// Pure and allocation-free.
    /// </summary>
    public static class MenuRouter
    {
        /// <summary>The action for this frame's pressed edges in <paramref name="input"/> given <paramref name="open"/>.</summary>
        public static MenuAction Decide(InputFrame input, OpenMenus open)
        {
            if (open.Rebinding) return MenuAction.None;
            if (input.WasPressed(GameButton.Pause))
            {
                if (open.Pause) return MenuAction.ClosePause;
                if (open.Phone) return MenuAction.ClosePhone;
                return open.Interaction || open.Shop ? MenuAction.None : MenuAction.OpenPause;
            }
            if (open.Pause) return MenuAction.None;
            if (input.WasPressed(GameButton.Phone) && open.PlayerAlive && !open.Phone && !open.Interaction && !open.Shop)
                return MenuAction.OpenPhone;
            if (input.WasPressed(GameButton.InteractionMenu) && !open.Shop) return MenuAction.ToggleInteraction;
            return input.WasPressed(GameButton.BigMap) ? MenuAction.ToggleBigMap : MenuAction.None;
        }
    }
}
