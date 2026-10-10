using System;

namespace GtaSkylines.Mod.Hud
{
    /// <summary>A screen rectangle in GUI pixels (origin top-left, y down). Unity-free.</summary>
    public struct HudRect
    {
        /// <summary>Left.</summary>
        public float X;
        /// <summary>Top.</summary>
        public float Y;
        /// <summary>Width.</summary>
        public float W;
        /// <summary>Height.</summary>
        public float H;

        /// <summary>Creates a rectangle.</summary>
        public HudRect(float x, float y, float w, float h)
        {
            X = x;
            Y = y;
            W = w;
            H = h;
        }

        /// <summary>Right edge.</summary>
        public float Right { get { return X + W; } }

        /// <summary>Bottom edge.</summary>
        public float Bottom { get { return Y + H; } }

        /// <summary>True when the point is inside [X, Right) × [Y, Bottom).</summary>
        public bool Contains(float x, float y)
        {
            return x >= X && x < X + W && y >= Y && y < Y + H;
        }
    }

    /// <summary>
    /// Where every HUD element sits for a screen size (GTA V PC layout, 1080p reference). Unity-free.
    /// <para>
    /// Scale = screenHeight / 1080 × hudScale (hudScale clamped to [0.5, 2]; non-finite → 1). Safe margins: SafeX =
    /// 0.03 × width, SafeY = 0.03 × height (not scaled by hudScale). Minimap: height = 0.18 × screenHeight × hudScale
    /// (× 2 when <c>bigMap</c>), width = 1.55 × height, left = SafeX, bottom = screenHeight − SafeY − BarGap − BarHeight
    /// where BarHeight = 9 × Scale and BarGap = 4 × Scale. Health bar under it: left = minimap left, width = 0.55 ×
    /// minimap width − BarGap/2; armour bar: right-aligned to the minimap's right, width = 0.45 × minimap width −
    /// BarGap/2; both at y = minimap bottom + BarGap, height BarHeight. Breath bar: the full minimap width, BarHeight / 2
    /// tall, directly above the minimap (bottom = minimap top − BarGap).
    /// </para>
    /// <para>
    /// TopRight = (width − SafeX, SafeY): the stars/cash/weapon column is right-aligned to it. HelpBox: origin (SafeX,
    /// SafeY), max width 0.4 × width. BottomCentreY = height − SafeY − 40 × Scale: baseline of the objective line;
    /// subtitles sit above it. BottomRight = (width − SafeX, height − SafeY). Notifications: left = SafeX, bottom = the
    /// breath bar's top − BarGap, width = minimap width.
    /// </para>
    /// </summary>
    public struct HudLayout
    {
        /// <summary>Reference screen height.</summary>
        public const float ReferenceHeight = 1080f;
        /// <summary>Minimap aspect (width ÷ height).</summary>
        public const float MinimapAspect = 1.55f;

        /// <summary>Screen width in GUI pixels.</summary>
        public float ScreenW;
        /// <summary>Screen height in GUI pixels.</summary>
        public float ScreenH;
        /// <summary>Pixel scale (1 at 1080p and HUD scale 1).</summary>
        public float Scale;
        /// <summary>Left/right safe margin.</summary>
        public float SafeX;
        /// <summary>Top/bottom safe margin.</summary>
        public float SafeY;
        /// <summary>Health/armour bar height.</summary>
        public float BarHeight;
        /// <summary>Gap between minimap and bars.</summary>
        public float BarGap;
        /// <summary>Minimap.</summary>
        public HudRect Minimap;
        /// <summary>Health bar.</summary>
        public HudRect HealthBar;
        /// <summary>Armour bar.</summary>
        public HudRect ArmourBar;
        /// <summary>Breath bar.</summary>
        public HudRect BreathBar;
        /// <summary>Notification column (its Y is the bottom; it grows upward; H = 0).</summary>
        public HudRect Notifications;
        /// <summary>Top-right anchor x.</summary>
        public float TopRightX;
        /// <summary>Top-right anchor y.</summary>
        public float TopRightY;
        /// <summary>Help box left.</summary>
        public float HelpX;
        /// <summary>Help box top.</summary>
        public float HelpY;
        /// <summary>Help box maximum width.</summary>
        public float HelpMaxW;
        /// <summary>Objective baseline y.</summary>
        public float BottomCentreY;
        /// <summary>Bottom-right anchor x.</summary>
        public float BottomRightX;
        /// <summary>Bottom-right anchor y.</summary>
        public float BottomRightY;

        /// <summary>Lays out the HUD for a <paramref name="width"/> × <paramref name="height"/> screen.</summary>
        public static HudLayout Compute(float width, float height, float hudScale, bool bigMap)
        {
            float hs = float.IsNaN(hudScale) || float.IsInfinity(hudScale) ? 1f : Math.Max(0.5f, Math.Min(2f, hudScale));
            var l = new HudLayout();
            l.ScreenW = width;
            l.ScreenH = height;
            l.Scale = height / ReferenceHeight * hs;
            l.SafeX = 0.03f * width;
            l.SafeY = 0.03f * height;
            l.BarHeight = 9f * l.Scale;
            l.BarGap = 4f * l.Scale;
            float mmH = 0.18f * height * hs * (bigMap ? 2f : 1f);
            float mmW = MinimapAspect * mmH;
            float bottom = height - l.SafeY - l.BarGap - l.BarHeight;
            l.Minimap = new HudRect(l.SafeX, bottom - mmH, mmW, mmH);
            float armW = 0.45f * mmW - l.BarGap / 2f;
            l.HealthBar = new HudRect(l.SafeX, bottom + l.BarGap, 0.55f * mmW - l.BarGap / 2f, l.BarHeight);
            l.ArmourBar = new HudRect(l.SafeX + mmW - armW, bottom + l.BarGap, armW, l.BarHeight);
            l.BreathBar = new HudRect(l.SafeX, l.Minimap.Y - l.BarGap - l.BarHeight / 2f, mmW, l.BarHeight / 2f);
            l.Notifications = new HudRect(l.SafeX, l.BreathBar.Y - l.BarGap, mmW, 0f);
            l.TopRightX = width - l.SafeX;
            l.TopRightY = l.SafeY;
            l.HelpX = l.SafeX;
            l.HelpY = l.SafeY;
            l.HelpMaxW = 0.4f * width;
            l.BottomCentreY = height - l.SafeY - 40f * l.Scale;
            l.BottomRightX = width - l.SafeX;
            l.BottomRightY = height - l.SafeY;
            return l;
        }
    }
}
