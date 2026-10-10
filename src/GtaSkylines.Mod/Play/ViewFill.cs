using System.Collections.Generic;
using GtaSkylines.Game.Combat;
using GtaSkylines.Game.Missions;
using GtaSkylines.Game.Phone;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Session;
using GtaSkylines.Game.World;
using GtaSkylines.Mod.Hud;
using PhoneRow = GtaSkylines.Mod.Hud.PhoneRow;

namespace GtaSkylines.Mod.Play
{
    /// <summary>Fills the M4 views from the game (pure). Lists are rewritten in place; nothing allocates unless content changed.</summary>
    public static class ViewFill
    {
        /// <summary>
        /// Phone: Contacts (title = contact name), Messages in phone order (title = sender, detail = text, highlight =
        /// unread), Jobs (one row per <see cref="SideJobKind"/>: title = <see cref="PhoneText.EndShift"/> while that job
        /// runs, else the job name; detail = <see cref="PhoneText.RaceDetail"/> for the race, else the level text;
        /// highlight = running), Gps (<see cref="PhoneText.GpsRows"/>). Clock "HH:MM" from the game clock. Revision is
        /// incremented and true returned only when any row or the clock changed.
        /// </summary>
        public static bool Phone(PhoneView view, GameParts game)
        {
            GameWorld w = game.World;
            MissionRunner runner = game.Missions;
            PhoneService phone = runner.Phone;
            bool changed = false;
            IList<PhoneContact> contacts = phone.ContactList;
            for (int i = 0; i < contacts.Count; i++) changed |= Set(view.Contacts, i, contacts[i].Name, null, false);
            changed |= Trim(view.Contacts, contacts.Count);
            IList<PhoneMessage> messages = phone.Messages;
            for (int i = 0; i < messages.Count; i++) changed |= Set(view.Messages, i, messages[i].Sender, messages[i].Text, !messages[i].Read);
            changed |= Trim(view.Messages, messages.Count);
            for (int k = 0; k < SideJobTable.KindCount; k++)
            {
                var kind = (SideJobKind)k;
                bool running = runner.ActiveJob != null && runner.ActiveJob.Kind == kind;
                int level = SideJobTable.Clamp(w.Profile.GetSideJobLevel(SideJobTable.Id(kind)));
                string detail = kind == SideJobKind.StreetRace ? PhoneText.RaceDetail : PhoneText.Levels[level - 1];
                changed |= Set(view.Jobs, k, running ? PhoneText.EndShift : SideJobTable.Name(kind), detail, running);
            }
            changed |= Trim(view.Jobs, SideJobTable.KindCount);
            for (int i = 0; i < PhoneText.GpsRows.Length; i++) changed |= Set(view.Gps, i, PhoneText.GpsRows[i], null, false);
            changed |= Trim(view.Gps, PhoneText.GpsRows.Length);
            string clock = ClockText(w.Clock.Hour, view.Clock);
            if (!ReferenceEquals(clock, view.Clock)) { view.Clock = clock; changed = true; }
            if (changed) view.Revision++;
            return changed;
        }

        /// <summary>"HH:MM" for <paramref name="hours"/> (wrapped to a day); returns <paramref name="previous"/> when it already says that.</summary>
        public static string ClockText(float hours, string previous)
        {
            int minutes = (int)(((hours % 24f) + 24f) % 24f * 60f) % 1440;
            int h = minutes / 60, m = minutes % 60;
            if (previous != null && previous.Length == 5 && previous[0] - '0' == h / 10 && previous[1] - '0' == h % 10
                && previous[3] - '0' == m / 10 && previous[4] - '0' == m % 10) return previous;
            return h.ToString("00") + ":" + m.ToString("00");
        }

        /// <summary>
        /// Weapon wheel: one slot per <see cref="WeaponId"/> (slot index = weapon id): name from <see cref="WeaponTable"/>,
        /// ammo text = total rounds for firearms ("" for melee), owned from the profile; Equipped = the current weapon.
        /// Call when the wheel opens (allocates the ammo strings).
        /// </summary>
        public static void Wheel(WeaponWheelView view, PlayerProfile profile, int currentWeapon)
        {
            view.Slots.Clear();
            for (int i = 0; i < WeaponInfo.Count; i++)
            {
                var id = (WeaponId)i;
                view.Slots.Add(new WheelSlot
                {
                    Name = WeaponTable.Get(id).Name,
                    AmmoText = WeaponInfo.UsesAmmo(id) ? profile.GetAmmo(id).ToString() : "",
                    Owned = profile.OwnsWeapon(id),
                });
            }
            view.Equipped = currentWeapon;
        }

        /// <summary>Interaction menu: in a safehouse = inside an interior of an owned building; mission active = a mission runs; retry = same.</summary>
        public static void Interaction(InteractionView view, GameParts game)
        {
            GameWorld w = game.World;
            InteriorState i = w.Interior;
            view.InSafehouse = i.IsInside && i.Building != null && w.Profile.OwnsProperty(i.Building.Id);
            view.MissionActive = game.Missions.State == RunnerState.Mission;
            view.CanRetry = view.MissionActive;
        }

        private static bool Set(List<PhoneRow> rows, int index, string title, string detail, bool highlight)
        {
            if (index < rows.Count)
            {
                PhoneRow r = rows[index];
                if (r.Title == title && r.Detail == detail && r.Highlight == highlight) return false;
                rows[index] = new PhoneRow { Title = title, Detail = detail, Highlight = highlight };
                return true;
            }
            rows.Add(new PhoneRow { Title = title, Detail = detail, Highlight = highlight });
            return true;
        }

        private static bool Trim(List<PhoneRow> rows, int count)
        {
            if (rows.Count <= count) return false;
            rows.RemoveRange(count, rows.Count - count);
            return true;
        }
    }
}
