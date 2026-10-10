using System;
using System.Collections.Generic;
using GtaSkylines.Game.Hud;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.World;
using UnityEngine;

namespace GtaSkylines.Mod.Hud
{
    /// <summary>
    /// The minimap. Each repaint a GL pass into a RenderTexture draws the city map rotated and scaled around the player
    /// (textured quad, CS1's UI shader), then the GPS route and search zone (vertex-coloured triangles); IMGUI then draws
    /// that texture's used corner (the texture only grows, so the animated enlarge does not recreate it), the blips (edge-clamped when far) and the player arrow on top.
    /// </summary>
    public sealed class MinimapRenderer : IDisposable
    {
        private static readonly Color Land = new Color(0.227f, 0.247f, 0.267f, 1f);
        private RenderTexture _rt;
        private MinimapView _view;

        /// <summary>Draws the minimap into <paramref name="rect"/>. Call from OnGUI (only Repaint draws).</summary>
        public void Draw(HudStyle st, CityMapTexture map, HudModel m, Rect rect, bool northUp, float time, bool hasWaypoint, float wx, float wz)
        {
            if (Event.current.type != EventType.Repaint) return;
            int w = Math.Max(8, (int)rect.width), h = Math.Max(8, (int)rect.height);
            // Grow-only: the animated enlarge (and back) draws into a sub-viewport instead of recreating the texture per size.
            if (_rt == null || _rt.width < w || _rt.height < h)
            {
                int cw = _rt == null ? w : Math.Max(w, _rt.width), ch = _rt == null ? h : Math.Max(h, _rt.height);
                Release();
                _rt = new RenderTexture(cw, ch, 0, RenderTextureFormat.ARGB32);
                _rt.Create();
            }
            _view.CentreX = m.PlayerPosition.X;
            _view.CentreZ = m.PlayerPosition.Z;
            _view.YawDeg = northUp ? 0f : m.MapRotationDeg;
            _view.Radius = m.MinimapRadius > 1f ? m.MinimapRadius : HudModel.MinimapRadiusMin;
            _view.W = w;
            _view.H = h;
            _view.AnchorX = 0.5f;
            _view.AnchorY = MinimapMath.PlayerAnchorY;

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _rt;
            GL.Viewport(new Rect(0f, 0f, w, h));
            GL.PushMatrix();
            GL.LoadPixelMatrix(0f, w, h, 0f);
            GL.Clear(false, true, Land);
            Material tex = st.Textured;
            if (map != null && map.Ready && tex != null)
            {
                tex.mainTexture = map.Texture;
                tex.SetPass(0);
                GL.Begin(GL.QUADS);
                GL.Color(m.InInterior ? new Color(0.45f, 0.45f, 0.45f, 1f) : Color.white);
                Corner(map, 0f, 0f);
                Corner(map, w, 0f);
                Corner(map, w, h);
                Corner(map, 0f, h);
                GL.End();
            }
            Material col = st.Colored;
            if (col != null)
            {
                col.SetPass(0);
                GL.Begin(GL.QUADS);
                if (m.ShowSearchZone && m.SearchRadius > 0f) SearchZone(m, time);
                Route(m.GpsPoints, st.Px(5f));
                GL.End();
            }
            GL.PopMatrix();
            RenderTexture.active = prev;

            float b = st.Px(3f);
            st.Fill(new Rect(rect.x - b, rect.y - b, rect.width + 2 * b, rect.height + 2 * b), new Color(0f, 0f, 0f, 0.55f));
            GUI.DrawTextureWithTexCoords(rect, _rt, new Rect(0f, 0f, w / (float)_rt.width, h / (float)_rt.height));

            float icon = st.Px(16f);
            bool blink = (time * 2f) % 1f < 0.6f;
            List<HudBlip> blips = m.Blips;
            for (int i = 0; i < blips.Count; i++)
            {
                HudBlip bl = blips[i];
                if (bl.Kind == BlipKind.Player || (bl.Flashing && !blink)) continue;
                Blip(st, rect, bl.Kind, bl.Position.X, bl.Position.Z, icon, time);
            }
            if (hasWaypoint) Blip(st, rect, BlipKind.Waypoint, wx, wz, icon, time);

            // North marker on the edge, then the player arrow on top of everything.
            float nx, ny;
            MinimapMath.WorldToView(ref _view, _view.CentreX, _view.CentreZ + 100000f, out nx, out ny);
            MinimapMath.ClampToEdge(ref _view, icon * 0.6f, ref nx, ref ny);
            st.Text(new Rect(rect.x + nx - icon, rect.y + ny - icon, icon * 2f, icon * 2f), "N", 15f, TextAnchor.MiddleCenter, Color.white, 1f);
            float px, py;
            MinimapMath.WorldToView(ref _view, m.PlayerPosition.X, m.PlayerPosition.Z, out px, out py);
            float playerYaw = 0f;
            for (int i = 0; i < blips.Count; i++)
                if (blips[i].Kind == BlipKind.Player) { playerYaw = blips[i].YawDeg; break; }
            float a = st.Px(18f);
            st.IconRotated(new Rect(rect.x + px - a / 2, rect.y + py - a / 2, a, a), Icon.Arrow, Color.white,
                MinimapMath.ScreenAngle(ref _view, playerYaw));
        }

        private void Corner(CityMapTexture map, float px, float py)
        {
            float x, z, u, v;
            MinimapMath.ViewToWorld(ref _view, px, py, out x, out z);
            MinimapMath.WorldToTexture(map.MinX, map.MinZ, map.Size, x, z, out u, out v);
            GL.TexCoord2(u, v);
            GL.Vertex3(px, py, 0f);
        }

        private void Route(List<Vec3> pts, float width)
        {
            if (pts.Count < 2) return;
            GL.Color(HudStyle.Gps);
            float hw = width * 0.5f;
            float ax, ay;
            MinimapMath.WorldToView(ref _view, pts[0].X, pts[0].Z, out ax, out ay);
            for (int i = 1; i < pts.Count; i++)
            {
                float bx, by;
                MinimapMath.WorldToView(ref _view, pts[i].X, pts[i].Z, out bx, out by);
                float dx = bx - ax, dy = by - ay, len = (float)Math.Sqrt(dx * dx + dy * dy);
                if (len > 0.01f)
                {
                    // Extend each piece by half a width so consecutive pieces overlap at the joints.
                    float ux = dx / len * hw, uy = dy / len * hw, nx = -uy, ny = ux;
                    GL.Vertex3(ax - ux + nx, ay - uy + ny, 0f);
                    GL.Vertex3(bx + ux + nx, by + uy + ny, 0f);
                    GL.Vertex3(bx + ux - nx, by + uy - ny, 0f);
                    GL.Vertex3(ax - ux - nx, ay - uy - ny, 0f);
                }
                ax = bx;
                ay = by;
            }
        }

        private void SearchZone(HudModel m, float time)
        {
            float cx, cy;
            MinimapMath.WorldToView(ref _view, m.SearchCentre.X, m.SearchCentre.Z, out cx, out cy);
            float r = m.SearchRadius * (_view.H * 0.5f) / _view.Radius;
            bool red = (time * 2f) % 1f < 0.5f;
            Color c = red ? HudStyle.Red : HudStyle.Blue;
            const int n = 40;
            for (int i = 0; i < n; i++)
            {
                float a0 = i * 2f * Mathf.PI / n, a1 = (i + 1) * 2f * Mathf.PI / n;
                float c0 = Mathf.Cos(a0), s0 = Mathf.Sin(a0), c1 = Mathf.Cos(a1), s1 = Mathf.Sin(a1);
                GL.Color(new Color(c.r, c.g, c.b, 0.28f));
                GL.Vertex3(cx, cy, 0f);
                GL.Vertex3(cx + c0 * r, cy + s0 * r, 0f);
                GL.Vertex3(cx + c1 * r, cy + s1 * r, 0f);
                GL.Vertex3(cx, cy, 0f);
                GL.Color(new Color(c.r, c.g, c.b, 0.8f));
                float ri = r - 2f;
                GL.Vertex3(cx + c0 * ri, cy + s0 * ri, 0f);
                GL.Vertex3(cx + c0 * r, cy + s0 * r, 0f);
                GL.Vertex3(cx + c1 * r, cy + s1 * r, 0f);
                GL.Vertex3(cx + c1 * ri, cy + s1 * ri, 0f);
            }
        }

        private void Blip(HudStyle st, Rect rect, BlipKind kind, float x, float z, float size, float time)
        {
            float px, py;
            MinimapMath.WorldToView(ref _view, x, z, out px, out py);
            bool clamps = BlipStyle.ClampsToEdge(kind);
            bool clamped = MinimapMath.ClampToEdge(ref _view, size * 0.5f, ref px, ref py);
            if (clamped && !clamps) return;
            float s = kind == BlipKind.Cop ? size * 0.55f : clamped ? size * 0.8f : size;
            Color c = BlipStyle.Colour(kind, time);
            var r = new Rect(rect.x + px - s / 2, rect.y + py - s / 2, s, s);
            if (kind != BlipKind.Cop) st.Icon(new Rect(r.x - 1.5f, r.y - 1.5f, s + 3f, s + 3f), BlipStyle.Shape(kind), new Color(0f, 0f, 0f, 0.7f));
            st.Icon(r, BlipStyle.Shape(kind), c);
        }

        /// <summary>Frees the render texture.</summary>
        public void Dispose()
        {
            Release();
        }

        private void Release()
        {
            if (_rt == null) return;
            _rt.Release();
            UnityEngine.Object.Destroy(_rt);
            _rt = null;
        }
    }

    /// <summary>Blip icons and colours (GTA V conventions).</summary>
    public static class BlipStyle
    {
        /// <summary>Icon of a blip kind.</summary>
        public static Icon Shape(BlipKind kind)
        {
            switch (kind)
            {
                case BlipKind.Waypoint: return Icon.Diamond;
                case BlipKind.Shop: return Icon.Bag;
                case BlipKind.Safehouse: return Icon.House;
                case BlipKind.Checkpoint: return Icon.Ring;
                case BlipKind.Player: return Icon.Arrow;
                default: return Icon.Disc;
            }
        }

        /// <summary>Colour of a blip kind; cops alternate red/blue at 4 Hz.</summary>
        public static Color Colour(BlipKind kind, float time)
        {
            switch (kind)
            {
                case BlipKind.Waypoint: return HudStyle.Gps;
                case BlipKind.MissionStart:
                case BlipKind.Destination:
                case BlipKind.Checkpoint: return HudStyle.Mission;
                case BlipKind.Target: return HudStyle.Red;
                case BlipKind.Friendly:
                case BlipKind.Job: return HudStyle.Blue;
                case BlipKind.Shop: return HudStyle.Green;
                case BlipKind.Cop: return (time * 4f) % 1f < 0.5f ? HudStyle.Red : HudStyle.Blue;
                default: return Color.white;
            }
        }

        /// <summary>Kinds that stay on the minimap edge when far (objectives); the rest hide outside it.</summary>
        public static bool ClampsToEdge(BlipKind kind)
        {
            switch (kind)
            {
                case BlipKind.Waypoint:
                case BlipKind.MissionStart:
                case BlipKind.Destination:
                case BlipKind.Target:
                case BlipKind.Friendly:
                case BlipKind.Job:
                case BlipKind.Checkpoint: return true;
                default: return false;
            }
        }
    }
}
