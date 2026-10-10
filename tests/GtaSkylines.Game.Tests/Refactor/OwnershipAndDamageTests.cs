using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Combat;
using GtaSkylines.Game.Crime;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Interiors;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Session;
using GtaSkylines.Game.Tests.Combat;
using GtaSkylines.Game.Tests.CrimeTests;
using GtaSkylines.Game.Tests.SessionTests;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.Refactor
{
    /// <summary>Health and armour belong to the live player ped while playing; the profile is written on End().</summary>
    public class VitalsOwnershipTests
    {
        private sealed class Vitals : IVitals
        {
            public float Health { get; set; }
            public float Armour { get; set; }
        }

        private static Wallet Rich(out PlayerProfile profile)
        {
            profile = new PlayerProfile();
            profile.TakeCash(profile.Cash);
            profile.AddCash(10000);
            profile.Health = 60;
            profile.Armour = 10;
            return new Wallet(profile);
        }

        [Fact]
        public void Food_heals_the_given_vitals_and_leaves_the_profile_alone()
        {
            PlayerProfile p;
            Wallet w = Rich(out p);
            var v = new Vitals { Health = 50.5f, Armour = 0f };
            Assert.Equal(PurchaseResult.Ok, Shop.Buy(w, ShopKind.Food, "snack", v));
            Assert.Equal(75.5f, v.Health, 2);
            Assert.Equal(60, p.Health);
            Assert.Equal(10000L - 5, p.Cash);
        }

        [Fact]
        public void Armour_item_fills_the_vitals_armour_and_is_refused_when_full()
        {
            PlayerProfile p;
            Wallet w = Rich(out p);
            var v = new Vitals { Health = 100f, Armour = 20f };
            Assert.Equal(PurchaseResult.Ok, Shop.Buy(w, ShopKind.Ammunation, "armour", v));
            Assert.Equal(100f, v.Armour, 3);
            Assert.Equal(10, p.Armour);
            long cash = p.Cash;
            Assert.Equal(PurchaseResult.ArmourFull, Shop.Buy(w, ShopKind.Ammunation, "armour", v));
            Assert.Equal(cash, p.Cash);
        }

        [Fact]
        public void Heal_at_full_vitals_health_is_refused_without_charging()
        {
            PlayerProfile p;
            Wallet w = Rich(out p);
            p.Health = 20; // the profile is hurt, the live vitals are not: the vitals decide
            var v = new Vitals { Health = 100f };
            long cash = p.Cash;
            Assert.Equal(PurchaseResult.HealthFull, Shop.Buy(w, ShopKind.Hospital, "heal", v));
            Assert.Equal(cash, p.Cash);
        }

        [Fact]
        public void The_three_argument_overload_still_acts_on_the_profile()
        {
            PlayerProfile p;
            Wallet w = Rich(out p);
            Assert.Equal(PurchaseResult.Ok, Shop.Buy(w, ShopKind.Food, "snack"));
            Assert.Equal(85, p.Health);
        }

        [Fact]
        public void Buying_food_in_a_running_session_changes_the_ped_and_reaches_the_profile_only_on_End()
        {
            BuildingDescriptor b = Sim.FindBuilding(InteriorArchetype.ConvenienceStore, ServiceKind.Commercial, "", new Vec3(60f, 0f, 60f), 90f);
            var sim = new Sim(city: c => c.AddBuilding(b), profile: p => { p.AddCash(1000); p.Health = 60; });
            sim.EnterBuilding(b);
            sim.GoTo(InteractionKind.ShopCounter);
            sim.Player.Health = 50.5f;
            sim.Tick(In.Press(GameButton.Interact));
            sim.Run(0.1, In.Idle);
            int snack = Shop.ItemsFor(ShopKind.Food).FindIndex(i => i.Id == "snack");
            Assert.Equal(PurchaseResult.Ok, sim.S.ChooseMenuOption(snack));

            Sim.Near(75.5f, sim.W.Player.Health, 1f, "ped health");
            Assert.Equal(60, sim.W.Profile.Health);

            sim.S.End();
            Assert.Equal((int)System.Math.Round(sim.Player.Health), sim.W.Profile.Health);
            Assert.InRange(sim.W.Profile.Health, 75, 77);
        }
    }

    /// <summary>The profile is the only property owner; explosions and non-combat damage use Damage's death path.</summary>
    public class OwnershipAndDamageTests
    {
        private static readonly Vec3 HousePos = new Vec3(60f, 0f, 60f);

        private static Sim HouseSim(out BuildingDescriptor b, bool ownedInProfile, long cash = 0)
        {
            BuildingDescriptor bd = Sim.FindBuilding(InteriorArchetype.House, ServiceKind.Residential, "ResidentialLow", HousePos, 90f);
            b = bd;
            var sim = new Sim(city: c => c.AddBuilding(bd), profile: p =>
            {
                if (ownedInProfile) p.Properties.Add(bd.Id);
                if (cash > 0) p.AddCash(cash);
            });
            sim.S.AddSystem(new CrimeSystem(new FakePedServices(), 7u));
            return sim;
        }

        private static float HeatAfterHalfMinuteInside(bool owned)
        {
            BuildingDescriptor b;
            Sim sim = HouseSim(out b, owned);
            sim.EnterBuilding(b);
            sim.W.Wanted.PlayerHeat = 50f;
            sim.Run(30.0, In.Idle);
            return sim.W.Wanted.PlayerHeat;
        }

        [Fact]
        public void Heat_decays_faster_inside_a_building_the_profile_owns()
        {
            float owned = HeatAfterHalfMinuteInside(true);
            float unowned = HeatAfterHalfMinuteInside(false);
            Assert.True(owned < unowned, "owned " + owned + " vs unowned " + unowned);
            Assert.True(unowned < 50f, "unowned buildings still decay at the base rate");
        }

        [Fact]
        public void Buying_through_the_property_menu_makes_the_profile_the_owner_and_unlocks_the_bed()
        {
            BuildingDescriptor b;
            Sim sim = HouseSim(out b, false, 1000000);
            sim.EnterBuilding(b);
            sim.GoTo(InteractionKind.Bed);
            sim.Tick(In.Press(GameButton.Interact));
            sim.Run(0.1, In.Idle);
            Assert.False(sim.W.Menu.IsOpen, "bed is owner-only");

            sim.W.Menu.Kind = MenuKind.Property;
            sim.W.Menu.Building = b.Id;
            Assert.Equal(PurchaseResult.Ok, sim.S.ChooseMenuOption(0));
            Assert.True(sim.W.Profile.OwnsProperty(b.Id));

            sim.Tick(In.Press(GameButton.Interact));
            Assert.Equal(MenuKind.Bed, sim.W.Menu.Kind);
        }

        [Fact]
        public void Wardrobe_is_available_when_the_building_is_owned_only_via_the_profile()
        {
            BuildingDescriptor b;
            Sim sim = HouseSim(out b, true);
            sim.EnterBuilding(b);
            sim.GoTo(InteractionKind.Wardrobe);
            sim.Tick(In.Press(GameButton.Interact));
            Assert.Equal(MenuKind.Wardrobe, sim.W.Menu.Kind);
        }

        // ---- exploding cars ----------------------------------------------------------------------------------------

        [Fact]
        public void Exploding_car_kills_occupants_through_the_damage_path_exactly_once()
        {
            var sim = new Sim();
            Car car = sim.W.SpawnCar(0, new Vec3(40f, 0f, 40f), 0f, CarOwner.Civilian, 0x3366CC);
            Ped citizen = sim.W.SpawnPed(PedKind.Civilian, 3, car.Position, 0f);
            citizen.Citizen = 555u;
            Ped plain = sim.W.SpawnPed(PedKind.Civilian, 3, car.Position, 0f);
            sim.W.PutInCar(citizen, car, 0);
            sim.W.PutInCar(plain, car, 1);

            sim.W.DamageCar(car, 100000f, 0, car.Position);
            Assert.True(sim.Until(() => car.Destroyed, 12.0, In.Idle), "car never exploded");
            sim.Run(0.2, In.Idle);

            double at = sim.Last(GameEventType.CarDestroyed).GameTime;
            foreach (Ped p in new[] { citizen, plain })
            {
                Assert.True(p.IsDead);
                Assert.Equal(car.Tag, p.LastAttackerTag);
                Assert.Equal(at, p.LastDamageTime);
                int kills = 0;
                foreach (GameEvent e in sim.Log) if (e.Type == GameEventType.PedKilled && e.ActorB == p.Tag) kills++;
                Assert.Equal(1, kills);
            }
            int effects = 0;
            foreach (CityEffect e in sim.Effects) if (e.Kind == CityEffectKind.KillCitizen) { effects++; Assert.Equal(555u, e.Citizen); }
            Assert.Equal(1, effects);
        }

        // ---- Damage.ApplyUnarmoured --------------------------------------------------------------------------------

        [Fact]
        public void ApplyUnarmoured_on_the_player_costs_health_only_and_stamps_cause_and_time()
        {
            var a = new Arena();
            a.Player.Armour = 30f;
            a.Player.Health = 100f;
            a.World.Clock.GameSeconds = 42.5;
            DamageResult r = Damage.ApplyUnarmoured(a.World, a.Player, 20f, 777, DamageKind.Fall);
            Assert.Equal(20f, r.HealthLost, 3);
            Assert.False(r.Killed);
            Assert.Equal(80f, a.Player.Health, 3);
            Assert.Equal(30f, a.Player.Armour, 3);
            Assert.Equal(42.5, a.Player.LastDamageTime);
            Assert.Equal(777, a.Player.LastAttackerTag);
        }

        [Fact]
        public void ApplyUnarmoured_lethal_damage_clamps_at_zero_and_kills_through_the_death_path()
        {
            var a = new Arena();
            a.Player.Health = 10f;
            long seq = a.World.Events.NextSeq;
            DamageResult r = Damage.ApplyUnarmoured(a.World, a.Player, 500f, 5, DamageKind.Fall);
            Assert.True(r.Killed);
            Assert.Equal(10f, r.HealthLost, 3);
            Assert.Equal(0f, a.Player.Health);
            Assert.Equal(5, a.Player.LastAttackerTag);
            int killed = 0;
            foreach (GameEvent e in a.EventsSince(seq)) if (e.Type == GameEventType.PedKilled && e.ActorB == a.Player.Tag) killed++;
            Assert.Equal(1, killed);
        }

        [Fact]
        public void ApplyUnarmoured_on_a_citizen_ped_kills_the_city_citizen_once()
        {
            var a = new Arena();
            Ped p = a.Spawn(0f, 0f);
            p.Citizen = 91u;
            Damage.ApplyUnarmoured(a.World, p, 500f, 3, DamageKind.Fall);
            Damage.ApplyUnarmoured(a.World, p, 500f, 3, DamageKind.Fall);
            Assert.True(p.IsDead);
            Assert.Equal(1, a.CountEffects(CityEffectKind.KillCitizen));
        }

        [Fact]
        public void ApplyUnarmoured_dropping_below_35_flags_injured_and_raises_one_injury_event()
        {
            var a = new Arena();
            long seq = a.World.Events.NextSeq;
            Damage.ApplyUnarmoured(a.World, a.Player, 70f, 1, DamageKind.Fall);
            Damage.ApplyUnarmoured(a.World, a.Player, 5f, 1, DamageKind.Fall);
            Assert.True(a.Player.Has(PedFlags.Injured));
            int injured = 0;
            foreach (GameEvent e in a.EventsSince(seq)) if (e.Type == GameEventType.PedInjured && e.ActorB == a.Player.Tag) injured++;
            Assert.Equal(1, injured);
        }

        [Fact]
        public void ApplyUnarmoured_is_a_no_op_on_null_dead_or_non_positive_amounts()
        {
            var a = new Arena();
            Damage.ApplyUnarmoured(a.World, null, 10f, 1, DamageKind.Fall);
            DamageResult zero = Damage.ApplyUnarmoured(a.World, a.Player, 0f, 1, DamageKind.Fall);
            DamageResult neg = Damage.ApplyUnarmoured(a.World, a.Player, -5f, 1, DamageKind.Fall);
            Assert.Equal(0f, zero.HealthLost);
            Assert.Equal(0f, neg.HealthLost);
            Assert.Equal(100f, a.Player.Health);
            Assert.Equal(-1.0, a.Player.LastDamageTime);

            Ped dead = a.Spawn(5f, 5f);
            Damage.ApplyToPed(a.World, dead, 500f, 0, DamageKind.Other, false, 0f);
            double t = dead.LastDamageTime;
            a.World.Clock.GameSeconds += 10.0;
            DamageResult r = Damage.ApplyUnarmoured(a.World, dead, 10f, 9, DamageKind.Fall);
            Assert.False(r.Killed);
            Assert.Equal(t, dead.LastDamageTime);
        }

        [Fact]
        public void Bailing_from_a_moving_car_stamps_time_and_car_tag_and_leaves_armour_alone()
        {
            Sim sim = SessionTests.Scene.ParkedCar();
            sim.Tick(In.Press(GameButton.EnterExit));
            Assert.True(sim.Until(() => sim.Player.Has(PedFlags.InVehicle), 4.0, In.Idle));
            Car car = sim.W.Cars[0];
            Assert.True(sim.Until(() => car.Body.Speed > 10f, 15.0, In.Forward));
            sim.Player.Armour = 25f;
            float hp = sim.Player.Health;
            sim.Tick(In.Forward.WithPress(GameButton.EnterExit));
            Assert.True(sim.Until(() => !sim.Player.Has(PedFlags.InVehicle), 2.0, In.Forward));
            GameEvent exit = sim.Last(GameEventType.PlayerExitedCar);
            Assert.Equal(1f, exit.Value);
            Assert.Equal(exit.GameTime, sim.Player.LastDamageTime);
            Assert.Equal(car.Tag, sim.Player.LastAttackerTag);
            Assert.Equal(25f, sim.Player.Armour, 3);
            Sim.Near(hp - PlayerRules.BailDamage, sim.Player.Health, 1f, "bail damage");
        }
    }
}
