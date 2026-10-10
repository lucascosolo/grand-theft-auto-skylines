using UnityEngine;
using UnityEngine.Rendering;

namespace GtaSkylines.Mod.Rendering
{
    /// <summary>
    /// The one owner of Unity's built-in <c>Hidden/Internal-Colored</c> shader (vertex colour × <c>_Color</c>; blend, cull,
    /// depth write and depth test are material properties). Every vertex-coloured material in the mod is made here;
    /// the caller owns and destroys the material. Main thread only.
    /// </summary>
    internal static class ColoredMaterial
    {
        private static Shader s_shader;
        private static bool s_tried;

        /// <summary>The shader, or null when Unity does not have it (looked up once per process).</summary>
        public static Shader InternalColored
        {
            get
            {
                if (!s_tried)
                {
                    s_tried = true;
                    s_shader = Shader.Find("Hidden/Internal-Colored");
                }
                return s_shader;
            }
        }

        /// <summary>Opaque, back-face culled, depth written; null without the shader.</summary>
        public static Material Opaque(string name)
        {
            Shader s = InternalColored;
            if (s == null) return null;
            var m = new Material(s) { name = name, hideFlags = HideFlags.HideAndDontSave };
            m.SetInt("_Cull", (int)CullMode.Back);
            m.SetInt("_ZWrite", 1);
            return m;
        }

        /// <summary>Alpha blended, no culling, no depth write; null without the shader.</summary>
        public static Material Translucent(string name)
        {
            Shader s = InternalColored;
            if (s == null) return null;
            var m = new Material(s) { name = name, hideFlags = HideFlags.HideAndDontSave };
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_Cull", (int)CullMode.Off);
            m.SetInt("_ZWrite", 0);
            return m;
        }
    }
}
