using System;
using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Game.Interiors
{
    /// <summary>The kind of interior a building gets.</summary>
    public enum InteriorArchetype
    {
        /// <summary>No interior (parks, plazas, abandoned or collapsed buildings).</summary>
        None,
        /// <summary>Low-density home.</summary>
        House,
        /// <summary>High-density residential ground floor: lobby with an elevator door to <see cref="Apartment"/>.</summary>
        ApartmentLobby,
        /// <summary>One apartment, reached from an <see cref="ApartmentLobby"/> elevator.</summary>
        Apartment,
        /// <summary>Corner shop: food and drink (health).</summary>
        ConvenienceStore,
        /// <summary>Clothes shop: outfits.</summary>
        ClothesShop,
        /// <summary>Gun store: weapons, ammo, armour.</summary>
        GunStore,
        /// <summary>Restaurant or bar: meals (health).</summary>
        Restaurant,
        /// <summary>Office floor.</summary>
        Office,
        /// <summary>Warehouse or factory floor.</summary>
        Warehouse,
        /// <summary>Workshop with a respray spot (industrial).</summary>
        Garage,
        /// <summary>Police station.</summary>
        PoliceStation,
        /// <summary>Clinic or hospital.</summary>
        Hospital,
        /// <summary>Fire station.</summary>
        FireStation,
        /// <summary>School or university.</summary>
        School,
        /// <summary>Other city service building (power, water, garbage, transport, deathcare, government, monuments).</summary>
        ServiceHall,
        /// <summary>Anything else.</summary>
        Generic,
    }

    /// <summary>What a room is for (drives furnishing and floor material).</summary>
    public enum RoomKind
    {
        /// <summary>Entrance lobby or reception.</summary>
        Lobby,
        /// <summary>Corridor connecting rooms.</summary>
        Corridor,
        /// <summary>Living room.</summary>
        LivingRoom,
        /// <summary>Kitchen.</summary>
        Kitchen,
        /// <summary>Bedroom.</summary>
        Bedroom,
        /// <summary>Bathroom.</summary>
        Bathroom,
        /// <summary>Shop sales floor.</summary>
        ShopFloor,
        /// <summary>Stock or storage room.</summary>
        StockRoom,
        /// <summary>Restaurant dining room or bar.</summary>
        DiningRoom,
        /// <summary>Office room with desks.</summary>
        OfficeRoom,
        /// <summary>Meeting room.</summary>
        MeetingRoom,
        /// <summary>Warehouse or factory floor.</summary>
        WarehouseFloor,
        /// <summary>Workshop (garage).</summary>
        Workshop,
        /// <summary>Holding cells.</summary>
        Cells,
        /// <summary>Locker room.</summary>
        LockerRoom,
        /// <summary>Armoury.</summary>
        Armory,
        /// <summary>Hospital ward with beds.</summary>
        Ward,
        /// <summary>Treatment room.</summary>
        TreatmentRoom,
        /// <summary>Fire engine bay or vehicle garage.</summary>
        VehicleBay,
        /// <summary>Dormitory with bunks.</summary>
        Bunks,
        /// <summary>Classroom.</summary>
        Classroom,
        /// <summary>Control room with consoles.</summary>
        ControlRoom,
        /// <summary>Restroom.</summary>
        Restroom,
    }

    /// <summary>A piece of procedural furniture. Default sizes are in <see cref="FurnitureCatalog"/>.</summary>
    public enum FurnitureKind
    {
        /// <summary>Double bed.</summary>
        Bed,
        /// <summary>Single bed or bunk.</summary>
        SingleBed,
        /// <summary>Hospital bed.</summary>
        HospitalBed,
        /// <summary>Sofa.</summary>
        Sofa,
        /// <summary>Armchair.</summary>
        Armchair,
        /// <summary>Low coffee table.</summary>
        CoffeeTable,
        /// <summary>Dining table.</summary>
        DiningTable,
        /// <summary>Chair.</summary>
        Chair,
        /// <summary>Desk.</summary>
        Desk,
        /// <summary>Straight counter (shops, police and hospital desks, bars).</summary>
        Counter,
        /// <summary>Wall shelving.</summary>
        WallShelf,
        /// <summary>Free-standing shop shelf (gondola).</summary>
        ShopShelf,
        /// <summary>Clothes rail.</summary>
        ClothesRack,
        /// <summary>Glass display case.</summary>
        DisplayCase,
        /// <summary>Refrigerator.</summary>
        Fridge,
        /// <summary>Kitchen counter with a stove.</summary>
        KitchenCounter,
        /// <summary>Toilet.</summary>
        Toilet,
        /// <summary>Sink.</summary>
        Sink,
        /// <summary>Bathtub.</summary>
        Bathtub,
        /// <summary>Wardrobe.</summary>
        Wardrobe,
        /// <summary>Row of lockers.</summary>
        Locker,
        /// <summary>A straight run of cell bars (a wall segment with a gap for the cell door).</summary>
        CellBars,
        /// <summary>Bench.</summary>
        Bench,
        /// <summary>Potted plant.</summary>
        Plant,
        /// <summary>Television on a stand.</summary>
        Tv,
        /// <summary>Crate.</summary>
        Crate,
        /// <summary>Tall pallet rack.</summary>
        PalletRack,
        /// <summary>Workbench.</summary>
        Workbench,
        /// <summary>Control console.</summary>
        Console,
        /// <summary>Vending machine.</summary>
        VendingMachine,
        /// <summary>Row of mailboxes.</summary>
        Mailboxes,
        /// <summary>Rug (flat, no collision).</summary>
        Rug,
        /// <summary>School desk with chair.</summary>
        SchoolDesk,
        /// <summary>Whiteboard on a wall (flat, no collision).</summary>
        Whiteboard,
        /// <summary>Stool.</summary>
        Stool,
    }

    /// <summary>What the player can do at an interaction point.</summary>
    public enum InteractionKind
    {
        /// <summary>Leave the building (the entrance door).</summary>
        ExitDoor,
        /// <summary>Ride the elevator (lobby ↔ apartment).</summary>
        ElevatorDoor,
        /// <summary>Buy from a shop counter (see <see cref="InteractionPoint.Shop"/>).</summary>
        ShopCounter,
        /// <summary>Sleep and save (safehouse beds).</summary>
        Bed,
        /// <summary>Change outfit.</summary>
        Wardrobe,
        /// <summary>Hospital reception: heal for a fee.</summary>
        HospitalDesk,
        /// <summary>Police front desk: pay off heat.</summary>
        PoliceDesk,
        /// <summary>Drive-in respray spot (garage).</summary>
        RespraySpot,
    }

    /// <summary>Which catalogue a shop counter sells from.</summary>
    public enum ShopType
    {
        /// <summary>Not a shop.</summary>
        None,
        /// <summary>Snacks and drinks.</summary>
        Convenience,
        /// <summary>Outfits.</summary>
        Clothes,
        /// <summary>Weapons, ammo, armour.</summary>
        Guns,
        /// <summary>Meals.</summary>
        Food,
    }

    /// <summary>Who stands or sits at an NPC spot.</summary>
    public enum NpcRole
    {
        /// <summary>Shop or reception clerk behind a counter.</summary>
        Clerk,
        /// <summary>Resident at home.</summary>
        Resident,
        /// <summary>Worker at a desk or bench.</summary>
        Worker,
        /// <summary>Police officer.</summary>
        Cop,
        /// <summary>Doctor or nurse.</summary>
        Doctor,
        /// <summary>Patient in a bed.</summary>
        Patient,
        /// <summary>Customer or visitor.</summary>
        Visitor,
    }

    /// <summary>A way in or out of the interior.</summary>
    public enum PortalKind
    {
        /// <summary>The street door (always exactly one).</summary>
        Entrance,
        /// <summary>An elevator door to another interior of the same building.</summary>
        Elevator,
    }

    /// <summary>The visual surface family of a mesh part.</summary>
    public enum MaterialSlot
    {
        /// <summary>Floors.</summary>
        Floor,
        /// <summary>Walls.</summary>
        Wall,
        /// <summary>Ceilings.</summary>
        Ceiling,
        /// <summary>Wooden furniture and doors.</summary>
        Wood,
        /// <summary>Upholstery and bedding.</summary>
        Fabric,
        /// <summary>Metal (racks, lockers, bars, appliances).</summary>
        Metal,
        /// <summary>Glass (display cases, windows).</summary>
        Glass,
        /// <summary>Plastic and painted surfaces.</summary>
        Plastic,
        /// <summary>Leaves.</summary>
        Plant,
        /// <summary>Self-lit surfaces (ceiling lights, windows, screens).</summary>
        Emissive,
    }

    /// <summary>A procedural texture pattern the Mod knows how to paint.</summary>
    public enum TexturePattern
    {
        /// <summary>Flat colour with faint noise.</summary>
        Plain,
        /// <summary>Wood planks along u.</summary>
        Planks,
        /// <summary>Square tiles with grout lines.</summary>
        Tiles,
        /// <summary>Carpet (fine noise).</summary>
        Carpet,
        /// <summary>Brick courses.</summary>
        Bricks,
        /// <summary>Concrete (blotchy noise).</summary>
        Concrete,
        /// <summary>Vertical stripes (wallpaper).</summary>
        Stripes,
        /// <summary>Checkerboard.</summary>
        Checker,
        /// <summary>Brushed metal.</summary>
        Brushed,
        /// <summary>Sky seen through a window (gradient), drawn self-lit.</summary>
        WindowSky,
        /// <summary>A light panel, drawn self-lit.</summary>
        LightPanel,
    }

    /// <summary>Axis-aligned rectangle on the interior's x, z floor plane. Min is inclusive, max exclusive for point tests.</summary>
    [Serializable]
    public struct Rect2
    {
        /// <summary>Bounds.</summary>
        public float MinX, MinZ, MaxX, MaxZ;

        /// <summary>Creates a rectangle from its bounds.</summary>
        public Rect2(float minX, float minZ, float maxX, float maxZ)
        {
            MinX = minX; MinZ = minZ; MaxX = maxX; MaxZ = maxZ;
        }

        /// <summary>Extent along x.</summary>
        public float Width { get { return MaxX - MinX; } }

        /// <summary>Extent along z.</summary>
        public float Depth { get { return MaxZ - MinZ; } }

        /// <summary>Area.</summary>
        public float Area { get { return Width * Depth; } }

        /// <summary>Centre on the floor (y = 0).</summary>
        public Vec3 Center { get { return new Vec3((MinX + MaxX) * 0.5f, 0f, (MinZ + MaxZ) * 0.5f); } }

        /// <summary>True when the point's x, z lies inside (min inclusive, max exclusive).</summary>
        public bool Contains(float x, float z) { return x >= MinX && x < MaxX && z >= MinZ && z < MaxZ; }

        /// <summary>True when the interiors overlap by more than <paramref name="epsilon"/> on both axes.</summary>
        public bool Overlaps(Rect2 o, float epsilon)
        {
            return MinX < o.MaxX - epsilon && o.MinX < MaxX - epsilon && MinZ < o.MaxZ - epsilon && o.MinZ < MaxZ - epsilon;
        }

        /// <summary>The rectangle shrunk by <paramref name="d"/> on every side (may become empty).</summary>
        public Rect2 Inset(float d) { return new Rect2(MinX + d, MinZ + d, MaxX - d, MaxZ - d); }

        /// <inheritdoc />
        public override string ToString() { return "[" + MinX + ".." + MaxX + " x " + MinZ + ".." + MaxZ + "]"; }
    }

    /// <summary>A room: an axis-aligned rectangle of floor bounded by walls (the rectangle is measured to the wall centre lines).</summary>
    public sealed class Room
    {
        /// <summary>Index in <see cref="InteriorLayout.Rooms"/>.</summary>
        public int Id;
        /// <summary>Use.</summary>
        public RoomKind Kind;
        /// <summary>Extent, to the wall centre lines.</summary>
        public Rect2 Bounds;
        /// <summary>Index into <see cref="InteriorLayout.Recipes"/> for the floor.</summary>
        public int FloorRecipe;
        /// <summary>Index into <see cref="InteriorLayout.Recipes"/> for the walls.</summary>
        public int WallRecipe;
    }

    /// <summary>An open doorway in a wall between two rooms.</summary>
    public struct Doorway
    {
        /// <summary>Room on one side.</summary>
        public int RoomA;
        /// <summary>Room on the other side.</summary>
        public int RoomB;
        /// <summary>Centre of the opening on the floor, on the wall centre line.</summary>
        public Vec3 Center;
        /// <summary>True when the wall runs along x (the doorway is crossed by moving along z).</summary>
        public bool WallAlongX;
        /// <summary>Clear width of the opening, metres (≥ 1.0).</summary>
        public float Width;
        /// <summary>Height of the opening, metres.</summary>
        public float Height;
    }

    /// <summary>A door to outside this interior (always closed in the geometry; used through its interaction point).</summary>
    public struct Portal
    {
        /// <summary>Kind.</summary>
        public PortalKind Kind;
        /// <summary>Centre of the door on the floor, on the wall's inner face.</summary>
        public Vec3 Position;
        /// <summary>Direction pointing from the door into the room, degrees (0 = +z).</summary>
        public float InwardYawDeg;
        /// <summary>Room the door opens into.</summary>
        public int Room;
    }

    /// <summary>A placed piece of furniture: an oriented box on the floor (yaw a multiple of 90°).</summary>
    public struct FurnitureItem
    {
        /// <summary>Kind.</summary>
        public FurnitureKind Kind;
        /// <summary>Centre of the box's footprint on the floor (y = 0 is the floor) — or, for wall items, the centre of the item at its height.</summary>
        public Vec3 Position;
        /// <summary>Size before rotation: x width (across its front), y height, z depth (front to back). In its own frame the item's front faces +z.</summary>
        public Vec3 Size;
        /// <summary>Rotation about +y, degrees, one of 0, 90, 180, 270. The item's front (where a person stands to use it) faces the yaw direction.</summary>
        public float YawDeg;
        /// <summary>Room it stands in.</summary>
        public int Room;
        /// <summary>True when it blocks movement (rugs, whiteboards and wall-mounted items do not).</summary>
        public bool Solid;
        /// <summary>The footprint on the floor after rotation (x, z), the area it blocks when <see cref="Solid"/>.</summary>
        public Rect2 Footprint;
    }

    /// <summary>A spot where the player can do something; reached by standing within 1 m and facing it.</summary>
    public struct InteractionPoint
    {
        /// <summary>Kind.</summary>
        public InteractionKind Kind;
        /// <summary>Where the player stands, on the floor.</summary>
        public Vec3 Position;
        /// <summary>Direction the player faces while using it, degrees.</summary>
        public float YawDeg;
        /// <summary>Room.</summary>
        public int Room;
        /// <summary>Catalogue for <see cref="InteractionKind.ShopCounter"/>, else <see cref="ShopType.None"/>.</summary>
        public ShopType Shop;
    }

    /// <summary>A spot where an NPC stands or sits.</summary>
    public struct NpcSpot
    {
        /// <summary>Who belongs here.</summary>
        public NpcRole Role;
        /// <summary>Feet position on the floor (or on the furniture for sitting/lying).</summary>
        public Vec3 Position;
        /// <summary>Facing, degrees.</summary>
        public float YawDeg;
        /// <summary>Room.</summary>
        public int Room;
        /// <summary>True when the NPC sits or lies (bed, chair) rather than stands.</summary>
        public bool Seated;
    }

    /// <summary>How the Mod paints one texture (256 × 256, tiling). Colours are 0xRRGGBB.</summary>
    [Serializable]
    public struct TextureRecipe : IEquatable<TextureRecipe>
    {
        /// <summary>Pattern.</summary>
        public TexturePattern Pattern;
        /// <summary>Main colour.</summary>
        public int BaseRgb;
        /// <summary>Secondary colour (grout, plank seams, stripes).</summary>
        public int AccentRgb;
        /// <summary>World metres covered by one texture repeat.</summary>
        public float RepeatMetres;
        /// <summary>True for self-lit surfaces.</summary>
        public bool Emissive;

        /// <inheritdoc />
        public bool Equals(TextureRecipe o)
        {
            return Pattern == o.Pattern && BaseRgb == o.BaseRgb && AccentRgb == o.AccentRgb && RepeatMetres == o.RepeatMetres && Emissive == o.Emissive;
        }

        /// <inheritdoc />
        public override bool Equals(object obj) { return obj is TextureRecipe && Equals((TextureRecipe)obj); }

        /// <inheritdoc />
        public override int GetHashCode() { return ((int)Pattern * 397) ^ BaseRgb ^ (AccentRgb << 3) ^ RepeatMetres.GetHashCode() ^ (Emissive ? 1 : 0); }
    }

    /// <summary>
    /// Render geometry sharing one texture recipe. Flat-shaded triangles; at most 65000 vertices per part (16-bit
    /// indices). Front faces: <c>Vec3.Cross(b − a, c − a)</c> points to the visible side (Unity's clockwise winding).
    /// </summary>
    public sealed class MeshPart
    {
        /// <summary>Surface family.</summary>
        public MaterialSlot Slot;
        /// <summary>Index into <see cref="InteriorLayout.Recipes"/>.</summary>
        public int Recipe;
        /// <summary>Vertex positions, 3 floats each, interior-local.</summary>
        public float[] Positions = new float[0];
        /// <summary>Vertex normals, 3 floats each.</summary>
        public float[] Normals = new float[0];
        /// <summary>Texture coordinates, 2 floats each; planar, in units of the recipe's repeat.</summary>
        public float[] Uvs = new float[0];
        /// <summary>Triangle indices, 3 per triangle.</summary>
        public int[] Indices = new int[0];

        /// <summary>Number of vertices.</summary>
        public int VertexCount { get { return Positions.Length / 3; } }

        /// <summary>Number of triangles.</summary>
        public int TriangleCount { get { return Indices.Length / 3; } }
    }
}
