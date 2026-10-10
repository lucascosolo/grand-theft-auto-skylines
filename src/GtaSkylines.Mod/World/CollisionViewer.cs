using System.Collections.Generic;
using Skylines.Core.Geometry;
using Skylines.Host;
using Skylines.Host.Geometry;
using UnityEngine;
using UnityEngine.Rendering;
using UInput = UnityEngine.Input;

namespace GtaSkylines.Mod.World
{
    /// <summary>
    /// Debug wireframe of the streamed collision (ported from the reference's CollisionViewer): Ctrl+Shift+F9 toggles
    /// GL lines of the regions within 32 m of the player (terrain green, road yellow, bridge red, building blue, trees
    /// dark green, props orange, tunnels magenta, area walls pale blue); hidden lines faint. Regions are copied only
    /// while on, so it costs nothing when off. Main thread only.
    /// </summary>
    public sealed class CollisionViewer
    {
        private const float DrawRadius = 32f;
        private const float HiddenAlpha = 0.25f;

        private struct Region
        {
            public float MinX, MinZ, MaxX, MaxZ;
            public float[] Positions;
            public ushort[] Flags;
        }

        private readonly HostLog _log;
        private readonly Dictionary<long, Region> _regions = new Dictionary<long, Region>();
        private Material _material;
        private Vector3 _player;
        private bool _on;

        internal CollisionViewer(HostLog log)
        {
            _log = log;
        }

        /// <summary>Whether the wireframe is drawn.</summary>
        public bool On
        {
            get { return _on; }
            set { SetOn(value); }
        }

        /// <summary>Per frame from Update: the Ctrl+Shift+F9 toggle.</summary>
        public void Update()
        {
            if (!UInput.GetKeyDown(KeyCode.F9)
                || !(UInput.GetKey(KeyCode.LeftControl) || UInput.GetKey(KeyCode.RightControl))
                || !(UInput.GetKey(KeyCode.LeftShift) || UInput.GetKey(KeyCode.RightShift))) return;
            SetOn(!_on);
            _log.Info("collision viewer " + (_on ? "on" : "off"));
        }

        internal void Track(Vector3 player)
        {
            _player = player;
        }

        internal void Store(long key, float minX, float minZ, float maxX, float maxZ, TriangleBuffer cs)
        {
            if (!_on) return;
            var r = new Region { MinX = minX, MinZ = minZ, MaxX = maxX, MaxZ = maxZ, Positions = new float[9 * cs.Count], Flags = new ushort[cs.Count] };
            System.Array.Copy(cs.Positions, r.Positions, r.Positions.Length);
            System.Array.Copy(cs.Flags, r.Flags, r.Flags.Length);
            _regions[key] = r;
        }

        internal void Remove(long key)
        {
            _regions.Remove(key);
        }

        internal void Clear()
        {
            _regions.Clear();
        }

        internal void Dispose()
        {
            SetOn(false);
            if (_material != null) Object.Destroy(_material);
            _material = null;
        }

        private void SetOn(bool on)
        {
            if (on == _on) return;
            _on = on;
            if (on)
            {
                if (_material == null)
                {
                    _material = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
                    _material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                    _material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                    _material.SetInt("_Cull", (int)CullMode.Off);
                    _material.SetInt("_ZWrite", 0);
                }
                // Loaded regions were not copied while off: rebuild those around the player so they appear.
                CollisionStreamer.RequestRebuild(_player.x, _player.z, DrawRadius);
                Camera.onPostRender += Draw;
            }
            else
            {
                Camera.onPostRender -= Draw;
                _regions.Clear();
            }
        }

        private void Draw(Camera cam)
        {
            try
            {
                if (cam != Camera.main) return;
                GL.PushMatrix();
                GL.MultMatrix(Matrix4x4.identity);
                Pass(CompareFunction.Greater, HiddenAlpha);
                Pass(CompareFunction.LessEqual, 1f);
                GL.PopMatrix();
            }
            catch (System.Exception e)
            {
                _log.Error("collision viewer draw", e);
                SetOn(false);
            }
        }

        private void Pass(CompareFunction ztest, float alpha)
        {
            _material.SetInt("_ZTest", (int)ztest);
            _material.SetPass(0);
            GL.Begin(GL.LINES);
            foreach (KeyValuePair<long, Region> kv in _regions)
            {
                Region r = kv.Value;
                if (Distance(r, _player) > DrawRadius) continue;
                float[] p = r.Positions;
                for (int i = 0; i < r.Flags.Length; i++)
                {
                    GL.Color(ColorOf(r.Flags[i], alpha));
                    int o = 9 * i;
                    Line(p, o, o + 3);
                    Line(p, o + 3, o + 6);
                    Line(p, o + 6, o);
                }
            }
            GL.End();
        }

        private static void Line(float[] p, int a, int b)
        {
            GL.Vertex3(p[a], p[a + 1], p[a + 2]);
            GL.Vertex3(p[b], p[b + 1], p[b + 2]);
        }

        private static Color ColorOf(ushort flags, float alpha)
        {
            if ((flags & AreaBoundary.BoundaryFlag) != 0) return new Color(0.6f, 0.9f, 1f, alpha);
            if ((flags & ObstacleGeometry.VegetationFlag) != 0) return new Color(0.05f, 0.45f, 0.1f, alpha);
            if ((flags & ObstacleGeometry.PropFlag) != 0) return new Color(1f, 0.55f, 0.05f, alpha);
            if ((flags & NetGeometry.TunnelFlag) != 0) return new Color(1f, 0.1f, 1f, alpha);
            if ((flags & BuildingGeometry.BuildingFlag) != 0) return new Color(0.15f, 0.4f, 1f, alpha);
            if ((flags & (NetGeometry.BridgeDeckFlag | NetGeometry.RailingFlag)) != 0) return new Color(1f, 0.15f, 0.1f, alpha);
            if ((flags & NetGeometry.RoadSurfaceFlag) != 0) return new Color(1f, 0.9f, 0.1f, alpha);
            if ((flags & 1) != 0) return new Color(0.2f, 0.9f, 0.2f, alpha);
            return new Color(1f, 1f, 1f, alpha);
        }

        private static float Distance(Region r, Vector3 p)
        {
            float dx = Mathf.Max(0f, Mathf.Max(r.MinX - p.x, p.x - r.MaxX));
            float dz = Mathf.Max(0f, Mathf.Max(r.MinZ - p.z, p.z - r.MaxZ));
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
