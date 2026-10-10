using System;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Roads;
using Skylines.Host;
using UnityEngine;

namespace GtaSkylines.Mod.Hud
{
    /// <summary>
    /// The city map texture shared by the minimap and the pause map: rasterised from the road graph and the city adapter
    /// (water, buildings, parks) at ~2 m per pixel around the road network by a <see cref="MapRasterBuilder"/>, a slice
    /// per frame and only when the road graph's version changes; the finished raster is uploaded in one go and the
    /// previous texture stays on screen meanwhile.
    /// </summary>
    public sealed class CityMapTexture : IDisposable
    {
        /// <summary>Target resolution, metres per pixel.</summary>
        public const float MetresPerPixel = 2f;
        /// <summary>Largest texture side.</summary>
        public const int MaxPixels = 2048;
        /// <summary><see cref="MapRasterBuilder"/> work units per frame.</summary>
        public const int WorkPerFrame = 40000;

        private readonly HostLog _log;
        private readonly RoadGraph _roads;
        private readonly MapRasterBuilder _builder;
        private int _failedVersion = -1;

        /// <summary>Creates the source; nothing is built until <see cref="Tick"/>.</summary>
        public CityMapTexture(HostLog log, RoadGraph roads, ICity city)
        {
            _log = log;
            _roads = roads;
            _builder = new MapRasterBuilder(roads, city, MetresPerPixel, MaxPixels);
        }

        /// <summary>The map, null until built.</summary>
        public Texture2D Texture { get; private set; }
        /// <summary>West edge, metres.</summary>
        public float MinX { get; private set; }
        /// <summary>South edge, metres.</summary>
        public float MinZ { get; private set; }
        /// <summary>Side, metres.</summary>
        public float Size { get; private set; }
        /// <summary>True once a texture exists.</summary>
        public bool Ready { get { return Texture != null; } }

        /// <summary>Advances the build by one frame's slice and uploads a finished raster. Main thread.</summary>
        public void Tick()
        {
            if (_roads == null || _roads.Version == _failedVersion) return;
            try
            {
                MapRaster r = _builder.Tick(WorkPerFrame);
                if (r != null) Upload(r);
            }
            catch (Exception e)
            {
                _failedVersion = _roads.Version;
                _log.Error("hud: city map", e);
            }
        }

        private void Upload(MapRaster r)
        {
            if (Texture == null || Texture.width != r.Pixels)
            {
                if (Texture != null) UnityEngine.Object.Destroy(Texture);
                Texture = new Texture2D(r.Pixels, r.Pixels, TextureFormat.RGBA32, false);
                Texture.wrapMode = TextureWrapMode.Clamp;
                Texture.filterMode = FilterMode.Bilinear;
            }
            Texture.LoadRawTextureData(r.Rgba);
            Texture.Apply(false, false);
            MinX = r.MinX;
            MinZ = r.MinZ;
            Size = r.Size;
            _log.Info("hud: city map " + r.Pixels + " px, " + r.MetresPerPixel.ToString("0.0") + " m/px, " + _builder.BuildingCount + " buildings");
        }

        /// <summary>Frees the texture.</summary>
        public void Dispose()
        {
            if (Texture != null) UnityEngine.Object.Destroy(Texture);
            Texture = null;
        }
    }
}
