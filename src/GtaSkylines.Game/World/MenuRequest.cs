using System;
using System.Collections.Generic;
using GtaSkylines.Game.Interiors;
using GtaSkylines.Game.Profile;

namespace GtaSkylines.Game.World
{
    /// <summary>Which menu the Mod presents.</summary>
    public enum MenuKind
    {
        /// <summary>No menu.</summary>
        None,
        /// <summary>A shop or service catalogue (<see cref="MenuRequest.Shop"/>, <see cref="MenuRequest.Items"/>): counters, hospital desk, police desk, respray.</summary>
        Shop,
        /// <summary>Safehouse bed: options Save (0), Sleep (1), Cancel (2).</summary>
        Bed,
        /// <summary>Wardrobe: <see cref="MenuRequest.Outfits"/> (citizen model ids), free.</summary>
        Wardrobe,
        /// <summary>Buy the residential building at the door: option Buy (0) at <see cref="MenuRequest.Price"/>, Cancel (1).</summary>
        Property,
    }

    /// <summary>
    /// A menu the Game asks the Mod to show (G6 raises them from interactions; G9 may raise its own). While a menu is
    /// open, the player's movement input is ignored and the simulation keeps running. The Mod answers through
    /// <c>GameSession.ChooseMenuOption</c> / <c>CloseMenu</c>.
    /// </summary>
    public sealed class MenuRequest
    {
        /// <summary>Kind; None when closed.</summary>
        public MenuKind Kind;
        /// <summary>Header (the shop's or building's name).</summary>
        public string Title;
        /// <summary>Catalogue for <see cref="MenuKind.Shop"/>.</summary>
        public ShopKind Shop;
        /// <summary>Items for <see cref="MenuKind.Shop"/> (<c>Shop.ItemsFor(Shop)</c>), option index = item index.</summary>
        public readonly List<ShopItem> Items = new List<ShopItem>();
        /// <summary>Outfit model ids for <see cref="MenuKind.Wardrobe"/>, option index = list index.</summary>
        public readonly List<int> Outfits = new List<int>();
        /// <summary>Building the menu belongs to (0 none).</summary>
        public ushort Building;
        /// <summary>Price for <see cref="MenuKind.Property"/>.</summary>
        public long Price;
        /// <summary>Result of the last chosen option of a Shop or Property menu (for M4's message line).</summary>
        public PurchaseResult LastResult;
        /// <summary>Incremented every time a new menu opens (lets the Mod detect a replaced menu).</summary>
        public int Revision;

        /// <summary>True when a menu is open.</summary>
        public bool IsOpen { get { return Kind != MenuKind.None; } }

        /// <summary>
        /// The catalogue a counter sells: Convenience and Food → Food; Guns → Ammunation; Clothes → ClothesBudget for
        /// building level 1-2, ClothesMid for 3-4, ClothesDesigner for 5. ShopType.None → Food.
        /// </summary>
        public static ShopKind ShopKindFor(ShopType type, int buildingLevel)
        {
            switch (type)
            {
                case ShopType.Guns: return ShopKind.Ammunation;
                case ShopType.Clothes:
                    return buildingLevel >= 5 ? ShopKind.ClothesDesigner : buildingLevel >= 3 ? ShopKind.ClothesMid : ShopKind.ClothesBudget;
                default: return ShopKind.Food;
            }
        }

        /// <summary>The catalogue of a service interaction: HospitalDesk → Hospital, PoliceDesk → PoliceStation, RespraySpot → Respray.</summary>
        /// <exception cref="ArgumentException">Any other kind.</exception>
        public static ShopKind ShopKindFor(InteractionKind kind)
        {
            switch (kind)
            {
                case InteractionKind.HospitalDesk: return ShopKind.Hospital;
                case InteractionKind.PoliceDesk: return ShopKind.PoliceStation;
                case InteractionKind.RespraySpot: return ShopKind.Respray;
                default: throw new ArgumentException("not a service interaction", "kind");
            }
        }

        /// <summary>Closes the menu (Kind None, lists emptied, Building 0, Price 0).</summary>
        public void Close()
        {
            Kind = MenuKind.None;
            Title = null;
            Items.Clear();
            Outfits.Clear();
            Building = 0;
            Price = 0;
        }
    }
}
