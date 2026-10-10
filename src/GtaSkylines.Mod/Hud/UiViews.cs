using System.Collections.Generic;

namespace GtaSkylines.Mod.Hud
{
    /// <summary>Phone apps, in home-screen order.</summary>
    public enum PhoneApp
    {
        /// <summary>Contacts: call → mission info.</summary>
        Contacts = 0,
        /// <summary>Text messages.</summary>
        Messages = 1,
        /// <summary>Side jobs.</summary>
        Jobs = 2,
        /// <summary>GPS shortcuts.</summary>
        Gps = 3,
    }

    /// <summary>One row of a phone list.</summary>
    public struct PhoneRow
    {
        /// <summary>Main text (contact name, sender, job name, place).</summary>
        public string Title;
        /// <summary>Second line (message body, job level, distance), or null.</summary>
        public string Detail;
        /// <summary>Unread / new marker.</summary>
        public bool Highlight;
    }

    /// <summary>
    /// What the phone shows; the wiring chunk fills the lists (only when their content changes) and the phone keeps its
    /// own app/selection state. Lists are reused, never replaced.
    /// </summary>
    public sealed class PhoneView
    {
        /// <summary>Contacts.</summary>
        public readonly List<PhoneRow> Contacts = new List<PhoneRow>();
        /// <summary>Messages, newest first.</summary>
        public readonly List<PhoneRow> Messages = new List<PhoneRow>();
        /// <summary>Side jobs.</summary>
        public readonly List<PhoneRow> Jobs = new List<PhoneRow>();
        /// <summary>GPS shortcuts.</summary>
        public readonly List<PhoneRow> Gps = new List<PhoneRow>();
        /// <summary>Clock text in the status bar ("14:05"), or null.</summary>
        public string Clock;
        /// <summary>Incremented by the writer whenever any list changes.</summary>
        public int Revision;

        /// <summary>The list behind an app.</summary>
        public List<PhoneRow> Rows(PhoneApp app)
        {
            switch (app)
            {
                case PhoneApp.Contacts: return Contacts;
                case PhoneApp.Messages: return Messages;
                case PhoneApp.Jobs: return Jobs;
                default: return Gps;
            }
        }
    }

    /// <summary>One weapon wheel slot.</summary>
    public struct WheelSlot
    {
        /// <summary>Weapon name ("Pistol"); "Unarmed" for fists.</summary>
        public string Name;
        /// <summary>Ammo text ("12 / 96"), or null for melee.</summary>
        public string AmmoText;
        /// <summary>Owned (unowned slots are drawn empty and cannot be chosen).</summary>
        public bool Owned;
    }

    /// <summary>What the weapon wheel shows; the wiring chunk fills it when the inventory changes.</summary>
    public sealed class WeaponWheelView
    {
        /// <summary>Slots clockwise from the top.</summary>
        public readonly List<WheelSlot> Slots = new List<WheelSlot>();
        /// <summary>The slot in hand.</summary>
        public int Equipped;
    }

    /// <summary>What the interaction menu offers.</summary>
    public sealed class InteractionView
    {
        /// <summary>Inside an owned safehouse (offers the wardrobe).</summary>
        public bool InSafehouse;
        /// <summary>A mission runs (offers retry/abort).</summary>
        public bool MissionActive;
        /// <summary>The mission can be retried.</summary>
        public bool CanRetry;
    }

    /// <summary>Rows of the BRIEF and STATS tabs (label, value), filled by the wiring chunk when they change.</summary>
    public sealed class PauseView
    {
        /// <summary>Mission title, or null.</summary>
        public string MissionTitle;
        /// <summary>Objective, or null.</summary>
        public string Objective;
        /// <summary>Mission log lines, newest first.</summary>
        public readonly List<string> Log = new List<string>();
        /// <summary>Side-job levels (name, "Level 3").</summary>
        public readonly List<KeyValuePair<string, string>> Jobs = new List<KeyValuePair<string, string>>();
        /// <summary>Stats (label, value).</summary>
        public readonly List<KeyValuePair<string, string>> Stats = new List<KeyValuePair<string, string>>();
        /// <summary>Player cash text for the header ("$1,234").</summary>
        public string CashText;
        /// <summary>City name for the header.</summary>
        public string CityName;
    }
}
