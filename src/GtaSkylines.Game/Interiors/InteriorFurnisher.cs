using System;
using System.Collections.Generic;
using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Game.Interiors
{
    /// <summary>
    /// Furnishes a planned layout: anchors first (counters, desks, beds, cells, respray spot) with their interaction
    /// points and NPC spots, then decoration; keeps door approaches, the entrance and interaction points clear; finally
    /// builds the nav grid and removes non-anchor furniture until every doorway and interaction point is reachable.
    /// </summary>
    internal sealed class InteriorFurnisher
    {
        private const float T = InteriorLayout.WallThickness;
        private const float C = InteriorNavGrid.Clearance;
        /// <summary>Margin that keeps a point's whole nav cell clear (half a cell diagonal, rounded up).</summary>
        private const float CellSlack = 0.18f;

        private enum Side { Front, Back, Left, Right }

        private static readonly Side[] AllSides = { Side.Front, Side.Back, Side.Left, Side.Right };
        private static readonly float[] Fractions = { 0.5f, 0.25f, 0.75f, 0.1f, 0.9f, 0.35f, 0.65f, 0f, 1f };

        private readonly InteriorLayout _l;
        private readonly Rng _rng;
        private readonly List<Rect2> _clear = new List<Rect2>();
        private readonly List<bool> _anchor = new List<bool>();
        private readonly List<int> _owner = new List<int>();
        private bool _checkReach;

        internal InteriorFurnisher(InteriorLayout layout, Rng rng)
        {
            _l = layout;
            _rng = rng;
        }

        internal void Furnish()
        {
            ReserveDoors();
            Interaction(InteractionKind.ExitDoor, new Vec3(0f, 0f, 0.7f), 180f, 0, ShopType.None, false);
            if (HasElevator)
            {
                Interaction(InteractionKind.ElevatorDoor, new Vec3(0f, 0f, _l.Depth - 0.7f), 0f, _l.RoomAt(0f, _l.Depth - 0.7f), ShopType.None, false);
            }

            bool primaryBedroom = true;
            foreach (Room r in _l.Rooms)
            {
                switch (r.Kind)
                {
                    case RoomKind.LivingRoom: Living(r); break;
                    case RoomKind.Kitchen: Kitchen(r); break;
                    case RoomKind.Bedroom: Bedroom(r, primaryBedroom); primaryBedroom = false; break;
                    case RoomKind.Bathroom: Bathroom(r); break;
                    case RoomKind.Restroom: Restroom(r); break;
                    case RoomKind.ShopFloor: ShopFloor(r); break;
                    case RoomKind.StockRoom: Stock(r); break;
                    case RoomKind.DiningRoom: Dining(r); break;
                    case RoomKind.OfficeRoom: OfficeRoom(r); break;
                    case RoomKind.MeetingRoom: Meeting(r); break;
                    case RoomKind.WarehouseFloor: WarehouseFloor(r); break;
                    case RoomKind.Workshop: Workshop(r); break;
                    case RoomKind.Cells: Cells(r); break;
                    case RoomKind.LockerRoom: Lockers(r); break;
                    case RoomKind.Armory: Armory(r); break;
                    case RoomKind.Ward: Ward(r); break;
                    case RoomKind.TreatmentRoom: Treatment(r); break;
                    case RoomKind.VehicleBay: Bay(r); break;
                    case RoomKind.Bunks: Bunks(r); break;
                    case RoomKind.Classroom: Classroom(r); break;
                    case RoomKind.ControlRoom: Control(r); break;
                    case RoomKind.Lobby: Lobby(r); break;
                }
            }
            if ((_l.Archetype == InteriorArchetype.House || _l.Archetype == InteriorArchetype.Apartment) && !HasInteraction(InteractionKind.Bed))
            {
                // No bedroom fitted: a studio bed in the largest room.
                Room big = _l.Rooms[0];
                foreach (Room r in _l.Rooms) if (r.Kind != RoomKind.Corridor && r.Bounds.Area > big.Bounds.Area) big = r;
                Bedroom(big, true);
            }

            ResolveReachability();
            if (_l.Archetype == InteriorArchetype.House || _l.Archetype == InteriorArchetype.Apartment)
            {
                // Placed again, this time only where the spot is reachable, if the first pass lost them.
                _checkReach = true;
                EnsureBed();
                EnsureWardrobe();
                _checkReach = false;
                _l.Nav = BuildNav(_l);
            }
            BlockUnreachable();
            StandingSpots();
        }

        // ----------------------------------------------------------------------------------------------------- rooms

        private void Living(Room r)
        {
            List<Side> free = FreeSides(r);
            int sofa = -1;
            Side sofaSide = Side.Back;
            foreach (Side s in free)
            {
                sofa = OnWallAuto(r, s, FurnitureKind.Sofa, Size(FurnitureKind.Sofa));
                if (sofa >= 0) { sofaSide = s; break; }
            }
            if (sofa >= 0)
            {
                FurnitureItem so = _l.Furniture[sofa];
                Vec3 f = Vec3.FromYawDeg(so.YawDeg);
                Vec3 ts = Size(FurnitureKind.CoffeeTable);
                Vec3 tc = so.Position + f * (so.Size.Z / 2f + 0.5f + ts.Z / 2f);
                if (Place(FurnitureKind.CoffeeTable, r, tc, ts, so.YawDeg) >= 0)
                {
                    Place(FurnitureKind.Rug, r, tc, Size(FurnitureKind.Rug), so.YawDeg);
                }
                MakeAnchor(sofa);
                Npc(NpcRole.Resident, so.Position, so.YawDeg, r.Id, true);
                int tv = OnWallAt(r, Opposite(sofaSide), FurnitureKind.Tv, Size(FurnitureKind.Tv), Along(so, sofaSide));
                if (tv < 0) OnWallAuto(r, Opposite(sofaSide), FurnitureKind.Tv, Size(FurnitureKind.Tv));
            }
            foreach (Side s in free)
            {
                if (s == sofaSide) continue;
                if (OnWallAuto(r, s, FurnitureKind.Armchair, Size(FurnitureKind.Armchair)) >= 0) break;
            }
            foreach (Side s in free) if (s != sofaSide && OnWallAuto(r, s, FurnitureKind.WallShelf, Size(FurnitureKind.WallShelf)) >= 0) break;
            Corner(r, FurnitureKind.Plant);
        }

        private void Kitchen(Room r)
        {
            List<Side> free = FreeSides(r);
            // Ties break on the side's index: List.Sort is unstable and differs between Mono and .NET.
            free.Sort((a, b) => { int c = WallLength(r, b).CompareTo(WallLength(r, a)); return c != 0 ? c : ((int)a).CompareTo((int)b); });
            Side s = free[0];
            float len = MathUtil.Clamp(Snap(WallLength(r, s) - 1.4f), 1.2f, 3.6f);
            Vec3 ks = Size(FurnitureKind.KitchenCounter);
            int counter = OnWallAuto(r, s, FurnitureKind.KitchenCounter, new Vec3(len, ks.Y, ks.Z));
            if (counter < 0) counter = OnWallAuto(r, s, FurnitureKind.KitchenCounter, new Vec3(1.2f, ks.Y, ks.Z));
            for (int i = 0; i < free.Count; i++) if (OnWallAuto(r, free[i], FurnitureKind.Fridge, Size(FurnitureKind.Fridge)) >= 0) break;
            Rect2 inner = Inner(r);
            if (inner.Width >= 2.8f && inner.Depth >= 2.8f && inner.Area >= 9f)
            {
                TableSet(r, inner.Center, Size(FurnitureKind.DiningTable), _rng.Chance(0.5f) ? 0f : 90f, NpcRole.Resident, 0.3f);
            }
        }

        private void Bedroom(Room r, bool primary)
        {
            List<Side> free = FreeSides(r);
            int bed = -1;
            Side bedSide = Side.Back;
            FurnitureKind kind = r.Kind == RoomKind.Bedroom ? FurnitureKind.Bed : FurnitureKind.SingleBed;
            foreach (Side s in free)
            {
                bed = OnWallAuto(r, s, kind, Size(kind));
                if (bed >= 0) { bedSide = s; break; }
            }
            if (bed < 0 && kind == FurnitureKind.Bed)
            {
                kind = FurnitureKind.SingleBed;
                foreach (Side s in free)
                {
                    bed = OnWallAuto(r, s, kind, Size(kind));
                    if (bed >= 0) { bedSide = s; break; }
                }
            }
            if (bed >= 0)
            {
                MakeAnchor(bed);
                if (primary)
                {
                    FurnitureItem b = _l.Furniture[bed];
                    Vec3 right = RightOf(b.YawDeg);
                    for (int k = 0; k < 2; k++)
                    {
                        float sign = k == 0 ? 1f : -1f;
                        Vec3 p = b.Position + right * (sign * (b.Size.X / 2f + 0.6f));
                        if (Interaction(InteractionKind.Bed, p, MathUtil.WrapDeg(b.YawDeg - 90f * sign), r.Id, ShopType.None, true, bed)) break;
                    }
                }
            }
            int wardrobe = -1;
            foreach (Side s in free)
            {
                if (s == bedSide && free.Count > 1) continue;
                wardrobe = OnWallAuto(r, s, FurnitureKind.Wardrobe, Size(FurnitureKind.Wardrobe));
                if (wardrobe >= 0) break;
            }
            if (wardrobe >= 0 && primary) InteractFront(InteractionKind.Wardrobe, wardrobe, ShopType.None);
            Corner(r, FurnitureKind.Plant);
        }

        private void Bathroom(Room r)
        {
            List<Side> free = FreeSides(r);
            foreach (Side s in free) if (OnWallAuto(r, s, FurnitureKind.Toilet, Size(FurnitureKind.Toilet)) >= 0) break;
            foreach (Side s in free) if (OnWallAuto(r, s, FurnitureKind.Sink, Size(FurnitureKind.Sink)) >= 0) break;
            if (Inner(r).Area >= 6f) foreach (Side s in free) if (OnWallAuto(r, s, FurnitureKind.Bathtub, Size(FurnitureKind.Bathtub)) >= 0) break;
        }

        private void Restroom(Room r)
        {
            List<Side> free = FreeSides(r);
            if (free.Count == 0) return;
            WallRun(r, free[0], FurnitureKind.Toilet, Size(FurnitureKind.Toilet), 0.5f, 4);
            if (free.Count > 1) WallRun(r, free[1], FurnitureKind.Sink, Size(FurnitureKind.Sink), 0.3f, 3);
        }

        private void ShopFloor(Room r)
        {
            ShopType shop = _l.Archetype == InteriorArchetype.ClothesShop ? ShopType.Clothes
                : _l.Archetype == InteriorArchetype.GunStore ? ShopType.Guns
                : _l.Archetype == InteriorArchetype.Restaurant ? ShopType.Food : ShopType.Convenience;
            int counter = ServiceCounter(r, NpcRole.Clerk);
            if (counter >= 0) InteractFront(InteractionKind.ShopCounter, counter, shop);
            Rect2 inner = Inner(r);
            List<Side> free = FreeSides(r);
            if (_l.Archetype == InteriorArchetype.GunStore)
            {
                if (counter >= 0)
                {
                    FurnitureItem c = _l.Furniture[counter];
                    Vec3 along = RightOf(c.YawDeg);
                    Vec3 ds = Size(FurnitureKind.DisplayCase);
                    Place(FurnitureKind.DisplayCase, r, c.Position + along * (c.Size.X / 2f + ds.X / 2f + 0.05f), ds, c.YawDeg);
                    Place(FurnitureKind.DisplayCase, r, c.Position - along * (c.Size.X / 2f + ds.X / 2f + 0.05f), ds, c.YawDeg);
                }
                foreach (Side s in free) WallRun(r, s, FurnitureKind.WallShelf, Size(FurnitureKind.WallShelf), 0.3f, 6);
                Islands(r, FurnitureKind.DisplayCase, Size(FurnitureKind.DisplayCase), 0f, 1.5f, 1.6f, 4);
            }
            else if (_l.Archetype == InteriorArchetype.ClothesShop)
            {
                int booth = -1;
                foreach (Side s in free)
                {
                    booth = OnWallAuto(r, s, FurnitureKind.Wardrobe, Size(FurnitureKind.Wardrobe));
                    if (booth >= 0) break;
                }
                if (booth >= 0) InteractFront(InteractionKind.Wardrobe, booth, ShopType.None);
                foreach (Side s in free) WallRun(r, s, FurnitureKind.WallShelf, Size(FurnitureKind.WallShelf), 0.4f, 4);
                Islands(r, FurnitureKind.ClothesRack, Size(FurnitureKind.ClothesRack), 0f, 1.4f, 1.4f, 12);
            }
            else
            {
                foreach (Side s in free)
                {
                    if (s == Side.Back) WallRun(r, s, FurnitureKind.Fridge, Size(FurnitureKind.Fridge), 0.05f, 8);
                    else WallRun(r, s, FurnitureKind.WallShelf, Size(FurnitureKind.WallShelf), 0.2f, 6);
                }
                Islands(r, FurnitureKind.ShopShelf, Size(FurnitureKind.ShopShelf), 90f, 1.4f, 1.4f, 10);
                Corner(r, FurnitureKind.VendingMachine);
            }
            Corner(r, FurnitureKind.Plant);
            if (inner.Area > 60f) Corner(r, FurnitureKind.Plant);
        }

        private void Stock(Room r)
        {
            foreach (Side s in FreeSides(r)) WallRun(r, s, FurnitureKind.WallShelf, Size(FurnitureKind.WallShelf), 0.2f, 6);
            Corner(r, FurnitureKind.Crate);
            Corner(r, FurnitureKind.Crate);
        }

        private void Dining(Room r)
        {
            int bar = ServiceCounter(r, NpcRole.Clerk);
            if (bar >= 0)
            {
                InteractFront(InteractionKind.ShopCounter, bar, ShopType.Food);
                FurnitureItem b = _l.Furniture[bar];
                Vec3 f = Vec3.FromYawDeg(b.YawDeg), along = RightOf(b.YawDeg);
                Vec3 ss = Size(FurnitureKind.Stool);
                for (float a = -b.Size.X / 2f + 0.3f; a <= b.Size.X / 2f - 0.3f; a += 0.8f)
                {
                    Place(FurnitureKind.Stool, r, b.Position + f * (b.Size.Z / 2f + 0.3f) + along * a, ss, b.YawDeg);
                }
            }
            Rect2 inner = Inner(r);
            Vec3 ts = new Vec3(1.2f, 0.75f, 0.8f);
            int sets = 0;
            for (float z = inner.MinZ + 3.0f; z < inner.MaxZ - 1.2f && sets < 12; z += 2.8f)
            {
                for (float x = inner.MinX + 1.4f; x < inner.MaxX - 1.0f && sets < 12; x += 2.8f)
                {
                    if (TableSet(r, new Vec3(x, 0f, z), ts, 0f, NpcRole.Visitor, 0.4f)) sets++;
                }
            }
            Corner(r, FurnitureKind.Plant);
        }

        private void OfficeRoom(Room r)
        {
            NpcRole role = _l.Archetype == InteriorArchetype.PoliceStation ? NpcRole.Cop
                : _l.Archetype == InteriorArchetype.Hospital ? NpcRole.Doctor : NpcRole.Worker;
            Rect2 inner = Inner(r);
            Vec3 ds = Size(FurnitureKind.Desk);
            int desks = 0;
            for (float z = inner.MinZ + 1.4f; z < inner.MaxZ - 0.8f && desks < 16; z += 2.6f)
            {
                for (float x = inner.MinX + 1.2f; x < inner.MaxX - 0.6f && desks < 16; x += 2.6f)
                {
                    if (DeskSet(r, new Vec3(x, 0f, z), ds, 180f, role, 0.5f)) desks++;
                }
            }
            foreach (Side s in FreeSides(r)) if (OnWallAuto(r, s, FurnitureKind.WallShelf, Size(FurnitureKind.WallShelf)) >= 0) break;
            Corner(r, FurnitureKind.Plant);
        }

        private void Meeting(Room r)
        {
            Rect2 inner = Inner(r);
            bool alongX = inner.Width >= inner.Depth;
            float len = MathUtil.Clamp(Snap((alongX ? inner.Width : inner.Depth) - 2.6f), 1.6f, 4f);
            Vec3 ts = new Vec3(len, 0.75f, 1.0f);
            TableSet(r, inner.Center, ts, alongX ? 0f : 90f, NpcRole.Worker, 0.3f);
            foreach (Side s in FreeSides(r))
            {
                if (WallItem(r, s, FurnitureKind.Whiteboard, Size(FurnitureKind.Whiteboard), 1.5f) >= 0) break;
            }
            Corner(r, FurnitureKind.Plant);
        }

        private void WarehouseFloor(Room r)
        {
            Rect2 inner = Inner(r);
            Vec3 rs = Size(FurnitureKind.PalletRack);
            int n = 0;
            for (float z = Math.Max(inner.MinZ + 3.2f, 3.4f); z + rs.Z / 2f < inner.MaxZ - 2.0f; z += rs.Z + 3.0f)
            {
                int k = 0;
                for (float x = inner.MinX + 0.6f + rs.X / 2f; x + rs.X / 2f < inner.MaxX - 0.6f; x += rs.X + 0.05f)
                {
                    k++;
                    if (k % 4 == 0) continue; // cross aisle
                    if (Place(FurnitureKind.PalletRack, r, new Vec3(x, 0f, z), rs, 0f) >= 0) n++;
                }
            }
            for (int i = 0; i < 6; i++)
            {
                Vec3 c = new Vec3(_rng.Range(inner.MinX + 1f, inner.MaxX - 1f), 0f, _rng.Range(inner.MinZ + 3f, inner.MaxZ - 1f));
                Place(FurnitureKind.Crate, r, c, Size(FurnitureKind.Crate), 0f);
            }
            Corner(r, FurnitureKind.Crate);
        }

        private void Workshop(Room r)
        {
            Rect2 inner = Inner(r);
            // The respray bay: a clear 5 x 3 m area in the middle of the workshop, its spot at the centre.
            float cz = MathUtil.Clamp((inner.MinZ + inner.MaxZ) / 2f, 3.2f, Math.Max(3.2f, inner.MaxZ - 2.2f));
            Vec3 centre = new Vec3((inner.MinX + inner.MaxX) / 2f, 0f, cz);
            Rect2 bay = new Rect2(centre.X - 2.5f, centre.Z - 1.5f, centre.X + 2.5f, centre.Z + 1.5f);
            if (!Interaction(InteractionKind.RespraySpot, centre, 0f, r.Id, ShopType.None, true))
            {
                // Fall back to anywhere in the room that is clear.
                for (int i = 0; i < 40 && !HasInteraction(InteractionKind.RespraySpot); i++)
                {
                    Vec3 p = new Vec3(_rng.Range(inner.MinX + 0.6f, inner.MaxX - 0.6f), 0f, _rng.Range(inner.MinZ + 0.6f, inner.MaxZ - 0.6f));
                    Interaction(InteractionKind.RespraySpot, p, 0f, r.Id, ShopType.None, true);
                }
            }
            _clear.Add(bay);
            foreach (Side s in FreeSides(r))
            {
                if (s == Side.Left) WallRun(r, s, FurnitureKind.Workbench, Size(FurnitureKind.Workbench), 0.6f, 4);
                else if (s == Side.Right) WallRun(r, s, FurnitureKind.WallShelf, Size(FurnitureKind.WallShelf), 0.4f, 4);
            }
            Corner(r, FurnitureKind.Crate);
            Corner(r, FurnitureKind.Crate);
        }

        private void Cells(Room r)
        {
            Rect2 inner = Inner(r);
            // The wall with the most room in front of it, among walls without a door.
            Side wall = Side.Back;
            float best = -1f;
            foreach (Side s in FreeSides(r))
            {
                float room = s == Side.Front || s == Side.Back ? inner.Depth : inner.Width;
                if (room > best) { best = room; wall = s; }
            }
            float cellDepth = Math.Min(2.4f, best - 1.9f);
            float barH = Math.Min(2.6f, _l.CeilingHeight - 0.2f);
            if (cellDepth < 1.2f)
            {
                // Too shallow for walk-in cells: a single run of bars along the wall.
                cellDepth = Math.Max(0.3f, best - 1.9f);
            }
            float span = WallLength(r, wall);
            int n = MathUtil.Clamp((int)Math.Floor(span / 2.2f), 1, 4);
            float cellW = span / n;
            float a0 = AlongStart(r, wall);
            float yawIn = InwardYaw(wall);
            for (int i = 0; i < n; i++)
            {
                float c0 = a0 + i * cellW, c1 = c0 + cellW;
                float door = Math.Min(0.9f, cellW - 0.4f);
                float seg = (cellW - door) / 2f;
                if (seg > 0.15f)
                {
                    Place(FurnitureKind.CellBars, r, WallPoint(r, wall, c0 + seg / 2f, cellDepth + 0.05f), new Vec3(seg, barH, 0.1f), yawIn, 1e-4f);
                    Place(FurnitureKind.CellBars, r, WallPoint(r, wall, c1 - seg / 2f, cellDepth + 0.05f), new Vec3(seg, barH, 0.1f), yawIn, 1e-4f);
                }
                if (i > 0)
                {
                    // Divider from the wall to the bars, as a run of bars turned 90 degrees.
                    Place(FurnitureKind.CellBars, r, WallPoint(r, wall, c0, cellDepth / 2f), new Vec3(cellDepth, barH, 0.1f), NormYaw(yawIn + 90f), 1e-4f);
                }
                Vec3 bs = Size(FurnitureKind.Bench);
                if (cellDepth >= 1.2f) Place(FurnitureKind.Bench, r, WallPoint(r, wall, (c0 + c1) / 2f, bs.Z / 2f + 0.02f), bs, yawIn);
            }
            if (Count(FurnitureKind.CellBars) == 0)
            {
                // Last resort: one run of bars on any free wall.
                foreach (Side s in FreeSides(r))
                {
                    if (OnWallAuto(r, s, FurnitureKind.CellBars, new Vec3(Math.Min(2.0f, WallLength(r, s) - 0.2f), barH, 0.1f)) >= 0) break;
                }
            }
        }

        private void Lockers(Room r)
        {
            List<Side> free = FreeSides(r);
            for (int i = 0; i < free.Count && i < 2; i++) WallRun(r, free[i], FurnitureKind.Locker, Size(FurnitureKind.Locker), 0.1f, 4);
            Rect2 inner = Inner(r);
            Place(FurnitureKind.Bench, r, inner.Center, Size(FurnitureKind.Bench), inner.Width >= inner.Depth ? 0f : 90f);
        }

        private void Armory(Room r)
        {
            foreach (Side s in FreeSides(r)) WallRun(r, s, FurnitureKind.WallShelf, Size(FurnitureKind.WallShelf), 0.2f, 4);
            Corner(r, FurnitureKind.Crate);
        }

        private void Ward(Room r)
        {
            foreach (Side s in FreeSides(r))
            {
                int before = _l.Furniture.Count;
                WallRun(r, s, FurnitureKind.HospitalBed, Size(FurnitureKind.HospitalBed), 0.9f, 6);
                for (int i = before; i < _l.Furniture.Count; i++)
                {
                    if (_l.Furniture[i].Kind != FurnitureKind.HospitalBed) continue;
                    if (_rng.Chance(0.6f))
                    {
                        MakeAnchor(i);
                        Npc(NpcRole.Patient, _l.Furniture[i].Position, _l.Furniture[i].YawDeg, r.Id, true);
                    }
                }
            }
            if (Count(FurnitureKind.HospitalBed) == 0)
            {
                Rect2 inner = Inner(r);
                Place(FurnitureKind.HospitalBed, r, inner.Center, Size(FurnitureKind.HospitalBed), inner.Width >= inner.Depth ? 90f : 0f);
            }
        }

        private void Treatment(Room r)
        {
            List<Side> free = FreeSides(r);
            foreach (Side s in free) if (OnWallAuto(r, s, FurnitureKind.HospitalBed, Size(FurnitureKind.HospitalBed)) >= 0) break;
            foreach (Side s in free) if (OnWallAuto(r, s, FurnitureKind.Counter, new Vec3(1.6f, 0.95f, 0.6f)) >= 0) break;
            foreach (Side s in free) if (OnWallAuto(r, s, FurnitureKind.Sink, Size(FurnitureKind.Sink)) >= 0) break;
        }

        private void Bay(Room r)
        {
            List<Side> free = FreeSides(r);
            if (free.Contains(Side.Left)) WallRun(r, Side.Left, FurnitureKind.Locker, Size(FurnitureKind.Locker), 0.2f, 4);
            if (free.Contains(Side.Right)) WallRun(r, Side.Right, FurnitureKind.Workbench, Size(FurnitureKind.Workbench), 0.8f, 3);
            Corner(r, FurnitureKind.Crate);
        }

        private void Bunks(Room r)
        {
            foreach (Side s in FreeSides(r)) WallRun(r, s, FurnitureKind.SingleBed, Size(FurnitureKind.SingleBed), 0.6f, 4);
            Corner(r, FurnitureKind.Plant);
        }

        private void Classroom(Room r)
        {
            List<Side> free = FreeSides(r);
            Side board = free.Count > 0 ? free[0] : Side.Back;
            float best = -1f;
            foreach (Side s in free)
            {
                float len = WallLength(r, s);
                if (len > best) { best = len; board = s; }
            }
            WallItem(r, board, FurnitureKind.Whiteboard, Size(FurnitureKind.Whiteboard), 1.5f);
            Vec3 ds = Size(FurnitureKind.Desk);
            Rect2 inner = Inner(r);
            float yawIn = InwardYaw(board);
            // Teacher's desk 1.2 m from the board, its front (the teacher's side) towards the board.
            Vec3 teacher = WallPoint(r, board, (AlongStart(r, board) + AlongEnd(r, board)) / 2f, 1.2f + ds.Z / 2f);
            int t = Place(FurnitureKind.Desk, r, teacher, ds, NormYaw(yawIn + 180f));
            if (t >= 0) MakeAnchor(t);
            Vec3 sd = Size(FurnitureKind.SchoolDesk);
            float startDepth = 1.2f + ds.Z + 1.2f;
            float depthRoom = board == Side.Front || board == Side.Back ? inner.Depth : inner.Width;
            int students = 0;
            for (float dd = startDepth + sd.Z / 2f; dd < depthRoom - 0.6f; dd += sd.Z + 0.8f)
            {
                for (float a = AlongStart(r, board) + 0.7f; a < AlongEnd(r, board) - 0.4f; a += sd.X + 0.6f)
                {
                    int k = Place(FurnitureKind.SchoolDesk, r, WallPoint(r, board, a, dd), sd, NormYaw(yawIn + 180f));
                    if (k >= 0 && _rng.Chance(0.5f) && students < 12)
                    {
                        MakeAnchor(k);
                        Npc(NpcRole.Visitor, _l.Furniture[k].Position, yawIn, r.Id, true);
                        students++;
                    }
                }
            }
        }

        private void Control(Room r)
        {
            List<Side> free = FreeSides(r);
            if (free.Count > 0) WallRun(r, free[0], FurnitureKind.Console, Size(FurnitureKind.Console), 0.4f, 4);
            Rect2 inner = Inner(r);
            DeskSet(r, inner.Center, Size(FurnitureKind.Desk), 180f, NpcRole.Worker, 1f);
            Corner(r, FurnitureKind.Plant);
        }

        private void Lobby(Room r)
        {
            List<Side> free = FreeSides(r);
            switch (_l.Archetype)
            {
                case InteriorArchetype.ApartmentLobby:
                    _clear.Add(new Rect2(-1.2f, 0f, 1.2f, _l.Depth));
                    if (free.Contains(Side.Left)) OnWallAuto(r, Side.Left, FurnitureKind.Mailboxes, Size(FurnitureKind.Mailboxes));
                    if (free.Contains(Side.Right))
                    {
                        int sofa = OnWallAuto(r, Side.Right, FurnitureKind.Sofa, Size(FurnitureKind.Sofa));
                        if (sofa >= 0)
                        {
                            MakeAnchor(sofa);
                            Npc(NpcRole.Resident, _l.Furniture[sofa].Position, _l.Furniture[sofa].YawDeg, r.Id, true);
                        }
                    }
                    break;
                case InteriorArchetype.PoliceStation:
                    {
                        int desk = ServiceCounter(r, NpcRole.Cop);
                        if (desk >= 0) InteractFront(InteractionKind.PoliceDesk, desk, ShopType.None);
                        foreach (Side s in free) if (OnWallAuto(r, s, FurnitureKind.Bench, Size(FurnitureKind.Bench)) >= 0) break;
                        break;
                    }
                case InteriorArchetype.Hospital:
                    {
                        int desk = ServiceCounter(r, NpcRole.Clerk);
                        if (desk >= 0) InteractFront(InteractionKind.HospitalDesk, desk, ShopType.None);
                        foreach (Side s in free) WallRun(r, s, FurnitureKind.Bench, Size(FurnitureKind.Bench), 0.6f, 3);
                        break;
                    }
                default:
                    {
                        ServiceCounter(r, NpcRole.Clerk);
                        foreach (Side s in free) if (OnWallAuto(r, s, FurnitureKind.Sofa, Size(FurnitureKind.Sofa)) >= 0) break;
                        break;
                    }
            }
            Corner(r, FurnitureKind.Plant);
            Corner(r, FurnitureKind.Plant);
        }

        // ------------------------------------------------------------------------------------------- composite helpers

        /// <summary>
        /// A counter facing the entrance side of the room with space for a person behind it, placed beside the central
        /// walkway; the NPC of <paramref name="role"/> stands behind it. Returns the counter's index or −1.
        /// </summary>
        private int ServiceCounter(Room r, NpcRole role)
        {
            Rect2 inner = Inner(r);
            Vec3 cs = Size(FurnitureKind.Counter);
            float len = MathUtil.Clamp(Snap(inner.Width * 0.28f), 1.6f, 3.0f);
            var size = new Vec3(len, cs.Y, cs.Z);
            float z = Math.Max(inner.MinZ, 0f) + 2.7f + cs.Z / 2f;
            float[] xs =
            {
                inner.MaxX - 0.6f - len / 2f, inner.MinX + 0.6f + len / 2f,
                inner.MaxX - 1.2f - len / 2f, inner.MinX + 1.2f + len / 2f,
            };
            foreach (float zz in new[] { z, z + 1.0f, z + 2.0f })
            {
                foreach (float x in xs)
                {
                    int k = TryCounter(r, new Vec3(x, 0f, zz), size, 180f, role);
                    if (k >= 0) return k;
                }
            }
            // Side islands facing the room's centre line.
            for (float zz = inner.MinZ + 2.7f + len / 2f; zz < inner.MaxZ - len / 2f - 0.3f; zz += 0.5f)
            {
                int k = TryCounter(r, new Vec3(inner.MaxX - 1.4f - cs.Z / 2f, 0f, zz), size, 270f, role);
                if (k >= 0) return k;
                k = TryCounter(r, new Vec3(inner.MinX + 1.4f + cs.Z / 2f, 0f, zz), size, 90f, role);
                if (k >= 0) return k;
            }
            // Anywhere at all, with a short counter.
            size = new Vec3(1.4f, cs.Y, cs.Z);
            for (int i = 0; i < 80; i++)
            {
                Vec3 c = new Vec3(_rng.Range(inner.MinX + 1f, inner.MaxX - 1f), 0f, _rng.Range(inner.MinZ + 1.4f, inner.MaxZ - 1.4f));
                float yaw = 90f * _rng.Range(0, 4);
                int k = TryCounter(r, c, size, yaw, role);
                if (k >= 0) return k;
            }
            return -1;
        }

        private int TryCounter(Room r, Vec3 centre, Vec3 size, float yaw, NpcRole role)
        {
            Vec3 f = Vec3.FromYawDeg(yaw);
            Vec3 front = centre + f * (size.Z / 2f + 0.6f);
            Vec3 back = centre - f * (size.Z / 2f + 0.55f);
            if (!PointClear(r, front) || !PointClear(r, back)) return -1;
            int k = Place(FurnitureKind.Counter, r, centre, size, yaw);
            if (k < 0) return -1;
            MakeAnchor(k);
            Npc(role, back, yaw, r.Id, false);
            _clear.Add(Around(back, 0.9f));
            return k;
        }

        /// <summary>A table with a chair on each long side (and NPCs in some), or false when the table does not fit.</summary>
        private bool TableSet(Room r, Vec3 centre, Vec3 tableSize, float yaw, NpcRole role, float seatedChance)
        {
            int t = Place(FurnitureKind.DiningTable, r, centre, tableSize, yaw);
            if (t < 0) return false;
            Vec3 f = Vec3.FromYawDeg(yaw);
            Vec3 cs = Size(FurnitureKind.Chair);
            for (int side = 0; side < 2; side++)
            {
                float sign = side == 0 ? 1f : -1f;
                Vec3 p = centre + f * (sign * (tableSize.Z / 2f + cs.Z / 2f + 0.05f));
                float chairYaw = NormYaw(side == 0 ? yaw + 180f : yaw);
                int c = Place(FurnitureKind.Chair, r, p, cs, chairYaw);
                if (c >= 0 && _rng.Chance(seatedChance))
                {
                    MakeAnchor(c);
                    Npc(role, p, chairYaw, r.Id, true);
                }
            }
            return true;
        }

        /// <summary>A desk with its chair on the user's side; an NPC sits there with the given chance.</summary>
        private bool DeskSet(Room r, Vec3 centre, Vec3 deskSize, float yaw, NpcRole role, float seatedChance)
        {
            Vec3 f = Vec3.FromYawDeg(yaw);
            Vec3 cs = Size(FurnitureKind.Chair);
            Vec3 chair = centre + f * (deskSize.Z / 2f + cs.Z / 2f + 0.1f);
            // Both must fit before either is placed.
            Rect2 df = FootprintOf(centre, deskSize, yaw), cf = FootprintOf(chair, cs, NormYaw(yaw + 180f));
            if (!Fits(r, df, true, 0.02f) || !Fits(r, cf, true, 0.02f)) return false;
            int d = Place(FurnitureKind.Desk, r, centre, deskSize, yaw);
            if (d < 0) return false;
            int c = Place(FurnitureKind.Chair, r, chair, cs, NormYaw(yaw + 180f));
            if (c >= 0 && _rng.Chance(seatedChance))
            {
                MakeAnchor(c);
                Npc(role, chair, NormYaw(yaw + 180f), r.Id, true);
            }
            return true;
        }

        /// <summary>Free-standing items on a grid over the room (aisles between), skipping cells that do not fit.</summary>
        private int Islands(Room r, FurnitureKind kind, Vec3 size, float yaw, float aisleX, float aisleZ, int max)
        {
            Rect2 inner = Inner(r);
            Rect2 fp = FootprintOf(Vec3.Zero, size, yaw);
            int n = 0;
            for (float z = inner.MinZ + 3.0f + fp.Depth / 2f; z + fp.Depth / 2f < inner.MaxZ - 1.2f && n < max; z += fp.Depth + aisleZ)
            {
                for (float x = inner.MinX + 1.3f + fp.Width / 2f; x + fp.Width / 2f < inner.MaxX - 1.3f && n < max; x += fp.Width + aisleX)
                {
                    if (Place(kind, r, new Vec3(x, 0f, z), size, yaw) >= 0) n++;
                }
            }
            return n;
        }

        // ---------------------------------------------------------------------------------------------- primitives

        private bool HasElevator
        {
            get
            {
                foreach (Portal p in _l.Portals) if (p.Kind == PortalKind.Elevator) return true;
                return false;
            }
        }

        private static Vec3 Size(FurnitureKind kind)
        {
            return FurnitureCatalog.DefaultSize(kind);
        }

        private static Rect2 Inner(Room r)
        {
            return r.Bounds.Inset(T / 2f);
        }

        private static float NormYaw(float yaw)
        {
            yaw %= 360f;
            if (yaw < 0f) yaw += 360f;
            float q = (float)Math.Round(yaw / 90f) * 90f;
            return q >= 360f ? 0f : q;
        }

        private static float Snap(float v)
        {
            return (float)Math.Round(v * 2f) / 2f;
        }

        private static Vec3 RightOf(float yaw)
        {
            return Vec3.FromYawDeg(yaw + 90f);
        }

        internal static Rect2 FootprintOf(Vec3 c, Vec3 size, float yaw)
        {
            bool swap = NormYaw(yaw) == 90f || NormYaw(yaw) == 270f;
            float hx = (swap ? size.Z : size.X) / 2f, hz = (swap ? size.X : size.Z) / 2f;
            return new Rect2(c.X - hx, c.Z - hz, c.X + hx, c.Z + hz);
        }

        private static Rect2 Around(Vec3 p, float half)
        {
            return new Rect2(p.X - half, p.Z - half, p.X + half, p.Z + half);
        }

        private static Rect2 Expand(Rect2 r, float d)
        {
            return new Rect2(r.MinX - d, r.MinZ - d, r.MaxX + d, r.MaxZ + d);
        }

        private static float DistToRect(float x, float z, Rect2 r)
        {
            float dx = Math.Max(Math.Max(r.MinX - x, 0f), x - r.MaxX), dz = Math.Max(Math.Max(r.MinZ - z, 0f), z - r.MaxZ);
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>True when the footprint is inside the room and clear of solid furniture (gap) and of keep-clear zones.</summary>
        private bool Fits(Room r, Rect2 fp, bool solid, float gap)
        {
            Rect2 inner = Inner(r);
            const float e = 1e-4f;
            if (fp.MinX < inner.MinX - e || fp.MaxX > inner.MaxX + e || fp.MinZ < inner.MinZ - e || fp.MaxZ > inner.MaxZ + e) return false;
            if (!solid) return true;
            Rect2 grown = Expand(fp, gap);
            foreach (FurnitureItem o in _l.Furniture)
            {
                if (o.Solid && grown.Overlaps(o.Footprint, 1e-4f)) return false;
            }
            foreach (Rect2 z in _clear)
            {
                if (fp.Overlaps(z, 1e-4f)) return false;
            }
            return true;
        }

        /// <summary>Places an item if it fits; returns its index or −1.</summary>
        private int Place(FurnitureKind kind, Room r, Vec3 centre, Vec3 size, float yaw, float gap = 0.03f)
        {
            yaw = NormYaw(yaw);
            Rect2 fp = FootprintOf(centre, size, yaw);
            bool solid = FurnitureCatalog.IsSolid(kind);
            if (!Fits(r, fp, solid, gap)) return -1;
            _l.Furniture.Add(new FurnitureItem
            {
                Kind = kind,
                Position = new Vec3(fp.Center.X, centre.Y, fp.Center.Z),
                Size = size,
                YawDeg = yaw,
                Room = r.Id,
                Solid = solid,
                Footprint = fp,
            });
            _anchor.Add(false);
            return _l.Furniture.Count - 1;
        }

        private void MakeAnchor(int index)
        {
            if (index >= 0) _anchor[index] = true;
        }

        private int Count(FurnitureKind kind)
        {
            int n = 0;
            foreach (FurnitureItem f in _l.Furniture) if (f.Kind == kind) n++;
            return n;
        }

        private bool HasInteraction(InteractionKind kind)
        {
            foreach (InteractionPoint p in _l.Interactions) if (p.Kind == kind) return true;
            return false;
        }

        /// <summary>True when a person can stand at the point: inside the room with a clear nav cell around it.</summary>
        private bool PointClear(Room r, Vec3 p)
        {
            Rect2 ok = r.Bounds.Inset(T / 2f + C + CellSlack);
            if (!ok.Contains(p.X, p.Z)) return false;
            foreach (FurnitureItem o in _l.Furniture)
            {
                if (o.Solid && DistToRect(p.X, p.Z, o.Footprint) < C + CellSlack) return false;
            }
            return true;
        }

        /// <summary>Adds an interaction point when the spot is clear; reserves the space around it. Returns success.</summary>
        private bool Interaction(InteractionKind kind, Vec3 p, float yaw, int room, ShopType shop, bool check, int owner = -1)
        {
            if (room < 0 || room >= _l.Rooms.Count) return false;
            if (_l.RoomAt(p.X, p.Z) != room) return false;
            if (check && !PointClear(_l.Rooms[room], p)) return false;
            if (_checkReach)
            {
                _l.Nav = BuildNav(_l);
                if (!_l.Nav.IsFree(p.X, p.Z) || !_l.Nav.Connected(_l.EntranceSpawn, p)) return false;
            }
            _l.Interactions.Add(new InteractionPoint { Kind = kind, Position = new Vec3(p.X, 0f, p.Z), YawDeg = MathUtil.WrapDeg(yaw), Room = room, Shop = shop });
            _owner.Add(owner);
            _clear.Add(Around(p, 0.55f));
            if (owner >= 0) MakeAnchor(owner);
            return true;
        }

        /// <summary>An interaction in front of an item (where a person uses it).</summary>
        private bool InteractFront(InteractionKind kind, int item, ShopType shop)
        {
            FurnitureItem f = _l.Furniture[item];
            Vec3 dir = Vec3.FromYawDeg(f.YawDeg);
            for (float d = 0.6f; d <= 1.0f; d += 0.2f)
            {
                Vec3 p = f.Position + dir * (f.Size.Z / 2f + d);
                if (Interaction(kind, new Vec3(p.X, 0f, p.Z), NormYaw(f.YawDeg + 180f), f.Room, shop, true, item)) return true;
            }
            return false;
        }

        private void Npc(NpcRole role, Vec3 p, float yaw, int room, bool seated)
        {
            if (_l.RoomAt(p.X, p.Z) != room) return;
            _l.NpcSpots.Add(new NpcSpot { Role = role, Position = new Vec3(p.X, 0f, p.Z), YawDeg = MathUtil.WrapDeg(yaw), Room = room, Seated = seated });
        }

        // --------------------------------------------------------------------------------------------- walls

        /// <summary>Sides of a room with no doorway or portal on them (all four sides if every side has one).</summary>
        private List<Side> FreeSides(Room r)
        {
            var list = new List<Side>();
            foreach (Side s in AllSides) if (!SideHasDoor(r, s)) list.Add(s);
            // Shuffle deterministically so layouts vary.
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Range(0, i + 1);
                Side t = list[i]; list[i] = list[j]; list[j] = t;
            }
            if (list.Count == 0) list.AddRange(AllSides);
            return list;
        }

        private bool SideHasDoor(Room r, Side s)
        {
            const float e = 0.01f;
            Rect2 b = r.Bounds;
            foreach (Doorway d in _l.Doorways)
            {
                if (d.RoomA != r.Id && d.RoomB != r.Id) continue;
                if (d.WallAlongX)
                {
                    if (s == Side.Front && Math.Abs(d.Center.Z - b.MinZ) < e) return true;
                    if (s == Side.Back && Math.Abs(d.Center.Z - b.MaxZ) < e) return true;
                }
                else
                {
                    if (s == Side.Left && Math.Abs(d.Center.X - b.MinX) < e) return true;
                    if (s == Side.Right && Math.Abs(d.Center.X - b.MaxX) < e) return true;
                }
            }
            foreach (Portal p in _l.Portals)
            {
                if (p.Room != r.Id) continue;
                if (s == Side.Front && Math.Abs(p.Position.Z - b.MinZ) < 0.2f) return true;
                if (s == Side.Back && Math.Abs(p.Position.Z - b.MaxZ) < 0.2f) return true;
            }
            return false;
        }

        private static Side Opposite(Side s)
        {
            switch (s)
            {
                case Side.Front: return Side.Back;
                case Side.Back: return Side.Front;
                case Side.Left: return Side.Right;
                default: return Side.Left;
            }
        }

        /// <summary>Yaw of an item standing against the wall and facing into the room.</summary>
        private static float InwardYaw(Side s)
        {
            switch (s)
            {
                case Side.Front: return 0f;
                case Side.Back: return 180f;
                case Side.Left: return 90f;
                default: return 270f;
            }
        }

        private static float WallLength(Room r, Side s)
        {
            Rect2 inner = Inner(r);
            return s == Side.Front || s == Side.Back ? inner.Width : inner.Depth;
        }

        private static float AlongStart(Room r, Side s)
        {
            Rect2 inner = Inner(r);
            return s == Side.Front || s == Side.Back ? inner.MinX : inner.MinZ;
        }

        private static float AlongEnd(Room r, Side s)
        {
            Rect2 inner = Inner(r);
            return s == Side.Front || s == Side.Back ? inner.MaxX : inner.MaxZ;
        }

        /// <summary>The point at <paramref name="along"/> on the wall's axis, <paramref name="offset"/> into the room from its inner face.</summary>
        private static Vec3 WallPoint(Room r, Side s, float along, float offset)
        {
            Rect2 inner = Inner(r);
            switch (s)
            {
                case Side.Front: return new Vec3(along, 0f, inner.MinZ + offset);
                case Side.Back: return new Vec3(along, 0f, inner.MaxZ - offset);
                case Side.Left: return new Vec3(inner.MinX + offset, 0f, along);
                default: return new Vec3(inner.MaxX - offset, 0f, along);
            }
        }

        private static float Along(FurnitureItem f, Side s)
        {
            return s == Side.Front || s == Side.Back ? f.Position.X : f.Position.Z;
        }

        private int OnWallAt(Room r, Side s, FurnitureKind kind, Vec3 size, float along)
        {
            return Place(kind, r, WallPoint(r, s, along, size.Z / 2f + 0.02f), size, InwardYaw(s));
        }

        private int OnWallAuto(Room r, Side s, FurnitureKind kind, Vec3 size)
        {
            float a0 = AlongStart(r, s) + size.X / 2f + 0.02f, a1 = AlongEnd(r, s) - size.X / 2f - 0.02f;
            if (a1 < a0) return -1;
            foreach (float f in Fractions)
            {
                int k = OnWallAt(r, s, kind, size, a0 + (a1 - a0) * f);
                if (k >= 0) return k;
            }
            return -1;
        }

        /// <summary>A flat wall-mounted item (non-solid) centred at height <paramref name="centreY"/>.</summary>
        private int WallItem(Room r, Side s, FurnitureKind kind, Vec3 size, float centreY)
        {
            float a0 = AlongStart(r, s) + size.X / 2f + 0.02f, a1 = AlongEnd(r, s) - size.X / 2f - 0.02f;
            if (a1 < a0) return -1;
            Vec3 p = WallPoint(r, s, (a0 + a1) / 2f, size.Z / 2f + 0.01f);
            return Place(kind, r, new Vec3(p.X, Math.Min(centreY, _l.CeilingHeight - size.Y / 2f - 0.1f), p.Z), size, InwardYaw(s));
        }

        private int WallRun(Room r, Side s, FurnitureKind kind, Vec3 size, float gap, int max)
        {
            float a = AlongStart(r, s) + size.X / 2f + 0.05f, end = AlongEnd(r, s) - size.X / 2f - 0.05f;
            int n = 0;
            while (a <= end && n < max)
            {
                if (OnWallAt(r, s, kind, size, a) >= 0)
                {
                    n++;
                    a += size.X + gap;
                }
                else
                {
                    a += 0.25f;
                }
            }
            return n;
        }

        private int Corner(Room r, FurnitureKind kind)
        {
            Rect2 inner = Inner(r);
            Vec3 s = Size(kind);
            float hx = s.X / 2f + 0.05f, hz = s.Z / 2f + 0.05f;
            int start = _rng.Range(0, 4);
            for (int i = 0; i < 4; i++)
            {
                int c = (start + i) % 4;
                float x = (c & 1) == 0 ? inner.MinX + hx : inner.MaxX - hx;
                float z = (c & 2) == 0 ? inner.MinZ + hz : inner.MaxZ - hz;
                int k = Place(kind, r, new Vec3(x, 0f, z), s, 0f);
                if (k >= 0) return k;
            }
            return -1;
        }

        /// <summary>Homes always get a bed to sleep and save in: beside any existing bed, or a new single bed anywhere.</summary>
        private void EnsureBed()
        {
            if (HasInteraction(InteractionKind.Bed)) return;
            for (int i = 0; i < _l.Furniture.Count; i++)
            {
                FurnitureItem b = _l.Furniture[i];
                if (b.Kind != FurnitureKind.Bed && b.Kind != FurnitureKind.SingleBed) continue;
                if (BedSide(i)) return;
            }
            var rooms = new List<Room>(_l.Rooms);
            rooms.Sort((a, b) => { int c = b.Bounds.Area.CompareTo(a.Bounds.Area); return c != 0 ? c : a.Id.CompareTo(b.Id); });
            foreach (Room r in rooms)
            {
                if (r.Kind == RoomKind.Corridor) continue;
                foreach (Side s in AllSides)
                {
                    if (SideHasDoor(r, s)) continue;
                    Vec3 size = Size(FurnitureKind.SingleBed);
                    float a0 = AlongStart(r, s) + size.X / 2f + 0.02f, a1 = AlongEnd(r, s) - size.X / 2f - 0.02f;
                    for (float a = a0; a <= a1; a += 0.25f)
                    {
                        int k = OnWallAt(r, s, FurnitureKind.SingleBed, size, a);
                        if (k < 0) continue;
                        if (BedSide(k)) return;
                        RemoveFurniture(k);
                    }
                }
            }
        }

        /// <summary>Adds the Bed interaction beside (or at the foot of) a bed; true on success.</summary>
        private bool BedSide(int bed)
        {
            FurnitureItem b = _l.Furniture[bed];
            Vec3 right = RightOf(b.YawDeg), fwd = Vec3.FromYawDeg(b.YawDeg);
            for (float d = 0.6f; d <= 1.01f; d += 0.2f)
            {
                for (int k = 0; k < 2; k++)
                {
                    float sign = k == 0 ? 1f : -1f;
                    Vec3 p = b.Position + right * (sign * (b.Size.X / 2f + d));
                    if (Interaction(InteractionKind.Bed, p, MathUtil.WrapDeg(b.YawDeg - 90f * sign), b.Room, ShopType.None, true, bed)) return true;
                }
                Vec3 foot = b.Position + fwd * (b.Size.Z / 2f + d);
                if (Interaction(InteractionKind.Bed, foot, NormYaw(b.YawDeg + 180f), b.Room, ShopType.None, true, bed)) return true;
            }
            return false;
        }

        /// <summary>Homes always get somewhere to change clothes: a wardrobe on any free wall of any room, largest first.</summary>
        private void EnsureWardrobe()
        {
            if (HasInteraction(InteractionKind.Wardrobe)) return;
            for (int i = 0; i < _l.Furniture.Count; i++)
            {
                if (_l.Furniture[i].Kind == FurnitureKind.Wardrobe && InteractFront(InteractionKind.Wardrobe, i, ShopType.None)) return;
            }
            var rooms = new List<Room>(_l.Rooms);
            rooms.Sort((a, b) => { int c = b.Bounds.Area.CompareTo(a.Bounds.Area); return c != 0 ? c : a.Id.CompareTo(b.Id); });
            Vec3 standard = Size(FurnitureKind.Wardrobe);
            foreach (Vec3 size in new[] { standard, new Vec3(0.8f, standard.Y, 0.55f) })
            {
                foreach (Room r in rooms)
                {
                    if (r.Kind == RoomKind.Corridor) continue;
                    foreach (Side s in AllSides)
                    {
                        if (SideHasDoor(r, s)) continue;
                        float a0 = AlongStart(r, s) + size.X / 2f + 0.02f, a1 = AlongEnd(r, s) - size.X / 2f - 0.02f;
                        for (float a = a0; a <= a1; a += 0.25f)
                        {
                            int w = OnWallAt(r, s, FurnitureKind.Wardrobe, size, a);
                            if (w < 0) continue;
                            if (InteractFront(InteractionKind.Wardrobe, w, ShopType.None)) return;
                            RemoveFurniture(w);
                        }
                    }
                }
            }
        }

        // ------------------------------------------------------------------------------------ reachability and nav

        private void ReserveDoors()
        {
            // The street door and the elevator: a clear approach 2.4 m deep.
            _clear.Add(new Rect2(-1.0f, 0f, 1.0f, 2.4f));
            if (HasElevator) _clear.Add(new Rect2(-1.0f, _l.Depth - 2.4f, 1.0f, _l.Depth));
            foreach (Doorway d in _l.Doorways)
            {
                float h = d.Width / 2f + 0.3f;
                _clear.Add(d.WallAlongX
                    ? new Rect2(d.Center.X - h, d.Center.Z - 1.3f, d.Center.X + h, d.Center.Z + 1.3f)
                    : new Rect2(d.Center.X - 1.3f, d.Center.Z - h, d.Center.X + 1.3f, d.Center.Z + h));
            }
            foreach (Room r in _l.Rooms)
            {
                if (r.Kind == RoomKind.Corridor) _clear.Add(r.Bounds);
            }
            // In corridor layouts keep the way from the street door to the corridor clear.
            foreach (Doorway d in _l.Doorways)
            {
                if ((d.RoomA == 0 || d.RoomB == 0) && d.WallAlongX && Math.Abs(d.Center.X) < 0.01f)
                {
                    float h = d.Width / 2f + 0.3f;
                    _clear.Add(new Rect2(-h, 0f, h, d.Center.Z));
                }
            }
        }

        internal static InteriorNavGrid BuildNav(InteriorLayout l)
        {
            var nav = new InteriorNavGrid(new Rect2(-l.Width / 2f, 0f, l.Width / 2f, l.Depth));
            foreach (Room r in l.Rooms) nav.FillRect(r.Bounds.Inset(T / 2f + C), true);
            foreach (Doorway d in l.Doorways)
            {
                float h = d.Width / 2f - C, across = T / 2f + C + 0.01f;
                if (h <= 0f) continue;
                nav.FillRect(d.WallAlongX
                    ? new Rect2(d.Center.X - h, d.Center.Z - across, d.Center.X + h, d.Center.Z + across)
                    : new Rect2(d.Center.X - across, d.Center.Z - h, d.Center.X + across, d.Center.Z + h), true);
            }
            foreach (FurnitureItem f in l.Furniture)
            {
                if (f.Solid) nav.FillRect(Expand(f.Footprint, C), false);
            }
            return nav;
        }

        private void ResolveReachability()
        {
            var targets = new List<Vec3>();
            var owners = new List<int>();
            var failed = new List<int>();
            Vec3 lastTarget = new Vec3(float.NaN, 0f, 0f);
            int removalsForTarget = 0;
            for (int iteration = 0; iteration < 400; iteration++)
            {
                _l.Nav = BuildNav(_l);
                SnapInteractions();
                targets.Clear();
                owners.Clear();
                foreach (Doorway d in _l.Doorways) { targets.Add(d.Center); owners.Add(-1); }
                for (int i = 0; i < _l.Interactions.Count; i++) { targets.Add(_l.Interactions[i].Position); owners.Add(i); }
                if (HasElevator) { targets.Add(_l.ElevatorSpawn); owners.Add(-1); }
                foreach (Room r in _l.Rooms)
                {
                    Vec3 c;
                    if (_l.Nav.NearestFree(r.Bounds.Center, Math.Max(r.Bounds.Width, r.Bounds.Depth), out c) && _l.RoomAt(c.X, c.Z) == r.Id)
                    {
                        targets.Add(c);
                        owners.Add(-1);
                    }
                }
                if (_l.Nav.AllConnected(_l.EntranceSpawn, targets, failed)) return;

                Vec3 target = targets[failed[0]];
                if (Vec3.DistanceXZ(target, lastTarget) > 1e-3f)
                {
                    lastTarget = target;
                    removalsForTarget = 0;
                }
                // Remove the nearest unprotected piece that could be in the way (within 2.5 m), a few times at most.
                int victim = NearestRemovable(target, removalsForTarget < 8 ? 2.5f : -1f);
                if (victim >= 0)
                {
                    RemoveFurniture(victim);
                    removalsForTarget++;
                    continue;
                }
                int owner = owners[failed[0]];
                if (owner >= 0 && _l.Interactions[owner].Kind != InteractionKind.ExitDoor && _l.Interactions[owner].Kind != InteractionKind.ElevatorDoor)
                {
                    // Protected furniture boxes this spot in: give up on the spot (required ones are placed again later).
                    _l.Interactions.RemoveAt(owner);
                    _owner.RemoveAt(owner);
                    continue;
                }
                victim = NearestRemovable(target, float.MaxValue);
                if (victim < 0) break;
                RemoveFurniture(victim);
            }
            _l.Nav = BuildNav(_l);
        }

        private int NearestRemovable(Vec3 target, float maxDistance)
        {
            int victim = -1;
            float best = float.MaxValue;
            for (int i = 0; i < _l.Furniture.Count; i++)
            {
                if (_anchor[i] || !_l.Furniture[i].Solid) continue;
                float dist = DistToRect(target.X, target.Z, _l.Furniture[i].Footprint);
                if (dist <= maxDistance && dist < best) { best = dist; victim = i; }
            }
            return victim;
        }

        private void SnapInteractions()
        {
            for (int i = 0; i < _l.Interactions.Count; i++)
            {
                InteractionPoint p = _l.Interactions[i];
                if (_l.Nav.IsFree(p.Position.X, p.Position.Z)) continue;
                Vec3 near;
                if (_l.Nav.NearestFree(p.Position, 0.6f, out near) && _l.RoomAt(near.X, near.Z) == p.Room)
                {
                    p.Position = near;
                    _l.Interactions[i] = p;
                }
            }
        }

        /// <summary>Free cells the player cannot reach from the street door are of no use: mark them blocked.</summary>
        private void BlockUnreachable()
        {
            InteriorNavGrid nav = _l.Nav;
            var all = new List<Vec3>();
            for (int cz = 0; cz < nav.CellsZ; cz++)
            {
                for (int cx = 0; cx < nav.CellsX; cx++)
                {
                    float x = nav.CentreX(cx), z = nav.CentreZ(cz);
                    if (nav.IsFree(x, z)) all.Add(new Vec3(x, 0f, z));
                }
            }
            var failed = new List<int>();
            nav.AllConnected(_l.EntranceSpawn, all, failed);
            foreach (int i in failed) nav.Set(all[i].X, all[i].Z, false);
        }

        private void RemoveFurniture(int index)
        {
            _l.Furniture.RemoveAt(index);
            _anchor.RemoveAt(index);
            for (int i = 0; i < _owner.Count; i++)
            {
                if (_owner[i] > index) _owner[i]--;
                else if (_owner[i] == index) _owner[i] = -1;
            }
        }

        /// <summary>Standing NPC spots on free cells: residents at home, staff at work, a few customers.</summary>
        private void StandingSpots()
        {
            NpcRole role;
            int count;
            switch (_l.Archetype)
            {
                case InteriorArchetype.House:
                case InteriorArchetype.Apartment:
                case InteriorArchetype.ApartmentLobby: role = NpcRole.Resident; count = 2; break;
                case InteriorArchetype.PoliceStation: role = NpcRole.Cop; count = 3; break;
                case InteriorArchetype.Hospital: role = NpcRole.Doctor; count = 3; break;
                case InteriorArchetype.Office:
                case InteriorArchetype.Warehouse:
                case InteriorArchetype.Garage:
                case InteriorArchetype.FireStation:
                case InteriorArchetype.ServiceHall:
                case InteriorArchetype.School: role = NpcRole.Worker; count = 2; break;
                default: role = NpcRole.Visitor; count = 2; break;
            }
            var spots = new List<Vec3>();
            for (int cz = 0; cz < _l.Nav.CellsZ; cz += 2)
            {
                for (int cx = 0; cx < _l.Nav.CellsX; cx += 2)
                {
                    float x = _l.Nav.CentreX(cx), z = _l.Nav.CentreZ(cz);
                    if (!_l.Nav.IsFree(x, z)) continue;
                    int room = _l.RoomAt(x, z);
                    if (room < 0 || _l.Rooms[room].Kind == RoomKind.Corridor) continue;
                    if (z < 2.6f && Math.Abs(x) < 1.2f) continue; // not in the doorway
                    bool near = false;
                    foreach (InteractionPoint p in _l.Interactions) if (Vec3.DistanceXZ(p.Position, new Vec3(x, 0f, z)) < 1.2f) { near = true; break; }
                    if (!near) spots.Add(new Vec3(x, 0f, z));
                }
            }
            for (int i = 0; i < count && spots.Count > 0; i++)
            {
                int k = _rng.Range(0, spots.Count);
                Vec3 p = spots[k];
                spots.RemoveAt(k);
                Npc(role, p, _rng.Range(0, 4) * 90f, _l.RoomAt(p.X, p.Z), false);
            }
        }
    }
}
