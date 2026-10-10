using System;
using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Interiors;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Physics;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Session;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.SessionTests
{
    public class RespawnTests
    {
        private static readonly Vec3 NearHospitalDoor = new Vec3(100f, 0f, 95f);
        private static readonly Vec3 FarHospitalDoor = new Vec3(-300f, 0f, -300f);

        private static Sim WithHospitals(Action<PlayerProfile> profile = null)
        {
            var sim = new Sim(city: c =>
            {
                BuildingDescriptor near = Sim.Building(80, ServiceKind.Health, "", new Vec3(100f, 0f, 100f), 0f, "Near Hospital");
                near.Doors = new[] { NearHospitalDoor };
                BuildingDescriptor far = Sim.Building(81, ServiceKind.Health, "", new Vec3(-300f, 0f, -295f), 0f, "Far Hospital");
                far.Doors = new[] { FarHospitalDoor };
                BuildingDescriptor cops = Sim.Building(82, ServiceKind.Police, "", new Vec3(-100f, 0f, 100f), 0f, "Precinct");
                cops.Doors = new[] { new Vec3(-100f, 0f, 95f) };
                c.AddBuilding(near); c.AddBuilding(far); c.AddBuilding(cops);
            }, profile: profile, feet: new Vec3(50f, 0f, 50f));
            return sim;
        }

        [Fact]
        public void HealthZero_ShowsWasted_ForFourSeconds_ThenRespawnsAtTheNearestHospitalDoor()
        {
            Sim sim = WithHospitals(p => p.AddCash(1500));
            sim.Player.Health = 0f;
            sim.Run(0.5, In.Idle);
            Assert.Equal(LifeState.Wasted, sim.PS.Life);
            Assert.True(sim.Player.IsDead);
            Assert.Equal(BigScreenKind.Wasted, sim.W.Messages.BigScreen);
            Assert.Equal(BigScreenKind.Wasted, sim.S.Hud.BigScreen);
            Assert.Equal(1, sim.Count(GameEventType.PlayerDied));
            Assert.Equal(sim.Player.Tag, sim.Last(GameEventType.PlayerDied).ActorB);
            Assert.Equal(1, sim.Profile.GetStat(StatNames.Deaths));
            sim.Run(2.5, In.Idle);
            Assert.Equal(LifeState.Wasted, sim.PS.Life);
            Assert.True(sim.Until(() => sim.PS.Life == LifeState.Alive, 4.0, In.Idle), "never respawned");
            sim.Run(0.5, In.Idle);
            Assert.True(Vec3.DistanceXZ(NearHospitalDoor, sim.Player.Position) < 4f, "at the nearest hospital door");
            Sim.Near(100f, sim.Player.Health, 0.01f, "full health");
            Assert.False(sim.Player.IsDead);
            Assert.Equal(BigScreenKind.None, sim.W.Messages.BigScreen);
            Assert.Equal(0, (int)(sim.Player.Flags & (PedFlags.Dead | PedFlags.HandsUp | PedFlags.Aiming)));
            GameEvent r = sim.Last(GameEventType.PlayerRespawned);
            Assert.Equal(0f, r.Value);
            Assert.True(Vec3.DistanceXZ(NearHospitalDoor, r.Position) < 4f);
        }

        [Fact]
        public void Wasted_CostsTheHospitalFee_ClearsTheWantedLevel_AndLosesTheWeaponsAmmo()
        {
            Sim sim = WithHospitals(p =>
            {
                p.AddCash(1500);
                p.GiveWeapon(WeaponId.Pistol);
                p.SetAmmo(WeaponId.Pistol, 40);
                p.GiveWeapon(WeaponId.Smg);
                p.SetAmmo(WeaponId.Smg, 60);
                p.Armour = 60;
            });
            long cash = sim.Profile.Cash;
            sim.Player.CurrentWeapon = (int)WeaponId.Pistol;
            sim.Player.Armour = 60f;
            sim.W.Wanted.Stars = 3;
            sim.Player.Health = 0f;
            Assert.True(sim.Until(() => sim.PS.Life == LifeState.Wasted, 1.0, In.Idle));
            Assert.True(sim.Until(() => sim.PS.Life == LifeState.Alive, 6.0, In.Idle));
            Assert.Equal(cash - PlayerRules.HospitalFee, sim.Profile.Cash);
            Assert.Equal(0, sim.W.Wanted.Stars);
            Assert.Equal(0, sim.Profile.GetAmmo(WeaponId.Pistol));
            Assert.Equal(60, sim.Profile.GetAmmo(WeaponId.Smg));
            Assert.True(sim.Profile.OwnsWeapon(WeaponId.Pistol));
            Sim.Near(60f, sim.Player.Armour, 0.01f, "armour kept after Wasted");
        }

        [Fact]
        public void Wasted_WithLessThanTheFee_TakesWhatThereIs_NeverNegative()
        {
            Sim sim = WithHospitals(p => { p.TakeCash(p.Cash); p.AddCash(120); });
            sim.Player.Health = 0f;
            Assert.True(sim.Until(() => sim.PS.Life == LifeState.Wasted, 1.0, In.Idle));
            Assert.True(sim.Until(() => sim.PS.Life == LifeState.Alive, 6.0, In.Idle));
            Assert.Equal(0L, sim.Profile.Cash);
        }

        [Fact]
        public void Wasted_PicksTheNearestOfSeveralHospitals()
        {
            Sim sim = WithHospitals();
            sim.Place(new Vec3(-280f, 0f, -280f), 0f);
            sim.Run(0.2, In.Idle);
            sim.Player.Health = 0f;
            Assert.True(sim.Until(() => sim.PS.Life == LifeState.Wasted, 1.0, In.Idle));
            Assert.True(sim.Until(() => sim.PS.Life == LifeState.Alive, 6.0, In.Idle));
            sim.Run(0.3, In.Idle);
            Assert.True(Vec3.DistanceXZ(FarHospitalDoor, sim.Player.Position) < 4f);
        }

        [Fact]
        public void Wasted_WithNoHospital_RespawnsAtTheBeginPosition()
        {
            var sim = new Sim(feet: new Vec3(12f, 0f, 7f));
            sim.Place(new Vec3(90f, 0f, 90f), 0f);
            sim.Player.Health = 0f;
            Assert.True(sim.Until(() => sim.PS.Life == LifeState.Wasted, 1.0, In.Idle));
            Assert.True(sim.Until(() => sim.PS.Life == LifeState.Alive, 6.0, In.Idle));
            sim.Run(0.3, In.Idle);
            Assert.True(Vec3.DistanceXZ(new Vec3(12f, 0f, 7f), sim.Player.Position) < 3f);
        }

        [Fact]
        public void Wasted_InsideAnInterior_RespawnsOutside()
        {
            BuildingDescriptor b;
            Sim sim = Stores.Convenience(out b);
            sim.EnterBuilding(b);
            sim.Player.Health = 0f;
            Assert.True(sim.Until(() => sim.PS.Life == LifeState.Wasted, 1.0, In.Idle));
            Assert.True(sim.Until(() => sim.PS.Life == LifeState.Alive, 6.0, In.Idle));
            sim.Run(1.5, In.Idle);
            Assert.False(sim.W.Interior.IsInside);
            Assert.True(sim.Player.Position.Y > -10f);
        }

        [Fact]
        public void Wasted_InACar_LeavesTheCar()
        {
            var sim = new Sim();
            Car car = sim.W.SpawnCar(0, Vec3.Zero, 0f, CarOwner.Player, 0xFFFFFF);
            sim.W.PutInCar(sim.Player, car, 0);
            sim.Player.Health = 0f;
            Assert.True(sim.Until(() => sim.PS.Life == LifeState.Wasted, 1.0, In.Idle));
            Assert.True(sim.Until(() => sim.PS.Life == LifeState.Alive, 6.0, In.Idle));
            Assert.False(sim.Player.Has(PedFlags.InVehicle));
            Assert.Equal(0, car.DriverId);
        }

        [Fact]
        public void BustedRequest_ShowsBusted_FinesBail_StripsWeapons_AndRespawnsAtThePoliceStation()
        {
            Sim sim = WithHospitals(p => { p.AddCash(1500); p.GiveWeapon(WeaponId.Pistol); p.SetAmmo(WeaponId.Pistol, 30); });
            long cash = sim.Profile.Cash;
            sim.Player.Armour = 50f;
            sim.W.Wanted.Stars = 2;
            sim.W.Wanted.BustedRequested = true;
            sim.W.Wanted.BustedByTag = WorldTag.ForPed(99);
            sim.Run(0.5, In.Idle);
            Assert.Equal(LifeState.Busted, sim.PS.Life);
            Assert.Equal(BigScreenKind.Busted, sim.S.Hud.BigScreen);
            GameEvent a = sim.Last(GameEventType.PlayerArrested);
            Assert.Equal(WorldTag.ForPed(99), a.ActorA);
            Assert.Equal(sim.Player.Tag, a.ActorB);
            Assert.Equal(1, sim.Profile.GetStat(StatNames.Arrests));
            Assert.False(sim.W.Wanted.BustedRequested);
            Assert.True(sim.Until(() => sim.PS.Life == LifeState.Alive, 6.0, In.Idle));
            sim.Run(0.3, In.Idle);
            Assert.True(Vec3.DistanceXZ(new Vec3(-100f, 0f, 95f), sim.Player.Position) < 4f, "at the police station door");
            Assert.Equal(cash - PlayerRules.BustedFine, sim.Profile.Cash);
            Assert.False(sim.Profile.OwnsWeapon(WeaponId.Pistol));
            Sim.Near(0f, sim.Player.Armour, 0.01f, "armour gone after Busted");
            Sim.Near(100f, sim.Player.Health, 0.01f);
            Assert.Equal(0, sim.W.Wanted.Stars);
            Assert.Equal(1f, sim.Last(GameEventType.PlayerRespawned).Value);
        }

        [Fact]
        public void DeathScreen_LastsAboutFourSeconds()
        {
            Sim sim = WithHospitals();
            sim.Player.Health = 0f;
            sim.Run(3.0, In.Idle);
            Assert.Equal(BigScreenKind.Wasted, sim.S.Hud.BigScreen);
            sim.Run(3.5, In.Idle);
            Assert.Equal(BigScreenKind.None, sim.S.Hud.BigScreen);
            Assert.Equal(LifeState.Alive, sim.PS.Life);
        }
    }

    public class AmbientPromotionTests
    {
        private sealed class Calm : IPedBrain
        {
            public bool Active;
            public string Name { get { return "Calm"; } }
            public bool IsActive { get { return Active; } }
            public void Step(GameWorld world, Ped ped, float dt) { }
        }

        private static Ped Promote(Sim sim, Vec3 at, uint citizen = 4000)
        {
            AmbientCitizen c = new AmbientCitizen { Instance = 400, Citizen = citizen, Position = at, Model = 2 };
            return sim.W.PromoteCitizen(c, PedOrigin.PromotedCitizen, 0);
        }

        // The player faces +x, so a ped far along -x is behind the camera and a ped far along +x is in plain view.
        private static Sim Facing() { var s = new Sim(yaw: 90f); s.Run(0.2, In.Idle); return s; }

        [Fact]
        public void PromoteCitizen_CreatesACivilianPed_AndTakesTheCitizenOnce()
        {
            Sim sim = Facing();
            Ped p = Promote(sim, new Vec3(10f, 0f, 3f));
            Assert.Equal(PedKind.Civilian, p.Kind);
            Assert.Equal(4000u, p.Citizen);
            Assert.Equal(2, p.Model);
            Assert.Same(p, sim.W.FindPedByCitizen(4000));
            Assert.Same(p, Promote(sim, new Vec3(10f, 0f, 3f)));
            sim.Run(0.1, In.Idle);
            Assert.Equal(1, sim.CountEffects(CityEffectKind.TakeCitizen));
            CityEffect e = sim.FirstEffect(CityEffectKind.TakeCitizen);
            Assert.Equal(4000u, e.Citizen);
            Assert.Equal((ushort)400, e.Instance);
        }

        [Fact]
        public void APromotedCitizen_NoLongerGetsASnapshotCapsule()
        {
            Sim sim = Facing();
            sim.AddCitizen(400, 4000, new Vec3(10f, 0f, 3f));
            sim.Run(0.1, In.Idle);
            Assert.Equal(2, sim.W.Collision.DynamicCount);
            Promote(sim, new Vec3(10f, 0f, 3f));
            sim.Run(0.1, In.Idle);
            Assert.Equal(2, sim.W.Collision.DynamicCount);
        }

        [Fact]
        public void Ambient_ShapesCarryTagsThatSayWhatTheyAre()
        {
            Sim sim = Facing();
            sim.AddCitizen(400, 4000, new Vec3(10f, 0f, 3f));
            sim.AddParkedCar(21, new Vec3(10f, 0.75f, -8f), 0f);
            sim.AddMovingCar(22, new Vec3(10f, 0.75f, 20f), 0f, new Vec3(0f, 0f, 4f), 0);
            sim.Run(0.1, In.Idle);
            PhysicsHit hit;
            QueryMask dyn = QueryMask.DynamicOnly.Ignoring(sim.Player.Tag);
            Assert.True(sim.W.Collision.Raycast(new Vec3(0f, 0.9f, 3f), new Vec3(1f, 0f, 0f), 30f, dyn, out hit));
            Assert.Equal(WorldTag.ForAmbientCitizen(400), hit.Tag);
            Assert.Equal(HitKind.Capsule, hit.Kind);
            Assert.True(sim.W.Collision.Raycast(new Vec3(0f, 0.75f, -8f), new Vec3(1f, 0f, 0f), 30f, dyn, out hit));
            Assert.Equal(WorldTag.ForAmbientVehicle(21, true), hit.Tag);
            Assert.Equal(HitKind.Box, hit.Kind);
            Assert.True(sim.W.Collision.Raycast(new Vec3(0f, 0.75f, 20f), new Vec3(1f, 0f, 0f), 30f, dyn, out hit));
            Assert.Equal(WorldTag.ForAmbientVehicle(22, false), hit.Tag);
        }

        [Fact]
        public void NullSnapshot_KeepsTheLastOne()
        {
            Sim sim = Facing();
            sim.AddParkedCar(21, new Vec3(10f, 0.75f, -8f), 0f);
            sim.Run(0.1, In.Idle);
            sim.S.Tick(Sim.Dt, In.Idle, null);
            PhysicsHit hit;
            Assert.True(sim.W.Collision.Raycast(new Vec3(0f, 0.75f, -8f), new Vec3(1f, 0f, 0f), 30f, QueryMask.DynamicOnly, out hit));
        }

        [Theory]
        [InlineData(-70f, true)]
        [InlineData(-50f, false)]
        [InlineData(70f, false)]
        [InlineData(160f, true)]
        public void CalmPed_IsDemotedBeyond60mUnseen_Or150mAlways(float x, bool demoted)
        {
            Sim sim = Facing();
            Ped p = Promote(sim, new Vec3(x, 0f, 0f));
            sim.Run(0.3, In.Idle);
            Assert.Equal(demoted, sim.W.FindPed(p.Id) == null);
            Assert.Equal(demoted ? 1 : 0, sim.CountEffects(CityEffectKind.ReturnCitizen));
            if (demoted) Assert.Equal(4000u, sim.FirstEffect(CityEffectKind.ReturnCitizen).Citizen);
        }

        [Fact]
        public void Demotion_SparesPersistentActiveAndBodyguardedPeds()
        {
            Sim sim = Facing();
            Ped persistent = Promote(sim, new Vec3(-90f, 0f, 0f), 4001);
            persistent.Flags |= PedFlags.Persistent;
            Ped busy = Promote(sim, new Vec3(-90f, 0f, 10f), 4002);
            busy.Brain = new Calm { Active = true };
            Ped idle = Promote(sim, new Vec3(-90f, 0f, 20f), 4003);
            idle.Brain = new Calm { Active = false };
            sim.Run(0.3, In.Idle);
            Assert.NotNull(sim.W.FindPed(persistent.Id));
            Assert.NotNull(sim.W.FindPed(busy.Id));
            Assert.Null(sim.W.FindPed(idle.Id));
        }

        [Fact]
        public void DeadAndInjuredPeds_AreRemovedBeyond120m_WithoutReturningTheCitizen()
        {
            Sim sim = Facing();
            Ped near = Promote(sim, new Vec3(100f, 0f, 0f), 4001);
            near.Flags |= PedFlags.Dead;
            Ped far = Promote(sim, new Vec3(-130f, 0f, 0f), 4002);
            far.Flags |= PedFlags.Dead;
            Ped hurt = Promote(sim, new Vec3(-125f, 0f, 20f), 4003);
            hurt.Flags |= PedFlags.Injured;
            sim.Run(0.3, In.Idle);
            Assert.NotNull(sim.W.FindPed(near.Id));
            Assert.Null(sim.W.FindPed(far.Id));
            Assert.Null(sim.W.FindPed(hurt.Id));
            Assert.Equal(0, sim.CountEffects(CityEffectKind.ReturnCitizen));
        }

        [Fact]
        public void AbandonedCar_FarAndUnseen_IsRemovedAfterTenSeconds()
        {
            var sim = new Sim();
            Car car = sim.W.SpawnCar(0, new Vec3(0f, 0f, 350f), 0f, CarOwner.Abandoned, 0xFFFFFF);
            sim.Run(8.5, In.Idle);
            Assert.NotNull(sim.W.FindCar(car.Id));
            sim.Run(3.0, In.Idle);
            Assert.Null(sim.W.FindCar(car.Id));
        }

        [Fact]
        public void AbandonedCar_Within300m_MissionAndPoliceCars_AreKept()
        {
            var sim = new Sim();
            Car close = sim.W.SpawnCar(0, new Vec3(0f, 0f, 250f), 0f, CarOwner.Abandoned, 0xFFFFFF);
            Car mission = sim.W.SpawnCar(0, new Vec3(30f, 0f, 350f), 0f, CarOwner.Mission, 0xFFFFFF);
            Car police = sim.W.SpawnCar(1, new Vec3(-30f, 0f, 350f), 0f, CarOwner.Police, 0xFFFFFF);
            sim.Run(14.0, In.Idle);
            Assert.NotNull(sim.W.FindCar(close.Id));
            Assert.NotNull(sim.W.FindCar(mission.Id));
            Assert.NotNull(sim.W.FindCar(police.Id));
        }
    }
}
