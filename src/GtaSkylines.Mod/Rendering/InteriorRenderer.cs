using System.Collections.Generic;
using GtaSkylines.Game.Interiors;
using Skylines.Host;
using UnityEngine;
using UnityEngine.Rendering;

namespace GtaSkylines.Mod.Rendering
{
    /// <summary>
    /// The active interior: one Unity mesh per <see cref="MeshPart"/> and one material per recipe, built when the layout
    /// changes and destroyed when it goes. Textures are 256 × 256 with a full mip chain (SetPixels32 + Apply(true, true)), bilinear, repeating. Self-lit recipes
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
            var colours = new Color32[TexturePainter.Size * TexturePainter.Size];
            for (int r = 0; r < materials.Length; r++) materials[r] = MakeMaterial(layout.Recipes[r], r, pixels, colours);
            int skipped = 0;
            for (int i = 0; i < layout.MeshParts.Count; i++)
            {
                MeshPart p = layout.MeshParts[i];
                string problem = InteriorMeshData.Check(p);
                if (problem == null && (p.Recipe < 0 || p.Recipe >= materials.Length)) problem = "recipe " + p.Recipe;
                if (problem == null && materials[p.Recipe] == null) problem = "no material for recipe " + p.Recipe;
                if (problem != null)
                {
                    skipped++;
                    _log.Warn("interior " + layout.BuildingId + ": part " + i + " skipped: " + problem);
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

        // Lit with the painted texture, or (self-lit recipes, or no lit shader) unlit in its mean colour; null when neither
        // shader exists, and the parts using it are skipped.
        private Material MakeMaterial(TextureRecipe recipe, int index, byte[] pixels, Color32[] colours)
        {
            TexturePainter.Paint(recipe, pixels);
            byte r, g, b;
            TexturePainter.AverageRgb(pixels, out r, out g, out b);
            if (recipe.Emissive)
            {
                Material u = Unlit(r, g, b, index);
                if (u != null) return u;
            }
            var tex = new Texture2D(TexturePainter.Size, TexturePainter.Size, TextureFormat.RGBA32, true, false)
            {
                name = "GtaSkylines.Interior." + recipe.Pattern + "." + index,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat,
                anisoLevel = 4,
            };
            // SetPixels32 fills level 0 and Apply(true, ...) builds the whole mip chain on any Unity version, which
            // LoadRawTextureData into a mipmapped texture does not promise (it is a native call, Texture2D.cs:189-198).
            for (int i = 0; i < colours.Length; i++)
                colours[i] = new Color32(pixels[i * 4], pixels[i * 4 + 1], pixels[i * 4 + 2], pixels[i * 4 + 3]);
            tex.SetPixels32(colours);
            tex.Apply(true, true);
            _built.Add(_kit.Own(tex));
            Material m = _kit.Lit(tex, "GtaSkylines.Interior." + index);
            if (m == null) return Unlit(r, g, b, index);
            _built.Add(m);
            return m;
        }

        private Material Unlit(byte r, byte g, byte b, int index)
        {
            Material u = _kit.Unlit(new Color32(r, g, b, 255), "GtaSkylines.InteriorLit." + index);
            if (u != null) _built.Add(u);
            return u;
        }
    }
}
