using GtaSkylines.Game.Interiors;
using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Mod.Rendering
{
    /// <summary>Checks a <see cref="MeshPart"/> before it becomes a Unity mesh (16-bit indices).</summary>
    public static class InteriorMeshData
    {
        /// <summary>Most vertices a 16-bit-index mesh part may have.</summary>
        public const int MaxVertices = 65000;

        /// <summary>Null when the part converts as is, else why not.</summary>
        public static string Check(MeshPart p)
        {
            if (p == null || p.Positions == null || p.Normals == null || p.Uvs == null || p.Indices == null) return "missing arrays";
            if (p.Positions.Length % 3 != 0) return "positions not a multiple of 3";
            int n = p.VertexCount;
            if (n > MaxVertices) return n + " vertices (max " + MaxVertices + ")";
            if (p.Normals.Length != p.Positions.Length) return "normals do not match positions";
            if (p.Uvs.Length != n * 2) return "uvs do not match positions";
            if (p.Indices.Length % 3 != 0) return "indices not a multiple of 3";
            for (int i = 0; i < p.Indices.Length; i++)
            {
                if (p.Indices[i] < 0 || p.Indices[i] >= n) return "index " + p.Indices[i] + " out of range";
            }
            return null;
        }

        /// <summary>Axis-aligned bounds of the part's positions (zero for an empty part).</summary>
        public static void Bounds(MeshPart p, out Vec3 min, out Vec3 max)
        {
            int n = p.VertexCount;
            if (n == 0)
            {
                min = Vec3.Zero;
                max = Vec3.Zero;
                return;
            }
            float[] a = p.Positions;
            min = new Vec3(a[0], a[1], a[2]);
            max = min;
            for (int i = 1; i < n; i++)
            {
                var v = new Vec3(a[i * 3], a[i * 3 + 1], a[i * 3 + 2]);
                min = Vec3.Min(min, v);
                max = Vec3.Max(max, v);
            }
        }
    }
}
