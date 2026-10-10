using System;
using System.Collections.Generic;
using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Game.Interiors
{
    /// <summary>
    /// Turns a furnished layout into render geometry (one accumulator per texture recipe, flat-shaded quads with planar
    /// UVs) and collision triangles (floors, ceilings, wall faces with open doorways, solid furniture boxes).
    /// </summary>
    internal sealed class InteriorMeshBuilder
    {
        private const float T = InteriorLayout.WallThickness;
        private const int MaxVertices = 65000;

        private sealed class Acc
        {
            public MaterialSlot Slot;
            public int Recipe;
            public readonly List<float> P = new List<float>();
            public readonly List<float> N = new List<float>();
            public readonly List<float> U = new List<float>();
            public readonly List<int> I = new List<int>();
        }

        private readonly InteriorLayout _l;
        private readonly InteriorPalette _pal;
        private readonly uint _seed;
        private readonly Dictionary<int, Acc> _acc = new Dictionary<int, Acc>();
        private readonly List<int> _order = new List<int>();
        private readonly List<float> _col = new List<float>();

        private InteriorMeshBuilder(InteriorLayout layout, InteriorPalette palette, uint seed)
        {
            _l = layout;
            _pal = palette;
            _seed = seed;
        }

        internal static void Build(InteriorLayout layout, InteriorPalette palette, uint seed)
        {
            var b = new InteriorMeshBuilder(layout, palette, seed);
            b.Run();
        }

        private void Run()
        {
            float h = _l.CeilingHeight;
            foreach (Room r in _l.Rooms)
            {
                Rect2 b = r.Bounds;
                Quad(MaterialSlot.Floor, r.FloorRecipe, new Vec3(b.MinX, 0f, b.MinZ), new Vec3(b.MinX, 0f, b.MaxZ), new Vec3(b.MaxX, 0f, b.MaxZ), new Vec3(b.MaxX, 0f, b.MinZ), Vec3.Up, true);
                Quad(MaterialSlot.Ceiling, _pal.Ceiling, new Vec3(b.MinX, h, b.MinZ), new Vec3(b.MinX, h, b.MaxZ), new Vec3(b.MaxX, h, b.MaxZ), new Vec3(b.MaxX, h, b.MinZ), -Vec3.Up, true);
                Walls(r);
                Lights(r);
                Windows(r);
            }
            foreach (Doorway d in _l.Doorways) Jambs(d);
            Doors();
            for (int i = 0; i < _l.Furniture.Count; i++) Furniture(_l.Furniture[i], i);
            Emit();
        }

        // ------------------------------------------------------------------------------------------ accumulation

        private Acc Get(MaterialSlot slot, int recipe)
        {
            Acc a;
            if (!_acc.TryGetValue(recipe, out a))
            {
                a = new Acc { Slot = slot, Recipe = recipe };
                _acc.Add(recipe, a);
                _order.Add(recipe);
            }
            return a;
        }

        /// <summary>Adds a quad (corners in order around it) facing <paramref name="n"/>; optionally to collision too.</summary>
        private void Quad(MaterialSlot slot, int recipe, Vec3 p0, Vec3 p1, Vec3 p2, Vec3 p3, Vec3 n, bool collide)
        {
            if (Vec3.Dot(Vec3.Cross(p1 - p0, p2 - p0), n) < 0f)
            {
                Vec3 t = p1; p1 = p3; p3 = t;
            }
            Acc a = Get(slot, recipe);
            float rep = Math.Max(0.05f, _l.Recipes[recipe].RepeatMetres);
            int baseIndex = a.P.Count / 3;
            Vert(a, p0, n, rep); Vert(a, p1, n, rep); Vert(a, p2, n, rep); Vert(a, p3, n, rep);
            a.I.Add(baseIndex); a.I.Add(baseIndex + 1); a.I.Add(baseIndex + 2);
            a.I.Add(baseIndex); a.I.Add(baseIndex + 2); a.I.Add(baseIndex + 3);
            if (collide)
            {
                Tri(p0, p1, p2);
                Tri(p0, p2, p3);
            }
        }

        private static void Vert(Acc a, Vec3 p, Vec3 n, float rep)
        {
            a.P.Add(p.X); a.P.Add(p.Y); a.P.Add(p.Z);
            a.N.Add(n.X); a.N.Add(n.Y); a.N.Add(n.Z);
            float ax = Math.Abs(n.X), ay = Math.Abs(n.Y), az = Math.Abs(n.Z);
            if (ay >= ax && ay >= az) { a.U.Add(p.X / rep); a.U.Add(p.Z / rep); }
            else if (ax >= az) { a.U.Add(p.Z / rep); a.U.Add(p.Y / rep); }
            else { a.U.Add(p.X / rep); a.U.Add(p.Y / rep); }
        }

        private void Tri(Vec3 a, Vec3 b, Vec3 c)
        {
            _col.Add(a.X); _col.Add(a.Y); _col.Add(a.Z);
            _col.Add(b.X); _col.Add(b.Y); _col.Add(b.Z);
            _col.Add(c.X); _col.Add(c.Y); _col.Add(c.Z);
        }

        /// <summary>An axis-aligned box; faces outward; the bottom is skipped when it rests on the floor.</summary>
        private void Box(MaterialSlot slot, int recipe, Vec3 mn, Vec3 mx, bool collide)
        {
            if (mx.X - mn.X < 1e-4f || mx.Y - mn.Y < 1e-4f || mx.Z - mn.Z < 1e-4f) return;
            Vec3 a = new Vec3(mn.X, mn.Y, mn.Z), b = new Vec3(mx.X, mn.Y, mn.Z), c = new Vec3(mx.X, mn.Y, mx.Z), d = new Vec3(mn.X, mn.Y, mx.Z);
            Vec3 e = new Vec3(mn.X, mx.Y, mn.Z), f = new Vec3(mx.X, mx.Y, mn.Z), g = new Vec3(mx.X, mx.Y, mx.Z), h = new Vec3(mn.X, mx.Y, mx.Z);
            Quad(slot, recipe, e, f, g, h, Vec3.Up, collide);
            if (mn.Y > 1e-3f) Quad(slot, recipe, a, b, c, d, -Vec3.Up, collide);
            Quad(slot, recipe, a, b, f, e, -Vec3.Forward, collide);
            Quad(slot, recipe, d, c, g, h, Vec3.Forward, collide);
            Quad(slot, recipe, a, d, h, e, -Vec3.Right, collide);
            Quad(slot, recipe, b, c, g, f, Vec3.Right, collide);
        }

        // ----------------------------------------------------------------------------------------------- shell

        private void Walls(Room r)
        {
            Rect2 inner = r.Bounds.Inset(T / 2f);
            float h = _l.CeilingHeight;
            WallSide(r, inner.MinX, inner.MaxX, inner.MinZ, true, Vec3.Forward, h, r.Bounds.MinZ);
            WallSide(r, inner.MinX, inner.MaxX, inner.MaxZ, true, -Vec3.Forward, h, r.Bounds.MaxZ);
            WallSide(r, inner.MinZ, inner.MaxZ, inner.MinX, false, Vec3.Right, h, r.Bounds.MinX);
            WallSide(r, inner.MinZ, inner.MaxZ, inner.MaxX, false, -Vec3.Right, h, r.Bounds.MaxX);
        }

        /// <summary>One wall face of a room from a0 to a1 along its axis at the fixed coordinate, with doorway gaps.</summary>
        private void WallSide(Room r, float a0, float a1, float fixedCoord, bool alongX, Vec3 n, float h, float centreLine)
        {
            var gaps = new List<float>();
            foreach (Doorway d in _l.Doorways)
            {
                if (d.RoomA != r.Id && d.RoomB != r.Id) continue;
                if (d.WallAlongX != alongX) continue;
                float c = alongX ? d.Center.Z : d.Center.X;
                if (Math.Abs(c - centreLine) > 0.01f) continue;
                float along = alongX ? d.Center.X : d.Center.Z;
                gaps.Add(Math.Max(a0, along - d.Width / 2f));
                gaps.Add(Math.Min(a1, along + d.Width / 2f));
            }
            // Sort gaps by start.
            for (int i = 0; i < gaps.Count; i += 2)
            {
                for (int j = i + 2; j < gaps.Count; j += 2)
                {
                    if (gaps[j] < gaps[i])
                    {
                        float s = gaps[i], e = gaps[i + 1];
                        gaps[i] = gaps[j]; gaps[i + 1] = gaps[j + 1];
                        gaps[j] = s; gaps[j + 1] = e;
                    }
                }
            }
            float cursor = a0;
            for (int i = 0; i <= gaps.Count; i += 2)
            {
                float segEnd = i < gaps.Count ? gaps[i] : a1;
                if (segEnd - cursor > 1e-3f) WallQuad(r, cursor, segEnd, 0f, h, fixedCoord, alongX, n);
                if (i < gaps.Count)
                {
                    WallQuad(r, gaps[i], gaps[i + 1], InteriorLayout.DoorHeight, h, fixedCoord, alongX, n);
                    cursor = gaps[i + 1];
                }
            }
        }

        private void WallQuad(Room r, float a0, float a1, float y0, float y1, float fixedCoord, bool alongX, Vec3 n)
        {
            if (y1 - y0 < 1e-3f) return;
            Vec3 p0, p1, p2, p3;
            if (alongX)
            {
                p0 = new Vec3(a0, y0, fixedCoord); p1 = new Vec3(a0, y1, fixedCoord); p2 = new Vec3(a1, y1, fixedCoord); p3 = new Vec3(a1, y0, fixedCoord);
            }
            else
            {
                p0 = new Vec3(fixedCoord, y0, a0); p1 = new Vec3(fixedCoord, y1, a0); p2 = new Vec3(fixedCoord, y1, a1); p3 = new Vec3(fixedCoord, y0, a1);
            }
            Quad(MaterialSlot.Wall, r.WallRecipe, p0, p1, p2, p3, n, true);
        }

        private void Jambs(Doorway d)
        {
            int recipe = _l.Rooms[d.RoomA].WallRecipe;
            float half = d.Width / 2f, dh = InteriorLayout.DoorHeight;
            if (d.WallAlongX)
            {
                float z0 = d.Center.Z - T / 2f, z1 = d.Center.Z + T / 2f, x0 = d.Center.X - half, x1 = d.Center.X + half;
                Quad(MaterialSlot.Wall, recipe, new Vec3(x0, 0f, z0), new Vec3(x0, dh, z0), new Vec3(x0, dh, z1), new Vec3(x0, 0f, z1), Vec3.Right, true);
                Quad(MaterialSlot.Wall, recipe, new Vec3(x1, 0f, z0), new Vec3(x1, dh, z0), new Vec3(x1, dh, z1), new Vec3(x1, 0f, z1), -Vec3.Right, true);
                // The soffit is drawn but not collided: nothing reaches 2.2 m inside a doorway, and rays up from the
                // opening should meet the ceiling.
                Quad(MaterialSlot.Wall, recipe, new Vec3(x0, dh, z0), new Vec3(x1, dh, z0), new Vec3(x1, dh, z1), new Vec3(x0, dh, z1), -Vec3.Up, false);
            }
            else
            {
                float x0 = d.Center.X - T / 2f, x1 = d.Center.X + T / 2f, z0 = d.Center.Z - half, z1 = d.Center.Z + half;
                Quad(MaterialSlot.Wall, recipe, new Vec3(x0, 0f, z0), new Vec3(x0, dh, z0), new Vec3(x1, dh, z0), new Vec3(x1, 0f, z0), Vec3.Forward, true);
                Quad(MaterialSlot.Wall, recipe, new Vec3(x0, 0f, z1), new Vec3(x0, dh, z1), new Vec3(x1, dh, z1), new Vec3(x1, 0f, z1), -Vec3.Forward, true);
                Quad(MaterialSlot.Wall, recipe, new Vec3(x0, dh, z0), new Vec3(x1, dh, z0), new Vec3(x1, dh, z1), new Vec3(x0, dh, z1), -Vec3.Up, false);
            }
        }

        private void Lights(Room r)
        {
            Rect2 inner = r.Bounds.Inset(T / 2f);
            float y = _l.CeilingHeight - 0.004f;
            int nx = Math.Max(1, (int)Math.Round(inner.Width / 3.5f)), nz = Math.Max(1, (int)Math.Round(inner.Depth / 3.5f));
            bool longX = inner.Width >= inner.Depth;
            float hx = longX ? 0.5f : 0.25f, hz = longX ? 0.25f : 0.5f;
            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < nz; j++)
                {
                    float cx = inner.MinX + (i + 0.5f) * inner.Width / nx, cz = inner.MinZ + (j + 0.5f) * inner.Depth / nz;
                    Quad(MaterialSlot.Emissive, _pal.LightPanel, new Vec3(cx - hx, y, cz - hz), new Vec3(cx - hx, y, cz + hz), new Vec3(cx + hx, y, cz + hz), new Vec3(cx + hx, y, cz - hz), -Vec3.Up, false);
                }
            }
        }

        private void Windows(Room r)
        {
            switch (r.Kind)
            {
                case RoomKind.Cells:
                case RoomKind.Armory:
                case RoomKind.StockRoom:
                case RoomKind.Bathroom:
                case RoomKind.Restroom:
                case RoomKind.Corridor:
                    return;
            }
            Rect2 b = r.Bounds, inner = b.Inset(T / 2f);
            float h = _l.CeilingHeight;
            bool high = r.Kind == RoomKind.WarehouseFloor || r.Kind == RoomKind.VehicleBay || r.Kind == RoomKind.Workshop;
            float y0 = high ? h - 1.4f : 0.9f, y1 = high ? h - 0.4f : Math.Min(2.3f, h - 0.3f);
            const float e = 0.01f;
            if (Math.Abs(b.MinX + _l.Width / 2f) < e) WindowRun(inner.MinZ, inner.MaxZ, inner.MinX + e, false, Vec3.Right, y0, y1, r);
            if (Math.Abs(b.MaxX - _l.Width / 2f) < e) WindowRun(inner.MinZ, inner.MaxZ, inner.MaxX - e, false, -Vec3.Right, y0, y1, r);
            if (Math.Abs(b.MaxZ - _l.Depth) < e && _l.Archetype != InteriorArchetype.ApartmentLobby) WindowRun(inner.MinX, inner.MaxX, inner.MaxZ - e, true, -Vec3.Forward, y0, y1, r);
            if (Math.Abs(b.MinZ) < e) WindowRun(inner.MinX, inner.MaxX, inner.MinZ + e, true, Vec3.Forward, y0, y1, r);
        }

        private void WindowRun(float a0, float a1, float fixedCoord, bool alongX, Vec3 n, float y0, float y1, Room r)
        {
            float len = a1 - a0;
            int count = (int)Math.Floor(len / 3.0f);
            if (count <= 0 || y1 - y0 < 0.4f) return;
            const float w = 1.4f;
            for (int i = 0; i < count; i++)
            {
                float c = a0 + (i + 0.5f) * len / count;
                if (alongX && Math.Abs(fixedCoord) < 0.2f && Math.Abs(c) < 1.6f) continue; // the street door
                if (Blocked(r, c - w / 2f, c + w / 2f, fixedCoord, alongX)) continue;
                Vec3 p0, p1, p2, p3;
                if (alongX)
                {
                    p0 = new Vec3(c - w / 2f, y0, fixedCoord); p1 = new Vec3(c - w / 2f, y1, fixedCoord); p2 = new Vec3(c + w / 2f, y1, fixedCoord); p3 = new Vec3(c + w / 2f, y0, fixedCoord);
                }
                else
                {
                    p0 = new Vec3(fixedCoord, y0, c - w / 2f); p1 = new Vec3(fixedCoord, y1, c - w / 2f); p2 = new Vec3(fixedCoord, y1, c + w / 2f); p3 = new Vec3(fixedCoord, y0, c + w / 2f);
                }
                Quad(MaterialSlot.Glass, _pal.WindowSky, p0, p1, p2, p3, n, false);
                // Sill.
                Vec3 off = n * 0.08f;
                Vec3 s0 = alongX ? new Vec3(c - w / 2f - 0.05f, y0 - 0.06f, fixedCoord) : new Vec3(fixedCoord, y0 - 0.06f, c - w / 2f - 0.05f);
                Vec3 s1 = alongX ? new Vec3(c + w / 2f + 0.05f, y0, fixedCoord) : new Vec3(fixedCoord, y0, c + w / 2f + 0.05f);
                Vec3 mn = Vec3.Min(s0, s1 + off), mx = Vec3.Max(s0, s1 + off);
                Box(MaterialSlot.Plastic, _pal.PlasticWhite, mn, mx, false);
            }
        }

        /// <summary>True when tall furniture stands against the wall over that stretch.</summary>
        private bool Blocked(Room r, float a0, float a1, float fixedCoord, bool alongX)
        {
            foreach (FurnitureItem f in _l.Furniture)
            {
                if (f.Room != r.Id || f.Size.Y < 1.0f) continue;
                Rect2 fp = f.Footprint;
                float near = alongX ? Math.Min(Math.Abs(fp.MinZ - fixedCoord), Math.Abs(fp.MaxZ - fixedCoord)) : Math.Min(Math.Abs(fp.MinX - fixedCoord), Math.Abs(fp.MaxX - fixedCoord));
                if (near > 0.3f) continue;
                float f0 = alongX ? fp.MinX : fp.MinZ, f1 = alongX ? fp.MaxX : fp.MaxZ;
                if (f0 < a1 && a0 < f1) return true;
            }
            return false;
        }

        private void Doors()
        {
            float z = T / 2f;
            Box(MaterialSlot.Wood, _pal.Wood, new Vec3(-0.55f, 0f, z), new Vec3(0.55f, 2.15f, z + 0.05f), false);
            Box(MaterialSlot.Metal, _pal.Metal, new Vec3(0.35f, 0.95f, z + 0.05f), new Vec3(0.45f, 1.05f, z + 0.1f), false);
            Box(MaterialSlot.Plastic, _pal.PlasticWhite, new Vec3(-0.65f, 0f, z), new Vec3(-0.55f, 2.25f, z + 0.08f), false);
            Box(MaterialSlot.Plastic, _pal.PlasticWhite, new Vec3(0.55f, 0f, z), new Vec3(0.65f, 2.25f, z + 0.08f), false);
            Box(MaterialSlot.Plastic, _pal.PlasticWhite, new Vec3(-0.65f, 2.15f, z), new Vec3(0.65f, 2.25f, z + 0.08f), false);
            if (_l.CeilingHeight > 2.7f) Box(MaterialSlot.Emissive, _pal.SignRed, new Vec3(-0.25f, 2.35f, z), new Vec3(0.25f, Math.Min(2.55f, _l.CeilingHeight - 0.05f), z + 0.06f), false);
            foreach (Portal p in _l.Portals)
            {
                if (p.Kind != PortalKind.Elevator) continue;
                float bz = p.Position.Z;
                Box(MaterialSlot.Metal, _pal.MetalDark, new Vec3(-0.75f, 0f, bz - 0.06f), new Vec3(0.75f, 2.35f, bz), false);
                Box(MaterialSlot.Metal, _pal.Metal, new Vec3(-0.6f, 0f, bz - 0.09f), new Vec3(-0.01f, 2.2f, bz - 0.06f), false);
                Box(MaterialSlot.Metal, _pal.Metal, new Vec3(0.01f, 0f, bz - 0.09f), new Vec3(0.6f, 2.2f, bz - 0.06f), false);
                Box(MaterialSlot.Emissive, _pal.Screen, new Vec3(0.9f, 1.05f, bz - 0.04f), new Vec3(1.02f, 1.25f, bz), false);
            }
        }

        // ---------------------------------------------------------------------------------------------- furniture

        private FurnitureItem _f;
        private float _baseY;

        /// <summary>A part in the item's own frame: x across its front, y up from its base, z from back (−) to front (+).</summary>
        private void Part(MaterialSlot slot, int recipe, float x0, float y0, float z0, float x1, float y1, float z1)
        {
            Vec3 a = Local(x0, z0), b = Local(x1, z1);
            Box(slot, recipe, new Vec3(Math.Min(a.X, b.X), _baseY + y0, Math.Min(a.Z, b.Z)), new Vec3(Math.Max(a.X, b.X), _baseY + y1, Math.Max(a.Z, b.Z)), false);
        }

        private Vec3 Local(float lx, float lz)
        {
            int k = ((int)Math.Round(_f.YawDeg / 90f) % 4 + 4) % 4;
            float x, z;
            switch (k)
            {
                case 0: x = lx; z = lz; break;
                case 1: x = lz; z = -lx; break;
                case 2: x = -lx; z = -lz; break;
                default: x = -lz; z = lx; break;
            }
            return new Vec3(_f.Position.X + x, 0f, _f.Position.Z + z);
        }

        private void Furniture(FurnitureItem f, int index)
        {
            _f = f;
            _baseY = f.Kind == FurnitureKind.Whiteboard ? f.Position.Y - f.Size.Y / 2f : 0f;
            float sx = f.Size.X / 2f, sy = f.Size.Y, sz = f.Size.Z / 2f;
            var rng = new Rng(Rng.Seed(_seed, (uint)index));
            switch (f.Kind)
            {
                case FurnitureKind.Bed:
                case FurnitureKind.SingleBed:
                    Part(MaterialSlot.Wood, _pal.Wood, -sx, 0f, -sz, sx, 0.3f, sz);
                    Part(MaterialSlot.Fabric, _pal.FabricWhite, -sx + 0.04f, 0.3f, -sz + 0.06f, sx - 0.04f, sy - 0.03f, sz - 0.03f);
                    Part(MaterialSlot.Fabric, _pal.Fabric, -sx + 0.02f, sy - 0.05f, -sz + 0.55f, sx - 0.02f, sy, sz - 0.01f);
                    Part(MaterialSlot.Fabric, _pal.FabricWhite, -sx + 0.12f, sy - 0.03f, -sz + 0.1f, sx - 0.12f, sy + 0.1f, -sz + 0.45f);
                    Part(MaterialSlot.Wood, _pal.Wood, -sx, 0f, -sz, sx, 0.95f, -sz + 0.06f);
                    break;
                case FurnitureKind.HospitalBed:
                    Legs(MaterialSlot.Metal, _pal.Metal, sx, sz, 0.5f, 0.04f);
                    Part(MaterialSlot.Metal, _pal.Metal, -sx, 0.5f, -sz, sx, 0.6f, sz);
                    Part(MaterialSlot.Fabric, _pal.FabricWhite, -sx + 0.04f, 0.6f, -sz + 0.04f, sx - 0.04f, sy, sz - 0.04f);
                    Part(MaterialSlot.Fabric, _pal.FabricWhite, -sx + 0.15f, sy, -sz + 0.08f, sx - 0.15f, sy + 0.1f, -sz + 0.42f);
                    Part(MaterialSlot.Metal, _pal.MetalDark, -sx, 0.6f, -sz, sx, 1.1f, -sz + 0.04f);
                    break;
                case FurnitureKind.Sofa:
                case FurnitureKind.Armchair:
                    Part(MaterialSlot.Fabric, _pal.Fabric, -sx, 0f, -sz, sx, 0.4f, sz);
                    Part(MaterialSlot.Fabric, _pal.FabricAlt, -sx + 0.18f, 0.4f, -sz + 0.22f, sx - 0.18f, 0.48f, sz - 0.02f);
                    Part(MaterialSlot.Fabric, _pal.Fabric, -sx, 0.4f, -sz, sx, sy, -sz + 0.22f);
                    Part(MaterialSlot.Fabric, _pal.Fabric, -sx, 0.4f, -sz, -sx + 0.18f, 0.62f, sz);
                    Part(MaterialSlot.Fabric, _pal.Fabric, sx - 0.18f, 0.4f, -sz, sx, 0.62f, sz);
                    break;
                case FurnitureKind.CoffeeTable:
                case FurnitureKind.DiningTable:
                    Part(MaterialSlot.Wood, _pal.WoodAlt, -sx, sy - 0.05f, -sz, sx, sy, sz);
                    Legs(MaterialSlot.Wood, _pal.WoodAlt, sx - 0.05f, sz - 0.05f, sy - 0.05f, 0.06f);
                    break;
                case FurnitureKind.Chair:
                    Part(MaterialSlot.Wood, _pal.WoodAlt, -sx, 0.42f, -sz, sx, 0.47f, sz);
                    Legs(MaterialSlot.Wood, _pal.WoodAlt, sx - 0.03f, sz - 0.03f, 0.42f, 0.04f);
                    Part(MaterialSlot.Wood, _pal.WoodAlt, -sx, 0.47f, -sz, sx, sy, -sz + 0.05f);
                    break;
                case FurnitureKind.Desk:
                case FurnitureKind.Workbench:
                    Part(MaterialSlot.Wood, f.Kind == FurnitureKind.Desk ? _pal.Wood : _pal.WoodAlt, -sx, sy - 0.04f, -sz, sx, sy, sz);
                    Part(MaterialSlot.Wood, _pal.Wood, -sx, 0f, -sz, -sx + 0.04f, sy - 0.04f, sz);
                    Part(MaterialSlot.Wood, _pal.Wood, sx - 0.04f, 0f, -sz, sx, sy - 0.04f, sz);
                    Part(MaterialSlot.Wood, _pal.Wood, -sx, 0.3f, -sz, sx, sy - 0.04f, -sz + 0.03f);
                    if (f.Kind == FurnitureKind.Desk) Part(MaterialSlot.Emissive, _pal.Screen, -0.25f, sy, -sz + 0.1f, 0.25f, sy + 0.35f, -sz + 0.13f);
                    else Part(MaterialSlot.Metal, _pal.MetalDark, sx - 0.4f, sy, -sz + 0.1f, sx - 0.15f, sy + 0.15f, -sz + 0.3f);
                    break;
                case FurnitureKind.Counter:
                    Part(MaterialSlot.Wood, _pal.WoodAlt, -sx, 0f, -sz + 0.05f, sx, sy - 0.05f, sz - 0.05f);
                    Part(MaterialSlot.Plastic, _pal.PlasticDark, -sx, sy - 0.05f, -sz, sx, sy, sz);
                    Part(MaterialSlot.Plastic, _pal.Accent, -sx, 0.15f, sz - 0.06f, sx, 0.25f, sz - 0.05f);
                    break;
                case FurnitureKind.WallShelf:
                    Part(MaterialSlot.Wood, _pal.Wood, -sx, 0f, -sz, sx, sy, -sz + 0.03f);
                    Part(MaterialSlot.Wood, _pal.Wood, -sx, 0f, -sz, -sx + 0.03f, sy, sz);
                    Part(MaterialSlot.Wood, _pal.Wood, sx - 0.03f, 0f, -sz, sx, sy, sz);
                    for (int i = 0; i < 4; i++)
                    {
                        float y = 0.1f + i * (sy - 0.2f) / 3f;
                        Part(MaterialSlot.Wood, _pal.Wood, -sx, y, -sz, sx, y + 0.03f, sz);
                        if (i < 3) Goods(rng, -sx + 0.05f, sx - 0.05f, y + 0.03f, y + (sy - 0.2f) / 3f - 0.08f, -sz + 0.05f, sz - 0.05f);
                    }
                    break;
                case FurnitureKind.ShopShelf:
                    Part(MaterialSlot.Metal, _pal.Metal, -sx, 0f, -sz, sx, 0.12f, sz);
                    Part(MaterialSlot.Plastic, _pal.PlasticWhite, -sx, 0.12f, -0.03f, sx, sy, 0.03f);
                    for (int i = 0; i < 3; i++)
                    {
                        float y = 0.12f + i * 0.5f;
                        Part(MaterialSlot.Metal, _pal.Metal, -sx, y, -sz, sx, y + 0.03f, sz);
                        Goods(rng, -sx + 0.05f, sx - 0.05f, y + 0.03f, y + 0.4f, -sz + 0.03f, -0.05f);
                        Goods(rng, -sx + 0.05f, sx - 0.05f, y + 0.03f, y + 0.4f, 0.05f, sz - 0.03f);
                    }
                    break;
                case FurnitureKind.ClothesRack:
                    Part(MaterialSlot.Metal, _pal.Metal, -sx, 0f, -0.03f, -sx + 0.04f, sy, 0.03f);
                    Part(MaterialSlot.Metal, _pal.Metal, sx - 0.04f, 0f, -0.03f, sx, sy, 0.03f);
                    Part(MaterialSlot.Metal, _pal.Metal, -sx, sy - 0.04f, -0.02f, sx, sy, 0.02f);
                    for (float x = -sx + 0.12f; x < sx - 0.12f; x += 0.11f)
                    {
                        Part(MaterialSlot.Fabric, rng.Chance(0.5f) ? _pal.Fabric : _pal.FabricAlt, x, 0.55f + rng.Range(0f, 0.2f), -sz + 0.05f, x + 0.04f, sy - 0.08f, sz - 0.05f);
                    }
                    break;
                case FurnitureKind.DisplayCase:
                    Part(MaterialSlot.Wood, _pal.WoodAlt, -sx, 0f, -sz, sx, 0.6f, sz);
                    Part(MaterialSlot.Glass, _pal.Glass, -sx, 0.6f, -sz, sx, sy, sz);
                    break;
                case FurnitureKind.Fridge:
                    Part(MaterialSlot.Plastic, _pal.PlasticWhite, -sx, 0f, -sz, sx, sy, sz - 0.02f);
                    Part(MaterialSlot.Glass, _pal.Glass, -sx + 0.05f, 0.1f, sz - 0.02f, sx - 0.05f, sy - 0.1f, sz);
                    Part(MaterialSlot.Metal, _pal.Metal, sx - 0.12f, 0.9f, sz, sx - 0.08f, 1.3f, sz + 0.04f);
                    break;
                case FurnitureKind.KitchenCounter:
                    Part(MaterialSlot.Wood, _pal.WoodAlt, -sx, 0f, -sz, sx, sy - 0.04f, sz - 0.02f);
                    Part(MaterialSlot.Plastic, _pal.PlasticDark, -sx, sy - 0.04f, -sz, sx, sy, sz);
                    Part(MaterialSlot.Metal, _pal.MetalDark, -sx + 0.1f, sy, -sz + 0.08f, -sx + 0.7f, sy + 0.02f, sz - 0.08f);
                    if (sx > 0.8f) Part(MaterialSlot.Metal, _pal.Metal, sx - 0.7f, sy - 0.02f, -sz + 0.1f, sx - 0.15f, sy + 0.01f, sz - 0.1f);
                    break;
                case FurnitureKind.Toilet:
                    Part(MaterialSlot.Plastic, _pal.PlasticWhite, -sx + 0.05f, 0f, -sz + 0.2f, sx - 0.05f, 0.42f, sz);
                    Part(MaterialSlot.Plastic, _pal.PlasticWhite, -sx, 0.3f, -sz, sx, sy, -sz + 0.2f);
                    break;
                case FurnitureKind.Sink:
                    Part(MaterialSlot.Plastic, _pal.PlasticWhite, -0.1f, 0f, -sz, 0.1f, sy - 0.15f, -sz + 0.2f);
                    Part(MaterialSlot.Plastic, _pal.PlasticWhite, -sx, sy - 0.15f, -sz, sx, sy, sz);
                    Part(MaterialSlot.Metal, _pal.Metal, -0.03f, sy, -sz + 0.02f, 0.03f, sy + 0.2f, -sz + 0.08f);
                    break;
                case FurnitureKind.Bathtub:
                    Part(MaterialSlot.Plastic, _pal.PlasticWhite, -sx, 0f, -sz, sx, sy, sz);
                    break;
                case FurnitureKind.Wardrobe:
                    Part(MaterialSlot.Wood, _pal.Wood, -sx, 0f, -sz, sx, sy, sz - 0.02f);
                    Part(MaterialSlot.Wood, _pal.WoodAlt, -sx + 0.03f, 0.05f, sz - 0.02f, -0.01f, sy - 0.05f, sz);
                    Part(MaterialSlot.Wood, _pal.WoodAlt, 0.01f, 0.05f, sz - 0.02f, sx - 0.03f, sy - 0.05f, sz);
                    Part(MaterialSlot.Metal, _pal.Metal, -0.08f, 0.9f, sz, -0.05f, 1.2f, sz + 0.03f);
                    Part(MaterialSlot.Metal, _pal.Metal, 0.05f, 0.9f, sz, 0.08f, 1.2f, sz + 0.03f);
                    break;
                case FurnitureKind.Locker:
                    Part(MaterialSlot.Metal, _pal.Metal, -sx, 0f, -sz, sx, sy, sz - 0.01f);
                    for (float x = -sx + 0.45f; x < sx - 0.05f; x += 0.45f) Part(MaterialSlot.Metal, _pal.MetalDark, x - 0.01f, 0.05f, sz - 0.01f, x + 0.01f, sy - 0.05f, sz);
                    break;
                case FurnitureKind.CellBars:
                    Part(MaterialSlot.Metal, _pal.MetalDark, -sx, sy - 0.08f, -sz, sx, sy, sz);
                    Part(MaterialSlot.Metal, _pal.MetalDark, -sx, 0f, -sz, sx, 0.06f, sz);
                    for (float x = -sx + 0.02f; x < sx - 0.01f; x += 0.12f) Part(MaterialSlot.Metal, _pal.MetalDark, x, 0.06f, -0.02f, Math.Min(sx, x + 0.035f), sy - 0.08f, 0.02f);
                    break;
                case FurnitureKind.Bench:
                    Part(MaterialSlot.Wood, _pal.Wood, -sx, sy - 0.05f, -sz, sx, sy, sz);
                    Part(MaterialSlot.Metal, _pal.MetalDark, -sx + 0.05f, 0f, -sz + 0.05f, -sx + 0.1f, sy - 0.05f, sz - 0.05f);
                    Part(MaterialSlot.Metal, _pal.MetalDark, sx - 0.1f, 0f, -sz + 0.05f, sx - 0.05f, sy - 0.05f, sz - 0.05f);
                    break;
                case FurnitureKind.Plant:
                    Part(MaterialSlot.Plastic, _pal.Terracotta, -sx + 0.08f, 0f, -sz + 0.08f, sx - 0.08f, 0.38f, sz - 0.08f);
                    Part(MaterialSlot.Plant, _pal.Leaves, -sx, 0.38f, -sz, sx, sy * 0.7f, sz);
                    Part(MaterialSlot.Plant, _pal.Leaves, -sx + 0.08f, sy * 0.7f, -sz + 0.08f, sx - 0.08f, sy, sz - 0.08f);
                    break;
                case FurnitureKind.Tv:
                    Part(MaterialSlot.Wood, _pal.Wood, -sx, 0f, -sz, sx, 0.5f, sz);
                    Part(MaterialSlot.Plastic, _pal.PlasticDark, -sx + 0.05f, 0.55f, -0.04f, sx - 0.05f, sy, 0.02f);
                    Part(MaterialSlot.Emissive, _pal.Screen, -sx + 0.08f, 0.58f, 0.02f, sx - 0.08f, sy - 0.03f, 0.03f);
                    break;
                case FurnitureKind.Crate:
                    Part(MaterialSlot.Wood, _pal.WoodAlt, -sx, 0f, -sz, sx, sy, sz);
                    break;
                case FurnitureKind.PalletRack:
                    Part(MaterialSlot.Metal, _pal.RackOrange, -sx, 0f, -sz, -sx + 0.08f, sy, -sz + 0.08f);
                    Part(MaterialSlot.Metal, _pal.RackOrange, -sx, 0f, sz - 0.08f, -sx + 0.08f, sy, sz);
                    Part(MaterialSlot.Metal, _pal.RackOrange, sx - 0.08f, 0f, -sz, sx, sy, -sz + 0.08f);
                    Part(MaterialSlot.Metal, _pal.RackOrange, sx - 0.08f, 0f, sz - 0.08f, sx, sy, sz);
                    for (int i = 0; i < 3; i++)
                    {
                        float y = 0.15f + i * 1.3f;
                        Part(MaterialSlot.Metal, _pal.RackOrange, -sx, y, -sz, sx, y + 0.1f, sz);
                        if (rng.Chance(0.8f)) Part(MaterialSlot.Wood, _pal.Cardboard, -sx + 0.15f, y + 0.1f, -sz + 0.1f, -0.05f, y + 0.1f + rng.Range(0.5f, 1.0f), sz - 0.1f);
                        if (rng.Chance(0.8f)) Part(MaterialSlot.Wood, _pal.Cardboard, 0.05f, y + 0.1f, -sz + 0.1f, sx - 0.15f, y + 0.1f + rng.Range(0.5f, 1.0f), sz - 0.1f);
                    }
                    break;
                case FurnitureKind.Console:
                    Part(MaterialSlot.Plastic, _pal.PlasticDark, -sx, 0f, -sz, sx, 0.75f, sz);
                    Part(MaterialSlot.Plastic, _pal.PlasticDark, -sx, 0.75f, -sz, sx, sy, -sz + 0.3f);
                    Part(MaterialSlot.Emissive, _pal.Screen, -sx + 0.1f, 0.8f, -sz + 0.3f, sx - 0.1f, sy - 0.05f, -sz + 0.31f);
                    break;
                case FurnitureKind.VendingMachine:
                    Part(MaterialSlot.Plastic, _pal.Accent, -sx, 0f, -sz, sx, sy, sz - 0.02f);
                    Part(MaterialSlot.Emissive, _pal.Screen, -sx + 0.08f, 0.6f, sz - 0.02f, sx - 0.25f, sy - 0.15f, sz);
                    break;
                case FurnitureKind.Mailboxes:
                    Part(MaterialSlot.Metal, _pal.Metal, -sx, 0.2f, -sz, sx, sy, sz - 0.01f);
                    for (float x = -sx + 0.3f; x < sx; x += 0.3f) Part(MaterialSlot.Metal, _pal.MetalDark, x - 0.01f, 0.2f, sz - 0.01f, x + 0.01f, sy, sz);
                    break;
                case FurnitureKind.Rug:
                    Part(MaterialSlot.Fabric, _pal.FabricAlt, -sx, 0f, -sz, sx, sy, sz);
                    break;
                case FurnitureKind.SchoolDesk:
                    Part(MaterialSlot.Wood, _pal.WoodAlt, -sx, 0.72f, -sz, sx, sy, -sz + 0.5f);
                    Part(MaterialSlot.Metal, _pal.Metal, -sx + 0.05f, 0f, -sz + 0.2f, -sx + 0.09f, 0.72f, -sz + 0.24f);
                    Part(MaterialSlot.Metal, _pal.Metal, sx - 0.09f, 0f, -sz + 0.2f, sx - 0.05f, 0.72f, -sz + 0.24f);
                    Part(MaterialSlot.Plastic, _pal.Accent, -0.2f, 0.42f, sz - 0.38f, 0.2f, 0.46f, sz - 0.02f);
                    Part(MaterialSlot.Plastic, _pal.Accent, -0.2f, 0.46f, sz - 0.04f, 0.2f, 0.8f, sz);
                    Part(MaterialSlot.Metal, _pal.Metal, -0.03f, 0f, sz - 0.23f, 0.03f, 0.42f, sz - 0.17f);
                    break;
                case FurnitureKind.Whiteboard:
                    Part(MaterialSlot.Metal, _pal.Metal, -sx, 0f, -sz, sx, sy, sz);
                    Part(MaterialSlot.Plastic, _pal.PlasticWhite, -sx + 0.04f, 0.04f, sz, sx - 0.04f, sy - 0.04f, sz + 0.005f);
                    break;
                case FurnitureKind.Stool:
                    Part(MaterialSlot.Fabric, _pal.Fabric, -sx, sy - 0.06f, -sz, sx, sy, sz);
                    Part(MaterialSlot.Metal, _pal.Metal, -0.03f, 0f, -0.03f, 0.03f, sy - 0.06f, 0.03f);
                    Part(MaterialSlot.Metal, _pal.Metal, -sx + 0.05f, 0f, -sz + 0.05f, sx - 0.05f, 0.02f, sz - 0.05f);
                    break;
                default:
                    Part(MaterialSlot.Wood, _pal.Wood, -sx, 0f, -sz, sx, sy, sz);
                    break;
            }
            if (f.Solid)
            {
                Rect2 fp = f.Footprint;
                CollisionBox(new Vec3(fp.MinX, 0f, fp.MinZ), new Vec3(fp.MaxX, Math.Min(sy, _l.CeilingHeight - 0.01f), fp.MaxZ));
            }
        }

        private void Legs(MaterialSlot slot, int recipe, float hx, float hz, float height, float thick)
        {
            Part(slot, recipe, -hx, 0f, -hz, -hx + thick, height, -hz + thick);
            Part(slot, recipe, hx - thick, 0f, -hz, hx, height, -hz + thick);
            Part(slot, recipe, -hx, 0f, hz - thick, -hx + thick, height, hz);
            Part(slot, recipe, hx - thick, 0f, hz - thick, hx, height, hz);
        }

        private void Goods(Rng rng, float x0, float x1, float y0, float y1, float z0, float z1)
        {
            float x = x0;
            while (x < x1 - 0.08f)
            {
                float w = rng.Range(0.12f, 0.35f);
                if (x + w > x1) w = x1 - x;
                if (rng.Chance(0.85f))
                {
                    float top = y0 + (y1 - y0) * rng.Range(0.5f, 1f);
                    Part(MaterialSlot.Plastic, rng.Chance(0.5f) ? _pal.Goods : _pal.Cardboard, x, y0, z0, x + w - 0.02f, top, z1);
                }
                x += w;
            }
        }

        private void CollisionBox(Vec3 mn, Vec3 mx)
        {
            Vec3 a = new Vec3(mn.X, mn.Y, mn.Z), b = new Vec3(mx.X, mn.Y, mn.Z), c = new Vec3(mx.X, mn.Y, mx.Z), d = new Vec3(mn.X, mn.Y, mx.Z);
            Vec3 e = new Vec3(mn.X, mx.Y, mn.Z), f = new Vec3(mx.X, mx.Y, mn.Z), g = new Vec3(mx.X, mx.Y, mx.Z), h = new Vec3(mn.X, mx.Y, mx.Z);
            ColQuad(e, f, g, h, Vec3.Up);
            ColQuad(a, b, f, e, -Vec3.Forward);
            ColQuad(d, c, g, h, Vec3.Forward);
            ColQuad(a, d, h, e, -Vec3.Right);
            ColQuad(b, c, g, f, Vec3.Right);
        }

        private void ColQuad(Vec3 p0, Vec3 p1, Vec3 p2, Vec3 p3, Vec3 n)
        {
            if (Vec3.Dot(Vec3.Cross(p1 - p0, p2 - p0), n) < 0f)
            {
                Vec3 t = p1; p1 = p3; p3 = t;
            }
            Tri(p0, p1, p2);
            Tri(p0, p2, p3);
        }

        // ------------------------------------------------------------------------------------------------- output

        private void Emit()
        {
            foreach (int recipe in _order)
            {
                Acc a = _acc[recipe];
                int vertCount = a.P.Count / 3;
                if (vertCount == 0) continue;
                int quads = a.I.Count / 6;
                int quadsPerPart = MaxVertices / 4;
                for (int q0 = 0; q0 < quads; q0 += quadsPerPart)
                {
                    int q1 = Math.Min(quads, q0 + quadsPerPart);
                    int v0 = q0 * 4, nv = (q1 - q0) * 4;
                    var part = new MeshPart
                    {
                        Slot = a.Slot,
                        Recipe = recipe,
                        Positions = a.P.GetRange(v0 * 3, nv * 3).ToArray(),
                        Normals = a.N.GetRange(v0 * 3, nv * 3).ToArray(),
                        Uvs = a.U.GetRange(v0 * 2, nv * 2).ToArray(),
                        Indices = new int[(q1 - q0) * 6],
                    };
                    for (int k = 0; k < part.Indices.Length; k++) part.Indices[k] = a.I[q0 * 6 + k] - v0;
                    _l.MeshParts.Add(part);
                }
            }
            _l.CollisionTriangles = _col.ToArray();
        }
    }
}
