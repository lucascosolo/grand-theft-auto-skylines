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
    internal static class Stores
    {
        public static readonly Vec3 Pos = new Vec3(60f, 0f, 60f);
        public const float Yaw = 90f;

        /// <summary>A sim with one building of the wanted archetype whose front faces +x; returns the building through <paramref name="b"/>.</summary>
        public static Sim With(InteriorArchetype a, ServiceKind svc, string sub, out BuildingDescriptor b, Action<PlayerProfile> profile = null, bool owned = false)
        {
            BuildingDescriptor bd = Sim.FindBuilding(a, svc, sub, Pos, Yaw);
            bd.OwnedByPlayer = owned;
            b = bd;
            var sim = new Sim(city: c => c.AddBuilding(bd), profile: p => { if (owned) p.Properties.Add(bd.Id); if (profile != null) profile(p); });
            return sim;
        }

        public static Sim Convenience(out BuildingDescriptor b, Action<PlayerProfile> profile = null)
        {
            return With(InteriorArchetype.ConvenienceStore, ServiceKind.Commercial, "", out b, profile);
        }

        public static Sim House(out BuildingDescriptor b, bool owned)
        {
            return With(InteriorArchetype.House, ServiceKind.Residential, "ResidentialLow", out b, null, owned);
        }
    }

    public class InteriorEnterExitTests
    {
        [Fact]
        public void E_AtABuildingDoor_FadesOut_ThenPlacesThePlayerAtEntranceSpawn()
        {
            BuildingDescriptor b;
            Sim sim = Stores.Convenience(out b);
            Vec3 door = b.Doors[0];
            sim.Place(door + new Vec3(1.2f, 0f, 0f), 270f);
            sim.Run(0.1, In.Idle);
            Assert.Equal(InteriorPhase.Outside, sim.W.Interior.Phase);
            sim.Tick(In.Press(GameButton.Interact));
            sim.Run(0.2, In.Move(0f, 1f));
            Assert.Equal(InteriorPhase.FadingOut, sim.W.Interior.Phase);
            Sim.Near(0.5f, sim.W.FadeAlpha, 0.3f, "fade halfway after 0.2 s");
            Assert.True(Vec3.DistanceXZ(sim.Player.Position, door) < 2.5f, "input is ignored during the fade, the player is still outside");
            Assert.Equal(1, sim.W.Interior.Phase == InteriorPhase.FadingOut ? 1 : 0);
            Assert.True(sim.Until(() => sim.W.Interior.IsInside, 1.0, In.Idle), "never went inside");
            Assert.Equal(b.Id, sim.W.Interior.Building.Id);
            Assert.True(sim.Until(() => sim.W.Interior.Phase == InteriorPhase.Inside, 1.0, In.Idle));
            Sim.Near(0f, sim.W.FadeAlpha, 0.01f, "faded back in");
        }

        [Fact]
        public void Inside_TheLayoutIsPlacedUnderTheBuildingWithItsYaw()
        {
            BuildingDescriptor b;
            Sim sim = Stores.Convenience(out b);
            sim.EnterBuilding(b);
            InteriorState it = sim.W.Interior;
            Assert.NotNull(it.Layout);
            Assert.Equal(InteriorArchetype.ConvenienceStore, it.Layout.Archetype);
            Assert.Equal(InteriorGenerator.Generate(b).ContentHash(), it.Layout.ContentHash());
            Sim.Near(new Vec3(b.Position.X, -1500f, b.Position.Z), it.Origin, 1e-3f, "origin");
            Sim.Near(b.YawDeg, it.YawDeg, 1e-3f, "yaw");
            Vec3 spawn = InteriorLayout.ToWorld(it.Layout.EntranceSpawn, it.Origin, it.YawDeg);
            Assert.True(Vec3.DistanceXZ(spawn, sim.Player.Position) < 0.5f, "player at EntranceSpawn");
            Sim.Near(spawn.Y, sim.Player.Position.Y, 0.2f, "player on the interior floor");
            Assert.True(sim.Player.Position.Y < -1400f);
            Assert.True(Sim.AngleDiff(it.Layout.EntranceSpawnYawDeg + it.YawDeg, sim.Player.YawDeg) < 6f, "facing EntranceSpawnYawDeg + yaw");
            Assert.Equal((int)b.Id, sim.Player.InteriorId);
            Sim.Near(new Vec3(b.Doors[0].X, 0f, b.Doors[0].Z), new Vec3(it.Door.X, 0f, it.Door.Z), 0.01f, "door recorded");
        }

        [Fact]
        public void Inside_ARayDownHitsTheInteriorFloorAtMinus1500()
        {
            BuildingDescriptor b;
            Sim sim = Stores.Convenience(out b);
            int regionsOutside = sim.W.Collision.RegionCount;
            sim.EnterBuilding(b);
            Assert.Equal(regionsOutside + 1, sim.W.Collision.RegionCount);
            Vec3 p = sim.Player.Position;
            PhysicsHit hit;
            Assert.True(sim.W.Collision.Raycast(new Vec3(p.X, p.Y + 1.5f, p.Z), new Vec3(0f, -1f, 0f), 5f, QueryMask.StaticOnly, out hit));
            Sim.Near(-1500f, hit.Point.Y, 0.06f, "floor height");
            Assert.True((hit.Flags & SurfaceFlags.Interior) != 0, "interior surface flag");
        }

        [Fact]
        public void Entering_EmitsEnteredInterior_AndCountsTheVisit()
        {
            BuildingDescriptor b;
            Sim sim = Stores.Convenience(out b);
            sim.EnterBuilding(b);
            GameEvent e = sim.Last(GameEventType.EnteredInterior);
            Assert.Equal(sim.Player.Tag, e.ActorA);
            Assert.Equal((float)b.Id, e.Value);
            Assert.True(Vec3.DistanceXZ(e.Position, b.Doors[0]) < 2.5f);
            Assert.Equal(1, sim.Profile.GetStat(PlayerRules.StatInteriorsVisited));
            Assert.Equal(0, sim.Count(GameEventType.Trespass));
            Assert.True(sim.S.Hud.InInterior);
        }

        [Fact]
        public void Inside_OccupantsAreNpcPedsAtTheNpcSpots_WithModelsFromTheCity()
        {
            BuildingDescriptor b;
            Sim sim = Stores.Convenience(out b);
            sim.City.OccupantsByBuilding[b.Id] = new List<Occupant>
            {
                new Occupant { Citizen = 901, Model = 6, Resident = false },
                new Occupant { Citizen = 902, Model = 2, Resident = false },
            };
            sim.EnterBuilding(b);
            InteriorLayout l = sim.W.Interior.Layout;
            Assert.True(l.NpcSpots.Count > 0, "a store has a clerk");
            Assert.Equal(l.NpcSpots.Count, sim.W.Interior.Occupants.Count);
            Ped first = sim.W.FindPed(sim.W.Interior.Occupants[0]);
            Assert.Equal(PedKind.Npc, first.Kind);
            Assert.Equal(6, first.Model);
            Assert.True(Vec3.DistanceXZ(first.Position, sim.W.Interior.ToWorld(l.NpcSpots[0].Position)) < 1f);
            Assert.Equal((int)b.Id, first.InteriorId);
            if (l.NpcSpots.Count > 1) Assert.Equal(2, sim.W.FindPed(sim.W.Interior.Occupants[1]).Model);
        }

        [Fact]
        public void Inside_TheAmbientSnapshotIsIgnored()
        {
            BuildingDescriptor b;
            Sim sim = Stores.Convenience(out b);
            sim.AddCitizen(60, 6000, new Vec3(10f, 0f, 10f));
            sim.AddParkedCar(61, new Vec3(20f, 0.75f, 20f), 0f);
            sim.EnterBuilding(b);
            sim.Run(0.3, In.Idle);
            Assert.Equal(sim.W.Peds.Count, sim.W.Collision.DynamicCount);
            Assert.Empty(sim.W.Cars);
        }

        [Fact]
        public void ExitDoor_FadesAndReturnsThePlayerOutsideFacingAwayFromTheBuilding()
        {
            BuildingDescriptor b;
            Sim sim = Stores.Convenience(out b);
            int regionsOutside = sim.W.Collision.RegionCount;
            sim.EnterBuilding(b);
            sim.Run(0.2, In.Idle);
            sim.GoTo(InteractionKind.ExitDoor);
            Assert.Equal(InteractionTargetKind.InteriorPoint, sim.PS.Interaction.Kind);
            sim.Tick(In.Press(GameButton.Interact));
            Assert.True(sim.Until(() => sim.W.Interior.Phase == InteriorPhase.Outside, 3.0, In.Idle), "never left");
            sim.Run(0.3, In.Idle);
            Vec3 outward = (b.Doors[0] - b.Position).Flat.Normalized;
            Sim.Near(b.Doors[0] + outward, sim.Player.Position, 0.6f, "one metre outside the door");
            Assert.True(Sim.AngleDiff(Vec3.YawDegOf(outward), sim.Player.YawDeg) < 15f, "facing away from the building");
            Assert.Equal(regionsOutside, sim.W.Collision.RegionCount);
            Assert.Single(sim.W.Peds);
            Assert.Equal(-1, sim.Player.InteriorId);
            Assert.Null(sim.W.Interior.Layout);
            Assert.False(sim.S.Hud.InInterior);
            Assert.Equal((float)b.Id, sim.Last(GameEventType.ExitedInterior).Value);
        }

        [Fact]
        public void BuildingWithoutAnInterior_ShowsClosed_AndStaysOutside()
        {
            var sim = new Sim();
            BuildingDescriptor park = Sim.Building(70, ServiceKind.Park, "", new Vec3(60f, 0f, 60f), 90f, "Little Park");
            sim.City.AddBuilding(park);
            sim.Place(park.Doors[0] + new Vec3(1.2f, 0f, 0f), 270f);
            sim.Run(0.1, In.Idle);
            sim.Tick(In.Press(GameButton.Interact));
            sim.Run(0.3, In.Idle);
            Assert.Contains("Closed", sim.S.Hud.HelpText);
            sim.Run(1.5, In.Idle);
            Assert.False(sim.W.Interior.IsInside);
            Sim.Near(0f, sim.W.FadeAlpha, 0.01f, "fade returns to clear");
            Assert.True(Vec3.DistanceXZ(sim.Player.Position, park.Doors[0]) < 2.5f);
        }

        [Fact]
        public void EnteringAnUnownedHome_IsTrespass_OwnedIsNot()
        {
            BuildingDescriptor b;
            Sim stranger = Stores.House(out b, false);
            stranger.EnterBuilding(b);
            Assert.Equal(1, stranger.Count(GameEventType.Trespass));
            Assert.Equal((float)b.Id, stranger.Last(GameEventType.Trespass).Value);
            Assert.True(stranger.W.Interior.Trespassing);

            BuildingDescriptor mine;
            Sim owner = Stores.House(out mine, true);
            owner.EnterBuilding(mine);
            Assert.Equal(0, owner.Count(GameEventType.Trespass));
            Assert.False(owner.W.Interior.Trespassing);
        }

        [Fact]
        public void HomeOccupants_AreTheResidents()
        {
            BuildingDescriptor b;
            Sim sim = Stores.House(out b, false);
            sim.City.OccupantsByBuilding[b.Id] = new List<Occupant>
            {
                new Occupant { Citizen = 1, Model = 7, Resident = false },
                new Occupant { Citizen = 2, Model = 1, Resident = true },
            };
            sim.EnterBuilding(b);
            if (sim.W.Interior.Occupants.Count > 0)
                Assert.Equal(1, sim.W.FindPed(sim.W.Interior.Occupants[0]).Model);
        }

        [Fact]
        public void ApartmentLobby_ElevatorLeadsToTheApartmentVariantAndBack()
        {
            BuildingDescriptor b = Sim.FindBuilding(InteriorArchetype.ApartmentLobby, ServiceKind.Residential, "ResidentialHigh", Stores.Pos, Stores.Yaw);
            var sim = new Sim(city: c => c.AddBuilding(b), profile: p => p.Properties.Add(b.Id));
            sim.EnterBuilding(b);
            Assert.Equal(InteriorArchetype.ApartmentLobby, sim.W.Interior.Layout.Archetype);
            sim.GoTo(InteractionKind.ElevatorDoor);
            sim.Tick(In.Press(GameButton.Interact));
            Assert.True(sim.Until(() => sim.W.Interior.Layout != null && sim.W.Interior.Layout.Archetype == InteriorArchetype.Apartment && sim.W.Interior.Phase == InteriorPhase.Inside, 3.0, In.Idle));
            Assert.Equal(1 + b.Id % InteriorState.ApartmentVariants, sim.W.Interior.Layout.Variant);
            Vec3 want = sim.W.Interior.ToWorld(sim.W.Interior.Layout.ElevatorSpawn);
            Assert.True(Vec3.DistanceXZ(want, sim.Player.Position) < 0.6f, "arrives at ElevatorSpawn");
            sim.GoTo(InteractionKind.ElevatorDoor);
            sim.Tick(In.Press(GameButton.Interact));
            Assert.True(sim.Until(() => sim.W.Interior.Layout != null && sim.W.Interior.Layout.Archetype == InteriorArchetype.ApartmentLobby && sim.W.Interior.Phase == InteriorPhase.Inside, 3.0, In.Idle));
        }

        [Fact]
        public void End_WhileInside_RemovesTheInteriorRegion()
        {
            BuildingDescriptor b;
            Sim sim = Stores.Convenience(out b);
            int outside = sim.W.Collision.RegionCount;
            sim.EnterBuilding(b);
            sim.S.End();
            Assert.Equal(outside, sim.W.Collision.RegionCount);
        }
    }

    public class InteriorMenuTests
    {
        private static Sim InStore(out BuildingDescriptor b, Action<PlayerProfile> profile = null)
        {
            Sim sim = Stores.Convenience(out b, profile);
            sim.EnterBuilding(b);
            sim.GoTo(InteractionKind.ShopCounter);
            return sim;
        }

        [Fact]
        public void ShopCounter_RaisesAMenuRequestWithTheConvenienceCatalogue()
        {
            BuildingDescriptor b;
            Sim sim = InStore(out b);
            int rev = sim.W.Menu.Revision;
            sim.Tick(In.Press(GameButton.Interact));
            sim.Run(0.1, In.Idle);
            MenuRequest m = sim.W.Menu;
            Assert.True(m.IsOpen);
            Assert.Equal(MenuKind.Shop, m.Kind);
            Assert.Equal(ShopKind.Food, m.Shop);
            Assert.Equal(Shop.ItemsFor(ShopKind.Food).Count, m.Items.Count);
            Assert.True(m.Revision > rev);
            Assert.Equal(b.Id, m.Building);
            Assert.Same(m, sim.S.Hud.Menu);
        }

        [Fact]
        public void OpenMenu_IgnoresMovementInput_ButTheSimulationKeepsRunning()
        {
            BuildingDescriptor b;
            Sim sim = InStore(out b);
            sim.Tick(In.Press(GameButton.Interact));
            sim.Run(0.1, In.Idle);
            Vec3 p = sim.Player.Position;
            double g = sim.W.Clock.GameSeconds;
            sim.Run(1.0, In.Forward);
            Sim.Near(p, sim.Player.Position, 0.05f, "player frozen by the menu");
            Assert.True(sim.W.Clock.GameSeconds - g > 0.9);
        }

        [Fact]
        public void Buying_TakesCash_EmitsPurchase_HealsThePed_AndShowsTheCashDelta()
        {
            BuildingDescriptor b;
            Sim sim = InStore(out b, p => p.AddCash(1000));
            sim.Player.Health = 50f;
            sim.Tick(In.Press(GameButton.Interact));
            sim.Run(0.1, In.Idle);
            long cash = sim.Profile.Cash;
            int snack = Shop.ItemsFor(ShopKind.Food).FindIndex(i => i.Id == "snack");
            Assert.Equal(PurchaseResult.Ok, sim.S.ChooseMenuOption(snack));
            Assert.Equal(cash - 5, sim.Profile.Cash);
            GameEvent e = sim.Last(GameEventType.Purchase);
            Assert.Equal(5f, e.Value);
            Assert.Equal("snack", e.Text);
            Sim.Near(75f, sim.Player.Health, 1.5f, "ped health synced from the profile");
            Assert.True(sim.W.Menu.IsOpen, "shop menus stay open after a purchase");
            sim.Run(0.1, In.Idle);
            Assert.Equal(-5L, sim.S.Hud.CashDelta);
            Assert.Equal("-$5", Sim.Ascii(sim.S.Hud.CashDeltaText));
        }

        [Fact]
        public void Buying_WithoutCash_FailsAndEmitsNothing()
        {
            BuildingDescriptor b;
            Sim sim = InStore(out b, p => p.TakeCash(p.Cash));
            sim.Player.Health = 50f;
            sim.Tick(In.Press(GameButton.Interact));
            int meal = Shop.ItemsFor(ShopKind.Food).FindIndex(i => i.Id == "meal");
            Assert.Equal(PurchaseResult.InsufficientCash, sim.S.ChooseMenuOption(meal));
            Assert.Equal(0, sim.Count(GameEventType.Purchase));
        }

        [Fact]
        public void ChooseMenuOption_BadIndexOrNoMenu_IsUnknownItem_AndCloseMenuCloses()
        {
            BuildingDescriptor b;
            Sim sim = InStore(out b);
            Assert.Equal(PurchaseResult.UnknownItem, sim.S.ChooseMenuOption(0));
            sim.Tick(In.Press(GameButton.Interact));
            Assert.Equal(PurchaseResult.UnknownItem, sim.S.ChooseMenuOption(99));
            Assert.Equal(PurchaseResult.UnknownItem, sim.S.ChooseMenuOption(-1));
            sim.S.CloseMenu();
            Assert.False(sim.W.Menu.IsOpen);
            sim.S.CloseMenu();
        }

        [Fact]
        public void GunStoreCounter_OffersTheAmmunationCatalogue()
        {
            BuildingDescriptor b;
            Sim sim = Stores.With(InteriorArchetype.GunStore, ServiceKind.Commercial, "", out b);
            sim.EnterBuilding(b);
            sim.GoTo(InteractionKind.ShopCounter);
            sim.Tick(In.Press(GameButton.Interact));
            Assert.Equal(ShopKind.Ammunation, sim.W.Menu.Shop);
        }

        [Fact]
        public void HospitalDesk_AndPoliceDesk_RaiseServiceMenus()
        {
            BuildingDescriptor h;
            Sim sim = Stores.With(InteriorArchetype.Hospital, ServiceKind.Health, "", out h);
            sim.EnterBuilding(h);
            sim.GoTo(InteractionKind.HospitalDesk);
            sim.Tick(In.Press(GameButton.Interact));
            Assert.Equal(MenuKind.Shop, sim.W.Menu.Kind);
            Assert.Equal(ShopKind.Hospital, sim.W.Menu.Shop);

            BuildingDescriptor p;
            Sim cops = Stores.With(InteriorArchetype.PoliceStation, ServiceKind.Police, "", out p);
            cops.EnterBuilding(p);
            cops.GoTo(InteractionKind.PoliceDesk);
            cops.Tick(In.Press(GameButton.Interact));
            Assert.Equal(ShopKind.PoliceStation, cops.W.Menu.Shop);
        }

        [Fact]
        public void Bed_InAnOwnedHome_SavesAndSleeps()
        {
            BuildingDescriptor b;
            Sim sim = Stores.House(out b, true);
            sim.EnterBuilding(b);
            sim.GoTo(InteractionKind.Bed);
            sim.Tick(In.Press(GameButton.Interact));
            Assert.Equal(MenuKind.Bed, sim.W.Menu.Kind);
            Assert.Equal(PurchaseResult.Ok, sim.S.ChooseMenuOption(0));
            Assert.True(sim.S.SaveRequested);
            Assert.Equal((uint)b.Id, sim.Profile.LastSafehouse);
            Assert.True(sim.S.ConsumeSaveRequest());
            Assert.False(sim.S.ConsumeSaveRequest());

            sim.Player.Health = 30f;
            sim.Tick(In.Press(GameButton.Interact));
            Assert.Equal(PurchaseResult.Ok, sim.S.ChooseMenuOption(1));
            sim.Run(0.1, In.Idle);
            Assert.Equal(1, sim.CountEffects(CityEffectKind.AdvanceTime));
            Sim.Near(PlayerRules.SleepHours, sim.FirstEffect(CityEffectKind.AdvanceTime).Amount, 0.01f);
            Assert.Equal(PlayerRules.SleepHours, sim.Last(GameEventType.Slept).Value);
            Sim.Near(100f, sim.Player.Health, 0.5f);
        }

        [Fact]
        public void Bed_InAHomeYouDoNotOwn_DoesNothing()
        {
            BuildingDescriptor b;
            Sim sim = Stores.House(out b, false);
            sim.EnterBuilding(b);
            sim.GoTo(InteractionKind.Bed);
            sim.Tick(In.Press(GameButton.Interact));
            sim.Run(0.1, In.Idle);
            Assert.False(sim.W.Menu.IsOpen);
        }

        [Fact]
        public void Wardrobe_InAnOwnedHome_ChangesTheOutfit()
        {
            BuildingDescriptor b;
            Sim sim = Stores.House(out b, true);
            sim.EnterBuilding(b);
            sim.GoTo(InteractionKind.Wardrobe);
            sim.Tick(In.Press(GameButton.Interact));
            Assert.Equal(MenuKind.Wardrobe, sim.W.Menu.Kind);
            Assert.True(sim.W.Menu.Outfits.Count > 1);
            int model = sim.W.Menu.Outfits[1];
            Assert.Equal(PurchaseResult.Ok, sim.S.ChooseMenuOption(1));
            Assert.Equal(model, sim.Profile.OutfitId);
            Assert.Equal(model, sim.Player.Model);
            Assert.Equal((float)model, sim.Last(GameEventType.OutfitChanged).Value);
        }

        [Fact]
        public void RespraySpot_RepaintsTheLastCarNearTheGarage()
        {
            BuildingDescriptor b = Sim.FindBuilding(InteriorArchetype.Garage, ServiceKind.Industrial, "", Stores.Pos, Stores.Yaw);
            var sim = new Sim(city: c => c.AddBuilding(b), profile: p => p.AddCash(1000));
            Car car = sim.W.SpawnCar(0, Stores.Pos + new Vec3(10f, 0f, 0f), 0f, CarOwner.Abandoned, 0x123456);
            sim.PS.LastCarId = car.Id;
            sim.W.Wanted.Description = new SuspectDescription { OutfitModel = 0, CarModel = 0, CarColour = 0x123456, CarId = car.Id };
            sim.EnterBuilding(b);
            sim.GoTo(InteractionKind.RespraySpot);
            sim.Tick(In.Press(GameButton.Interact));
            Assert.Equal(ShopKind.Respray, sim.W.Menu.Shop);
            long cash = sim.Profile.Cash;
            Assert.Equal(PurchaseResult.Ok, sim.S.ChooseMenuOption(0));
            Assert.Equal(cash - 300, sim.Profile.Cash);
            Assert.NotEqual(0x123456, car.ColourRgb);
            Assert.Equal(car.ColourRgb, sim.W.Wanted.Description.CarColour);
            GameEvent e = sim.Last(GameEventType.Resprayed);
            Assert.Equal(car.Tag, e.ActorB);
            Assert.Equal((float)car.ColourRgb, e.Value);
        }
    }
}
