namespace GtaSkylines.Game.World
{
    /// <summary>What a <see cref="WorldTag"/> names.</summary>
    public enum TagKind
    {
        /// <summary>Not a dynamic shape: tag 0 (static triangle in a <c>PhysicsHit</c>), −1 (static in a <c>VehicleImpact</c>) or any value that does not decode.</summary>
        None = 0,
        /// <summary>A walking CS1 citizen from the snapshot; the id is its CS1 citizen instance id (ushort, non-zero).</summary>
        AmbientCitizen = 1,
        /// <summary>A moving CS1 vehicle from the snapshot; the id is its CS1 vehicle id (ushort, non-zero).</summary>
        AmbientVehicle = 2,
        /// <summary>A CS1 parked vehicle from the snapshot; the id is its CS1 parked vehicle id (ushort, non-zero).</summary>
        AmbientParkedVehicle = 3,
        /// <summary>A Game <see cref="Ped"/> (player included); the id is <see cref="Ped.Id"/>.</summary>
        Ped = 4,
        /// <summary>A Game <see cref="Car"/>; the id is <see cref="Car.Id"/>.</summary>
        Car = 5,
    }

    /// <summary>
    /// The int tag every dynamic shape in <c>GameWorld.Collision</c> carries, and the actor encoding of
    /// <see cref="GameEvent.ActorA"/>/<see cref="GameEvent.ActorB"/>.
    /// <para>
    /// Encoding: <c>tag = ((int)kind &lt;&lt; 24) | id</c> with <c>kind</c> in 1..5 (bits 24-26) and <c>id</c> in
    /// 1..<see cref="MaxId"/> (bits 0-23). Every valid tag is therefore positive and non-zero, so it never collides with
    /// the static-geometry tags 0 (<c>PhysicsHit</c>) and −1 (<c>VehicleImpact</c>). Examples: ambient citizen instance
    /// 513 → 0x01000201; Game ped 7 → 0x04000007; Game car 12 → 0x0500000C.
    /// </para>
    /// <para>
    /// Game ped and car ids come from one counter (see <c>GameWorld</c>), so an id alone is also unique across both.
    /// </para>
    /// </summary>
    public static class WorldTag
    {
        /// <summary>Largest encodable id (2^24 − 1).</summary>
        public const int MaxId = 0xFFFFFF;

        /// <summary>The "nothing" tag used in events for an absent actor.</summary>
        public const int None = 0;

        /// <summary>Encodes a kind and id; returns <see cref="None"/> for <see cref="TagKind.None"/>, id ≤ 0 or id &gt; <see cref="MaxId"/>.</summary>
        public static int Encode(TagKind kind, int id)
        {
            if (kind == TagKind.None || id <= 0 || id > MaxId) return None;
            return ((int)kind << 24) | id;
        }

        /// <summary>The kind of a tag; <see cref="TagKind.None"/> for 0, negatives, an unknown kind or a zero id.</summary>
        public static TagKind KindOf(int tag)
        {
            if (tag <= 0 || (tag & MaxId) == 0) return TagKind.None;
            int k = tag >> 24;
            return k >= 1 && k <= 5 ? (TagKind)k : TagKind.None;
        }

        /// <summary>The id part of a tag (0 when <see cref="KindOf"/> is None).</summary>
        public static int IdOf(int tag)
        {
            return KindOf(tag) == TagKind.None ? 0 : tag & MaxId;
        }

        /// <summary>Tag of a Game ped.</summary>
        public static int ForPed(int pedId) { return Encode(TagKind.Ped, pedId); }

        /// <summary>Tag of a Game car.</summary>
        public static int ForCar(int carId) { return Encode(TagKind.Car, carId); }

        /// <summary>Tag of an ambient citizen by CS1 instance id.</summary>
        public static int ForAmbientCitizen(ushort instance) { return Encode(TagKind.AmbientCitizen, instance); }

        /// <summary>Tag of an ambient vehicle: <see cref="TagKind.AmbientParkedVehicle"/> when parked, else <see cref="TagKind.AmbientVehicle"/>.</summary>
        public static int ForAmbientVehicle(ushort id, bool parked)
        {
            return Encode(parked ? TagKind.AmbientParkedVehicle : TagKind.AmbientVehicle, id);
        }

        /// <summary>True for an ambient citizen tag or a Game ped tag.</summary>
        public static bool IsPerson(int tag)
        {
            TagKind k = KindOf(tag);
            return k == TagKind.AmbientCitizen || k == TagKind.Ped;
        }

        /// <summary>True for an ambient (moving or parked) vehicle tag or a Game car tag.</summary>
        public static bool IsVehicle(int tag)
        {
            TagKind k = KindOf(tag);
            return k == TagKind.AmbientVehicle || k == TagKind.AmbientParkedVehicle || k == TagKind.Car;
        }
    }
}
