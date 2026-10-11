using System.Collections.Generic;
using GtaSkylines.Game.Combat;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Peds;
using GtaSkylines.Game.Tests.Combat;
using GtaSkylines.Game.Tests.SessionTests;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.Peds
{
    /// <summary>F-PED-7: a promoted citizen keeps its CS1 errand (walks to its door and is handed back arrived).</summary>
    public class ErrandTests
    {
        private const ushort Shop = 50, Home = 51;
        private static readonly Vec3 ShopDoor = new Vec3(30f, 0f, 3f);

        private static BuildingDescriptor Bld(ushort id, Vec3 position, params Vec3[] doors)
        {
            return new BuildingDescriptor { Id = id, Name = "B" + id, Position = position, Doors = doors };
        }

        private static Sim NewSim()
        {
            var sim = new Sim();
            CombatSystem.Install(sim.S);
            return sim;
        }

        private static Ped Promote(Sim sim, Vec3 at, uint citizen, ushort building, ErrandKind kind)
        {
            var c = new AmbientCitizen { Instance = 400, Citizen = citizen, Position = at, Model = 2, ErrandBuilding = building, Errand = kind };
            return sim.W.PromoteCitizen(c, PedOrigin.PromotedCitizen, 0);
        }

        private static int Count(IEnumerable<CityEffect> fx, CityEffectKind k)
        {
            int n = 0;
            foreach (CityEffect e in fx) if (e.Kind == k) n++;
            return n;
        }

        // ---- 1, 2: pure data --------------------------------------------------------------------------------------

        [Theory]
        [InlineData(0, 5, 6, 7, ErrandKind.None)]
        [InlineData(5, 5, 6, 7, ErrandKind.Home)]
        [InlineData(6, 5, 6, 7, ErrandKind.Work)]
        [InlineData(7, 5, 6, 7, ErrandKind.Visit)]
        [InlineData(9, 5, 6, 7, ErrandKind.Visit)]
        [InlineData(5, 5, 5, 7, ErrandKind.Home)]
        [InlineData(0, 0, 0, 0, ErrandKind.None)]
        public void Classify_NamesTheErrand(int target, int home, int work, int visit, ErrandKind expected)
        {
            Assert.Equal(expected, Errands.Classify((ushort)target, (ushort)home, (ushort)work, (ushort)visit));
        }

        [Fact]
        public void ReturnCitizen_SixArg_CarriesTheErrand()
        {
            CityEffect e = CityEffect.ReturnCitizen(9u, new Vec3(1f, 2f, 3f), 45f, 77, ErrandKind.Work, true);
            Assert.Equal(CityEffectKind.ReturnCitizen, e.Kind);
            Assert.Equal(9u, e.Citizen);
            Assert.Equal(new Vec3(1f, 2f, 3f), e.Position);
            Assert.Equal(45f, e.YawDeg);
            Assert.Equal((ushort)77, e.Building);
            Assert.Equal(ErrandKind.Work, e.Errand);
            Assert.True(e.Arrived);
        }

        [Fact]
        public void ReturnCitizen_TwoArg_HasNoErrand()
        {
            CityEffect e = CityEffect.ReturnCitizen(9u, new Vec3(1f, 2f, 3f));
            Assert.Equal(ErrandKind.None, e.Errand);
            Assert.Equal((ushort)0, e.Building);
            Assert.False(e.Arrived);
        }

        // ---- 3: promotion and factory ------------------------------------------------------------------------------

        [Fact]
        public void PromoteCitizen_CopiesTheErrand_AndTheFactoryInstallsAnErrandBrainAtTheNearestDoor()
        {
            Sim sim = NewSim();
            sim.City.AddBuilding(Bld(Shop, new Vec3(40f, 0f, 3f), new Vec3(60f, 0f, 3f), ShopDoor));
            Ped p = Promote(sim, new Vec3(10f, 0f, 3f), 4000, Shop, ErrandKind.Work);
            Assert.Equal(Shop, p.ErrandBuilding);
            Assert.Equal(ErrandKind.Work, p.Errand);
            var brain = Assert.IsType<ErrandBrain>(p.Brain);
            Assert.Equal("Errand", brain.Name);
            Assert.False(brain.IsActive);
            Assert.Equal(Shop, brain.Building);
            Assert.Equal(ShopDoor, brain.Door);
        }

        [Fact]
        public void ErrandBrain_WithoutDoors_AimsAtTheBuildingPosition()
        {
            var a = new Arena();
            a.World.Hooks.Brains = new PedBrainFactory();
            a.City.AddBuilding(Bld(Shop, new Vec3(25f, 0f, 0f)));
            Ped p = a.Spawn(0f, 0f);
            p.ErrandBuilding = Shop;
            p.Errand = ErrandKind.Visit;
            ErrandBrain b = ErrandBrain.For(a.World, p, BrainTests.Adult);
            Assert.NotNull(b);
            Assert.Equal(new Vec3(25f, 0f, 0f), b.Door);
        }

        // ---- 4: walking and arriving -----------------------------------------------------------------------------

        [Fact]
        public void ErrandWalker_WalksToTheDoor_ThenIsHandedBackArrivedExactlyOnce()
        {
            Sim sim = NewSim();
            sim.City.AddBuilding(Bld(Shop, new Vec3(40f, 0f, 3f), ShopDoor));
            Ped p = Promote(sim, new Vec3(10f, 0f, 3f), 4000, Shop, ErrandKind.Work);
            float d0 = Vec3.DistanceXZ(p.Position, ShopDoor);
            sim.Run(2.0, In.Idle);
            Assert.NotNull(sim.W.FindPed(p.Id));
            Assert.True(Vec3.DistanceXZ(p.Position, ShopDoor) < d0 - 1.5f, "walks toward the door");
            Assert.Equal(PedAnim.Walk, p.Anim);
            Assert.True(sim.Until(() => sim.W.FindPed(p.Id) == null, 40.0, In.Idle), "never arrived");
            Assert.Equal(1, sim.CountEffects(CityEffectKind.ReturnCitizen));
            CityEffect e = sim.FirstEffect(CityEffectKind.ReturnCitizen);
            Assert.True(e.Arrived);
            Assert.Equal(4000u, e.Citizen);
            Assert.Equal(Shop, e.Building);
            Assert.Equal(ErrandKind.Work, e.Errand);
            Assert.True(Vec3.DistanceXZ(e.Position, ShopDoor) <= ErrandBrain.ArriveDistance + 0.5f);
            sim.Run(3.0, In.Idle);
            Assert.Equal(1, sim.CountEffects(CityEffectKind.ReturnCitizen));
        }

        // ---- 5: demotion mid-walk ----------------------------------------------------------------------------------

        [Fact]
        public void DemotedMidWalk_ReturnsTheCitizenNotArrived_WithPoseAndErrand()
        {
            Sim sim = NewSim();
            var far = new Vec3(300f, 0f, 0f);
            sim.City.AddBuilding(Bld(Shop, far, far));
            sim.Place(Vec3.Zero, 90f); // facing +x, the ped is in plain view
            Ped p = Promote(sim, new Vec3(15f, 0f, 0f), 4000, Shop, ErrandKind.Work);
            sim.Run(3.0, In.Idle);
            Assert.NotNull(sim.W.FindPed(p.Id));
            sim.Place(new Vec3(-140f, 0f, 0f), 270f); // 150+ m away: beyond DemoteAlwaysDistance
            Vec3 last = p.Position;
            float yaw = p.YawDeg;
            for (int i = 0; i < 60 && sim.W.FindPed(p.Id) != null; i++)
            {
                last = p.Position;
                yaw = p.YawDeg;
                sim.Tick(In.Idle);
            }
            Assert.Null(sim.W.FindPed(p.Id));
            Assert.Equal(1, sim.CountEffects(CityEffectKind.ReturnCitizen));
            CityEffect e = sim.FirstEffect(CityEffectKind.ReturnCitizen);
            Assert.False(e.Arrived);
            Assert.True(Vec3.DistanceXZ(e.Position, last) < 0.5f);
            Assert.True(Sim.AngleDiff(e.YawDeg, yaw) < 5f);
            Assert.Equal(Shop, e.Building);
            Assert.Equal(ErrandKind.Work, e.Errand);
        }

        // ---- 6, 7: home fallback and no destination -----------------------------------------------------------------

        [Fact]
        public void NoErrand_WithAHomeProfile_WalksHome_AndArrivesAsHome()
        {
            Sim sim = NewSim();
            sim.City.AddBuilding(Bld(Home, new Vec3(40f, 0f, 3f), ShopDoor));
            sim.City.Profiles[4000u] = new CitizenProfile { Citizen = 4000u, HomeBuilding = Home };
            Ped p = Promote(sim, new Vec3(10f, 0f, 3f), 4000, 0, ErrandKind.None);
            var brain = Assert.IsType<ErrandBrain>(p.Brain);
            Assert.Equal(Home, brain.Building);
            Assert.Equal(ShopDoor, brain.Door);
            Assert.Equal(ErrandKind.Home, p.Errand);
            Assert.Equal(Home, p.ErrandBuilding);
            Assert.True(sim.Until(() => sim.W.FindPed(p.Id) == null, 40.0, In.Idle));
            CityEffect e = sim.FirstEffect(CityEffectKind.ReturnCitizen);
            Assert.True(e.Arrived);
            Assert.Equal(Home, e.Building);
            Assert.Equal(ErrandKind.Home, e.Errand);
        }

        [Fact]
        public void NoErrandAndNoHome_GivesAnIdleBrain_AndForReturnsNull()
        {
            Sim sim = NewSim();
            Ped p = Promote(sim, new Vec3(10f, 0f, 3f), 4000, 0, ErrandKind.None);
            Assert.IsType<IdleBrain>(p.Brain);
            Assert.Null(ErrandBrain.For(sim.W, p, BrainTests.Adult));
        }

        [Fact]
        public void InteriorPed_HasNoErrandBrain()
        {
            var a = new Arena();
            a.City.AddBuilding(Bld(Shop, new Vec3(25f, 0f, 0f), new Vec3(25f, 0f, 0f)));
            Ped p = a.Spawn(0f, 0f);
            p.ErrandBuilding = Shop;
            p.Errand = ErrandKind.Work;
            p.InteriorId = 1;
            Assert.Null(ErrandBrain.For(a.World, p, BrainTests.Adult));
        }

        // ---- 8: carjacked driver -----------------------------------------------------------------------------------

        [Fact]
        public void PromoteVehicle_GivesTheDriverItsErrand_AndTakesTheCitizenOnce()
        {
            Sim sim = NewSim();
            var v = new AmbientVehicle
            {
                Id = 40, Position = new Vec3(100f, 0.75f, 0f), Rotation = Quat.Yaw(0f), Velocity = new Vec3(0f, 0f, 3f),
                Model = 0, Kind = VehicleKind.Car, DriverCitizen = 77, DriverErrandBuilding = Shop, DriverErrand = ErrandKind.Visit,
            };
            sim.W.PromoteVehicle(v, true, PedOrigin.CarjackedDriver, 0);
            Ped driver = sim.W.FindPedByCitizen(77);
            Assert.NotNull(driver);
            Assert.Equal(Shop, driver.ErrandBuilding);
            Assert.Equal(ErrandKind.Visit, driver.Errand);
            var fx = new List<CityEffect>(sim.W.Effects.Items);
            Assert.Equal(1, Count(fx, CityEffectKind.TakeCitizen));
            CityEffect take = fx.Find(e => e.Kind == CityEffectKind.TakeCitizen);
            Assert.Equal(77u, take.Citizen);
            Assert.Equal((ushort)0, take.Instance);
            Assert.True(fx.FindIndex(e => e.Kind == CityEffectKind.TakeVehicle) < fx.FindIndex(e => e.Kind == CityEffectKind.TakeCitizen));
        }

        [Fact]
        public void PromoteVehicle_WithoutADriverCitizen_EmitsNoTakeCitizen()
        {
            Sim sim = NewSim();
            var v = new AmbientVehicle { Id = 41, Position = new Vec3(100f, 0.75f, 0f), Rotation = Quat.Yaw(0f), Model = 0, Kind = VehicleKind.Car };
            sim.W.PromoteVehicle(v, true, PedOrigin.CarjackedDriver, 0);
            Assert.Equal(0, Count(sim.W.Effects.Items, CityEffectKind.TakeCitizen));
        }

        [Fact]
        public void CarjackedFleeingDriver_ResumesItsErrandAfterFleeing()
        {
            for (uint citizen = 100; citizen < 160; citizen++)
            {
                Sim sim = NewSim();
                sim.City.AddBuilding(Bld(Shop, new Vec3(40f, 0f, 40f), new Vec3(40f, 0f, 40f)));
                sim.AddMovingCar(40, new Vec3(0f, 0.75f, 0f), 0f, new Vec3(0f, 0f, 1f), citizen);
                AmbientVehicle v = sim.Snap.Vehicles[0];
                v.DriverErrandBuilding = Shop;
                v.DriverErrand = ErrandKind.Work;
                sim.Snap.Vehicles[0] = v;
                sim.Place(new Vec3(-3f, 0f, 0.44f), 90f);
                sim.Run(0.2, In.Idle);
                sim.Tick(In.Press(GameButton.EnterExit));
                Assert.True(sim.Until(() => sim.Player.Has(PedFlags.InVehicle), 5.0, In.Idle));
                Ped driver = sim.W.FindPedByCitizen(citizen);
                Assert.NotNull(driver);
                Assert.Equal(ErrandKind.Work, driver.Errand);
                if (!(driver.Brain is FleeBrain)) continue; // this citizen fights instead
                bool sawErrand = false;
                for (int i = 0; i < 20 * 60 && !sawErrand; i++)
                {
                    sim.Tick(In.Idle);
                    Ped d = sim.W.FindPedByCitizen(citizen);
                    if (d == null) break;
                    sawErrand = d.Brain is ErrandBrain;
                }
                if (!sawErrand)
                {
                    // demoted first (far and unseen): handed back with its errand, not arrived
                    Assert.Null(sim.W.FindPedByCitizen(citizen));
                    int n = 0;
                    foreach (CityEffect e in sim.Effects)
                    {
                        if (e.Kind != CityEffectKind.ReturnCitizen || e.Citizen != citizen) continue;
                        n++;
                        Assert.Equal(Shop, e.Building);
                        Assert.Equal(ErrandKind.Work, e.Errand);
                        Assert.False(e.Arrived);
                    }
                    Assert.Equal(1, n);
                }
                return;
            }
            Assert.Fail("no citizen in range flees");
        }

        // ---- 9: reactions ------------------------------------------------------------------------------------------

        [Fact]
        public void BumpedErrandWalker_Stumbles_ThenResumesTheErrand()
        {
            var a = new Arena();
            a.World.Hooks.Brains = new PedBrainFactory();
            a.City.AddBuilding(Bld(Shop, new Vec3(60f, 0f, 0f), new Vec3(60f, 0f, 0f)));
            Ped p = a.Spawn(0f, 0f);
            p.ErrandBuilding = Shop;
            p.Errand = ErrandKind.Work;
            p.Brain = ErrandBrain.For(a.World, p, BrainTests.Adult);
            Assert.IsType<ErrandBrain>(p.Brain);
            Assert.True(PedsSystem.Deliver(a.World, p, BrainTests.Stim(StimulusKind.Bumped, a.Player.Tag, new Vec3(0f, 0f, -1f), 2f)));
            Assert.IsType<StumbleBrain>(p.Brain);
            var sys = new PedsSystem();
            a.Run(sys, 1.2f);
            Assert.IsType<ErrandBrain>(p.Brain);
        }
    }
}
