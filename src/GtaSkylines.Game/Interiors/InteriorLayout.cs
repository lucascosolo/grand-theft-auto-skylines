using System;
using System.Collections.Generic;
using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Game.Interiors
{
    /// <summary>
    /// A generated interior in interior-local metres: the floor is y = 0; the entrance door is centred on x = 0 in the
    /// front wall, whose centre line is z = 0; the interior extends over x ∈ [−Width/2, Width/2], z ∈ [0, Depth]
    /// (wall centre lines), so +z points into the building from the street door. Everything is deterministic for a
    /// given building descriptor, archetype and variant.
    /// </summary>
    public sealed class InteriorLayout
    {
        /// <summary>Wall thickness, metres.</summary>
        public const float WallThickness = 0.15f;

        /// <summary>Height of open doorways, metres.</summary>
        public const float DoorHeight = 2.2f;

        /// <summary>Building the interior belongs to.</summary>
        public ushort BuildingId;

        /// <summary>Kind of interior.</summary>
        public InteriorArchetype Archetype;

        /// <summary>Variant number (0 for the street-level interior; apartments use the floor number).</summary>
        public int Variant;

        /// <summary>Extent along x between the outer wall centre lines, metres (8-40).</summary>
        public float Width;

        /// <summary>Extent along z between the outer wall centre lines, metres (8-40).</summary>
        public float Depth;

        /// <summary>Floor to ceiling, metres.</summary>
        public float CeilingHeight;

        /// <summary>Rooms; they tile the footprint without overlapping. Room 0 is the room the entrance opens into.</summary>
        public readonly List<Room> Rooms = new List<Room>();

        /// <summary>Openings between rooms; every room is reachable from room 0 through them.</summary>
        public readonly List<Doorway> Doorways = new List<Doorway>();

        /// <summary>Doors out of the interior: exactly one <see cref="PortalKind.Entrance"/>, at most one elevator.</summary>
        public readonly List<Portal> Portals = new List<Portal>();

        /// <summary>Furniture.</summary>
        public readonly List<FurnitureItem> Furniture = new List<FurnitureItem>();

        /// <summary>Interaction points, including one <see cref="InteractionKind.ExitDoor"/> in front of the entrance.</summary>
        public readonly List<InteractionPoint> Interactions = new List<InteractionPoint>();

        /// <summary>NPC spots.</summary>
        public readonly List<NpcSpot> NpcSpots = new List<NpcSpot>();

        /// <summary>Where the player appears when coming in through the entrance (on the floor, ~1.2 m inside).</summary>
        public Vec3 EntranceSpawn;

        /// <summary>Facing at <see cref="EntranceSpawn"/>, degrees (into the room: 0).</summary>
        public float EntranceSpawnYawDeg;

        /// <summary>Where the player appears when arriving by elevator (meaningful when an elevator portal exists).</summary>
        public Vec3 ElevatorSpawn;

        /// <summary>Facing at <see cref="ElevatorSpawn"/>, degrees.</summary>
        public float ElevatorSpawnYawDeg;

        /// <summary>Texture recipes referenced by rooms and mesh parts (at most 32, no duplicates).</summary>
        public readonly List<TextureRecipe> Recipes = new List<TextureRecipe>();

        /// <summary>Render geometry, grouped by recipe.</summary>
        public readonly List<MeshPart> MeshParts = new List<MeshPart>();

        /// <summary>
        /// Collision triangles, 9 floats each (a, b, c), interior-local: floor, ceiling, every wall face (closed at the
        /// entrance and elevator doors, open at doorways), and every solid furniture box. Normals
        /// (<c>Cross(b − a, c − a)</c>) point into walkable space.
        /// </summary>
        public float[] CollisionTriangles = new float[0];

        /// <summary>Number of collision triangles.</summary>
        public int CollisionTriangleCount { get { return CollisionTriangles.Length / 9; } }

        /// <summary>Walkability grid for the player and NPCs (0.25 m cells, 0.35 m clearance).</summary>
        public InteriorNavGrid Nav;

        /// <summary>
        /// A 32-bit hash of the whole content (rooms, doorways, portals, furniture, interactions, NPC spots, recipes,
        /// mesh and collision data), for determinism checks.
        /// </summary>
        public uint ContentHash()
        {
            throw new NotImplementedException();
        }

        /// <summary>The room containing the point's x, z, or −1.</summary>
        public int RoomAt(float x, float z)
        {
            throw new NotImplementedException();
        }

        /// <summary>Maps an interior-local point to world space for an interior placed at <paramref name="origin"/> turned by <paramref name="yawDeg"/>.</summary>
        public static Vec3 ToWorld(Vec3 local, Vec3 origin, float yawDeg)
        {
            return origin + Quat.Yaw(yawDeg) * local;
        }

        /// <summary>Inverse of <see cref="ToWorld"/>.</summary>
        public static Vec3 ToLocal(Vec3 world, Vec3 origin, float yawDeg)
        {
            return Quat.Yaw(-yawDeg) * (world - origin);
        }
    }

    /// <summary>
    /// Walkable cells of an interior on a 0.25 m grid: a cell is free when its centre is inside a room, at least
    /// <see cref="Clearance"/> from every wall face and solid furniture footprint (doorways counted as open).
    /// </summary>
    public sealed class InteriorNavGrid
    {
        /// <summary>Cell edge, metres.</summary>
        public const float CellSize = 0.25f;

        /// <summary>Required distance from obstacles, metres (the player's radius).</summary>
        public const float Clearance = 0.35f;

        /// <summary>Cells along x.</summary>
        public int CellsX { get; private set; }

        /// <summary>Cells along z.</summary>
        public int CellsZ { get; private set; }

        /// <summary>Interior-local x of the grid's minimum corner.</summary>
        public float OriginX { get; private set; }

        /// <summary>Interior-local z of the grid's minimum corner.</summary>
        public float OriginZ { get; private set; }

        /// <summary>Creates an empty grid covering the rectangle; every cell blocked.</summary>
        public InteriorNavGrid(Rect2 area)
        {
            throw new NotImplementedException();
        }

        /// <summary>True when the cell containing the point is free; false outside the grid.</summary>
        public bool IsFree(float x, float z)
        {
            throw new NotImplementedException();
        }

        /// <summary>Marks the cell containing the point free or blocked (no-op outside the grid).</summary>
        public void Set(float x, float z, bool free)
        {
            throw new NotImplementedException();
        }

        /// <summary>Number of free cells.</summary>
        public int FreeCount()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// The nearest free cell centre to a point within <paramref name="maxDistance"/> m; false when there is none.
        /// </summary>
        public bool NearestFree(Vec3 point, float maxDistance, out Vec3 free)
        {
            throw new NotImplementedException();
        }

        /// <summary>True when a path of free cells (8-connected, no corner cutting) joins the cells of two points.</summary>
        public bool Connected(Vec3 a, Vec3 b)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// A* path of free cell centres from <paramref name="from"/> to <paramref name="to"/> (both snapped to the
        /// nearest free cell within 1 m), simplified to straight runs; written into <paramref name="into"/> (cleared
        /// first), floor height 0. False and an empty list when there is no path.
        /// </summary>
        public bool FindPath(Vec3 from, Vec3 to, List<Vec3> into)
        {
            throw new NotImplementedException();
        }
    }
}
