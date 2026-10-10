using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Game.Interiors
{
    /// <summary>Default sizes of the procedural furniture kit (x width across the front, y height, z depth), metres.</summary>
    public static class FurnitureCatalog
    {
        /// <summary>Default size of a kind. Counters, shelves, racks and bars are stretched along x when placed.</summary>
        public static Vec3 DefaultSize(FurnitureKind kind)
        {
            switch (kind)
            {
                case FurnitureKind.Bed: return new Vec3(1.6f, 0.55f, 2.1f);
                case FurnitureKind.SingleBed: return new Vec3(1.0f, 0.55f, 2.0f);
                case FurnitureKind.HospitalBed: return new Vec3(1.0f, 0.8f, 2.1f);
                case FurnitureKind.Sofa: return new Vec3(2.1f, 0.85f, 0.9f);
                case FurnitureKind.Armchair: return new Vec3(0.9f, 0.85f, 0.85f);
                case FurnitureKind.CoffeeTable: return new Vec3(1.1f, 0.42f, 0.6f);
                case FurnitureKind.DiningTable: return new Vec3(1.6f, 0.75f, 0.9f);
                case FurnitureKind.Chair: return new Vec3(0.45f, 0.9f, 0.45f);
                case FurnitureKind.Desk: return new Vec3(1.4f, 0.75f, 0.7f);
                case FurnitureKind.Counter: return new Vec3(2.4f, 1.05f, 0.7f);
                case FurnitureKind.WallShelf: return new Vec3(2.0f, 2.0f, 0.45f);
                case FurnitureKind.ShopShelf: return new Vec3(2.4f, 1.6f, 0.9f);
                case FurnitureKind.ClothesRack: return new Vec3(1.6f, 1.5f, 0.6f);
                case FurnitureKind.DisplayCase: return new Vec3(1.8f, 1.0f, 0.6f);
                case FurnitureKind.Fridge: return new Vec3(0.8f, 1.9f, 0.7f);
                case FurnitureKind.KitchenCounter: return new Vec3(2.4f, 0.92f, 0.62f);
                case FurnitureKind.Toilet: return new Vec3(0.45f, 0.8f, 0.7f);
                case FurnitureKind.Sink: return new Vec3(0.6f, 0.9f, 0.5f);
                case FurnitureKind.Bathtub: return new Vec3(1.7f, 0.6f, 0.8f);
                case FurnitureKind.Wardrobe: return new Vec3(1.2f, 2.0f, 0.6f);
                case FurnitureKind.Locker: return new Vec3(1.8f, 1.9f, 0.5f);
                case FurnitureKind.CellBars: return new Vec3(3.0f, 2.6f, 0.1f);
                case FurnitureKind.Bench: return new Vec3(1.6f, 0.45f, 0.45f);
                case FurnitureKind.Plant: return new Vec3(0.5f, 1.2f, 0.5f);
                case FurnitureKind.Tv: return new Vec3(1.2f, 1.2f, 0.45f);
                case FurnitureKind.Crate: return new Vec3(1.2f, 1.0f, 1.2f);
                case FurnitureKind.PalletRack: return new Vec3(2.7f, 4.0f, 1.1f);
                case FurnitureKind.Workbench: return new Vec3(2.0f, 0.95f, 0.8f);
                case FurnitureKind.Console: return new Vec3(2.0f, 1.1f, 0.8f);
                case FurnitureKind.VendingMachine: return new Vec3(0.9f, 1.9f, 0.8f);
                case FurnitureKind.Mailboxes: return new Vec3(1.5f, 1.6f, 0.35f);
                case FurnitureKind.Rug: return new Vec3(2.0f, 0.02f, 1.4f);
                case FurnitureKind.SchoolDesk: return new Vec3(0.7f, 0.75f, 0.9f);
                case FurnitureKind.Whiteboard: return new Vec3(2.4f, 1.2f, 0.05f);
                case FurnitureKind.Stool: return new Vec3(0.4f, 0.75f, 0.4f);
                default: return new Vec3(1f, 1f, 1f);
            }
        }

        /// <summary>True for kinds that block movement (everything except rugs and whiteboards).</summary>
        public static bool IsSolid(FurnitureKind kind)
        {
            return kind != FurnitureKind.Rug && kind != FurnitureKind.Whiteboard;
        }
    }
}
