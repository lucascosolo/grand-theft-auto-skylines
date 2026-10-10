using System;
using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Crime;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Interiors;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Missions;
using GtaSkylines.Game.Physics;
using GtaSkylines.Game.Police;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Roads;
using GtaSkylines.Game.Session;
using GtaSkylines.Game.Settings;
using GtaSkylines.Game.Tests.Fakes;
using GtaSkylines.Game.Tests.PhysicsTests;
using GtaSkylines.Game.Tests.Roads;
using GtaSkylines.Game.Tests.SessionTests;
using GtaSkylines.Game.World;
using Xunit;
using Sim = GtaSkylines.Game.Tests.SessionTests.Sim;

namespace GtaSkylines.Game.Tests.IntegrationTests
{
    /// <summary>A full game from <see cref="GameFactory.Create"/> on a flat FakeCity: floor, 5x5 road grid, police station, shop, parked cars.</summary>
    internal sealed class Rig
    {
        public const float Dt = GameSession.FixedDt;
        public readonly FakeCity City = new FakeCity();
        public readonly PlayerProfile Profile = new PlayerProfile();
        public readonly GameParts Parts;
        public readonly CitySnapshot Snap = new CitySnapshot();
        public readonly List<GameEvent> Log = new List<GameEvent>();
        public readonly BuildingDescriptor Shop;
        public readonly Vec3 CarSpot = new Vec3(150f, 0.75f, 50f);
        public readonly Vec3 CrashSpot = new Vec3(150f, 0.75f, 110f);
        private long _cursor = 1;

        public Rig(uint seed)
        {
            City.AddBuilding(new BuildingDescriptor
            {
                Id = 9, Service = ServiceKind.Police, Position = new Vec3(300f, 0f, 0f), Doors = new[] { new Vec3(300f, 0f, 0f) },
            });
            Shop = Sim.FindBuilding(InteriorArchetype.ConvenienceStore, ServiceKind.Commercial, "", new Vec3(-120f, 0f, -150f), 90f);
            City.AddBuilding(Shop);
            Profile.AddCash(1000);
            Parts = GameFactory.Create(City, new GameSettings(), Profile, seed);
            new MeshBuilder().Floor(-Sim.FloorHalf, -Sim.FloorHalf, Sim.FloorHalf, Sim.FloorHalf, 0f).Apply(World.Collision, Sim.FloorKey);
            Net g = Nets.Grid(5, 100f, 14f);
            Vec3 o = new Vec3(-200f, 0f, -200f);
            var nodes = new List<RoadNode>();
            foreach (RoadNode n in g.Nodes) nodes.Add(new RoadNode(n.Id, n.Position + o));
            var segs = new List<RoadSegment>();
            foreach (RoadSegment s in g.Segments) { RoadSegment t = s; t.A = s.A + o; t.B = s.B + o; t.C = s.C + o; t.D = s.D + o; segs.Add(t); }
            World.Roads.Rebuild(nodes, segs, false);
            Snap.Vehicles.Add(new AmbientVehicle { Id = 21, Parked = true, Position = CarSpot, Rotation = Quat.Yaw(0f), Model = 0, Kind = VehicleKind.Car, ColorRgb = 0x3366CC });
            Snap.Vehicles.Add(new AmbientVehicle { Id = 22, Parked = true, Position = CrashSpot, Rotation = Quat.Yaw(0f), Model = 0, Kind = VehicleKind.Car, ColorRgb = 0xCC3333 });
            Parts.Session.Begin(new Vec3(130f, 0f, 20f), 0f);
        }

        public GameSession S { get { return Parts.Session; } }
        public GameWorld World { get { return Parts.Session.World; } }
        public Ped Player { get { return World.Player; } }

        public void Tick(InputFrame f)
        {
            S.Tick(Dt, f, Snap);
            Drain();
        }

        private void Drain()
        {
            GameEvent e;
            while (World.Events.Read(ref _cursor, out e)) Log.Add(e);
        }

        public void Run(double seconds, InputFrame f)
        {
            int n = (int)Math.Round(seconds * 60.0);
            for (int i = 0; i < n; i++) Tick(i == 0 ? f : f.Continued());
        }

        public bool Until(Func<bool> cond, double maxSeconds, InputFrame f)
        {
            int n = (int)Math.Round(maxSeconds * 60.0);
            for (int i = 0; i < n; i++)
            {
                if (cond()) return true;
                Tick(i == 0 ? f : f.Continued());
            }
            return cond();
        }

        public int Count(params GameEventType[] types)
        {
            Drain();
            int c = 0;
            foreach (GameEvent x in Log) if (Array.IndexOf(types, x.Type) >= 0) c++;
            return c;
        }

        public void Place(Vec3 feet, float yaw)
        {
            Player.Motor.Teleport(feet);
            Player.YawDeg = yaw;
            World.PlayerState.ViewYawDeg = yaw;
            World.PlayerState.ViewPitchDeg = 0f;
        }

        public Car CarById(int id)
        {
            foreach (Car c in World.Cars) if (c.Id == id) return c;
            return null;
        }

        public Vec3 Interaction(InteractionKind kind)
        {
            InteriorLayout l = World.Interior.Layout;
            for (int i = 0; i < l.Interactions.Count; i++)
                if (l.Interactions[i].Kind == kind) return World.Interior.ToWorld(l.Interactions[i].Position);
            Assert.Fail("layout has no " + kind);
            return Vec3.Zero;
        }
    }

    public class GameFactoryIntegrationTests
    {
        [Fact]
        public void A_scripted_play_session_walks_steals_drives_crashes_is_hunted_shops_and_starts_V1()
        {
            var r = new Rig(2024u);
            r.Run(0.3, In.Idle);

            // 1. Walk.
            Vec3 start = r.Player.Position;
            r.Run(3.0, In.Forward);
            Assert.True(Vec3.DistanceXZ(start, r.Player.Position) > 5f, "walked " + Vec3.DistanceXZ(start, r.Player.Position));

            // 2. Steal the parked car (stand beside its driver door, facing it).
            r.Place(r.CarSpot + new Vec3(-3f, -0.75f, 0.44f), 90f);
            r.Run(0.3, In.Idle);
            r.Tick(In.Press(GameButton.EnterExit));
            Assert.True(r.Until(() => r.Player.Has(PedFlags.InVehicle), 4.0, In.Idle), "never got in");
            Assert.True(r.Count(GameEventType.CarStolen) >= 1, "no CarStolen event");
            Car car = r.CarById(r.Player.VehicleId);
            Assert.NotNull(car);

            // 3. Drive straight into the second parked car ahead: the car moves > 30 m and collides.
            Vec3 carStart = car.Position;
            float health = car.Body.Health;
            bool hit = r.Until(() => r.Count(GameEventType.CarCollision, GameEventType.CarDamaged) > 0, 10.0, In.Forward);
            Assert.True(Vec3.DistanceXZ(carStart, car.Position) > 30f, "drove " + Vec3.DistanceXZ(carStart, car.Position));
            Assert.True(hit || car.Body.Health < health, "no collision event and no health loss");

            // 4. Out of the car, then wanted. No witness citizens exist in the scene, so the wanted level comes from the
            //    public ReportKnownCrime API rather than an organic sighting.
            r.Tick(In.Press(GameButton.EnterExit));
            Assert.True(r.Until(() => !r.Player.Has(PedFlags.InVehicle), 4.0, In.Idle), "never got out");
            r.Parts.Crime.ReportKnownCrime(r.World, CrimeType.ShotAtPed, r.Player.Position, 0);
            r.Run(0.2, In.Idle);
            Assert.True(r.World.Wanted.Stars >= 1, "stars " + r.World.Wanted.Stars);

            // 5. Police are dispatched and one car closes at least 10 m on the standing player.
            Assert.True(r.Until(() => r.Parts.Police.ActiveUnitCount >= 1, 30.0, In.Idle), "no police unit appeared");
            var first = new Dictionary<int, float>();
            float best = 0f;
            for (int i = 0; i < 20 * 60 && best < 10f; i++)
            {
                r.Tick(In.Idle);
                foreach (PoliceUnit u in r.Parts.Police.Units)
                {
                    Car pc = u.InUse ? r.CarById(u.CarId) : null;
                    if (pc == null) continue;
                    float d = Vec3.DistanceXZ(pc.Position, r.Player.Position), d0;
                    if (!first.TryGetValue(u.CarId, out d0)) first[u.CarId] = d; else best = Math.Max(best, d0 - d);
                }
            }
            Assert.True(best >= 10f, "best police approach " + best + " m");

            // 6. Shop. Stars are cleared directly so the door, not an arrest, is what is under test.
            r.World.Wanted.Stars = 0;
            Vec3 door = r.Shop.Doors[0];
            Vec3 outward = (door - r.Shop.Position).Flat.Normalized;
            r.Place(door + outward * 1.2f, Vec3.YawDegOf(-outward));
            r.Run(0.1, In.Idle);
            r.Tick(In.Press(GameButton.Interact));
            Assert.True(r.Until(() => r.World.Interior.Phase == InteriorPhase.Inside, 3.0, In.Idle), "phase " + r.World.Interior.Phase);
            Assert.True(r.World.Interior.IsInside);
            Assert.True(r.Count(GameEventType.EnteredInterior) >= 1);
            r.Place(r.Interaction(InteractionKind.ShopCounter), r.World.PlayerState.ViewYawDeg);
            r.Run(0.2, In.Idle);
            r.Tick(In.Press(GameButton.Interact));
            r.Run(0.1, In.Idle);
            r.Player.Health = 50f; // a food item refuses a full-health buyer
            long cash = r.Profile.Cash;
            int snack = Shop.ItemsFor(ShopKind.Food).FindIndex(i => i.Id == "snack");
            Assert.Equal(PurchaseResult.Ok, r.S.ChooseMenuOption(snack));
            Assert.True(r.Profile.Cash < cash, "cash did not drop");
            Assert.True(r.Count(GameEventType.Purchase) >= 1);
            r.S.CloseMenu();

            // 7. Leave.
            r.Place(r.Interaction(InteractionKind.ExitDoor), r.World.PlayerState.ViewYawDeg);
            r.Run(0.2, In.Idle);
            r.Tick(In.Press(GameButton.Interact));
            Assert.True(r.Until(() => r.World.Interior.Phase == InteriorPhase.Outside, 3.0, In.Idle), "never left");
            Assert.True(r.Count(GameEventType.ExitedInterior) >= 1);

            // 8. Mission V1.
            // The player's walk through the city may already have triggered V1's start marker (organic start); otherwise start it by API.
            if (r.Parts.Missions.State != RunnerState.Mission)
                Assert.True(r.Parts.Missions.TryStartMission(r.World, "V1"), "V1 would not start");
            r.Run(0.2, In.Idle);
            Assert.Equal(RunnerState.Mission, r.Parts.Missions.State);
            Assert.Equal("V1", r.Parts.Missions.ActiveMission.Id);
        }

        [Fact]
        public void Every_system_ticks_without_an_exception_for_a_minute()
        {
            var r = new Rig(7u);
            r.Tick(In.Idle);
            Assert.NotNull(r.Parts.Combat.PlayerWeapon);
            Assert.NotNull(r.Parts.Missions.Campaign);
            long steps = r.World.Clock.Steps;
            Assert.True(steps >= 1);
            r.Parts.Crime.ReportKnownCrime(r.World, CrimeType.ShotAtPed, r.Player.Position, 0);
            int policeSeen = 0;
            for (int i = 0; i < 3600; i++)
            {
                r.Tick(i % 600 < 300 ? In.Forward : In.Idle);
                policeSeen = Math.Max(policeSeen, Math.Max(r.Parts.Police.ActiveUnitCount, r.Parts.Police.Budget.Cars));
            }
            Assert.True(r.World.Clock.Steps >= steps + 3500, "steps " + r.World.Clock.Steps);
            Assert.True(policeSeen > 0, "police never stepped (no units, empty budget)");
        }

        [Fact]
        public void Two_sessions_with_the_same_seed_and_inputs_end_with_the_same_state_hash()
        {
            Assert.Equal(Script(99u), Script(99u));
        }

        private static uint Script(uint seed)
        {
            var r = new Rig(seed);
            r.Parts.Crime.ReportKnownCrime(r.World, CrimeType.ShotAtPed, r.Player.Position, 0);
            for (int i = 0; i < 600; i++) r.Tick(i < 120 ? In.Forward : i < 240 ? In.Move(1f, 0f) : In.Idle);
            return r.S.StateHash();
        }
    }
}
