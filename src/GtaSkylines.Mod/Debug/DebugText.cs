using System;
using System.Globalization;
using GtaSkylines.Game.Police;
using GtaSkylines.Game.Vehicles;
using GtaSkylines.Game.World;

namespace GtaSkylines.Mod.Debug
{
    /// <summary>
    /// The debug overlay's text (F-QA-3): one line per actor and police unit, the wanted blackboard and the performance panel.
    /// Pure; the overlay calls it only on its refresh tick and caches the strings.
    /// </summary>
    public static class DebugText
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly string[] Roles =
        {
            "Patrol", "Respond", "Pursue", "Intercept", "Search", "Roadblock", "BoxIn", "PIT", "OnFoot", "Helicopter", "Leave",
        };
        private static readonly string[] Kinds = { "Player", "Civ", "Cop", "Npc" };
        private static readonly string[] Owners = { "Player", "Police", "Civ", "Mission", "Abandoned" };

        /// <summary>Short name of a police role ("PIT" for <see cref="UnitRole.Pit"/>, "?" when undefined).</summary>
        public static string RoleName(UnitRole r) { return Name(Roles, (int)r); }

        /// <summary>"Cop #12 Chase/Run hp 80 ar 20 in #3 DEAD".</summary>
        public static string Ped(Ped p)
        {
            string s = Name(Kinds, (int)p.Kind) + " #" + p.Id.ToString(Inv) + " " + (p.Brain == null ? "-" : p.Brain.Name) + "/" + p.Anim
                + " hp " + p.Health.ToString("0", Inv);
            if (p.Armour > 0) s += " ar " + p.Armour.ToString("0", Inv);
            if (p.VehicleId != 0) s += " in #" + p.VehicleId.ToString(Inv);
            if (p.IsDead) s += " DEAD";
            return s;
        }

        /// <summary>"Police car #3 drv #12 54 km/h dmg 20% smoking".</summary>
        public static string Car(Car c)
        {
            string s = Name(Owners, (int)c.Owner) + " car #" + c.Id.ToString(Inv) + " drv " + (c.DriverId > 0 ? "#" + c.DriverId.ToString(Inv) : "-")
                + " " + (Math.Abs(c.Body.Speed) * 3.6f).ToString("0", Inv) + " km/h dmg " + (100f - c.Body.Health).ToString("0", Inv) + "%";
            switch (c.Body.DamageState)
            {
                case VehicleDamageState.Smoking: return s + " smoking";
                case VehicleDamageState.OnFire: return s + " fire";
                case VehicleDamageState.Exploded: return s + " exploded";
                default: return s;
            }
        }

        /// <summary>"U4 Intercept lost 3s -> (120, -40)".</summary>
        public static string Unit(PoliceUnit u)
        {
            return "U" + u.Id.ToString(Inv) + " " + RoleName(u.Role) + " "
                + (u.SeesPlayer ? "sees" : "lost " + u.SecondsSinceSeen.ToString("0", Inv) + "s")
                + " -> (" + u.Target.X.ToString("0", Inv) + ", " + u.Target.Z.ToString("0", Inv) + ")";
        }

        /// <summary>The wanted blackboard: stars and state, last known position, search zone, heat (four lines).</summary>
        public static string Wanted(WantedStatus w)
        {
            string state = w.Stars == 0 ? "clear" : w.Evading ? "evading" : "seen";
            string lkp = w.HasLastKnownPosition
                ? "LKP (" + w.LastKnownPosition.X.ToString("0", Inv) + ", " + w.LastKnownPosition.Z.ToString("0", Inv) + ") "
                    + w.SecondsSinceSeen.ToString("0.0", Inv) + "s ago"
                : "LKP none";
            string zone = w.Stars > 0 && w.SearchRadius > 0
                ? "Search r " + w.SearchRadius.ToString("0", Inv) + " m at (" + w.SearchCentre.X.ToString("0", Inv) + ", "
                    + w.SearchCentre.Z.ToString("0", Inv) + ")"
                : "Search none";
            return "Wanted " + w.Stars.ToString(Inv) + "/5 " + state + "\n" + lkp + "\n" + zone + "\nHeat " + w.PlayerHeat.ToString("0", Inv);
        }

        /// <summary>"sim 3.20 ms / 4.00", with " OVER" past the budget; no budget part when <paramref name="budgetMs"/> ≤ 0.</summary>
        public static string Budget(string label, double avgMs, double budgetMs)
        {
            string s = label + " " + avgMs.ToString("0.00", Inv) + " ms";
            if (budgetMs <= 0) return s;
            s += " / " + budgetMs.ToString("0.00", Inv);
            return avgMs > budgetMs ? s + " OVER" : s;
        }

        /// <summary>"FPS 58.8  frame 17.0 ms (max 25.1) / 33.3", with " OVER" past the frame budget.</summary>
        public static string Fps(PerfWindow w)
        {
            string s = "FPS " + w.Fps.ToString("0.0", Inv) + "  frame " + w.FrameMsAvg.ToString("0.0", Inv) + " ms (max "
                + w.FrameMsMax.ToString("0.0", Inv) + ") / " + PerfBudget.FrameMs.ToString("0.0", Inv);
            return w.FrameMsAvg > PerfBudget.FrameMs ? s + " OVER" : s;
        }

        private static string Name(string[] table, int i) { return i >= 0 && i < table.Length ? table[i] : "?"; }
    }
}
