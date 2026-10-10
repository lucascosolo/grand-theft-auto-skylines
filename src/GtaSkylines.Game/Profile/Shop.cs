using System;
using System.Collections.Generic;

namespace GtaSkylines.Game.Profile
{
    /// <summary>Kinds of shop. Each sells only its own catalogue items.</summary>
    public enum ShopKind
    {
        /// <summary>Weapons, ammo packs and armour.</summary>
        Ammunation = 0,
        /// <summary>Food: snack and meal.</summary>
        Food = 1,
        /// <summary>Cheap clothes ($80-$120).</summary>
        ClothesBudget = 2,
        /// <summary>Mid-range clothes ($200-$260).</summary>
        ClothesMid = 3,
        /// <summary>Designer clothes ($320-$400).</summary>
        ClothesDesigner = 4,
        /// <summary>Vehicle respray.</summary>
        Respray = 5,
        /// <summary>Hospital heal.</summary>
        Hospital = 6,
        /// <summary>Police station: pay off heat.</summary>
        PoliceStation = 7,
    }

    /// <summary>What an item does when bought.</summary>
    public enum ItemEffect
    {
        /// <summary>Grants <see cref="ShopItem.Weapon"/> plus <see cref="WeaponInfo.StartAmmo"/> rounds.</summary>
        Weapon = 0,
        /// <summary>Adds <see cref="ShopItem.Amount"/> rounds to <see cref="ShopItem.Weapon"/>.</summary>
        Ammo = 1,
        /// <summary>Sets armour to 100.</summary>
        Armour = 2,
        /// <summary>Adds <see cref="ShopItem.Amount"/> health, capped at 100.</summary>
        Health = 3,
        /// <summary>Sets <see cref="PlayerProfile.OutfitId"/> to <see cref="ShopItem.Amount"/>.</summary>
        Outfit = 4,
        /// <summary>No profile change; the Mod resprays the vehicle when <see cref="PurchaseResult.Ok"/> is returned.</summary>
        Respray = 5,
        /// <summary>Sets health to 100.</summary>
        Heal = 6,
        /// <summary>Sets player heat and all district heat to 0; the price depends on heat (see <see cref="Shop.PriceOf"/>).</summary>
        PayOffHeat = 7,
    }

    /// <summary>Result of a purchase attempt. Anything but <see cref="Ok"/> changes nothing and charges nothing.</summary>
    public enum PurchaseResult
    {
        /// <summary>Bought and applied.</summary>
        Ok = 0,
        /// <summary>Not enough cash.</summary>
        InsufficientCash = 1,
        /// <summary>Weapon, outfit or property already owned/worn.</summary>
        AlreadyOwned = 2,
        /// <summary>Ammo already at the maximum.</summary>
        AmmoFull = 3,
        /// <summary>Ammo pack for a weapon that is not owned.</summary>
        WeaponNotOwned = 4,
        /// <summary>Health already 100 (food, heal).</summary>
        HealthFull = 5,
        /// <summary>Armour already 100.</summary>
        ArmourFull = 6,
        /// <summary>Pay-off requested with zero heat.</summary>
        NoHeat = 7,
        /// <summary>No such item id.</summary>
        UnknownItem = 8,
        /// <summary>The item is not sold by this kind of shop.</summary>
        WrongShop = 9,
        /// <summary>Property: building id 0, level outside 1..5 or area not positive.</summary>
        InvalidProperty = 10,
    }

    /// <summary>A catalogue entry.</summary>
    public sealed class ShopItem
    {
        /// <summary>Stable id, e.g. "pistol".</summary>
        public string Id { get; private set; }
        /// <summary>Display name.</summary>
        public string Name { get; private set; }
        /// <summary>The shop that sells it.</summary>
        public ShopKind Shop { get; private set; }
        /// <summary>Effect on purchase.</summary>
        public ItemEffect Effect { get; private set; }
        /// <summary>Price in dollars; for <see cref="ItemEffect.PayOffHeat"/> the price per star-equivalent.</summary>
        public long Price { get; private set; }
        /// <summary>Weapon for Weapon/Ammo effects; otherwise Fists.</summary>
        public WeaponId Weapon { get; private set; }
        /// <summary>Rounds (Ammo), health (Health) or outfit id (Outfit); otherwise 0.</summary>
        public int Amount { get; private set; }

        /// <summary>Creates an item.</summary>
        public ShopItem(string id, string name, ShopKind shop, ItemEffect effect, long price, WeaponId weapon, int amount)
        {
            Id = id; Name = name; Shop = shop; Effect = effect; Price = price; Weapon = weapon; Amount = amount;
        }
    }

    /// <summary>Where a purchase reads and writes health and armour (0-100): the live player ped while playing, else the profile.</summary>
    public interface IVitals
    {
        /// <summary>Health 0-100.</summary>
        float Health { get; set; }
        /// <summary>Armour 0-100.</summary>
        float Armour { get; set; }
    }

    /// <summary>The full catalogue and purchase rules (DESIGN F-ECO-1).</summary>
    public static class Shop
    {
        /// <summary>Price of heat pay-off per star-equivalent of heat (500).</summary>
        public const long PayOffPerStar = 500;

        /// <summary>
        /// Every item, in this order. Ids, prices, effects:
        /// Ammunation: bat $100; pistol $600; smg $2200; shotgun $1800; carbine $4500;
        /// ammo_pistol $50 (+36); ammo_smg $90 (+90); ammo_shotgun $70 (+16); ammo_carbine $120 (+90); armour $250.
        /// Food: snack $5 (+25 health); meal $20 (+100 health).
        /// ClothesBudget: outfit_1 $80, outfit_2 $120; ClothesMid: outfit_3 $200, outfit_4 $260;
        /// ClothesDesigner: outfit_5 $320, outfit_6 $400 (Amount = outfit id).
        /// Respray: respray $300. Hospital: heal $200. PoliceStation: payoff_heat $500 per star-equivalent.
        /// </summary>
        public static readonly ShopItem[] Items = new ShopItem[]
        {
            new ShopItem("bat", "Baseball Bat", ShopKind.Ammunation, ItemEffect.Weapon, 100, WeaponId.Bat, 0),
            new ShopItem("pistol", "Pistol", ShopKind.Ammunation, ItemEffect.Weapon, 600, WeaponId.Pistol, 0),
            new ShopItem("smg", "SMG", ShopKind.Ammunation, ItemEffect.Weapon, 2200, WeaponId.Smg, 0),
            new ShopItem("shotgun", "Shotgun", ShopKind.Ammunation, ItemEffect.Weapon, 1800, WeaponId.Shotgun, 0),
            new ShopItem("carbine", "Carbine", ShopKind.Ammunation, ItemEffect.Weapon, 4500, WeaponId.Carbine, 0),
            new ShopItem("ammo_pistol", "Pistol Ammo", ShopKind.Ammunation, ItemEffect.Ammo, 50, WeaponId.Pistol, 36),
            new ShopItem("ammo_smg", "SMG Ammo", ShopKind.Ammunation, ItemEffect.Ammo, 90, WeaponId.Smg, 90),
            new ShopItem("ammo_shotgun", "Shotgun Shells", ShopKind.Ammunation, ItemEffect.Ammo, 70, WeaponId.Shotgun, 16),
            new ShopItem("ammo_carbine", "Carbine Ammo", ShopKind.Ammunation, ItemEffect.Ammo, 120, WeaponId.Carbine, 90),
            new ShopItem("armour", "Body Armour", ShopKind.Ammunation, ItemEffect.Armour, 250, WeaponId.Fists, 0),
            new ShopItem("snack", "Snack", ShopKind.Food, ItemEffect.Health, 5, WeaponId.Fists, 25),
            new ShopItem("meal", "Meal", ShopKind.Food, ItemEffect.Health, 20, WeaponId.Fists, 100),
            new ShopItem("outfit_1", "Tee and Jeans", ShopKind.ClothesBudget, ItemEffect.Outfit, 80, WeaponId.Fists, 1),
            new ShopItem("outfit_2", "Hoodie", ShopKind.ClothesBudget, ItemEffect.Outfit, 120, WeaponId.Fists, 2),
            new ShopItem("outfit_3", "Smart Casual", ShopKind.ClothesMid, ItemEffect.Outfit, 200, WeaponId.Fists, 3),
            new ShopItem("outfit_4", "Leather Jacket", ShopKind.ClothesMid, ItemEffect.Outfit, 260, WeaponId.Fists, 4),
            new ShopItem("outfit_5", "Suit", ShopKind.ClothesDesigner, ItemEffect.Outfit, 320, WeaponId.Fists, 5),
            new ShopItem("outfit_6", "Designer Set", ShopKind.ClothesDesigner, ItemEffect.Outfit, 400, WeaponId.Fists, 6),
            new ShopItem("respray", "Respray", ShopKind.Respray, ItemEffect.Respray, 300, WeaponId.Fists, 0),
            new ShopItem("heal", "Hospital Treatment", ShopKind.Hospital, ItemEffect.Heal, 200, WeaponId.Fists, 0),
            new ShopItem("payoff_heat", "Pay Off Heat", ShopKind.PoliceStation, ItemEffect.PayOffHeat, PayOffPerStar, WeaponId.Fists, 0),
        };

        /// <summary>Finds an item by id (ordinal); null if unknown or null.</summary>
        public static ShopItem Find(string id)
        {
            if (id == null) return null;
            for (int i = 0; i < Items.Length; i++)
                if (Items[i].Id == id) return Items[i];
            return null;
        }

        /// <summary>Items sold by a shop kind, in <see cref="Items"/> order (new list).</summary>
        public static List<ShopItem> ItemsFor(ShopKind kind)
        {
            var list = new List<ShopItem>();
            for (int i = 0; i < Items.Length; i++)
                if (Items[i].Shop == kind) list.Add(Items[i]);
            return list;
        }

        /// <summary>
        /// Price the player pays now. Equals <see cref="ShopItem.Price"/> except PayOffHeat:
        /// <see cref="PayOffPerStar"/> times <see cref="PlayerProfile.HeatStars"/> (0 when there is no heat).
        /// </summary>
        public static long PriceOf(ShopItem item, PlayerProfile profile)
        {
            if (item.Effect != ItemEffect.PayOffHeat) return item.Price;
            return PayOffPerStar * profile.HeatStars;
        }

        /// <summary>
        /// Buys an item. Checks in order: unknown id gives UnknownItem; item.Shop != shop gives WrongShop; "already" conditions
        /// (Weapon owned: AlreadyOwned; Ammo for unowned weapon: WeaponNotOwned, ammo at max: AmmoFull; Armour at 100: ArmourFull;
        /// Health/Heal at 100: HealthFull; Outfit equal to current: AlreadyOwned; PayOffHeat with 0 stars: NoHeat);
        /// then cash below <see cref="PriceOf"/> gives InsufficientCash; otherwise charges through <paramref name="wallet"/>
        /// (reason = item name) and applies the effect. Ammo that would exceed the maximum is bought whole and capped.
        /// Nothing changes on any non-Ok result.
        /// </summary>
        public static PurchaseResult Buy(Wallet wallet, ShopKind shop, string itemId)
        {
            return Buy(wallet, shop, itemId, wallet.Profile);
        }

        /// <summary>
        /// <see cref="Buy(Wallet, ShopKind, string)"/> with health and armour checked on and applied to
        /// <paramref name="vitals"/> (the live player while playing; values clamped to 0-100) instead of the profile.
        /// </summary>
        public static PurchaseResult Buy(Wallet wallet, ShopKind shop, string itemId, IVitals vitals)
        {
            ShopItem item = Find(itemId);
            if (item == null) return PurchaseResult.UnknownItem;
            if (item.Shop != shop) return PurchaseResult.WrongShop;
            PlayerProfile p = wallet.Profile;
            int w = (int)item.Weapon;
            switch (item.Effect)
            {
                case ItemEffect.Weapon:
                    if (p.OwnsWeapon(item.Weapon)) return PurchaseResult.AlreadyOwned;
                    break;
                case ItemEffect.Ammo:
                    if (!p.OwnsWeapon(item.Weapon)) return PurchaseResult.WeaponNotOwned;
                    if (p.GetAmmo(item.Weapon) >= WeaponInfo.MaxAmmo[w]) return PurchaseResult.AmmoFull;
                    break;
                case ItemEffect.Armour:
                    if (vitals.Armour >= PlayerProfile.MaxVital) return PurchaseResult.ArmourFull;
                    break;
                case ItemEffect.Health:
                case ItemEffect.Heal:
                    if (vitals.Health >= PlayerProfile.MaxVital) return PurchaseResult.HealthFull;
                    break;
                case ItemEffect.Outfit:
                    if (p.OutfitId == item.Amount) return PurchaseResult.AlreadyOwned;
                    break;
                case ItemEffect.PayOffHeat:
                    if (p.HeatStars == 0) return PurchaseResult.NoHeat;
                    break;
            }

            if (!wallet.TrySpend(PriceOf(item, p), item.Name)) return PurchaseResult.InsufficientCash;

            switch (item.Effect)
            {
                case ItemEffect.Weapon:
                    p.GiveWeapon(item.Weapon);
                    p.SetAmmo(item.Weapon, WeaponInfo.StartAmmo[w]);
                    break;
                case ItemEffect.Ammo: p.AddAmmo(item.Weapon, item.Amount); break;
                case ItemEffect.Armour: vitals.Armour = PlayerProfile.MaxVital; break;
                case ItemEffect.Health: vitals.Health = Math.Min(PlayerProfile.MaxVital, vitals.Health + item.Amount); break;
                case ItemEffect.Heal: vitals.Health = PlayerProfile.MaxVital; break;
                case ItemEffect.Outfit: p.OutfitId = item.Amount; break;
                case ItemEffect.PayOffHeat:
                    p.Heat = 0f;
                    p.DistrictHeat.Clear();
                    break;
            }
            return PurchaseResult.Ok;
        }
    }
}
