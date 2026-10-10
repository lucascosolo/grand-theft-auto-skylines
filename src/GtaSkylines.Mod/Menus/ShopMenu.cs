using System.Collections.Generic;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.World;
using GtaSkylines.Mod.Hud;
using UnityEngine;

namespace GtaSkylines.Mod.Menus
{
    /// <summary>
    /// Shop, service, wardrobe, property and safehouse-bed menus from the Game's <see cref="MenuRequest"/>, in the GTA V
    /// list style. Row texts are built when the request's revision changes; the purchase result shows as a message line.
    /// </summary>
    public sealed class ShopMenu : IMenuRows
    {
        private readonly MenuList _list = new MenuList(10);
        private readonly RichTextCache _desc = new RichTextCache();
        private readonly CounterText _counter = new CounterText();
        private readonly List<string> _labels = new List<string>();
        private readonly List<string> _values = new List<string>();
        private readonly List<long> _prices = new List<long>();
        private readonly List<string> _descs = new List<string>();
        private readonly List<bool> _owned = new List<bool>();
        private int _revision = -1;
        private long _cash;
        private string _message;
        private float _messageUntil;
        private string _subHeader;

        /// <summary>Draws the open menu and handles its input. Returns false when no menu is open.</summary>
        public bool Draw(HudStyle st, MenuRequest req, long cash, MenuKey key, IMenuCommands cmds, IUiSounds sounds, int generation)
        {
            if (req == null || !req.IsOpen)
            {
                _revision = -1;
                return false;
            }
            if (req.Revision != _revision) Build(req, cmds, sounds);
            _cash = cash;

            int choose = -1;
            if (!MenuPanel.Navigate(key, _list, sounds))
            {
                if (key == MenuKey.Accept) choose = _list.Selected;
                else if (key == MenuKey.Back)
                {
                    sounds.Back();
                    cmds.CloseMenu();
                    return true;
                }
            }
            string desc = Time.realtimeSinceStartup < _messageUntil ? _message : Desc(_list.Selected);
            int clicked = MenuPanel.Draw(st, st.Px(48f), st.Px(48f), req.Title ?? "Shop", _subHeader, _counter.Get(_list.Selected, _list.Count),
                _list, this, _desc, desc, generation, sounds);
            if (clicked >= 0) choose = clicked;
            if (choose >= 0) Choose(req, choose, cmds, sounds);
            return true;
        }

        private void Choose(MenuRequest req, int option, IMenuCommands cmds, IUiSounds sounds)
        {
            if (option < _owned.Count && _owned[option])
            {
                sounds.Error();
                Message(ResultText(PurchaseResult.AlreadyOwned));
                return;
            }
            if (req.Kind == MenuKind.Shop && option < _prices.Count && _prices[option] > _cash)
            {
                sounds.Error();
                Message("You cannot afford this.");
                return;
            }
            int revision = req.Revision;
            cmds.ChooseMenuOption(option);
            if (req.Kind != MenuKind.Shop && req.Kind != MenuKind.Property)
            {
                sounds.Select();
                return;
            }
            if (req.Revision != revision || !req.IsOpen) return;
            if (req.LastResult == PurchaseResult.Ok)
            {
                sounds.Purchase();
                Message("Purchased.");
                if (req.Kind == MenuKind.Shop) Build(req, cmds, null);
            }
            else
            {
                sounds.Error();
                Message(ResultText(req.LastResult));
            }
        }

        private void Message(string text)
        {
            _message = text;
            _messageUntil = Time.realtimeSinceStartup + 3f;
        }

        private void Build(MenuRequest req, IMenuCommands cmds, IUiSounds sounds)
        {
            bool reopened = req.Revision != _revision;
            _revision = req.Revision;
            _labels.Clear();
            _values.Clear();
            _prices.Clear();
            _descs.Clear();
            _owned.Clear();
            switch (req.Kind)
            {
                case MenuKind.Shop:
                    _subHeader = Category(req.Shop);
                    foreach (ShopItem item in req.Items)
                    {
                        bool outfit = item.Effect == ItemEffect.Outfit;
                        string name = outfit ? cmds.OutfitName(item.Amount) : item.Name;
                        if (cmds.IsOwned(item))
                        {
                            Row(name, outfit ? "WORN" : "OWNED", 0, outfit ? "You are wearing this." : "You already own the " + name + ".");
                            _owned[_owned.Count - 1] = true;
                            continue;
                        }
                        long price = cmds.PriceOf(item);
                        Row(name, price <= 0 ? "FREE" : HudFormat.Money(price), price, outfit ? "Wear: " + name + "." : Describe(item));
                    }
                    break;
                case MenuKind.Bed:
                    _subHeader = "SAFEHOUSE";
                    Row("Save game", null, 0, "Save your progress and the city.");
                    Row("Sleep", null, 0, "Sleep until morning.");
                    Row("Cancel", null, 0, null);
                    break;
                case MenuKind.Wardrobe:
                    _subHeader = "WARDROBE";
                    for (int i = 0; i < req.Outfits.Count; i++) Row(cmds.OutfitName(req.Outfits[i]), null, 0, "Change into this outfit.");
                    break;
                case MenuKind.Property:
                    _subHeader = "PROPERTY";
                    if (cmds.OwnsBuilding(req.Building))
                    {
                        Row("Buy property", "OWNED", 0, "You already own this property.");
                        _owned[0] = true;
                    }
                    else Row("Buy property", HudFormat.Money(req.Price), req.Price, "Buy this building as a safehouse.");
                    Row("Cancel", null, 0, null);
                    break;
            }
            _list.SetCount(_labels.Count);
            for (int i = 0; i < _owned.Count; i++) _list.SetEnabled(i, !_owned[i]);
            if (reopened)
            {
                _messageUntil = 0f;
                while (_list.Selected > 0 && _list.Up()) { }
                if (sounds != null) sounds.Open();
            }
        }

        private void Row(string label, string value, long price, string desc)
        {
            _labels.Add(label);
            _values.Add(value);
            _prices.Add(price);
            _descs.Add(desc);
            _owned.Add(false);
        }

        private string Desc(int row)
        {
            return row >= 0 && row < _descs.Count ? _descs[row] : null;
        }

        /// <inheritdoc/>
        public string Label(int row) { return row < _labels.Count ? _labels[row] : null; }

        /// <inheritdoc/>
        public string Value(int row) { return row < _values.Count ? _values[row] : null; }

        /// <inheritdoc/>
        public Color ValueColour(int row)
        {
            return row < _prices.Count && _prices[row] > _cash ? HudStyle.Red : Color.white;
        }

        private static string Category(ShopKind k)
        {
            switch (k)
            {
                case ShopKind.Ammunation: return "WEAPONS";
                case ShopKind.Food: return "FOOD AND DRINK";
                case ShopKind.Respray: return "RESPRAY";
                case ShopKind.Hospital: return "TREATMENT";
                case ShopKind.PoliceStation: return "POLICE";
                default: return "CLOTHES";
            }
        }

        private static string Describe(ShopItem item)
        {
            switch (item.Effect)
            {
                case ItemEffect.Weapon: return "Buy the " + item.Name + " with starting ammo.";
                case ItemEffect.Ammo: return "Adds " + item.Amount + " rounds.";
                case ItemEffect.Armour: return "Full body armour.";
                case ItemEffect.Health: return "Restores " + item.Amount + " health.";
                case ItemEffect.Respray: return "Respray the vehicle and lose the police's description of it.";
                case ItemEffect.Heal: return "Full medical treatment.";
                default: return "Pay off the police: clears your heat in every district.";
            }
        }

        private static string ResultText(PurchaseResult r)
        {
            switch (r)
            {
                case PurchaseResult.InsufficientCash: return "You cannot afford this.";
                case PurchaseResult.AlreadyOwned: return "You already own this.";
                case PurchaseResult.AmmoFull: return "You cannot carry any more ammo.";
                case PurchaseResult.WeaponNotOwned: return "You need the weapon first.";
                case PurchaseResult.HealthFull: return "Your health is already full.";
                case PurchaseResult.ArmourFull: return "Your armour is already full.";
                case PurchaseResult.NoHeat: return "The police are not looking for you.";
                default: return "Not available.";
            }
        }
    }

    /// <summary>The interaction menu (M): quick GPS, outfit in a safehouse, mission retry/abort.</summary>
    public sealed class InteractionMenu : IMenuRows
    {
        private static readonly string[] GpsNames = { "None", "Safehouse", "Ammu-Nation", "Clothes shop", "Food", "Hospital", "Police station" };
        private static readonly string[] GpsValues = BuildValues();
        private static readonly string[] Labels = { "Quick GPS", "Outfit", "Retry mission", "Abort mission", "Close" };
        private static readonly string[] Descs =
        {
            "Set a GPS route to the nearest location of this type. Left/right to choose.", "Change your outfit.",
            "Restart the mission from the last checkpoint.", "Abandon the current mission.", null,
        };
        private readonly MenuList _list = new MenuList(0);
        private readonly RichTextCache _desc = new RichTextCache();
        private readonly CounterText _counter = new CounterText();
        private int _gps;

        /// <summary>Opens the menu at the first row.</summary>
        public void Open()
        {
            _list.Reset();
            _list.SetCount(Labels.Length);
        }

        /// <summary>Draws and handles input; returns false when the menu closed itself.</summary>
        public bool Draw(HudStyle st, InteractionView view, MenuKey key, IMenuCommands cmds, IUiSounds sounds, int generation)
        {
            _list.SetEnabled(1, view.InSafehouse);
            _list.SetEnabled(2, view.MissionActive && view.CanRetry);
            _list.SetEnabled(3, view.MissionActive);
            int choose = -1;
            if (!MenuPanel.Navigate(key, _list, sounds))
            {
                if ((key == MenuKey.Left || key == MenuKey.Right) && _list.Selected == 0)
                {
                    _gps = (_gps + (key == MenuKey.Right ? 1 : GpsNames.Length - 1)) % GpsNames.Length;
                    sounds.Navigate();
                }
                else if (key == MenuKey.Accept) choose = _list.Selected;
                else if (key == MenuKey.Back)
                {
                    sounds.Back();
                    return false;
                }
            }
            int clicked = MenuPanel.Draw(st, st.Px(48f), st.Px(48f), "Interaction", "INTERACTION MENU", _counter.Get(_list.Selected, _list.Count),
                _list, this, _desc, _list.Selected >= 0 ? Descs[_list.Selected] : null, generation, sounds);
            if (clicked >= 0) choose = clicked;
            switch (choose)
            {
                case 0: sounds.Select(); cmds.QuickGps((QuickGpsTarget)_gps); return true;
                case 1: sounds.Select(); cmds.OpenWardrobe(); return false;
                case 2: sounds.Select(); cmds.RetryMission(); return false;
                case 3: sounds.Select(); cmds.AbortMission(); return false;
                case 4: sounds.Back(); return false;
                default: return true;
            }
        }

        /// <inheritdoc/>
        public string Label(int row) { return Labels[row]; }

        /// <inheritdoc/>
        public string Value(int row) { return row == 0 ? GpsValues[_gps] : null; }

        /// <inheritdoc/>
        public Color ValueColour(int row) { return Color.white; }

        private static string[] BuildValues()
        {
            var v = new string[GpsNames.Length];
            for (int i = 0; i < v.Length; i++) v[i] = "< " + GpsNames[i] + " >";
            return v;
        }
    }
}
