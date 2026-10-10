using GtaSkylines.Mod.Hud;

namespace GtaSkylines.Mod.Menus
{
    /// <summary>Quick GPS targets of the interaction menu.</summary>
    public enum QuickGpsTarget
    {
        /// <summary>Clear the waypoint.</summary>
        None,
        /// <summary>Nearest owned safehouse.</summary>
        Safehouse,
        /// <summary>Nearest Ammu-Nation.</summary>
        Ammunation,
        /// <summary>Nearest clothes shop.</summary>
        Clothes,
        /// <summary>Nearest food shop.</summary>
        Food,
        /// <summary>Nearest hospital.</summary>
        Hospital,
        /// <summary>Nearest police station.</summary>
        PoliceStation,
    }

    /// <summary>
    /// Everything the menus ask the game to do. The wiring chunk implements it over <c>GameSession</c>, the profile and
    /// CS1; the menus never change game state themselves. Calls happen from OnGUI on the main thread.
    /// </summary>
    public interface IMenuCommands
    {
        /// <summary>Close the pause menu and resume play.</summary>
        void Resume();

        /// <summary>Choose option <paramref name="option"/> of the open <c>MenuRequest</c> (shop item index, bed Save/Sleep/Cancel, wardrobe outfit, property Buy/Cancel); the result comes back in <c>MenuRequest.LastResult</c>.</summary>
        void ChooseMenuOption(int option);

        /// <summary>Close the open <c>MenuRequest</c> without choosing.</summary>
        void CloseMenu();

        /// <summary>Price shown for a shop item (Shop.PriceOf with the live profile; e.g. the police pay-off depends on heat).</summary>
        long PriceOf(GtaSkylines.Game.Profile.ShopItem item);

        /// <summary>Set the map waypoint at world (x, z); the GPS routes to it.</summary>
        void SetWaypoint(float x, float z);

        /// <summary>Remove the map waypoint.</summary>
        void ClearWaypoint();

        /// <summary>The current waypoint, false when none.</summary>
        bool TryGetWaypoint(out float x, out float z);

        /// <summary>A <c>GameSettings</c> field or key binding was changed in place: apply it now and persist it.</summary>
        void ApplySettings();

        /// <summary>Trigger CS1's own save.</summary>
        void SaveGame();

        /// <summary>Leave play mode for the city builder (already confirmed by the player).</summary>
        void ReturnToCityBuilder();

        /// <summary>Equip the weapon in wheel slot <paramref name="slot"/> (<see cref="WeaponWheelView"/> slot index).</summary>
        void SelectWeapon(int slot);

        /// <summary>A phone choice: <paramref name="app"/> and the row index inside it.</summary>
        void PhoneAction(PhoneApp app, int index);

        /// <summary>Interaction menu: route the GPS to the nearest target of a kind (None clears it).</summary>
        void QuickGps(QuickGpsTarget target);

        /// <summary>Interaction menu: open the wardrobe (only offered inside a safehouse).</summary>
        void OpenWardrobe();

        /// <summary>Interaction menu: restart the current mission from its last checkpoint.</summary>
        void RetryMission();

        /// <summary>Interaction menu: abandon the current mission.</summary>
        void AbortMission();
    }

    /// <summary>Menu sound hooks (M5 audio implements them).</summary>
    public interface IUiSounds
    {
        /// <summary>Selection moved.</summary>
        void Navigate();
        /// <summary>Accepted / tab entered.</summary>
        void Select();
        /// <summary>Back / closed.</summary>
        void Back();
        /// <summary>Refused (insufficient funds, disabled row).</summary>
        void Error();
        /// <summary>A purchase succeeded.</summary>
        void Purchase();
        /// <summary>A menu, the phone or the wheel opened.</summary>
        void Open();
        /// <summary>The pause menu tab changed.</summary>
        void Tab();
    }
}
