using System;
using ColossalFramework.UI;
using Skylines.Host;
using UnityEngine;

namespace GtaSkylines.Mod.Hud
{
    /// <summary>
    /// The GTA V PC look: fonts, colours, the icon atlas, GL materials and outlined text. Every draw call allocates
    /// nothing: one GUIStyle and one GUIContent are reused and their fields set per call.
    /// </summary>
    public sealed class HudStyle : IDisposable
    {
        /// <summary>Health green #57A85E.</summary>
        public static readonly Color Health = Rgb(0x57A85E);
        /// <summary>Armour blue #5DA9E9.</summary>
        public static readonly Color Armour = Rgb(0x5DA9E9);
        /// <summary>Cash gained #72CC72.</summary>
        public static readonly Color CashPlus = Rgb(0x72CC72);
        /// <summary>Cash lost / hostile red #E05050.</summary>
        public static readonly Color Red = Rgb(0xE05050);
        /// <summary>Mission yellow #F0C850.</summary>
        public static readonly Color Mission = Rgb(0xF0C850);
        /// <summary>Friendly blue (objective highlights, job blips).</summary>
        public static readonly Color Blue = Rgb(0x5DA9E9);
        /// <summary>Goal green.</summary>
        public static readonly Color Green = Rgb(0x72CC72);
        /// <summary>GPS route and waypoint purple.</summary>
        public static readonly Color Gps = Rgb(0xA64EDD);
        /// <summary>Breath bar.</summary>
        public static readonly Color Breath = Rgb(0x8FD3F4);
        /// <summary>Grey of a pulsing star.</summary>
        public static readonly Color StarGrey = Rgb(0x8C8C8C);
        /// <summary>Menu header banner.</summary>
        public static readonly Color Banner = Rgb(0x2D6EB4);
        /// <summary>Chirper blue.</summary>
        public static readonly Color Chirper = Rgb(0x55ACEE);

        private static readonly string[] CondensedFonts =
        {
            "Bahnschrift SemiBold Condensed", "Bahnschrift Condensed", "Arial Narrow", "Roboto Condensed", "Liberation Sans Narrow",
            "Ubuntu Condensed", "DejaVu Sans Condensed", "Nimbus Sans Narrow",
        };
        private static readonly string[] TitleFonts = { "Pricedown", "Georgia", "Times New Roman", "DejaVu Serif", "Liberation Serif" };

        private readonly GUIStyle _style = new GUIStyle();
        private readonly GUIContent _content = new GUIContent();
        private Material _colored;
        private Material _textured;

        /// <summary>Loads fonts and builds the atlas. Call from the main thread once Unity and CS1's UI exist.</summary>
        public HudStyle(HostLog log)
        {
            string name;
            Font = PickFont(CondensedFonts, out name);
            if (Font == null) Font = Cs1Font(out name);
            if (Font == null)
            {
                Font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                name = "Unity built-in Arial";
            }
            FontName = name;
            string title;
            TitleFont = PickFont(TitleFonts, out title) ?? Font;
            log.Info("hud: font " + FontName + ", title font " + (title ?? FontName));

            Atlas = new Texture2D(IconAtlas.Size, IconAtlas.Size, TextureFormat.RGBA32, false);
            Atlas.LoadRawTextureData(IconAtlas.Paint());
            Atlas.wrapMode = TextureWrapMode.Clamp;
            Atlas.filterMode = FilterMode.Bilinear;
            Atlas.Apply(false, true);

            _style.font = Font;
            _style.fontStyle = FontStyle.Bold;
            _style.wordWrap = false;
            _style.richText = false;
            _style.clipping = TextClipping.Overflow;
            Measurer = MeasureWidth;
        }

        /// <summary>The body font (condensed bold when the OS has one, else CS1's UI font, else Arial).</summary>
        public Font Font { get; private set; }
        /// <summary>Big-screen title font.</summary>
        public Font TitleFont { get; private set; }
        /// <summary>Which font was found (for the log and the report).</summary>
        public string FontName { get; private set; }
        /// <summary>The icon atlas.</summary>
        public Texture2D Atlas { get; private set; }
        /// <summary>Pixel scale of this frame (screen height / 1080 × HUD scale).</summary>
        public float Scale { get; set; }
        /// <summary>Font size <see cref="Measurer"/> measures at.</summary>
        public int MeasureSize { get; set; }
        /// <summary>Width of a string at <see cref="MeasureSize"/> (allocation-free delegate for <see cref="RichText"/>).</summary>
        public Func<string, float> Measurer { get; private set; }

        /// <summary>Vertex-coloured GL material (Hidden/Internal-Colored, alpha blended, no culling or depth).</summary>
        public Material Colored
        {
            get
            {
                if (_colored == null)
                {
                    _colored = GtaSkylines.Mod.Rendering.ColoredMaterial.Translucent("GtaSkylines.Hud");
                    if (_colored == null) return null;
                    _colored.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                }
                return _colored;
            }
        }

        /// <summary>Textured GL material (CS1's "UI/Default UI Shader", as UIView uses), or null when missing.</summary>
        public Material Textured
        {
            get
            {
                if (_textured == null)
                {
                    Shader s = Shader.Find("UI/Default UI Shader");
                    if (s == null) return null;
                    _textured = new Material(s);
                }
                return _textured;
            }
        }

        /// <summary>Scaled pixel size, at least 1.</summary>
        public int Px(float basePixels)
        {
            return Math.Max(1, (int)(basePixels * Scale + 0.5f));
        }

        /// <summary>Text size at the current scale and font size (pixels at 1080p).</summary>
        public Vector2 Measure(string text, float baseSize)
        {
            _style.font = Font;
            _style.fontSize = Px(baseSize);
            _content.text = text;
            return _style.CalcSize(_content);
        }

        private float MeasureWidth(string text)
        {
            _style.font = Font;
            _style.fontSize = MeasureSize;
            _content.text = text;
            return _style.CalcSize(_content).x;
        }

        /// <summary>
        /// Draws a single line of text with a dark outline (four diagonal copies) in <paramref name="colour"/>. Alpha
        /// multiplies everything. <paramref name="baseSize"/> is the font size at 1080p.
        /// </summary>
        public void Text(Rect r, string text, float baseSize, TextAnchor anchor, Color colour, float alpha)
        {
            TextPx(r, text, Px(baseSize), anchor, colour, alpha, Font);
        }

        /// <summary>As <see cref="Text"/> with the title font at an absolute pixel size.</summary>
        public void Title(Rect r, string text, int pixelSize, TextAnchor anchor, Color colour, float alpha)
        {
            TextPx(r, text, pixelSize, anchor, colour, alpha, TitleFont);
        }

        /// <summary>As <see cref="Text"/> without the outline (for text on solid panels).</summary>
        public void Plain(Rect r, string text, float baseSize, TextAnchor anchor, Color colour, float alpha)
        {
            if (string.IsNullOrEmpty(text) || alpha <= 0f) return;
            _style.font = Font;
            _style.fontSize = Px(baseSize);
            _style.alignment = anchor;
            _content.text = text;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            _style.normal.textColor = colour;
            GUI.Label(r, _content, _style);
            GUI.color = Color.white;
        }

        private void TextPx(Rect r, string text, int size, TextAnchor anchor, Color colour, float alpha, Font font)
        {
            if (string.IsNullOrEmpty(text) || alpha <= 0f) return;
            _style.font = font;
            _style.fontSize = size;
            _style.alignment = anchor;
            _content.text = text;
            float o = Math.Max(1f, Mathf.Round(size / 16f));
            GUI.color = new Color(1f, 1f, 1f, alpha * 0.85f);
            _style.normal.textColor = Color.black;
            GUI.Label(new Rect(r.x - o, r.y - o, r.width, r.height), _content, _style);
            GUI.Label(new Rect(r.x + o, r.y - o, r.width, r.height), _content, _style);
            GUI.Label(new Rect(r.x - o, r.y + o, r.width, r.height), _content, _style);
            GUI.Label(new Rect(r.x + o, r.y + o, r.width, r.height), _content, _style);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            _style.normal.textColor = colour;
            GUI.Label(r, _content, _style);
            GUI.color = Color.white;
        }

        /// <summary>Fills a rectangle with a colour (alpha in the colour).</summary>
        public void Fill(Rect r, Color c)
        {
            if (c.a <= 0f) return;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        /// <summary>Draws an atlas icon tinted with <paramref name="c"/>.</summary>
        public void Icon(Rect r, Icon icon, Color c)
        {
            if (c.a <= 0f) return;
            float x, y, w, h;
            IconAtlas.Uv(icon, out x, out y, out w, out h);
            GUI.color = c;
            GUI.DrawTextureWithTexCoords(r, Atlas, new Rect(x, y, w, h));
            GUI.color = Color.white;
        }

        /// <summary>Draws an atlas icon rotated clockwise by <paramref name="degrees"/> around its centre.</summary>
        public void IconRotated(Rect r, Icon icon, Color c, float degrees)
        {
            Matrix4x4 saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(degrees, r.center);
            Icon(r, icon, c);
            GUI.matrix = saved;
        }

        /// <summary>A GTA key cap: white rounded box with the key name in black.</summary>
        public void KeyCap(Rect r, string key, float baseSize, float alpha)
        {
            Fill(r, new Color(1f, 1f, 1f, 0.92f * alpha));
            Plain(r, key, baseSize, TextAnchor.MiddleCenter, Color.black, alpha);
        }

        /// <summary>Colour of a rich-text run.</summary>
        public static Color RunColour(RunStyle s)
        {
            switch (s)
            {
                case RunStyle.Place: return Mission;
                case RunStyle.Friendly: return Blue;
                case RunStyle.Target: return Red;
                case RunStyle.Goal: return Green;
                default: return Color.white;
            }
        }

        /// <summary>Draws laid-out rich text with its top-left at (x, y), each line offset by lineOffset(line) when given (centring).</summary>
        public void Runs(RichTextCache layout, float x, float y, float lineHeight, float baseSize, float alpha, bool outline, Func<int, float> lineOffset)
        {
            int size = Px(baseSize);
            for (int i = 0; i < layout.Runs.Count; i++)
            {
                TextRun run = layout.Runs[i];
                float lx = x + run.X + (lineOffset == null ? 0f : lineOffset(run.Line));
                var r = new Rect(lx, y + run.Line * lineHeight, run.Width + 4f, lineHeight);
                if (run.Style == RunStyle.Key)
                {
                    KeyCap(new Rect(r.x, r.y + lineHeight * 0.1f, run.Width, lineHeight * 0.8f), run.Text, baseSize * 0.85f, alpha);
                }
                else if (outline) TextPx(r, run.Text, size, TextAnchor.MiddleLeft, RunColour(run.Style), alpha, Font);
                else Plain(r, run.Text, baseSize, TextAnchor.MiddleLeft, RunColour(run.Style), alpha);
            }
        }

        /// <summary>Frees the atlas and materials.</summary>
        public void Dispose()
        {
            if (Atlas != null) UnityEngine.Object.Destroy(Atlas);
            if (_colored != null) UnityEngine.Object.Destroy(_colored);
            if (_textured != null) UnityEngine.Object.Destroy(_textured);
        }

        /// <summary>Colour from 0xRRGGBB.</summary>
        public static Color Rgb(int rgb)
        {
            return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);
        }

        /// <summary>The same colour with alpha <paramref name="a"/>.</summary>
        public static Color A(Color c, float a)
        {
            return new Color(c.r, c.g, c.b, a);
        }

        private static Font PickFont(string[] wanted, out string name)
        {
            name = null;
            string[] installed;
            try
            {
                installed = Font.GetOSInstalledFontNames();
            }
            catch (Exception)
            {
                return null;
            }
            if (installed == null) return null;
            foreach (string w in wanted)
                foreach (string have in installed)
                    if (string.Equals(w, have, StringComparison.OrdinalIgnoreCase))
                    {
                        name = have;
                        return Font.CreateDynamicFontFromOSFont(have, 32);
                    }
            return null;
        }

        private static Font Cs1Font(out string name)
        {
            name = null;
            UIView view = UIView.GetAView();
            UIFont ui = view == null ? null : view.defaultFont;
            Font f = ui == null ? null : ui.baseFont;
            if (f != null) name = "CS1 UI font " + f.name;
            return f;
        }
    }
}
