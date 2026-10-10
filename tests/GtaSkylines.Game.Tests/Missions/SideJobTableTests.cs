using GtaSkylines.Game.City;
using GtaSkylines.Game.Missions;
using Xunit;

namespace GtaSkylines.Game.Tests.Missions
{
    public class SideJobTableTests
    {
        [Theory]
        [InlineData(0, 1)]
        [InlineData(2, 1)]
        [InlineData(3, 2)]
        [InlineData(5, 2)]
        [InlineData(6, 3)]
        [InlineData(26, 9)]
        [InlineData(27, 10)]
        [InlineData(1000, 10)]
        [InlineData(-5, 1)]
        public void LevelIsOnePlusRoundsOverThreeCappedAtTen(long rounds, int level)
        {
            Assert.Equal(level, SideJobTable.LevelForRounds(rounds));
        }

        [Fact]
        public void PayGrowsWithLevelForEveryKindAndClamps()
        {
            for (int k = 0; k < SideJobTable.KindCount; k++)
            {
                var kind = (SideJobKind)k;
                for (int level = 2; level <= 10; level++)
                    Assert.True(SideJobTable.Pay(kind, level) > SideJobTable.Pay(kind, level - 1), kind + " level " + level);
                Assert.Equal(SideJobTable.Pay(kind, 1), SideJobTable.Pay(kind, 0));
                Assert.Equal(SideJobTable.Pay(kind, 10), SideJobTable.Pay(kind, 99));
            }
        }

        [Fact]
        public void LevelOneAndTenPayMatchTheTable()
        {
            Assert.Equal(20, SideJobTable.Pay(SideJobKind.Taxi, 1));
            Assert.Equal(75, SideJobTable.Pay(SideJobKind.Paramedic, 1));
            Assert.Equal(450, SideJobTable.Pay(SideJobKind.Firefighter, 10));
            Assert.Equal(1300, SideJobTable.Pay(SideJobKind.StreetRace, 10));
        }

        [Fact]
        public void IdsNamesAndStatsAreStable()
        {
            Assert.Equal("taxi", SideJobTable.Id(SideJobKind.Taxi));
            Assert.Equal("street_race", SideJobTable.Id(SideJobKind.StreetRace));
            Assert.Equal("Street Race", SideJobTable.Name(SideJobKind.StreetRace));
            Assert.Equal("job_paramedic_rounds", SideJobTable.RoundsStat(SideJobKind.Paramedic));
        }

        [Theory]
        [InlineData(SideJobKind.Taxi, VehicleKind.Taxi, true)]
        [InlineData(SideJobKind.Taxi, VehicleKind.Car, false)]
        [InlineData(SideJobKind.Paramedic, VehicleKind.Ambulance, true)]
        [InlineData(SideJobKind.Paramedic, VehicleKind.Taxi, false)]
        [InlineData(SideJobKind.Firefighter, VehicleKind.FireTruck, true)]
        [InlineData(SideJobKind.Firefighter, VehicleKind.Ambulance, false)]
        [InlineData(SideJobKind.Vigilante, VehicleKind.Police, true)]
        [InlineData(SideJobKind.Vigilante, VehicleKind.Car, false)]
        [InlineData(SideJobKind.Delivery, VehicleKind.Van, true)]
        [InlineData(SideJobKind.Delivery, VehicleKind.Truck, true)]
        [InlineData(SideJobKind.Delivery, VehicleKind.Car, false)]
        [InlineData(SideJobKind.StreetRace, VehicleKind.Car, true)]
        [InlineData(SideJobKind.StreetRace, VehicleKind.Bus, false)]
        public void VehicleAcceptance(SideJobKind kind, VehicleKind vehicle, bool accepted)
        {
            Assert.Equal(accepted, SideJobTable.AcceptsVehicle(kind, vehicle));
        }

        public enum VehicleKindName { Car, Van, Truck, Bus, Taxi, Police, Ambulance, FireTruck }

        [Fact]
        public void TaxiFareAddsDistanceAndTipByTimeLeft()
        {
            // Level 1: base 20 + 30 per km. 2 km -> 80. Tip is 50 % at half the time left or more.
            Assert.Equal(80 + 40, SideJobTable.TaxiFare(1, 2000f, 0.5f));
            Assert.Equal(80 + 40, SideJobTable.TaxiFare(1, 2000f, 1f));
            // Quarter of the time left: tip fraction = 0.5 * min(1, 0.5) = 0.25 -> 20.
            Assert.Equal(80 + 20, SideJobTable.TaxiFare(1, 2000f, 0.25f));
            // No time left: no tip.
            Assert.Equal(80, SideJobTable.TaxiFare(1, 2000f, 0f));
        }

        [Fact]
        public void TaxiFareRoundsDistanceDownToWholeDollars()
        {
            // 1.5 km at 30 $/km = 45 (+20 base), no tip.
            Assert.Equal(65, SideJobTable.TaxiFare(1, 1500f, 0f));
            // 1234 m -> 37.02 -> 37.
            Assert.Equal(57, SideJobTable.TaxiFare(1, 1234f, 0f));
        }

        [Fact]
        public void TaxiFareUsesTheLevelRates()
        {
            Assert.Equal(60 + 50, SideJobTable.TaxiFare(10, 1000f, 0f));
        }

        [Fact]
        public void DeliveryPayScalesWithHealthAndDropsToZeroBelowTwenty()
        {
            Assert.Equal(100, SideJobTable.DeliveryPay(1, 100f));
            Assert.Equal(50, SideJobTable.DeliveryPay(1, 50f));
            Assert.Equal(33, SideJobTable.DeliveryPay(1, 33.9f));
            Assert.Equal(20, SideJobTable.DeliveryPay(1, 20f));
            Assert.Equal(0, SideJobTable.DeliveryPay(1, 19.9f));
            Assert.Equal(280, SideJobTable.DeliveryPay(10, 100f));
        }

        [Fact]
        public void RacePrizeFollowsPositionShares()
        {
            Assert.Equal(400, SideJobTable.RacePrize(1, 1));
            Assert.Equal(160, SideJobTable.RacePrize(1, 2));
            Assert.Equal(60, SideJobTable.RacePrize(1, 3));
            Assert.Equal(0, SideJobTable.RacePrize(1, 4));
            Assert.Equal(0, SideJobTable.RacePrize(1, 0));
            Assert.Equal(0, SideJobTable.RacePrize(1, 5));
            Assert.Equal(1300, SideJobTable.RacePrize(10, 1));
        }

        [Fact]
        public void TimeLimitIsRoundedUpToFiveSecondsAndShrinksWithLevel()
        {
            // 20 s + 1000 m / 10 m/s * 1.6 = 180 s.
            Assert.Equal(180f, SideJobTable.TimeLimit(1, 1000f));
            // 20 + 100 * 1.6 / ... -> 20 + 10 * 1.6 = 36 -> 40.
            Assert.Equal(40f, SideJobTable.TimeLimit(1, 100f));
            Assert.True(SideJobTable.TimeLimit(10, 1000f) < SideJobTable.TimeLimit(1, 1000f));
            Assert.Equal(0f, SideJobTable.TimeLimit(3, 777f) % 5f);
        }

        [Fact]
        public void RaceAndLevelTablesCoverTenLevels()
        {
            Assert.Equal(10, SideJobTable.RaceCheckpoints.Length);
            Assert.Equal(6, SideJobTable.RaceCheckpoints[0]);
            Assert.Equal(12, SideJobTable.RaceCheckpoints[9]);
            Assert.Equal(1.1f, SideJobTable.RaceRivalSpeedFactor[0]);
        }
    }
}
