using System;
using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Roads;
using Skylines.Host;
using UnityEngine;

namespace GtaSkylines.Mod.Hud
{
    /// <summary>
    /// The city map texture shared by the minimap and the pause map: rasterised from the road graph and the city adapter
    /// (water, buildings, parks) at ~2 m per pixel around the road network, rebuilt when the road graph's version changes.
    /// </summary>
    public sealed class CityMapTexture : IDisposable
    {
        /// <summary>Target resolution, metres per pixel.</summary>
        public const float MetresPerPixel = 2f;
        /// <summary>Largest texture side.</summary>
        public const int MaxPixels = 2048;
        /// <summary>Highest CS1 segment id probed (ushort range).</summary>
        private const int SegmentIds = 65536;

        private readonly HostLog _log;
        private readonly RoadGraph _roads;
        private readonly ICity _city;
        private readonly List<ushort> _buildings = new List<ushort>();
        private int _builtVersion = -1;

        /// <summary>Creates the source; nothing is built until <see cref="Tick"/>.</summary>
        public CityMapTexture(HostLog log, RoadGraph roads, ICity city)
        {
            _log = log;
            _roads = roads;
            _city = city;
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

        /// <summary>Rebuilds when the road graph changed (cheap int compare otherwise). Main thread.</summary>
        public void Tick()
        {
            if (_roads == null || _roads.Version == _builtVersion || _roads.SegmentCount == 0) return;
            _builtVersion = _roads.Version;
            try
            {
                Rebuild();
            }
            catch (Exception e)
            {
                _log.Error("hud: city map", e);
            }
        }

        private void Rebuild()
        {
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            RoadSegment sg;
            for (int id = 0; id < SegmentIds; id++)
            {
                if (!_roads.TryGetSegment(id, out sg)) continue;
                Grow(sg.A, ref minX, ref minZ, ref maxX, ref maxZ);
                Grow(sg.D, ref minX, ref minZ, ref maxX, ref maxZ);
            }
            if (minX > maxX) return;
            MapRaster r = MapRaster.ForArea(minX, minZ, maxX, maxZ, 300f, MetresPerPixel, MaxPixels);
            r.Clear(MapStyle.Land);

            if (_city != null)
            {
                ICity city = _city;
                r.FillCellsWhere((x, z) => { float y; return city.TryWaterSurface(x, z, out y); }, 4, MapStyle.Water);
                _buildings.Clear();
                float half = r.Size * 0.5f;
                city.BuildingsNear(new Vec3(r.MinX + half, 0f, r.MinZ + half), half * 1.4143f, _buildings);
                BuildingDescriptor b;
                for (int i = 0; i < _buildings.Count; i++)
                    if (city.TryGetBuilding(_buildings[i], out b) && b != null)
                        r.FillRect(b.Position.X, b.Position.Z, b.YawDeg, b.Width, b.Length,
                            b.Service == ServiceKind.Park ? MapStyle.Park : MapStyle.Building);
            }

            // Paths, then roads, then highways on top.
            for (int pass = 0; pass < 3; pass++)
                for (int id = 0; id < SegmentIds; id++)
                {
                    if (!_roads.TryGetSegment(id, out sg)) continue;
                    bool ped = (sg.Flags & RoadFlags.PedestrianOnly) != 0;
                    bool hw = (sg.Flags & RoadFlags.Highway) != 0;
                    int want = ped ? 0 : hw ? 2 : 1;
                    if (want != pass) continue;
                    float w = MapStyle.RoadWidth(sg.ForwardLanes, sg.BackwardLanes, sg.LaneWidth, ped);
                    uint c = ped ? MapStyle.Path : hw ? MapStyle.Highway : MapStyle.Road;
                    r.DrawBezier(sg.A.X, sg.A.Z, sg.B.X, sg.B.Z, sg.C.X, sg.C.Z, sg.D.X, sg.D.Z, w, 8f, c);
                }

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
            _log.Info("hud: city map " + r.Pixels + " px, " + r.MetresPerPixel.ToString("0.0") + " m/px, " + _buildings.Count + " buildings");
        }

        private static void Grow(Vec3 p, ref float minX, ref float minZ, ref float maxX, ref float maxZ)
        {
            if (p.X < minX) minX = p.X;
            if (p.Z < minZ) minZ = p.Z;
            if (p.X > maxX) maxX = p.X;
            if (p.Z > maxZ) maxZ = p.Z;
        }

        /// <summary>Frees the texture.</summary>
        public void Dispose()
        {
            if (Texture != null) UnityEngine.Object.Destroy(Texture);
            Texture = null;
        }
    }
}
