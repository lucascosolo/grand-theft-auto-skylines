using GtaSkylines.Game.Interiors;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Mod.Rendering;
using Xunit;

namespace GtaSkylines.Mod.Tests
{
    public class InteriorMeshDataTests
    {
        static MeshPart Triangle()
        {
            return new MeshPart
            {
                Positions = new float[] { 0, 0, 0, 1, 0, 0, 0, 2, 3 },
                Normals = new float[] { 0, 0, 1, 0, 0, 1, 0, 0, 1 },
                Uvs = new float[] { 0, 0, 1, 0, 0, 1 },
                Indices = new[] { 0, 1, 2 },
            };
        }

        [Fact]
        public void Valid_part_returns_null()
        {
            Assert.Null(InteriorMeshData.Check(Triangle()));
        }

        [Fact]
        public void Empty_part_is_valid()
        {
            Assert.Null(InteriorMeshData.Check(new MeshPart()));
        }

        [Fact]
        public void Positions_not_multiple_of_three_is_invalid()
        {
            var p = Triangle();
            p.Positions = new float[] { 0, 0, 0, 1, 0, 0, 0, 2 };
            p.Normals = new float[p.Positions.Length];
            p.Indices = new int[0];
            Assert.False(string.IsNullOrEmpty(InteriorMeshData.Check(p)));
        }

        [Fact]
        public void Normals_length_mismatch_is_invalid()
        {
            var p = Triangle();
            p.Normals = new float[6];
            Assert.False(string.IsNullOrEmpty(InteriorMeshData.Check(p)));
        }

        [Fact]
        public void Uvs_length_mismatch_is_invalid()
        {
            var p = Triangle();
            p.Uvs = new float[4];
            Assert.False(string.IsNullOrEmpty(InteriorMeshData.Check(p)));
        }

        [Fact]
        public void Indices_not_multiple_of_three_is_invalid()
        {
            var p = Triangle();
            p.Indices = new[] { 0, 1, 2, 0 };
            Assert.False(string.IsNullOrEmpty(InteriorMeshData.Check(p)));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(3)]
        public void Index_out_of_range_is_invalid(int bad)
        {
            var p = Triangle();
            p.Indices = new[] { 0, 1, bad };
            Assert.False(string.IsNullOrEmpty(InteriorMeshData.Check(p)));
        }

        [Fact]
        public void More_than_65000_vertices_is_invalid_but_65000_is_fine()
        {
            Assert.Equal(65000, InteriorMeshData.MaxVertices);
            Assert.Null(InteriorMeshData.Check(Sized(65000)));
            Assert.False(string.IsNullOrEmpty(InteriorMeshData.Check(Sized(65001))));
        }

        static MeshPart Sized(int vertices)
        {
            return new MeshPart
            {
                Positions = new float[vertices * 3],
                Normals = new float[vertices * 3],
                Uvs = new float[vertices * 2],
                Indices = new int[0],
            };
        }

        [Fact]
        public void Bounds_cover_all_vertices()
        {
            Vec3 min, max;
            InteriorMeshData.Bounds(Triangle(), out min, out max);
            MeshAsserts.Near(new Vec3(0, 0, 0), min);
            MeshAsserts.Near(new Vec3(1, 2, 3), max);
        }

        [Fact]
        public void Bounds_of_empty_part_are_zero()
        {
            Vec3 min, max;
            InteriorMeshData.Bounds(new MeshPart(), out min, out max);
            Assert.Equal(Vec3.Zero, min);
            Assert.Equal(Vec3.Zero, max);
        }
    }
}
