using System;
using System.Collections.Generic;
using GtaSkylines.Game.Interiors;
using GtaSkylines.Game.Mathx;
using Xunit;
using static GtaSkylines.Game.Tests.Interiors.InteriorTestSupport;

namespace GtaSkylines.Game.Tests.Interiors
{
    /// <summary>Structural invariants of every generated interior, for every archetype over many ids and sizes.</summary>
    public class InteriorLayoutTests
    {
        private const float Eps = 0.01f;
        private const string Arch = nameof(InteriorTestSupport.AllArchetypes);

        private static void ForEach(InteriorArchetype a, Action<InteriorLayout, string> check)
        {
            InteriorLayout[] ls = Layouts(a);
            Assert.Equal(Cases, ls.Length);
            for (int i = 0; i < ls.Length; i++)
            {
                Assert.NotNull(ls[i]);
                check(ls[i], Tag(a, i));
            }
        }

        private static InteractionPoint? First(InteriorLayout l, InteractionKind k)
        {
            foreach (InteractionPoint p in l.Interactions) if (p.Kind == k) return p;
            return null;
        }

        private static int Count(InteriorLayout l, InteractionKind k)
        {
            int n = 0;
            foreach (InteractionPoint p in l.Interactions) if (p.Kind == k) n++;
            return n;
        }

        private static int Count(InteriorLayout l, FurnitureKind k)
        {
            int n = 0;
            foreach (FurnitureItem f in l.Furniture) if (f.Kind == k) n++;
            return n;
        }

        [Theory, MemberData(Arch, MemberType = typeof(InteriorTestSupport))]
        public void IdentityFieldsAreSet(InteriorArchetype a)
        {
            InteriorLayout[] ls = Layouts(a);
            for (int i = 0; i < ls.Length; i++)
            {
                Assert.Equal(a, ls[i].Archetype);
                Assert.Equal(CaseVariant(a, i), ls[i].Variant);
                Assert.Equal(IdFor(i), ls[i].BuildingId);
            }
        }

        [Theory, MemberData(Arch, MemberType = typeof(InteriorTestSupport))]
        public void FootprintIsClampedAndHalfMetreAligned(InteriorArchetype a)
        {
            ForEach(a, (l, t) =>
            {
                Assert.InRange(l.Width, InteriorGenerator.MinSide, InteriorGenerator.MaxSide);
                Assert.InRange(l.Depth, InteriorGenerator.MinSide, InteriorGenerator.MaxSide);
                Assert.True(l.Width * 2f == (float)Math.Round(l.Width * 2f), t + " width " + l.Width);
                Assert.True(l.Depth * 2f == (float)Math.Round(l.Depth * 2f), t + " depth " + l.Depth);
                Assert.InRange(l.CeilingHeight, 2.8f, 4.5f);
            });
        }

        [Fact]
        public void ApartmentFootprintFollowsTheNineToSixteenByEightToFourteenRule()
        {
            ForEach(InteriorArchetype.Apartment, (l, t) =>
            {
                Assert.InRange(l.Width, 9f, 16f);
                Assert.InRange(l.Depth, 8f, 14f);
            });
        }

        [Fact]
        public void ApartmentVariantsDiffer()
        {
            var hashes = new HashSet<uint>();
            foreach (InteriorLayout l in Layouts(InteriorArchetype.Apartment)) hashes.Add(l.ContentHash());
            Assert.True(hashes.Count >= 10, "apartment variants should give different interiors, distinct=" + hashes.Count);
        }

        [Theory, MemberData(Arch, MemberType = typeof(InteriorTestSupport))]
        public void RoomsAreInsideTheFootprintAndDoNotOverlap(InteriorArchetype a)
        {
            ForEach(a, (l, t) =>
            {
                Assert.NotEmpty(l.Rooms);
                var foot = new Rect2(-l.Width / 2f, 0f, l.Width / 2f, l.Depth);
                for (int i = 0; i < l.Rooms.Count; i++)
                {
                    Room r = l.Rooms[i];
                    Assert.Equal(i, r.Id);
                    Assert.True(r.Bounds.MinX >= foot.MinX - Eps && r.Bounds.MaxX <= foot.MaxX + Eps &&
                                r.Bounds.MinZ >= foot.MinZ - Eps && r.Bounds.MaxZ <= foot.MaxZ + Eps, t + " room " + i + " " + r.Bounds);
                    Assert.True(r.Bounds.Width >= 2f - Eps && r.Bounds.Depth >= 2f - Eps, t + " room " + i + " too small " + r.Bounds);
                    Assert.InRange(r.FloorRecipe, 0, l.Recipes.Count - 1);
                    Assert.InRange(r.WallRecipe, 0, l.Recipes.Count - 1);
                    for (int j = i + 1; j < l.Rooms.Count; j++)
                        Assert.False(r.Bounds.Overlaps(l.Rooms[j].Bounds, Eps), t + " rooms " + i + " and " + j + " overlap");
                }
            });
        }

        [Theory, MemberData(Arch, MemberType = typeof(InteriorTestSupport))]
        public void RoomsTileTheFootprint(InteriorArchetype a)
        {
            ForEach(a, (l, t) =>
            {
                double sum = 0;
                foreach (Room r in l.Rooms) sum += (double)r.Bounds.Width * r.Bounds.Depth;
                double total = (double)l.Width * l.Depth;
                Assert.True(Math.Abs(sum - total) <= 0.01 * total, t + " rooms cover " + sum + " of " + total);
            });
        }

        [Theory, MemberData(Arch, MemberType = typeof(InteriorTestSupport))]
        public void ExactlyOneEntrancePortalCentredOnTheFrontWall(InteriorArchetype a)
        {
            ForEach(a, (l, t) =>
            {
                int entrances = 0;
                foreach (Portal p in l.Portals)
                {
                    if (p.Kind != PortalKind.Entrance) continue;
                    entrances++;
                    Assert.True(Math.Abs(p.Position.X) <= Eps, t + " entrance x " + p.Position.X);
                    Assert.InRange(p.Position.Z, -Eps, InteriorLayout.WallThickness + Eps);
                    Assert.True(Math.Abs(p.Position.Y) <= Eps, t + " entrance y");
                    Assert.Equal(0, p.Room);
                    Assert.True(Vec3.FromYawDeg(p.InwardYawDeg).Z > 0.99f, t + " entrance faces into the room");
                }
                Assert.Equal(1, entrances);
                Assert.True(l.Rooms[0].Bounds.MinX <= 0f && l.Rooms[0].Bounds.MaxX >= 0f && l.Rooms[0].Bounds.MinZ <= Eps, t + " room 0 holds the entrance");
                Assert.Equal(0, l.RoomAt(0f, 0.5f));
            });
        }

        [Theory, MemberData(Arch, MemberType = typeof(InteriorTestSupport))]
        public void ElevatorPortalsExistOnlyWhereAllowed(InteriorArchetype a)
        {
            ForEach(a, (l, t) =>
            {
                int elevators = 0;
                foreach (Portal p in l.Portals) if (p.Kind == PortalKind.Elevator) elevators++;
                if (a == InteriorArchetype.ApartmentLobby) Assert.Equal(1, elevators);
                else if (a == InteriorArchetype.Apartment) Assert.InRange(elevators, 0, 1);
                else Assert.Equal(0, elevators);
                Assert.Equal(elevators, Count(l, InteractionKind.ElevatorDoor));
                foreach (Portal p in l.Portals)
                {
                    Assert.InRange(p.Room, 0, l.Rooms.Count - 1);
                    Assert.InRange(p.Position.X, -l.Width / 2f - Eps, l.Width / 2f + Eps);
                    Assert.InRange(p.Position.Z, -Eps, l.Depth + Eps);
                }
                if (elevators == 1)
                {
                    Assert.True(l.Nav.IsFree(l.ElevatorSpawn.X, l.ElevatorSpawn.Z), t + " elevator spawn must be free");
                    Assert.True(l.Nav.Connected(l.EntranceSpawn, l.ElevatorSpawn), t + " elevator spawn must be reachable");
                }
            });
        }

        [Theory, MemberData(Arch, MemberType = typeof(InteriorTestSupport))]
        public void EntranceSpawnIsInsideNearTheDoorAndFree(InteriorArchetype a)
        {
            ForEach(a, (l, t) =>
            {
                Assert.InRange(l.EntranceSpawn.Z, 0.8f, 1.6f);
                Assert.True(Math.Abs(l.EntranceSpawn.Y) <= Eps);
                Assert.True(Math.Abs(l.EntranceSpawn.X) <= 1.0f, t + " spawn x " + l.EntranceSpawn.X);
                Assert.Equal(0, l.RoomAt(l.EntranceSpawn.X, l.EntranceSpawn.Z));
                Assert.True(Vec3.FromYawDeg(l.EntranceSpawnYawDeg).Z > 0.99f, t + " spawn faces into the room");
                Assert.True(l.Nav.IsFree(l.EntranceSpawn.X, l.EntranceSpawn.Z), t + " entrance spawn must be on a free cell");
            });
        }

        [Theory, MemberData(Arch, MemberType = typeof(InteriorTestSupport))]
        public void ExitDoorInteractionStandsInFrontOfTheEntrance(InteriorArchetype a)
        {
            ForEach(a, (l, t) =>
            {
                Assert.Equal(1, Count(l, InteractionKind.ExitDoor));
                InteractionPoint e = First(l, InteractionKind.ExitDoor).Value;
                Assert.True(Math.Abs(e.Position.X) <= 1.0f, t + " exit x " + e.Position.X);
                Assert.InRange(e.Position.Z, 0f, 2.0f);
                Assert.Equal(0, e.Room);
                Assert.True(Vec3.FromYawDeg(e.YawDeg).Z < -0.99f, t + " exit faces the door (towards -z)");
                Assert.Equal(ShopType.None, e.Shop);
            });
        }

        [Theory, MemberData(Arch, MemberType = typeof(InteriorTestSupport))]
        public void DoorwaysAreWideAndLieOnTheSharedEdge(InteriorArchetype a)
        {
            ForEach(a, (l, t) =>
            {
                foreach (Doorway d in l.Doorways)
                {
                    Assert.True(d.Width >= InteriorGenerator.DoorWidth - 1e-4f, t + " doorway width " + d.Width);
                    Assert.InRange(d.Height, 2.0f, l.CeilingHeight);
                    Assert.NotEqual(d.RoomA, d.RoomB);
                    Assert.InRange(d.RoomA, 0, l.Rooms.Count - 1);
                    Assert.InRange(d.RoomB, 0, l.Rooms.Count - 1);
                    Rect2 ra = l.Rooms[d.RoomA].Bounds, rb = l.Rooms[d.RoomB].Bounds;
                    float half = d.Width / 2f - Eps;
                    if (d.WallAlongX)
                    {
                        bool ab = Math.Abs(ra.MaxZ - rb.MinZ) <= Eps && Math.Abs(d.Center.Z - ra.MaxZ) <= Eps;
                        bool ba = Math.Abs(rb.MaxZ - ra.MinZ) <= Eps && Math.Abs(d.Center.Z - rb.MaxZ) <= Eps;
                        Assert.True(ab || ba, t + " doorway not on a shared z edge");
                        Assert.True(d.Center.X - half >= Math.Max(ra.MinX, rb.MinX) - Eps && d.Center.X + half <= Math.Min(ra.MaxX, rb.MaxX) + Eps,
                            t + " doorway extends beyond the shared edge");
                    }
                    else
                    {
                        bool ab = Math.Abs(ra.MaxX - rb.MinX) <= Eps && Math.Abs(d.Center.X - ra.MaxX) <= Eps;
                        bool ba = Math.Abs(rb.MaxX - ra.MinX) <= Eps && Math.Abs(d.Center.X - rb.MaxX) <= Eps;
                        Assert.True(ab || ba, t + " doorway not on a shared x edge");
                        Assert.True(d.Center.Z - half >= Math.Max(ra.MinZ, rb.MinZ) - Eps && d.Center.Z + half <= Math.Min(ra.MaxZ, rb.MaxZ) + Eps,
                            t + " doorway extends beyond the shared edge");
                    }
                }
            });
        }

        [Theory, MemberData(Arch, MemberType = typeof(InteriorTestSupport))]
        public void EveryRoomIsReachableFromRoomZeroThroughDoorways(InteriorArchetype a)
        {
            ForEach(a, (l, t) =>
            {
                var seen = new bool[l.Rooms.Count];
                var stack = new Stack<int>();
                seen[0] = true;
                stack.Push(0);
                while (stack.Count > 0)
                {
                    int r = stack.Pop();
                    foreach (Doorway d in l.Doorways)
                    {
                        int other = d.RoomA == r ? d.RoomB : d.RoomB == r ? d.RoomA : -1;
                        if (other >= 0 && !seen[other]) { seen[other] = true; stack.Push(other); }
                    }
                }
                for (int i = 0; i < seen.Length; i++) Assert.True(seen[i], t + " room " + i + " unreachable");
            });
        }

        [Theory, MemberData(Arch, MemberType = typeof(InteriorTestSupport))]
        public void FurnitureStaysInsideItsRoomAndSolidPiecesDoNotOverlap(InteriorArchetype a)
        {
            ForEach(a, (l, t) =>
            {
                var solids = new List<FurnitureItem>();
                foreach (FurnitureItem f in l.Furniture)
                {
                    Assert.InRange(f.Room, 0, l.Rooms.Count - 1);
                    Rect2 inner = l.Rooms[f.Room].Bounds.Inset(InteriorLayout.WallThickness / 2f - Eps);
                    Assert.True(f.Footprint.MinX >= inner.MinX && f.Footprint.MaxX <= inner.MaxX &&
                                f.Footprint.MinZ >= inner.MinZ && f.Footprint.MaxZ <= inner.MaxZ,
                        t + " " + f.Kind + " " + f.Footprint + " outside room " + f.Room + " " + l.Rooms[f.Room].Bounds);
                    Assert.True(f.YawDeg == 0f || f.YawDeg == 90f || f.YawDeg == 180f || f.YawDeg == 270f, t + " yaw " + f.YawDeg);
                    bool quarter = f.YawDeg == 90f || f.YawDeg == 270f;
                    float fx = quarter ? f.Size.Z : f.Size.X, fz = quarter ? f.Size.X : f.Size.Z;
                    Assert.True(Near(f.Footprint.Width, fx, Eps) && Near(f.Footprint.Depth, fz, Eps), t + " " + f.Kind + " footprint does not match size/yaw");
                    Assert.True(Near(f.Footprint.Center.X, f.Position.X, Eps) && Near(f.Footprint.Center.Z, f.Position.Z, Eps), t + " " + f.Kind + " footprint not centred on position");
                    if (f.Kind == FurnitureKind.Rug || f.Kind == FurnitureKind.Whiteboard) Assert.False(f.Solid, t + " " + f.Kind + " must not be solid");
                    if (f.Solid) solids.Add(f);
                }
                for (int i = 0; i < solids.Count; i++)
                    for (int j = i + 1; j < solids.Count; j++)
                        Assert.False(solids[i].Footprint.Overlaps(solids[j].Footprint, Eps),
                            t + " " + solids[i].Kind + " overlaps " + solids[j].Kind);
            });
        }

        [Theory, MemberData(Arch, MemberType = typeof(InteriorTestSupport))]
        public void InteractionsAndSpawnsAreFreeAndConnectedToTheEntrance(InteriorArchetype a)
        {
            ForEach(a, (l, t) =>
            {
                Assert.NotNull(l.Nav);
                Assert.True(l.Nav.FreeCount() > 0);
                foreach (InteractionPoint p in l.Interactions)
                {
                    Assert.InRange(p.Room, 0, l.Rooms.Count - 1);
                    Assert.Equal(p.Room, l.RoomAt(p.Position.X, p.Position.Z));
                    Assert.True(l.Nav.IsFree(p.Position.X, p.Position.Z), t + " " + p.Kind + " at " + p.Position + " is not on a free cell");
                    Assert.True(l.Nav.Connected(l.EntranceSpawn, p.Position), t + " " + p.Kind + " not connected to the entrance");
                    if (p.Kind != InteractionKind.ShopCounter) Assert.Equal(ShopType.None, p.Shop);
                }
            });
        }

        [Theory, MemberData(Arch, MemberType = typeof(InteriorTestSupport))]
        public void EveryRoomAndDoorwayHasAFreeCellConnectedToTheEntrance(InteriorArchetype a)
        {
            ForEach(a, (l, t) =>
            {
                List<Vec3> free = FreeCells(l.Nav);
                for (int r = 0; r < l.Rooms.Count; r++)
                {
                    Vec3? cell = null;
                    foreach (Vec3 c in free) if (l.RoomAt(c.X, c.Z) == r) { cell = c; break; }
                    Assert.True(cell.HasValue, t + " room " + r + " has no free cell");
                    Assert.True(l.Nav.Connected(l.EntranceSpawn, cell.Value), t + " room " + r + " not walkable from the entrance");
                }
                foreach (Doorway d in l.Doorways)
                {
                    Assert.True(l.Nav.IsFree(d.Center.X, d.Center.Z), t + " doorway at " + d.Center + " blocked");
                    Assert.True(l.Nav.Connected(l.EntranceSpawn, d.Center), t + " doorway not walkable from the entrance");
                }
            });
        }

        [Theory, MemberData(Arch, MemberType = typeof(InteriorTestSupport))]
        public void FreeCellsKeepClearanceFromWallsAndSolidFurniture(InteriorArchetype a)
        {
            ForEach(a, (l, t) =>
            {
                List<Vec3> free = FreeCells(l.Nav);
                int step = Math.Max(1, free.Count / 400);
                for (int i = 0; i < free.Count; i += step)
                {
                    Vec3 c = free[i];
                    Assert.True(c.X >= -l.Width / 2f + InteriorNavGrid.Clearance - Eps && c.X <= l.Width / 2f - InteriorNavGrid.Clearance + Eps &&
                                c.Z >= InteriorNavGrid.Clearance - Eps && c.Z <= l.Depth - InteriorNavGrid.Clearance + Eps, t + " free cell near outer wall " + c);
                    Assert.NotEqual(-1, l.RoomAt(c.X, c.Z));
                    foreach (FurnitureItem f in l.Furniture)
                        if (f.Solid)
                            Assert.True(DistToRect(c.X, c.Z, f.Footprint) >= InteriorNavGrid.Clearance - Eps, t + " free cell " + c + " too close to " + f.Kind);
                }
            });
        }

        [Theory, MemberData(Arch, MemberType = typeof(InteriorTestSupport))]
        public void NpcSpotsAreInTheirRoomAndStandOnFreeGroundOrBesideFurniture(InteriorArchetype a)
        {
            ForEach(a, (l, t) =>
            {
                foreach (NpcSpot n in l.NpcSpots)
                {
                    Assert.InRange(n.Room, 0, l.Rooms.Count - 1);
                    Assert.Equal(n.Room, l.RoomAt(n.Position.X, n.Position.Z));
                    if (!n.Seated)
                    {
                        Vec3 near;
                        Assert.True(l.Nav.NearestFree(n.Position, 1.0f, out near), t + " standing " + n.Role + " at " + n.Position + " has no free cell within 1 m");
                    }
                }
            });
        }

        [Theory]
        [InlineData(InteriorArchetype.ConvenienceStore, ShopType.Convenience)]
        [InlineData(InteriorArchetype.ClothesShop, ShopType.Clothes)]
        [InlineData(InteriorArchetype.GunStore, ShopType.Guns)]
        [InlineData(InteriorArchetype.Restaurant, ShopType.Food)]
        public void ShopsHaveACounterOfTheRightTypeAndAClerk(InteriorArchetype a, ShopType type)
        {
            ForEach(a, (l, t) =>
            {
                int counters = 0;
                foreach (InteractionPoint p in l.Interactions)
                    if (p.Kind == InteractionKind.ShopCounter)
                    {
                        counters++;
                        Assert.Equal(type, p.Shop);
                    }
                Assert.True(counters >= 1, t + " has no shop counter");
                bool clerk = false;
                foreach (NpcSpot n in l.NpcSpots) if (n.Role == NpcRole.Clerk) clerk = true;
                Assert.True(clerk, t + " has no clerk");
                Assert.True(Count(l, FurnitureKind.Counter) >= 1, t + " has no counter furniture");
            });
        }

        [Theory, MemberData(Arch, MemberType = typeof(InteriorTestSupport))]
        public void OnlyShopsHaveShopCounters(InteriorArchetype a)
        {
            bool shop = a == InteriorArchetype.ConvenienceStore || a == InteriorArchetype.ClothesShop ||
                        a == InteriorArchetype.GunStore || a == InteriorArchetype.Restaurant;
            ForEach(a, (l, t) =>
            {
                if (shop) Assert.True(Count(l, InteractionKind.ShopCounter) >= 1, t);
                else Assert.Equal(0, Count(l, InteractionKind.ShopCounter));
            });
        }

        [Fact]
        public void ClothesShopHasAWardrobe()
        {
            ForEach(InteriorArchetype.ClothesShop, (l, t) => Assert.True(Count(l, InteractionKind.Wardrobe) >= 1, t));
        }

        [Fact]
        public void PoliceStationHasADeskAndCellBars()
        {
            ForEach(InteriorArchetype.PoliceStation, (l, t) =>
            {
                Assert.True(Count(l, InteractionKind.PoliceDesk) >= 1, t + " no police desk");
                Assert.True(Count(l, FurnitureKind.CellBars) >= 1, t + " no cell bars");
            });
        }

        [Fact]
        public void HospitalHasADeskAndBeds()
        {
            ForEach(InteriorArchetype.Hospital, (l, t) =>
            {
                Assert.True(Count(l, InteractionKind.HospitalDesk) >= 1, t + " no hospital desk");
                Assert.True(Count(l, FurnitureKind.HospitalBed) >= 1, t + " no hospital bed");
            });
        }

        [Fact]
        public void GarageHasARespraySpot()
        {
            ForEach(InteriorArchetype.Garage, (l, t) => Assert.True(Count(l, InteractionKind.RespraySpot) >= 1, t));
        }

        [Theory]
        [InlineData(InteriorArchetype.House)]
        [InlineData(InteriorArchetype.Apartment)]
        public void HomesHaveABedAndAWardrobe(InteriorArchetype a)
        {
            ForEach(a, (l, t) =>
            {
                Assert.True(Count(l, InteractionKind.Bed) >= 1, t + " no bed interaction");
                Assert.True(Count(l, InteractionKind.Wardrobe) >= 1, t + " no wardrobe interaction");
            });
        }

        [Theory, MemberData(Arch, MemberType = typeof(InteriorTestSupport))]
        public void ContentHashIsStableForTheSameLayout(InteriorArchetype a)
        {
            InteriorLayout l = Layouts(a)[4];
            Assert.Equal(l.ContentHash(), l.ContentHash());
        }

        [Theory, MemberData(Arch, MemberType = typeof(InteriorTestSupport))]
        public void EqualDescriptorsGiveEqualHashesAndDifferentIdsUsuallyDiffer(InteriorArchetype a)
        {
            var hashes = new HashSet<uint>();
            for (int i = 0; i < 20; i++)
            {
                InteriorLayout x = InteriorGenerator.Generate(CaseBuilding(a, 4 + i * 15), a, CaseVariant(a, 3));
                InteriorLayout y = InteriorGenerator.Generate(CaseBuilding(a, 4 + i * 15), a, CaseVariant(a, 3));
                Assert.Equal(x.ContentHash(), y.ContentHash());
                hashes.Add(x.ContentHash());
            }
            Assert.True(hashes.Count >= 10, a + " distinct hashes " + hashes.Count + " of 20");
        }
    }
}
