using System;
using GtaSkylines.Game.City;

namespace GtaSkylines.Game.Missions
{
    /// <summary>The six repeatable vehicle side jobs.</summary>
    public enum SideJobKind
    {
        /// <summary>Taxi fares (in a taxi).</summary>
        Taxi,
        /// <summary>Patients to hospital (in an ambulance).</summary>
        Paramedic,
        /// <summary>Fires (in a fire engine).</summary>
        Firefighter,
        /// <summary>Fleeing criminals (in a police car).</summary>
        Vigilante,
        /// <summary>Industry to shops (in a van or truck).</summary>
        Delivery,
        /// <summary>Checkpoint races against three rivals (in any car, from the race marker).</summary>
        StreetRace,
    }

    /// <summary>
    /// Data tables of the side jobs: ids, names, vehicles, levels and pay. Level is 1-10. Progress is stored in the
    /// profile: completed rounds in the stat <see cref="RoundsStat"/> and the level in <c>SideJobLevels[Id]</c>, where
    /// level = min(10, 1 + rounds / <see cref="RoundsPerLevel"/>) (level 0 = never played, treated as 1).
    /// All tables are indexed [kind, level − 1].
    /// </summary>
    public static class SideJobTable
    {
        /// <summary>Highest level.</summary>
        public const int MaxLevel = 10;
        /// <summary>Completed rounds per level.</summary>
        public const int RoundsPerLevel = 3;
        /// <summary>Kinds, in <see cref="SideJobKind"/> order.</summary>
        public const int KindCount = 6;

        private static readonly string[] Ids = { "taxi", "paramedic", "firefighter", "vigilante", "delivery", "street_race" };
        private static readonly string[] Names = { "Taxi", "Paramedic", "Firefighter", "Vigilante", "Delivery", "Street Race" };
        private static readonly string[] Stats = { "job_taxi_rounds", "job_paramedic_rounds", "job_firefighter_rounds", "job_vigilante_rounds", "job_delivery_rounds", "job_street_race_rounds" };

        /// <summary>
        /// Base pay per round in dollars by [kind, level − 1]. Taxi: per fare before distance and tip; Paramedic: per
        /// patient delivered; Firefighter: per fire out; Vigilante: per criminal stopped; Delivery: per drop at full
        /// health; StreetRace: the winner's prize.
        /// </summary>
        public static readonly long[,] BasePay =
        {
            { 20, 24, 28, 32, 36, 40, 45, 50, 55, 60 },
            { 75, 90, 105, 120, 135, 150, 165, 180, 195, 210 },
            { 180, 210, 240, 270, 300, 330, 360, 390, 420, 450 },
            { 150, 190, 230, 270, 310, 350, 400, 450, 500, 550 },
            { 100, 120, 140, 160, 180, 200, 220, 240, 260, 280 },
            { 400, 500, 600, 700, 800, 900, 1000, 1100, 1200, 1300 },
        };

        /// <summary>Taxi: dollars per kilometre of straight-line distance from pick-up to drop-off, by level.</summary>
        public static readonly long[] TaxiPerKm = { 30, 32, 34, 36, 38, 40, 42, 44, 46, 50 };

        /// <summary>Taxi: maximum tip as a fraction of the fare, earned in full at ≥ 50 % of the time left, linearly less to 0 at no time left.</summary>
        public const float TaxiMaxTip = 0.5f;

        /// <summary>Paramedic: patients per round by level (each delivered before the next is shown).</summary>
        public static readonly int[] ParamedicPatients = { 1, 1, 2, 2, 2, 3, 3, 3, 4, 4 };

        /// <summary>Firefighter: fires per round by level.</summary>
        public static readonly int[] FirefighterFires = { 1, 1, 1, 2, 2, 2, 3, 3, 3, 3 };

        /// <summary>Vigilante: level from which a second criminal car flees with the first.</summary>
        public const int VigilanteSecondCarLevel = 4;
        /// <summary>Vigilante: level from which the criminal car carries an armed passenger (Pistol).</summary>
        public const int VigilanteArmedLevel = 7;
        /// <summary>Vigilante: distance of the criminal car's start from the player, metres.</summary>
        public const float VigilanteStartDistance = 300f;
        /// <summary>Vigilante: criminal flee speed factor by level.</summary>
        public static readonly float[] VigilanteSpeedFactor = { 1.0f, 1.05f, 1.1f, 1.15f, 1.2f, 1.25f, 1.3f, 1.3f, 1.35f, 1.4f };

        /// <summary>Delivery: pay multiplier = car health at drop / 100 (no pay below <see cref="DeliveryMinHealth"/>).</summary>
        public const float DeliveryMinHealth = 20f;

        /// <summary>
        /// Time-limit factor by level for timed rounds (Taxi, Paramedic, Delivery): limit = 20 s + route length (or
        /// 1.4 × straight-line distance with no route) / 10 m/s × factor, rounded up to 5 s.
        /// </summary>
        public static readonly float[] TimeFactor = { 1.6f, 1.55f, 1.5f, 1.45f, 1.4f, 1.35f, 1.3f, 1.25f, 1.2f, 1.15f };

        /// <summary>Street race: checkpoints by level (6 at level 1 to 12 at level 10).</summary>
        public static readonly int[] RaceCheckpoints = { 6, 6, 7, 8, 8, 9, 10, 10, 11, 12 };

        /// <summary>Street race: prize fraction of BasePay by finishing position 1-4.</summary>
        public static readonly float[] RacePrizeShare = { 1f, 0.4f, 0.15f, 0f };

        /// <summary>Street race: rival speed factor by level (1.1 × the limit at level 1, the contract's figure).</summary>
        public static readonly float[] RaceRivalSpeedFactor = { 1.1f, 1.1f, 1.12f, 1.14f, 1.16f, 1.18f, 1.2f, 1.22f, 1.24f, 1.26f };

        /// <summary>Taxi fares: drop-off building distance band, metres.</summary>
        public const float TaxiMinDistance = 300f, TaxiMaxDistance = 1500f;

        /// <summary>Profile id ("taxi", "paramedic", "firefighter", "vigilante", "delivery", "street_race").</summary>
        public static string Id(SideJobKind kind) { return Ids[(int)kind]; }

        /// <summary>Display name ("Taxi", "Street Race").</summary>
        public static string Name(SideJobKind kind) { return Names[(int)kind]; }

        /// <summary>Stat counting completed rounds ("job_taxi_rounds").</summary>
        public static string RoundsStat(SideJobKind kind) { return Stats[(int)kind]; }

        /// <summary>
        /// True when a car of this model kind can run the job: Taxi → Taxi; Paramedic → Ambulance; Firefighter →
        /// FireTruck; Vigilante → Police; Delivery → Van or Truck; StreetRace → Car, Van or Taxi.
        /// </summary>
        public static bool AcceptsVehicle(SideJobKind kind, VehicleKind vehicle)
        {
            switch (kind)
            {
                case SideJobKind.Taxi: return vehicle == VehicleKind.Taxi;
                case SideJobKind.Paramedic: return vehicle == VehicleKind.Ambulance;
                case SideJobKind.Firefighter: return vehicle == VehicleKind.FireTruck;
                case SideJobKind.Vigilante: return vehicle == VehicleKind.Police;
                case SideJobKind.Delivery: return vehicle == VehicleKind.Van || vehicle == VehicleKind.Truck;
                default: return vehicle == VehicleKind.Car || vehicle == VehicleKind.Van || vehicle == VehicleKind.Taxi;
            }
        }

        /// <summary>Level for a number of completed rounds: min(10, 1 + rounds / 3); negative counts as 0.</summary>
        public static int LevelForRounds(long rounds)
        {
            if (rounds < 0) rounds = 0;
            long level = 1 + rounds / RoundsPerLevel;
            return level > MaxLevel ? MaxLevel : (int)level;
        }

        /// <summary>Base pay for a kind and level (level clamped to 1-10).</summary>
        public static long Pay(SideJobKind kind, int level)
        {
            return BasePay[(int)kind, Clamp(level) - 1];
        }

        /// <summary>Level clamped to 1-10.</summary>
        public static int Clamp(int level)
        {
            return level < 1 ? 1 : level > MaxLevel ? MaxLevel : level;
        }

        /// <summary>
        /// Taxi fare in dollars: BasePay + TaxiPerKm × km (straight line, rounded down to whole dollars), plus a tip of up
        /// to <see cref="TaxiMaxTip"/> × that fare by <paramref name="timeLeftFraction"/> (0-1 of the limit left; tip
        /// fraction = TaxiMaxTip × min(1, 2 × timeLeftFraction)), rounded down.
        /// </summary>
        public static long TaxiFare(int level, float distanceM, float timeLeftFraction)
        {
            throw new NotImplementedException();
        }

        /// <summary>Delivery pay: BasePay × health / 100 rounded down; 0 below <see cref="DeliveryMinHealth"/>.</summary>
        public static long DeliveryPay(int level, float carHealth)
        {
            throw new NotImplementedException();
        }

        /// <summary>Race prize for a finishing position 1-4 (0 outside): BasePay × RacePrizeShare, rounded down.</summary>
        public static long RacePrize(int level, int position)
        {
            throw new NotImplementedException();
        }

        /// <summary>Time limit for a timed round, seconds (see <see cref="TimeFactor"/>).</summary>
        public static float TimeLimit(int level, float routeLengthM)
        {
            throw new NotImplementedException();
        }
    }
}
