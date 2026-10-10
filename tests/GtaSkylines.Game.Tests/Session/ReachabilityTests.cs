using GtaSkylines.Game.City;
using GtaSkylines.Game.Crime;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Interiors;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Missions;
using GtaSkylines.Game.Police;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Session;
using GtaSkylines.Game.Tests.IntegrationTests;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.SessionTests
{
    /// <summary>DESIGN F-HEAT-5, F-ECO-3, F-JOB-2: behaviours that must be reachable through play.</summary>
    public class ReachabilityTests
    {
        private static void Wanted(Sim sim)
        {
            sim.W.Wanted.Stars = 2;
            sim.W.Wanted.Description = Recognition.Describe(sim.W);
            Assert.True(Recognition.Matches(sim.W, sim.W.Wanted.Description));
        }

        [Fact]
        public void Wardrobe_OutfitChange_MakesTheSuspectDescriptionNoLongerMatch()
        {
            BuildingDescriptor b;
            Sim sim = Stores.House(out b, true);
            sim.EnterBuilding(b);
            sim.GoTo(InteractionKind.Wardrobe);
            sim.Tick(In.Press(GameButton.Interact));
            int model = sim.W.Menu.Outfits[1];
            Assert.NotEqual(sim.Player.Model, model);
            Wanted(sim);
            Assert.Equal(PurchaseResult.Ok, sim.S.ChooseMenuOption(1));
            Assert.Equal(model, sim.Player.Model);
            Assert.False(Recognition.Matches(sim.W, sim.W.Wanted.Description), "the old outfit still identifies the player");
        }

        [Fact]
        public void ClothesShop_BuyingAnOutfit_MakesTheSuspectDescriptionNoLongerMatch()
        {
            BuildingDescriptor b;
            Sim sim = Stores.With(InteriorArchetype.ClothesShop, ServiceKind.Commercial, "", out b, p => p.AddCash(2000));
            sim.EnterBuilding(b);
            sim.GoTo(InteractionKind.ShopCounter);
            sim.Tick(In.Press(GameButton.Interact));
            Assert.True(sim.W.Menu.IsOpen);
            int before = sim.Player.Model;
            int index = -1;
            for (int i = 0; i < sim.W.Menu.Items.Count; i++)
                if (sim.W.Menu.Items[i].Effect == ItemEffect.Outfit && sim.W.Menu.Items[i].Amount != before) { index = i; break; }
            Assert.True(index >= 0);
            Wanted(sim);
            Assert.Equal(PurchaseResult.Ok, sim.S.ChooseMenuOption(index));
            Assert.NotEqual(before, sim.Player.Model);
            Assert.False(Recognition.Matches(sim.W, sim.W.Wanted.Description), "the old outfit still identifies the player");
        }

        [Fact]
        public void Respray_MakesTheSuspectDescriptionNoLongerMatchTheCar()
        {
            BuildingDescriptor b = Sim.FindBuilding(InteriorArchetype.Garage, ServiceKind.Industrial, "", Stores.Pos, Stores.Yaw);
            var sim = new Sim(city: c => c.AddBuilding(b), profile: p => p.AddCash(1000));
            Car car = sim.W.SpawnCar(0, Stores.Pos + new Vec3(10f, 0f, 0f), 0f, CarOwner.Abandoned, 0x123456);
            sim.PS.LastCarId = car.Id;
            sim.W.Wanted.Stars = 2;
            sim.W.Wanted.Description = new SuspectDescription { OutfitModel = 0, CarModel = 0, CarColour = 0x123456, CarId = car.Id };
            sim.EnterBuilding(b);
            sim.GoTo(InteractionKind.RespraySpot);
            sim.Tick(In.Press(GameButton.Interact));
            Assert.Equal(PurchaseResult.Ok, sim.S.ChooseMenuOption(0));
            Assert.NotEqual(0x123456, car.ColourRgb);
            Assert.Equal(0x123456, sim.W.Wanted.Description.CarColour);
            sim.W.Wanted.Description.CarModel = car.Model.Id;
            sim.Player.VehicleId = car.Id;
            Assert.False(GtaSkylines.Game.Police.Recognition.Matches(sim.W, sim.W.Wanted.Description), "cops must re-identify a resprayed car (F-HEAT-5)");
        }

        [Fact]
        public void Stats_AreIncrementedByWalkingStealingDrivingAndCrime()
        {
            var r = new Rig(2024u);
            r.Run(0.3, In.Idle);
            r.Run(3.0, In.Forward);
            Assert.True(r.Profile.GetStat(PlayerRules.StatDistanceWalked) > 0, "distance walked");
            r.Place(r.CarSpot + new Vec3(-3f, -0.75f, 0.44f), 90f);
            r.Run(0.3, In.Idle);
            r.Tick(In.Press(GameButton.EnterExit));
            Assert.True(r.Until(() => r.Player.Has(PedFlags.InVehicle), 4.0, In.Idle), "never got in");
            Assert.Equal(1, r.Profile.GetStat(StatNames.VehiclesStolen));
            r.Run(4.0, In.Forward);
            Assert.True(r.Profile.GetStat(PlayerRules.StatDistanceDriven) > 0, "distance driven");
            Assert.True(r.Profile.GetStat(StatNames.DistanceM) >= r.Profile.GetStat(PlayerRules.StatDistanceDriven));
            r.Parts.Crime.ReportKnownCrime(r.World, CrimeType.ShotAtPed, r.Player.Position, 0);
            r.Run(0.2, In.Idle);
            Assert.True(r.Profile.GetStat(StatNames.Crimes) >= 1, "crimes");
        }

        [Fact]
        public void Stats_MoneyEarnedAndSpentAreCounted()
        {
            var sim = new Sim();
            sim.W.Wallet.Earn(300, "test");
            Assert.Equal(300, sim.Profile.GetStat(StatNames.MoneyEarned));
            Assert.True(sim.W.Wallet.TrySpend(100, "test"));
            sim.W.Wallet.Fine(50, "test");
            Assert.Equal(150, sim.Profile.GetStat(StatNames.MoneySpent));
        }

        [Fact]
        public void FreshProfile_HasAMissionMarkerAtStart_AndItIsOnTheMinimap()
        {
            var r = new Rig(2024u);
            r.Run(0.3, In.Idle);
            Blip start = r.World.Mission.Blips.Find(x => x.Kind == BlipKind.MissionStart);
            Assert.Equal(BlipKind.MissionStart, start.Kind);
            Assert.NotEmpty(r.World.Mission.Markers);
            Assert.Contains(r.S.Hud.Blips, x => x.Kind == BlipKind.MissionStart);
        }
    }
}
