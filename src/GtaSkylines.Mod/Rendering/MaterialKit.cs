using System.Collections.Generic;
using GtaSkylines.Game.Mathx;
using Skylines.Host;
using Skylines.Host.Rendering;
using UnityEngine;

namespace GtaSkylines.Mod.Rendering
{
    /// <summary>
    /// Shaders, shared materials and every Unity object the renderer creates. <see cref="ReleaseAll"/> destroys exactly
    /// what was tracked (never CS1's). Main thread only.
    /// </summary>
    internal sealed class MaterialKit
    {
        private readonly HostLog _log;
        private readonly List<Object> _owned = new List<Object>();
        private Shader _prop;
        private bool _shadersTried;
        private Texture2D _xys, _aci;
        private Material _translucent;

        public MaterialKit(HostLog log)
        {
            _log = log;
            int layer = LayerMask.NameToLayer("Props");
            PropLayer = layer >= 0 ? layer : 10;
        }

        /// <summary>The layer CS1 draws props on (the reference's block renderer uses it).</summary>
        public int PropLayer { get; private set; }

        /// <summary>Registers an object for <see cref="ReleaseAll"/>.</summary>
        public T Own<T>(T o) where T : Object
        {
            if (o != null)
            {
                o.hideFlags = HideFlags.HideAndDontSave;
                _owned.Add(o);
            }
            return o;
        }

        /// <summary>Destroys one owned object now.</summary>
        public void Destroy(Object o)
        {
            if (o == null) return;
            _owned.Remove(o);
            Object.Destroy(o);
        }

        /// <summary>
        /// A lit material with CS1's prop shader (<c>Custom/Props/Prop/Default</c>), the texture, white <c>_Color</c> and
        /// neutral XYS/ACI maps, as the reference's BlockRenderer variant 1; Unity Diffuse if the prop shader is missing.
        /// </summary>
        public Material Lit(Texture texture, string name)
        {
            FindShaders();
            Shader s = _prop ?? Shader.Find("Diffuse");
            if (s == null) return null;
            var m = Own(new Material(s) { name = name });
            m.SetTexture("_MainTex", texture);
            m.SetColor("_Color", Color.white);
            if (_prop != null)
            {
                if (_xys == null) _xys = Own(TextureUtil.Solid(new Color32(128, 128, 255, 255), true, "GtaSkylines.NeutralXYS"));
                if (_aci == null) _aci = Own(TextureUtil.Solid(new Color32(0, 0, 0, 255), true, "GtaSkylines.NeutralACI"));
                m.SetTexture("_XYSMap", _xys);
                m.SetTexture("_ACIMap", _aci);
            }
            return m;
        }

        /// <summary>An unlit opaque material (<c>Hidden/Internal-Colored</c>) of one colour, for self-lit surfaces.</summary>
        public Material Unlit(Color colour, string name)
        {
            Material m = Own(ColoredMaterial.Opaque(name));
            if (m != null) m.SetColor("_Color", colour);
            return m;
        }

        /// <summary>
        /// The shared alpha-blended unlit material (<see cref="ColoredMaterial.Translucent"/>); per-draw colour comes from a <see cref="MaterialPropertyBlock"/> "_Color".
        /// </summary>
        public Material Translucent
        {
            get
            {
                if (_translucent != null) return _translucent;
                _translucent = Own(ColoredMaterial.Translucent("GtaSkylines.Translucent"));
                if (_translucent != null) _translucent.renderQueue = 3000;
                return _translucent;
            }
        }

        /// <summary>A Unity mesh from <see cref="SimpleMesh"/> arrays, with white vertex colours (Internal-Colored multiplies by them).</summary>
        public Mesh Mesh(SimpleMesh s, string name)
        {
            int n = s.VertexCount;
            var v = new Vector3[n];
            var nm = new Vector3[n];
            var uv = new Vector2[n];
            var c = new Color32[n];
            for (int i = 0; i < n; i++)
            {
                v[i] = new Vector3(s.Positions[i * 3], s.Positions[i * 3 + 1], s.Positions[i * 3 + 2]);
                nm[i] = new Vector3(s.Normals[i * 3], s.Normals[i * 3 + 1], s.Normals[i * 3 + 2]);
                uv[i] = new Vector2(s.Uvs[i * 2], s.Uvs[i * 2 + 1]);
                c[i] = new Color32(255, 255, 255, 255);
            }
            var m = Own(new Mesh { name = name });
            m.vertices = v;
            m.normals = nm;
            m.uv = uv;
            m.colors32 = c;
            m.triangles = s.Indices;
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Destroys everything owned. Idempotent, never throws.</summary>
        public void ReleaseAll()
        {
            for (int i = _owned.Count - 1; i >= 0; i--)
            {
                try { if (_owned[i] != null) Object.Destroy(_owned[i]); }
                catch (System.Exception e) { _log.Error("render: destroy " + i, e); }
            }
            _owned.Clear();
            _xys = _aci = null;
            _translucent = null;
        }

        private void FindShaders()
        {
            if (_shadersTried) return;
            _shadersTried = true;
            _prop = Shader.Find("Custom/Props/Prop/Default");
            _log.Info("render: prop shader " + (_prop != null ? "found" : "MISSING (Diffuse fallback)") + ", Internal-Colored "
                + (ColoredMaterial.InternalColored != null ? "found" : "MISSING (markers and effects hidden)") + ", props layer " + PropLayer);
        }

        public static Vector3 V(Vec3 v) { return new Vector3(v.X, v.Y, v.Z); }

        public static Quaternion Q(Quat q) { return new Quaternion(q.X, q.Y, q.Z, q.W); }

        public static Color Rgb(int rgb, float alpha)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);
        }
    }
}
