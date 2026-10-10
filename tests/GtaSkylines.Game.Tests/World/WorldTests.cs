using System;
using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Physics;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Session;
using GtaSkylines.Game.Settings;
using GtaSkylines.Game.Tests.Fakes;
using GtaSkylines.Game.Tests.PhysicsTests;
using GtaSkylines.Game.Tests.SessionTests;
using GtaSkylines.Game.Vehicles;
using GtaSkylines.Game.World;
using Xunit;
using Sim = GtaSkylines.Game.Tests.SessionTests.Sim;

namespace GtaSkylines.Game.Tests.WorldTests
{
    public class WorldTagTests
    {
        [Theory]
        [InlineData(TagKind.AmbientCitizen, 513, 0x01000201)]
        [InlineData(TagKind.Ped, 7, 0x04000007)]
        [InlineData(TagKind.Car, 12, 0x0500000C)]
        [InlineData(TagKind.AmbientVehicle, 65535, 0x0200FFFF)]
        [InlineData(TagKind.AmbientParkedVehicle, 1, 0x03000001)]
        public void EncodeDecode_RoundTrips(TagKind kind, int id, int expected)
        {
            int tag = WorldTag.Encode(kind, id);
            Assert.Equal(expected, tag);
            Assert.Equal(kind, WorldTag.KindOf(tag));
            Assert.Equal(id, WorldTag.IdOf(tag));
        }

        [Fact]
        public void InvalidInputs_EncodeToNone_AndDecodeToNone()
        {
            Assert.Equal(0, WorldTag.Encode(TagKind.None, 5));
            Assert.Equal(0, WorldTag.Encode(TagKind.Ped, 0));
            Assert.Equal(0, WorldTag.Encode(TagKind.Ped, -3));
            Assert.Equal(0, WorldTag.Encode(TagKind.Ped, WorldTag.MaxId + 1));
            Assert.True(WorldTag.Encode(TagKind.Ped, WorldTag.MaxId) > 0);
            foreach (int bad in new[] { 0, -1, int.MinValue, 0x06000001, 0x7F000001, 0x04000000 })
            {
                Assert.Equal(TagKind.None, WorldTag.KindOf(bad));
                Assert.Equal(0, WorldTag.IdOf(bad));
            }
        }

        [Fact]
        public void EveryValidTag_IsPositive_SoItNeverCollidesWithStaticTags()
        {
            foreach (TagKind k in new[] { TagKind.AmbientCitizen, TagKind.AmbientVehicle, TagKind.AmbientParkedVehicle, TagKind.Ped, TagKind.Car })
                Assert.True(WorldTag.Encode(k, WorldTag.MaxId) > 0 && WorldTag.Encode(k, 1) > 0);
        }

        [Fact]
        public void Helpers_PickTheRightKind()
        {
            Assert.Equal(TagKind.Ped, WorldTag.KindOf(WorldTag.ForPed(3)));
            Assert.Equal(TagKind.Car, WorldTag.KindOf(WorldTag.ForCar(3)));
            Assert.Equal(TagKind.AmbientCitizen, WorldTag.KindOf(WorldTag.ForAmbientCitizen(3)));
            Assert.Equal(TagKind.AmbientParkedVehicle, WorldTag.KindOf(WorldTag.ForAmbientVehicle(3, true)));
            Assert.Equal(TagKind.AmbientVehicle, WorldTag.KindOf(WorldTag.ForAmbientVehicle(3, false)));
            Assert.True(WorldTag.IsPerson(WorldTag.ForPed(1)) && WorldTag.IsPerson(WorldTag.ForAmbientCitizen(1)));
            Assert.False(WorldTag.IsPerson(WorldTag.ForCar(1)));
            Assert.True(WorldTag.IsVehicle(WorldTag.ForCar(1)) && WorldTag.IsVehicle(WorldTag.ForAmbientVehicle(1, true)) && WorldTag.IsVehicle(WorldTag.ForAmbientVehicle(1, false)));
            Assert.False(WorldTag.IsVehicle(WorldTag.ForPed(1)));
            Assert.False(WorldTag.IsVehicle(0) || WorldTag.IsPerson(-1));
        }
    }

    public class GameEventsTests
    {
        private static GameEvent E(float v) { return new GameEvent { Type = GameEventType.Bumped, Value = v }; }

        [Fact]
        public void Sequence_StartsAtOne_AndEachConsumerKeepsItsOwnCursor()
        {
            var ev = new GameEvents();
            Assert.Equal(1L, ev.NextSeq);
            Assert.Equal(1L, ev.Emit(E(10)));
            Assert.Equal(2L, ev.Emit(E(20)));
            long a = 1, b = 2;
            GameEvent e;
            Assert.True(ev.Read(ref a, out e));
            Assert.Equal(10f, e.Value);
            Assert.Equal(1L, e.Seq);
            Assert.True(ev.Read(ref a, out e));
            Assert.Equal(20f, e.Value);
            Assert.False(ev.Read(ref a, out e));
            Assert.Equal(3L, a);
            Assert.True(ev.Read(ref b, out e));
            Assert.Equal(20f, e.Value);
        }

        [Fact]
        public void NoEventIsMissedBetweenReads_WhileTheConsumerKeepsUp()
        {
            var ev = new GameEvents();
            long cursor = ev.NextSeq;
            int seen = 0;
            GameEvent e;
            for (int round = 0; round < 40; round++)
            {
                for (int i = 0; i < 100; i++) ev.Emit(E(round * 100 + i));
                while (ev.Read(ref cursor, out e)) { Assert.Equal((float)seen, e.Value); seen++; }
            }
            Assert.Equal(4000, seen);
            Assert.Equal(0L, ev.Missed);
        }

        [Fact]
        public void Wrap_OverwritesTheOldest_AndALateReaderJumpsToOldestAndCountsTheLoss()
        {
            var ev = new GameEvents();
            for (int i = 0; i < GameEvents.Capacity + 44; i++) ev.Emit(E(i));
            Assert.Equal(45L, ev.OldestSeq);
            long cursor = 1;
            GameEvent e;
            Assert.True(ev.Read(ref cursor, out e));
            Assert.Equal(45L, e.Seq);
            Assert.Equal(44f, e.Value);
            Assert.Equal(44L, ev.Missed);
            int rest = 0;
            while (ev.Read(ref cursor, out e)) rest++;
            Assert.Equal(GameEvents.Capacity - 1, rest);
        }

        [Fact]
        public void ReadingAtTheEnd_ReturnsFalseAndLeavesTheCursor()
        {
            var ev = new GameEvents();
            long c = ev.NextSeq;
            GameEvent e;
            Assert.False(ev.Read(ref c, out e));
            Assert.Equal(1L, c);
        }

        [Fact]
        public void EmitAndRead_AllocateNothing()
        {
            var ev = new GameEvents();
            long c = 1;
            GameEvent e;
            for (int i = 0; i < 300; i++) { ev.Emit(E(i)); ev.Read(ref c, out e); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 2000; i++) { ev.Emit(E(i)); ev.Read(ref c, out e); }
            Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        [Fact]
        public void WorldEmit_StampsTheClockTime_AndStoresPayloads()
        {
            var w = NewWorld();
            w.Clock.GameSeconds = 12.5;
            long seq = w.Emit(GameEventType.Purchase, new Vec3(1f, 2f, 3f), 5, 6, 7f, "snack");
            long s2 = w.Emit(GameEventType.Shot, new Vec3(1f, 0f, 0f), new Vec3(9f, 0f, 0f), 5, 0, 20f);
            long c = seq;
            GameEvent e;
            Assert.True(w.Events.Read(ref c, out e));
            Assert.Equal(12.5, e.GameTime, 6);
            Assert.Equal("snack", e.Text);
            Assert.Equal(7f, e.Value);
            Assert.Equal(5, e.ActorA);
            Assert.Equal(6, e.ActorB);
            Assert.True(w.Events.Read(ref c, out e));
            Assert.Equal(s2, e.Seq);
            Sim.Near(new Vec3(9f, 0f, 0f), e.Position2, 1e-5f);
        }

        internal static GameWorld NewWorld(FakeCity city = null, uint seed = 1u)
        {
            return new GameWorld(city ?? new FakeCity(), new GameSettings(), new PlayerProfile(), seed);
        }
    }

    public class GameWorldRegistryTests
    {
        private static GameWorld W(FakeCity city = null) { return GameEventsTests.NewWorld(city); }

        [Fact]
        public void Constructor_RejectsNulls()
        {
            Assert.Throws<ArgumentNullException>(() => new GameWorld(null, new GameSettings(), new PlayerProfile(), 1u));
            Assert.Throws<ArgumentNullException>(() => new GameWorld(new FakeCity(), null, new PlayerProfile(), 1u));
            Assert.Throws<ArgumentNullException>(() => new GameWorld(new FakeCity(), new GameSettings(), null, 1u));
        }

        [Fact]
        public void NewWorld_HasServicesAndNoActors()
        {
            GameWorld w = W();
            Assert.Null(w.Player);
            Assert.Empty(w.Peds);
            Assert.Empty(w.Cars);
            Assert.NotNull(w.Collision);
            Assert.NotNull(w.Roads);
            Assert.NotNull(w.Events);
            Assert.NotNull(w.Wanted);
            Assert.NotNull(w.Mission);
            Assert.NotNull(w.Messages);
            Assert.NotNull(w.Interior);
            Assert.NotNull(w.Menu);
            Assert.NotNull(w.Hooks);
        }

        [Fact]
        public void SpawnPed_AssignsSequentialIds_AndDefaults()
        {
            GameWorld w = W();
            Ped a = w.SpawnPed(PedKind.Civilian, 3, new Vec3(5f, 0f, 6f), 45f);
            Ped b = w.SpawnPed(PedKind.Cop, 1, new Vec3(7f, 0f, 6f), 0f);
            Assert.Equal(1, a.Id);
            Assert.Equal(2, b.Id);
            Assert.Equal(100f, a.Health);
            Assert.Equal(0f, a.Armour);
            Assert.Equal(PedFlags.None, a.Flags);
            Assert.Equal(PedAnim.Idle, a.Anim);
            Assert.Null(a.Brain);
            Assert.Equal(-1, a.InteriorId);
            Assert.Equal(45f, a.YawDeg);
            Sim.Near(new Vec3(5f, 0f, 6f), a.Position, 1e-3f);
            Assert.Equal(WorldTag.ForPed(1), a.Tag);
            Assert.Equal(new[] { a, b }, new List<Ped>(w.Peds).ToArray());
            Assert.Same(b, w.FindPed(2));
            Assert.Null(w.FindPed(99));
        }

        [Fact]
        public void Ids_AreNeverReused_AndPedsAndCarsShareOneCounter()
        {
            GameWorld w = W();
            Ped a = w.SpawnPed(PedKind.Civilian, 0, Vec3.Zero, 0f);
            Car c = w.SpawnCar(0, new Vec3(20f, 0f, 0f), 0f, CarOwner.Civilian, 0);
            Ped b = w.SpawnPed(PedKind.Civilian, 0, Vec3.Zero, 0f);
            Assert.Equal(new[] { 1, 2, 3 }, new[] { a.Id, c.Id, b.Id });
            w.RemovePed(b, false);
            w.RemoveCar(c);
            Ped d = w.SpawnPed(PedKind.Civilian, 0, Vec3.Zero, 0f);
            Car e = w.SpawnCar(0, new Vec3(20f, 0f, 0f), 0f, CarOwner.Civilian, 0);
            Assert.Equal(4, d.Id);
            Assert.Equal(5, e.Id);
            Assert.Null(w.FindPed(3));
            Assert.Null(w.FindCar(2));
        }

        [Fact]
        public void SpawnCar_UnknownModel_ReturnsNull_AndConsumesNoId()
        {
            GameWorld w = W();
            Assert.Null(w.SpawnCar(999, Vec3.Zero, 0f, CarOwner.Civilian, 0));
            Assert.Equal(1, w.SpawnCar(0, Vec3.Zero, 0f, CarOwner.Civilian, 0).Id);
        }

        [Fact]
        public void SpawnCar_PlacesTheBoundsCentreAboveGroundByTheRotatedOffset()
        {
            var city = new FakeCity();
            city.AddVehicleModel(new VehicleModel { Id = 9, Name = "Odd", HalfExtents = new Vec3(1f, 1f, 3f), CenterOffset = new Vec3(0.3f, 1f, 0.5f) });
            GameWorld w = W(city);
            Car car = w.SpawnCar(9, new Vec3(5f, 0f, 5f), 90f, CarOwner.Civilian, 0xABCDEF);
            Sim.Near(new Vec3(5f, 0f, 5f) + Quat.Yaw(90f) * new Vec3(0.3f, 1f, 0.5f), car.Position, 0.02f, "centre");
            Sim.Near(90f, car.Rotation.YawDeg, 0.5f, "yaw");
            Assert.Equal(0xABCDEF, car.ColourRgb);
            Assert.Equal(CarOwner.Civilian, car.Owner);
            Assert.Equal(7, car.Plate.Length);
            Assert.Equal(WorldTag.ForCar(car.Id), car.Tag);
        }

        [Fact]
        public void SpawnCar_HeadlightsFollowTheClock()
        {
            GameWorld w = W();
            w.Clock.Hour = 13f;
            Assert.False(w.SpawnCar(0, Vec3.Zero, 0f, CarOwner.Civilian, 0).Headlights);
            w.Clock.Hour = 23f;
            Assert.True(w.SpawnCar(0, new Vec3(10f, 0f, 0f), 0f, CarOwner.Civilian, 0).Headlights);
        }

        [Fact]
        public void PutInCar_SeatsThePed_AndRejectsBadSeats()
        {
            GameWorld w = W();
            Car car = w.SpawnCar(0, Vec3.Zero, 0f, CarOwner.Civilian, 0);
            Ped a = w.SpawnPed(PedKind.Civilian, 0, new Vec3(9f, 0f, 0f), 0f);
            Ped b = w.SpawnPed(PedKind.Civilian, 0, new Vec3(9f, 0f, 3f), 0f);
            w.PutInCar(a, car, 0);
            Assert.True(a.Has(PedFlags.InVehicle));
            Assert.Equal(car.Id, a.VehicleId);
            Assert.Equal(0, a.Seat);
            Assert.Equal(a.Id, car.Seats[0]);
            Assert.Equal(a.Id, car.DriverId);
            Assert.Throws<InvalidOperationException>(() => w.PutInCar(b, car, 0));
            Assert.Throws<InvalidOperationException>(() => w.PutInCar(b, car, 4));
            Assert.Throws<InvalidOperationException>(() => w.PutInCar(b, car, -1));
            Assert.Throws<InvalidOperationException>(() => w.PutInCar(a, car, 1));
            Assert.Throws<ArgumentNullException>(() => w.PutInCar(null, car, 1));
            Assert.Throws<ArgumentNullException>(() => w.PutInCar(b, null, 1));
            w.PutInCar(b, car, 3);
            Assert.Equal(b.Id, car.Seats[3]);
        }

        [Fact]
        public void TakeOutOfCar_FreesTheSeat_AndMovesThePed()
        {
            GameWorld w = W();
            Car car = w.SpawnCar(0, Vec3.Zero, 0f, CarOwner.Civilian, 0);
            Ped a = w.SpawnPed(PedKind.Civilian, 0, new Vec3(9f, 0f, 0f), 0f);
            w.PutInCar(a, car, 0);
            w.TakeOutOfCar(a, new Vec3(-3f, 0f, 1f), 120f);
            Assert.False(a.Has(PedFlags.InVehicle));
            Assert.Equal(0, a.VehicleId);
            Assert.Equal(-1, a.Seat);
            Assert.Equal(0, car.DriverId);
            Sim.Near(new Vec3(-3f, 0f, 1f), a.Position, 1e-3f);
            Sim.Near(120f, a.YawDeg, 1e-3f);
            w.TakeOutOfCar(a, Vec3.Zero, 0f);
        }

        [Fact]
        public void DoorPosition_IsBesideTheCar_LeftIsTheDriverSide()
        {
            GameWorld w = W();
            Car car = w.SpawnCar(0, new Vec3(10f, 0f, 10f), 90f, CarOwner.Civilian, 0);
            Vec3 left = w.DoorPosition(car, true), right = w.DoorPosition(car, false);
            Vec3 ground = new Vec3(10f, 0f, 10f);
            Sim.Near(ground + Quat.Yaw(90f) * new Vec3(-(0.9f + GameWorld.DoorGap), 0f, 0.1f * 4.4f), left, 0.05f, "left door");
            Sim.Near(ground + Quat.Yaw(90f) * new Vec3(0.9f + GameWorld.DoorGap, 0f, 0.1f * 4.4f), right, 0.05f, "right door");
        }

        [Fact]
        public void RemovePed_ReturnsALiveCitizen_NotADeadOrAnonymousOne_AndNeverThePlayer()
        {
            GameWorld w = W();
            Ped citizen = w.SpawnPed(PedKind.Civilian, 0, new Vec3(3f, 0f, 3f), 0f);
            citizen.Citizen = 777;
            Ped dead = w.SpawnPed(PedKind.Civilian, 0, new Vec3(6f, 0f, 3f), 0f);
            dead.Citizen = 778;
            dead.Flags |= PedFlags.Dead;
            Ped anon = w.SpawnPed(PedKind.Civilian, 0, new Vec3(9f, 0f, 3f), 0f);
            w.RemovePed(citizen, true);
            w.RemovePed(dead, true);
            w.RemovePed(anon, true);
            Assert.Single(w.Effects.Items);
            Assert.Equal(CityEffectKind.ReturnCitizen, w.Effects.Items[0].Kind);
            Assert.Equal(777u, w.Effects.Items[0].Citizen);
            Assert.Empty(w.Peds);

            var sim = new Sim();
            Assert.Throws<InvalidOperationException>(() => sim.W.RemovePed(sim.Player, false));
        }

        [Fact]
        public void RemoveCar_RemovesPassengers_AndRefusesWhenThePlayerIsInside()
        {
            GameWorld w = W();
            Car car = w.SpawnCar(0, Vec3.Zero, 0f, CarOwner.Civilian, 0);
            Ped p = w.SpawnPed(PedKind.Civilian, 0, new Vec3(9f, 0f, 0f), 0f);
            p.Citizen = 55;
            w.PutInCar(p, car, 0);
            w.RemoveCar(car);
            Assert.Empty(w.Cars);
            Assert.Empty(w.Peds);
            Assert.Equal(55u, w.Effects.Items[0].Citizen);

            var sim = new Sim();
            Car mine = sim.W.SpawnCar(0, new Vec3(20f, 0f, 20f), 0f, CarOwner.Player, 0);
            sim.W.PutInCar(sim.Player, mine, 0);
            Assert.Throws<InvalidOperationException>(() => sim.W.RemoveCar(mine));
        }

        [Fact]
        public void PromoteVehicle_Moving_CreatesACivilianCarWithADriver()
        {
            GameWorld w = W();
            var v = new AmbientVehicle { Id = 40, Position = new Vec3(5f, 0.75f, 5f), Rotation = Quat.Yaw(30f), Velocity = new Vec3(0f, 0f, 8f), Model = 0, Kind = VehicleKind.Car, DriverCitizen = 90, ColorRgb = 0x445566 };
            Car car = w.PromoteVehicle(v, true, PedOrigin.ShockedDriver, 0);
            Assert.Equal(CarOwner.Civilian, car.Owner);
            Assert.Equal((ushort)40, car.SourceCs1Vehicle);
            Assert.False(car.SourceParked);
            Assert.Equal(0x445566, car.ColourRgb);
            Sim.Near(v.Position, car.Position, 0.05f, "pose");
            Sim.Near(30f, car.Rotation.YawDeg, 0.5f, "yaw");
            Sim.Near(8f, car.Body.Velocity.Length, 0.3f, "velocity");
            Ped driver = w.FindPed(car.DriverId);
            Assert.NotNull(driver);
            Assert.Equal(PedKind.Civilian, driver.Kind);
            Assert.Equal(90u, driver.Citizen);
            Assert.InRange(driver.Model, 0, w.City.CitizenModelCount - 1);
            Assert.Equal(1, CountKind(w, CityEffectKind.TakeVehicle));
            Assert.Same(car, w.PromoteVehicle(v, true, PedOrigin.ShockedDriver, 0));
            Assert.Equal(1, CountKind(w, CityEffectKind.TakeVehicle));
        }

        [Fact]
        public void PromoteVehicle_Parked_HasNoDriver_PoliceGetsACop()
        {
            GameWorld w = W();
            var parked = new AmbientVehicle { Id = 41, Parked = true, Position = new Vec3(5f, 0.75f, 5f), Rotation = Quat.Identity, Model = 0, Kind = VehicleKind.Car };
            Car p = w.PromoteVehicle(parked, true, PedOrigin.ShockedDriver, 0);
            Assert.Equal(0, p.DriverId);
            Assert.True(p.SourceParked);
            Assert.Equal(1, CountKind(w, CityEffectKind.TakeParkedVehicle));
            var cop = new AmbientVehicle { Id = 42, Position = new Vec3(25f, 0.75f, 5f), Rotation = Quat.Identity, Model = 1, Kind = VehicleKind.Police, DriverCitizen = 91 };
            Car pc = w.PromoteVehicle(cop, true, PedOrigin.ShockedDriver, 0);
            Assert.Equal(CarOwner.Police, pc.Owner);
            Assert.Equal(PedKind.Cop, w.FindPed(pc.DriverId).Kind);
        }

        [Fact]
        public void PedFromTag_And_CarFromTag_ResolveOrPromote()
        {
            GameWorld w = W();
            w.Ambient.Citizens.Add(new AmbientCitizen { Instance = 12, Citizen = 120, Position = new Vec3(4f, 0f, 4f), Model = 1 });
            w.Ambient.Vehicles.Add(new AmbientVehicle { Id = 33, Position = new Vec3(30f, 0.75f, 4f), Rotation = Quat.Identity, Model = 0, Kind = VehicleKind.Car, DriverCitizen = 130 });
            int citizenTag = WorldTag.ForAmbientCitizen(12), vehicleTag = WorldTag.ForAmbientVehicle(33, false);
            Assert.Null(w.PedFromTag(citizenTag, false, 0));
            Assert.Null(w.CarFromTag(vehicleTag, false, 0));
            Assert.Null(w.PedFromTag(0, true, 0));
            Ped p = w.PedFromTag(citizenTag, true, 0);
            Assert.NotNull(p);
            Assert.Equal(120u, p.Citizen);
            Assert.Same(p, w.PedFromTag(citizenTag, false, 0));
            Assert.Same(p, w.PedFromTag(p.Tag, false, 0));
            Car c = w.CarFromTag(vehicleTag, true, 0);
            Assert.NotNull(c);
            Assert.Same(c, w.CarFromTag(c.Tag, false, 0));
            Vec3 pos;
            Assert.True(w.TryGetTagPosition(p.Tag, out pos));
            Sim.Near(p.Position, pos, 1e-3f);
            Assert.True(w.TryGetTagPosition(c.Tag, out pos));
            Sim.Near(c.Position, pos, 1e-3f);
            Assert.False(w.TryGetTagPosition(WorldTag.ForPed(999), out pos));
        }

        [Fact]
        public void DamageCar_ReducesHealth_EmitsCarDamaged_AndIgnoresZero()
        {
            GameWorld w = W();
            Car car = w.SpawnCar(0, Vec3.Zero, 0f, CarOwner.Civilian, 0);
            float h0 = car.Body.Health;
            w.DamageCar(car, 0f, 0, Vec3.Zero);
            Assert.Equal(h0, car.Body.Health);
            long seq = w.Events.NextSeq;
            w.DamageCar(car, 100f, WorldTag.ForPed(5), new Vec3(1f, 1f, 1f));
            Assert.True(car.Body.Health < h0);
            GameEvent e;
            Assert.True(w.Events.Read(ref seq, out e));
            Assert.Equal(GameEventType.CarDamaged, e.Type);
            Assert.Equal(car.Tag, e.ActorB);
            Sim.Near(h0 - car.Body.Health, e.Value, 0.01f, "health lost");
        }

        [Fact]
        public void CanPlayerSee_NeedsRange_Cone_AndAClearStaticLine()
        {
            GameWorld w = W();
            new MeshBuilder().Wall(-50f, 20f, 50f, 20f, 0f, 30f).Apply(w.Collision, 3);
            w.ViewPosition = new Vec3(0f, 1.6f, 0f);
            w.ViewRotation = Quat.Yaw(0f);
            w.ViewFovDeg = 70f;
            Assert.True(w.CanPlayerSee(new Vec3(0f, 1.6f, 10f)));
            Assert.False(w.CanPlayerSee(new Vec3(0f, 1.6f, -10f)), "behind");
            Assert.False(w.CanPlayerSee(new Vec3(0f, 1.6f, 40f)), "behind a wall");
            Assert.False(w.CanPlayerSee(new Vec3(120f, 1.6f, 5f)), "outside the cone");
            w.Collision.RemoveRegion(3);
            Assert.True(w.CanPlayerSee(new Vec3(0f, 1.6f, 240f)));
            Assert.False(w.CanPlayerSee(new Vec3(0f, 1.6f, 260f)), "beyond 250 m");
            new MeshBuilder().Wall(-50f, 20f, 50f, 20f, 0f, 30f, SurfaceFlags.Building | SurfaceFlags.NoCamera).Apply(w.Collision, 4);
            Assert.True(w.CanPlayerSee(new Vec3(0f, 1.6f, 40f)), "NoCamera surfaces do not block sight");
        }

        private static int CountKind(GameWorld w, CityEffectKind k)
        {
            int n = 0;
            foreach (CityEffect e in w.Effects.Items) if (e.Kind == k) n++;
            return n;
        }
    }

    public class RouteDriverTests
    {
        [Fact]
        public void NewDriver_HasNoDestination_AndBrakes()
        {
            var d = new RouteDriver();
            Assert.False(d.HasDestination);
            Assert.False(d.Driverless);
            Assert.Equal("Route", d.Name);
            var sim = new Sim();
            Car car = sim.W.SpawnCar(0, new Vec3(1.5f, 0f, 20f), 0f, CarOwner.Civilian, 0);
            VehicleControls c = d.Drive(sim.W, car, 1f / 60f);
            Assert.Equal(0f, c.Throttle);
            Assert.True(c.Brake > 0.5f);
        }

        [Fact]
        public void SetDestination_Records_AndClearsArrived()
        {
            var d = new RouteDriver();
            d.SetDestination(new Vec3(1f, 0f, 300f));
            Assert.True(d.HasDestination);
            Sim.Near(new Vec3(1f, 0f, 300f), d.Destination, 1e-3f);
            Assert.False(d.Arrived);
        }

        [Fact]
        public void DrivesAlongTheRoadToTheDestination_AndStopsThere()
        {
            var sim = new Sim(feet: new Vec3(30f, 0f, 30f));
            Car car = sim.W.SpawnCar(0, new Vec3(1.5f, 0f, 10f), 0f, CarOwner.Civilian, 0);
            Ped driver = sim.W.SpawnPed(PedKind.Civilian, 0, new Vec3(40f, 0f, 40f), 0f);
            sim.W.PutInCar(driver, car, 0);
            var rd = new RouteDriver();
            rd.SetDestination(new Vec3(1.5f, 0f, 150f));
            car.Driver = rd;
            Assert.True(sim.Until(() => rd.Arrived, 60.0, In.Idle), "never arrived");
            sim.Run(5.0, In.Idle);
            Assert.True(Vec3.DistanceXZ(car.Position, new Vec3(1.5f, 0f, 150f)) < 12f);
            Assert.True(car.Body.Speed < 1f, "stopped after arrival");
        }
    }
}
