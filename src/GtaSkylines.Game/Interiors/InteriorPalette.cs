using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Game.Interiors
{
    /// <summary>
    /// Chooses an interior's colours once (seeded) and registers texture recipes on demand, deduplicated, at most
    /// <see cref="MaxRecipes"/> per layout.
    /// </summary>
    internal sealed class InteriorPalette
    {
        internal const int MaxRecipes = 32;

        private static readonly int[] PaintWarm = { 0xE8DCC8, 0xF1E6D2, 0xD9C7A7, 0xEDE3D1, 0xCFC1A9 };
        private static readonly int[] PaintCool = { 0xD4DCE2, 0xC9D6D0, 0xE0E4E8, 0xB8C6CF, 0xDDE6DE };
        private static readonly int[] PaintBold = { 0x8C3B3B, 0x3B5C8C, 0x4F7A4A, 0x7A5C3B, 0x5C4A7A };
        private static readonly int[] WoodTones = { 0x8B5A2B, 0xA0703C, 0x6B4423, 0xB98A55, 0x5A3A1E };
        private static readonly int[] FabricTones = { 0x7A2E2E, 0x2E4A7A, 0x3E6B3E, 0x8A7A5A, 0x555A66, 0x6B3E6B, 0xB0703A };
        private static readonly int[] CarpetTones = { 0x5A6070, 0x4A5A6A, 0x6A5A50, 0x3E4E5E, 0x707070 };
        private static readonly int[] TileTones = { 0xE6E6E0, 0xD8D2C4, 0xC8D4D8, 0xEDE8DC };

        private readonly InteriorLayout _layout;
        private readonly int _paint, _accentPaint, _wood, _woodAlt, _fabric, _fabricAlt, _carpet, _tile;

        internal InteriorPalette(InteriorLayout layout, Rng rng)
        {
            _layout = layout;
            _paint = rng.Chance(0.5f) ? rng.Pick(PaintWarm) : rng.Pick(PaintCool);
            _accentPaint = rng.Pick(PaintBold);
            _wood = rng.Pick(WoodTones);
            _woodAlt = rng.Pick(WoodTones);
            _fabric = rng.Pick(FabricTones);
            _fabricAlt = rng.Pick(FabricTones);
            _carpet = rng.Pick(CarpetTones);
            _tile = rng.Pick(TileTones);
        }

        /// <summary>Registers a recipe (or finds its twin) and returns its index; past the cap, the closest existing one.</summary>
        internal int Add(TexturePattern pattern, int baseRgb, int accentRgb, float repeatMetres, bool emissive)
        {
            var r = new TextureRecipe { Pattern = pattern, BaseRgb = baseRgb, AccentRgb = accentRgb, RepeatMetres = repeatMetres, Emissive = emissive };
            int i = _layout.Recipes.IndexOf(r);
            if (i >= 0) return i;
            if (_layout.Recipes.Count < MaxRecipes)
            {
                _layout.Recipes.Add(r);
                return _layout.Recipes.Count - 1;
            }
            // Over the cap: reuse the most similar recipe of the same pattern, else the first.
            int best = 0, bestScore = int.MaxValue;
            for (int k = 0; k < _layout.Recipes.Count; k++)
            {
                TextureRecipe o = _layout.Recipes[k];
                int score = (o.Pattern == pattern ? 0 : 1000) + (o.Emissive == emissive ? 0 : 2000) + ColourDistance(o.BaseRgb, baseRgb);
                if (score < bestScore) { bestScore = score; best = k; }
            }
            return best;
        }

        private static int ColourDistance(int a, int b)
        {
            int dr = ((a >> 16) & 0xFF) - ((b >> 16) & 0xFF), dg = ((a >> 8) & 0xFF) - ((b >> 8) & 0xFF), db = (a & 0xFF) - (b & 0xFF);
            return (dr < 0 ? -dr : dr) + (dg < 0 ? -dg : dg) + (db < 0 ? -db : db);
        }

        internal int Ceiling { get { return Add(TexturePattern.Plain, 0xF2F0EA, 0xE0DED8, 2f, false); } }
        internal int LightPanel { get { return Add(TexturePattern.LightPanel, 0xFFF8E8, 0xD8D8D8, 1f, true); } }
        internal int WindowSky { get { return Add(TexturePattern.WindowSky, 0x9CC7E8, 0xE8F2FA, 1f, true); } }
        internal int Screen { get { return Add(TexturePattern.Plain, 0x3A6EA8, 0x1A2E48, 1f, true); } }
        internal int SignRed { get { return Add(TexturePattern.Plain, 0xD83A2A, 0xD83A2A, 1f, true); } }
        internal int Wood { get { return Add(TexturePattern.Planks, _wood, Darken(_wood), 1.5f, false); } }
        internal int WoodAlt { get { return Add(TexturePattern.Planks, _woodAlt, Darken(_woodAlt), 1.5f, false); } }
        internal int Fabric { get { return Add(TexturePattern.Plain, _fabric, Darken(_fabric), 1f, false); } }
        internal int FabricAlt { get { return Add(TexturePattern.Plain, _fabricAlt, Darken(_fabricAlt), 1f, false); } }
        internal int FabricWhite { get { return Add(TexturePattern.Plain, 0xEDEDE8, 0xD0D0CC, 1f, false); } }
        internal int Metal { get { return Add(TexturePattern.Brushed, 0xA8ACB0, 0x8A8E92, 1f, false); } }
        internal int MetalDark { get { return Add(TexturePattern.Brushed, 0x4A4E54, 0x3A3E44, 1f, false); } }
        internal int RackOrange { get { return Add(TexturePattern.Plain, 0xD8742A, 0xB05A1E, 1f, false); } }
        internal int Glass { get { return Add(TexturePattern.Plain, 0xBFD8E0, 0xA8C8D0, 1f, false); } }
        internal int PlasticWhite { get { return Add(TexturePattern.Plain, 0xF0F0EC, 0xDCDCD8, 1f, false); } }
        internal int PlasticDark { get { return Add(TexturePattern.Plain, 0x2A2A2E, 0x1E1E22, 1f, false); } }
        internal int Accent { get { return Add(TexturePattern.Plain, _accentPaint, Darken(_accentPaint), 1f, false); } }
        internal int Leaves { get { return Add(TexturePattern.Plain, 0x3E7A34, 0x2E5A26, 1f, false); } }
        internal int Terracotta { get { return Add(TexturePattern.Plain, 0xB0603A, 0x8A4A2C, 1f, false); } }
        internal int Goods { get { return Add(TexturePattern.Stripes, 0xD84A3A, 0x3A7AD8, 0.5f, false); } }
        internal int Cardboard { get { return Add(TexturePattern.Plain, 0xB8915E, 0x9A7848, 1f, false); } }

        /// <summary>Floor recipe for a room kind.</summary>
        internal int Floor(RoomKind kind, InteriorArchetype archetype)
        {
            switch (kind)
            {
                case RoomKind.LivingRoom:
                case RoomKind.Bedroom:
                    return Add(TexturePattern.Planks, _wood, Darken(_wood), 2f, false);
                case RoomKind.Kitchen:
                case RoomKind.Bathroom:
                case RoomKind.Restroom:
                case RoomKind.LockerRoom:
                    return Add(TexturePattern.Tiles, _tile, 0x9A968C, 0.6f, false);
                case RoomKind.ShopFloor:
                case RoomKind.Lobby:
                case RoomKind.Corridor:
                case RoomKind.Ward:
                case RoomKind.TreatmentRoom:
                case RoomKind.ControlRoom:
                    return archetype == InteriorArchetype.House || archetype == InteriorArchetype.Apartment
                        ? Add(TexturePattern.Planks, _wood, Darken(_wood), 2f, false)
                        : Add(TexturePattern.Tiles, 0xD8D8D4, 0xA8A8A4, 1.2f, false);
                case RoomKind.DiningRoom:
                    return Add(TexturePattern.Checker, 0xE8E2D2, 0x3A3A3A, 1.2f, false);
                case RoomKind.OfficeRoom:
                case RoomKind.MeetingRoom:
                case RoomKind.Classroom:
                case RoomKind.Bunks:
                    return Add(TexturePattern.Carpet, _carpet, Darken(_carpet), 2f, false);
                default:
                    return Add(TexturePattern.Concrete, 0x9A9A96, 0x7E7E7A, 3f, false);
            }
        }

        /// <summary>Wall recipe for a room kind.</summary>
        internal int Wall(RoomKind kind, InteriorArchetype archetype)
        {
            switch (kind)
            {
                case RoomKind.Kitchen:
                case RoomKind.Bathroom:
                case RoomKind.Restroom:
                    return Add(TexturePattern.Tiles, 0xF0F0EC, 0xC8C8C4, 0.3f, false);
                case RoomKind.Cells:
                case RoomKind.WarehouseFloor:
                case RoomKind.Workshop:
                case RoomKind.VehicleBay:
                case RoomKind.StockRoom:
                case RoomKind.Armory:
                    return Add(TexturePattern.Bricks, 0xA8A49A, 0x7A766E, 2f, false);
                case RoomKind.DiningRoom:
                    return Add(TexturePattern.Bricks, 0x9A4A32, 0x6A3222, 2f, false);
                case RoomKind.Bedroom:
                case RoomKind.LivingRoom:
                    return archetype == InteriorArchetype.House ? Add(TexturePattern.Stripes, _paint, Darken(_paint), 1f, false) : Add(TexturePattern.Plain, _paint, Darken(_paint), 2f, false);
                default:
                    return Add(TexturePattern.Plain, _paint, Darken(_paint), 2f, false);
            }
        }

        private static int Darken(int rgb)
        {
            int r = ((rgb >> 16) & 0xFF) * 3 / 4, g = ((rgb >> 8) & 0xFF) * 3 / 4, b = (rgb & 0xFF) * 3 / 4;
            return (r << 16) | (g << 8) | b;
        }
    }
}
