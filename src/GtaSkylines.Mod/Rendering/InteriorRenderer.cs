using System.Collections.Generic;
using GtaSkylines.Game.Interiors;
using Skylines.Host;
using UnityEngine;
using UnityEngine.Rendering;

namespace GtaSkylines.Mod.Rendering
{
    /// <summary>
    /// The active interior: one Unity mesh per <see cref="MeshPart"/> and one material per recipe, built when the layout
    /// changes and destroyed when it goes. Textures are 256 × 256 with mipmaps, bilinear, repeating. Self-lit recipes
    /// use an unlit Internal-Colored material of the texture's mean colour (CS1's prop illumination channel is gated by
    /// night time, so it would not read as lit by day). Drawn on the props layer, no shadows. Main thread only.
    /// </summary>
    internal sealed class InteriorRenderer
    {
        private readonly HostLog _log;
        private readonly MaterialKit _kit;
        private readonly List<Object> _built = new List<Object>();
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly List<Material> _partMaterial = new List<Material>();
        private InteriorLayout _layout;

        public InteriorRenderer(HostLog log, MaterialKit kit)
        {
            _log = log;
            _kit = kit;
        }

        public void Draw(InteriorDraw d)
        {
            if (d.Layout != _layout) Build(d.Layout);
            if (_layout == null) return;
            Matrix4x4 m = Matrix4x4.TRS(MaterialKit.V(d.Origin), Quaternion.Euler(0f, d.YawDeg, 0f), Vector3.one);
            for (int i = 0; i < _meshes.Count; i++)
            {
                if (_meshes[i] == null || _partMaterial[i] == null) continue;
                Graphics.DrawMesh(_meshes[i], m, _partMaterial[i], _kit.PropLayer, null, 0, null, ShadowCastingMode.Off, false);
            }
        }

        public void Release()
        {
            Build(null);
        }

        private void Build(InteriorLayout layout)
        {
            for (int i = 0; i < _built.Count; i++) _kit.Destroy(_built[i]);
            _built.Clear();
            _meshes.Clear();
            _partMaterial.Clear();
            _layout = layout;
            if (layout == null) return;
            var materials = new Material[layout.Recipes.Count];
            var pixels = new byte[TexturePainter.Size * TexturePainter.Size * 4];
            for (int r = 0; r < materials.Length; r++) materials[r] = MakeMaterial(layout.Recipes[r], r, pixels);
            int skipped = 0;
            for (int i = 0; i < layout.MeshParts.Count; i++)
            {
                MeshPart p = layout.MeshParts[i];
                string problem = InteriorMeshData.Check(p);
                if (problem != null || p.Recipe < 0 || p.Recipe >= materials.Length)
                {
                    skipped++;
                    _log.Warn("interior " + layout.BuildingId + ": part " + i + " skipped: " + (problem ?? "recipe " + p.Recipe));
                    continue;
                }
                Mesh mesh = _kit.Mesh(new SimpleMesh { Positions = p.Positions, Normals = p.Normals, Uvs = p.Uvs, Indices = p.Indices },
                    "GtaSkylines.Interior." + i);
                _built.Add(mesh);
                _meshes.Add(mesh);
                _partMaterial.Add(materials[p.Recipe]);
            }
            _log.Info("interior " + layout.BuildingId + " (" + layout.Archetype + "): " + _meshes.Count + " meshes, " + materials.Length
                + " materials" + (skipped > 0 ? ", " + skipped + " parts skipped" : ""));
        }

        private Material MakeMaterial(TextureRecipe recipe, int index, byte[] pixels)
        {
            TexturePainter.Paint(recipe, pixels);
            if (recipe.Emissive)
            {
                byte r, g, b;
                TexturePainter.AverageRgb(pixels, out r, out g, out b);
                Material u = _kit.Unlit(new Color32(r, g, b, 255), "GtaSkylines.InteriorLit." + index);
                if (u != null)
                {
                    _built.Add(u);
                    return u;
                }
            }
            var tex = new Texture2D(TexturePainter.Size, TexturePainter.Size, TextureFormat.RGBA32, true, false)
            {
                name = "GtaSkylines.Interior." + recipe.Pattern + "." + index,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat,
                anisoLevel = 4,
            };
            tex.LoadRawTextureData(pixels);
            tex.Apply(true, true);
            _built.Add(_kit.Own(tex));
            Material m = _kit.Lit(tex, "GtaSkylines.Interior." + index);
            if (m != null) _built.Add(m);
            return m;
        }
    }
}
