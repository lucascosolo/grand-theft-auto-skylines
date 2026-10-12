using System;

namespace Skylines.Core.Geometry
{
    /// <summary>
    /// How much of a building CS1 draws while it is under construction or upgrading (<c>CommonBuildingAI.RenderInstance</c>,
    /// CommonBuildingAI.cs:649-760), as a fraction of its height, and the state its collision depends on.
    /// </summary>
    public static class BuildingCollision
    {
        /// <summary><c>Building.Flags.Created</c> (Building.cs:12).</summary>
        public const uint FlagCreated = 1;
        /// <summary><c>Building.Flags.Deleted</c> (Building.cs:13).</summary>
        public const uint FlagDeleted = 2;
        /// <summary><c>Building.Flags.Completed</c> (Building.cs:49).</summary>
        public const uint FlagCompleted = 0x10000;
        /// <summary><c>Building.Flags.Collapsed</c> (Building.cs:57).</summary>
        public const uint FlagCollapsed = 0x400000;
        /// <summary><c>Building.Flags.Upgrading</c> (Building.cs:59).</summary>
        public const uint FlagUpgrading = 0x800000;
        /// <summary><c>Building.Flags.Hidden</c> (Building.cs:63).</summary>
        public const uint FlagHidden = 0x8000000;

        private const uint ShapeFlags = FlagCreated | FlagDeleted | FlagCompleted | FlagCollapsed | FlagUpgrading | FlagHidden;

        /// <summary>
        /// Drawn fraction of a building that is not upgrading: 1 when completed, 0 when not created, else
        /// <see cref="RisingScale"/> with the building as its own tallest.
        /// </summary>
        public static float HeightScale(uint flags, byte constructState, float height, int constructionTime)
        {
            if ((flags & FlagCompleted) != 0) return 1f;
            if ((flags & FlagCreated) == 0) return 0f;
            return RisingScale(constructState, height, height, constructionTime);
        }

        /// <summary>
        /// Drawn fraction of the building being built: it rises floor by floor and is whole about half way through
        /// construction. <paramref name="tallest"/> is the taller of the old and new building while upgrading.
        /// </summary>
        public static float RisingScale(byte constructState, float height, float tallest, int constructionTime)
        {
            if (!(height > 0f)) return 0f;
            float floor;
            float n = Progress(constructState, tallest, constructionTime, out floor);
            return Clamp01((n - 6f) * floor / height);
        }

        /// <summary>Drawn fraction of the old building while upgrading: whole at first, then taken down from the top.</summary>
        public static float FallingScale(byte constructState, float height, float tallest, int constructionTime)
        {
            if (!(height > 0f)) return 0f;
            float floor;
            float top = Progress(constructState, tallest, constructionTime, out floor) * floor;
            return Clamp01((top <= height ? height : 2f * height - top) / height);
        }

        /// <summary>What a building's collision shape depends on besides its placement; equal signatures, equal shapes.</summary>
        public static ulong Signature(ushort infoIndex, uint flags, byte constructState)
        {
            return ((ulong)infoIndex << 40) | ((ulong)constructState << 32) | (flags & ShapeFlags);
        }

        // num20 (returned) and num18 (floor) of CommonBuildingAI.cs:681-685.
        private static float Progress(byte constructState, float tallest, int constructionTime, out float floor)
        {
            float k = constructionTime / Math.Max(1f, constructionTime - 6f);
            floor = Math.Max(0.5f, tallest / 60f);
            float steps = (float)Math.Ceiling(tallest / floor / 6f) * 6f;
            return (steps * 2f + 6f) * k * constructState * (1f / 255f);
        }

        private static float Clamp01(float v)
        {
            return v < 0f ? 0f : (v > 1f ? 1f : v);
        }
    }
}
