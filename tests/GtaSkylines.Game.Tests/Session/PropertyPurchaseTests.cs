using System;
using System.Globalization;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Interiors;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Session;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.SessionTests
{
    /// <summary>DESIGN F-ECO-2: buying a residential building from inside it through a "For sale" interaction.</summary>
    public class PropertyPurchaseTests
    {
        private const string SaleText = "buy this home";

        private static Sim Home(InteriorArchetype a, string sub, out BuildingDescriptor b, Action<PlayerProfile> profile = null, bool owned = false)
        {
            return Stores.With(a, ServiceKind.Residential, sub, out b, profile, owned);
        }

        private static long PriceOf(BuildingDescriptor b) { return Property.PriceFor(b.Level, b.Width * b.Length); }

        private static int SaleIndex(Sim sim)
        {
            var list = sim.W.Interior.CustomInteractions;
            for (int i = 0; i < list.Count; i++)
                if (list[i].Prompt != null && list[i].Prompt.Contains(SaleText)) return i;
            return -1;
        }

        /// <summary>Stands on the For sale point, ready to press Interact.</summary>
        private static void GoToSale(Sim sim)
        {
            int i = SaleIndex(sim);
            Assert.True(i >= 0, "no 'For sale' custom interaction in the interior");
            sim.Place(sim.W.Interior.ToWorld(sim.W.Interior.CustomInteractions[i].LocalPosition), sim.PS.ViewYawDeg);
            sim.Run(0.2, In.Idle);
        }

        private static void OpenMenu(Sim sim)
        {
            GoToSale(sim);
            sim.Tick(In.Press(GameButton.Interact));
        }

        [Theory]
        [InlineData(InteriorArchetype.House, "ResidentialLow")]
        [InlineData(InteriorArchetype.ApartmentLobby, "ResidentialHigh")]
        public void UnownedHome_HasAForSalePointNearTheEntrance(InteriorArchetype a, string sub)
        {
            BuildingDescriptor b;
            Sim sim = Home(a, sub, out b);
            sim.EnterBuilding(b);
            int i = SaleIndex(sim);
            Assert.True(i >= 0, "no 'For sale' custom interaction in the interior");
            Vec3 at = sim.W.Interior.CustomInteractions[i].LocalPosition;
            Vec3 spawn = sim.W.Interior.Layout.EntranceSpawn;
            Assert.True(Vec3.DistanceXZ(at, spawn) <= 4f, "the point is near the entrance");
        }

        [Fact]
        public void UnownedApartment_HasAForSalePoint()
        {
            BuildingDescriptor b;
            Sim sim = Home(InteriorArchetype.ApartmentLobby, "ResidentialHigh", out b);
            sim.EnterBuilding(b);
            sim.GoTo(InteractionKind.ElevatorDoor);
            sim.Tick(In.Press(GameButton.Interact));
            Assert.True(sim.Until(() => sim.W.Interior.Layout != null && sim.W.Interior.Layout.Archetype == InteriorArchetype.Apartment
                && sim.W.Interior.Phase == InteriorPhase.Inside, 3.0, In.Idle));
            Assert.True(SaleIndex(sim) >= 0, "no 'For sale' custom interaction in the apartment");
        }

        [Fact]
        public void StandingAtThePoint_ShowsThePromptWithKeyAndPrice()
        {
            BuildingDescriptor b;
            Sim sim = Home(InteriorArchetype.House, "ResidentialLow", out b);
            sim.EnterBuilding(b);
            GoToSale(sim);
            Assert.Equal(InteractionTargetKind.CustomInteraction, sim.PS.Interaction.Kind);
            string help = sim.S.Hud.HelpText;
            Assert.NotNull(help);
            Assert.StartsWith("Press ~E~ to buy this home ($", help);
            Assert.Contains(PriceOf(b).ToString(CultureInfo.InvariantCulture), help.Replace(",", ""));
        }

        [Fact]
        public void ThePrompt_UsesTheRebindableKey()
        {
            BuildingDescriptor b = Sim.FindBuilding(InteriorArchetype.House, ServiceKind.Residential, "ResidentialLow", Stores.Pos, Stores.Yaw);
            var sim = new Sim(city: c => c.AddBuilding(b), settings: s => s.Bindings.Set(GameButton.Interact, 0, "G"));
            sim.EnterBuilding(b);
            GoToSale(sim);
            Assert.StartsWith("Press ~G~ to buy this home", sim.S.Hud.HelpText);
        }

        [Fact]
        public void Interact_AtThePoint_OpensAPropertyMenuForThatBuildingAndPrice()
        {
            BuildingDescriptor b;
            Sim sim = Home(InteriorArchetype.House, "ResidentialLow", out b);
            sim.EnterBuilding(b);
            OpenMenu(sim);
            MenuRequest m = sim.W.Menu;
            Assert.Equal(MenuKind.Property, m.Kind);
            Assert.Equal(b.Id, m.Building);
            Assert.Equal(PriceOf(b), m.Price);
        }

        [Fact]
        public void Buying_TakesTheMoney_RecordsOwnership_SetsSafehouse_EmitsPurchase_AndRemovesThePoint()
        {
            BuildingDescriptor b;
            Sim sim = Home(InteriorArchetype.House, "ResidentialLow", out b, p => { p.AddCash(1000000); p.Properties.Add(999); p.LastSafehouse = 999; });
            sim.EnterBuilding(b);
            long price = PriceOf(b);
            long cash = sim.Profile.Cash;
            OpenMenu(sim);
            Assert.Equal(PurchaseResult.Ok, sim.S.ChooseMenuOption(0));
            Assert.Equal(cash - price, sim.Profile.Cash);
            Assert.True(sim.Profile.OwnsProperty(b.Id));
            Assert.Equal((uint)b.Id, sim.Profile.LastSafehouse);
            Assert.Equal((float)price, sim.Last(GameEventType.Purchase).Value);
            Assert.False(sim.W.Menu.IsOpen);
            Assert.Equal(-1, SaleIndex(sim));
            sim.Run(0.2, In.Idle);
            Assert.NotEqual(InteractionTargetKind.CustomInteraction, sim.PS.Interaction.Kind);
        }

        [Fact]
        public void Buying_WithTooLittleCash_ChangesNothing_AndReportsInsufficientFunds()
        {
            BuildingDescriptor b;
            Sim sim = Home(InteriorArchetype.House, "ResidentialLow", out b, p => p.AddCash(100));
            sim.EnterBuilding(b);
            long cash = sim.Profile.Cash;
            int purchases = sim.Count(GameEventType.Purchase);
            OpenMenu(sim);
            Assert.Equal(MenuKind.Property, sim.W.Menu.Kind);
            Assert.Equal(PurchaseResult.InsufficientCash, sim.S.ChooseMenuOption(0));
            Assert.Equal(PurchaseResult.InsufficientCash, sim.W.Menu.LastResult);
            Assert.Equal(cash, sim.Profile.Cash);
            Assert.False(sim.Profile.OwnsProperty(b.Id));
            Assert.Equal(0u, sim.Profile.LastSafehouse);
            Assert.Equal(purchases, sim.Count(GameEventType.Purchase));
            Assert.True(SaleIndex(sim) >= 0, "the point stays");
        }

        [Fact]
        public void OwnedHome_HasNoForSalePoint_IsNotTrespass_AndTheBedOffersSaveAndSleep()
        {
            BuildingDescriptor b;
            Sim sim = Home(InteriorArchetype.House, "ResidentialLow", out b, null, true);
            sim.EnterBuilding(b);
            Assert.Equal(-1, SaleIndex(sim));
            Assert.Equal(0, sim.Count(GameEventType.Trespass));
            sim.GoTo(InteractionKind.Bed);
            sim.Tick(In.Press(GameButton.Interact));
            Assert.Equal(MenuKind.Bed, sim.W.Menu.Kind);
        }

        [Fact]
        public void AfterBuying_TheHomeIsOwnedOnReentry_NoPoint_NoTrespass()
        {
            BuildingDescriptor b;
            Sim sim = Home(InteriorArchetype.House, "ResidentialLow", out b, p => p.AddCash(1000000));
            sim.EnterBuilding(b);
            OpenMenu(sim);
            Assert.Equal(PurchaseResult.Ok, sim.S.ChooseMenuOption(0));
            sim.GoTo(InteractionKind.ExitDoor);
            sim.Tick(In.Press(GameButton.Interact));
            Assert.True(sim.Until(() => sim.W.Interior.Phase == InteriorPhase.Outside, 3.0, In.Idle));
            int trespass = sim.Count(GameEventType.Trespass);
            sim.EnterBuilding(b);
            Assert.Equal(-1, SaleIndex(sim));
            Assert.False(sim.W.Interior.Trespassing);
            Assert.Equal(trespass, sim.Count(GameEventType.Trespass));
        }
    }
}
