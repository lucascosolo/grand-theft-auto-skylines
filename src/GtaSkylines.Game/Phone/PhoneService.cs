using System;
using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Interiors;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Missions;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Phone
{
    /// <summary>A phone contact.</summary>
    public sealed class PhoneContact
    {
        /// <summary>Which contact.</summary>
        public ContactId Id;
        /// <summary>Display name (<see cref="Contacts.Name"/>).</summary>
        public string Name;
    }

    /// <summary>A received text.</summary>
    public sealed class PhoneMessage
    {
        /// <summary>Sender display name.</summary>
        public string Sender;
        /// <summary>Text (placeholders filled).</summary>
        public string Text;
        /// <summary><c>GameClock.GameSeconds</c> when received.</summary>
        public double GameTime;
        /// <summary>Opened on the phone.</summary>
        public bool Read;
    }

    /// <summary>Quick GPS destinations.</summary>
    public enum GpsShortcut
    {
        /// <summary>Nearest Commercial building with a shop archetype (ConvenienceStore, ClothesShop, GunStore, Restaurant).</summary>
        Shop,
        /// <summary>Nearest Industrial building with archetype Garage (resprays), else Vince's garage.</summary>
        Garage,
        /// <summary>Nearest Health building (<c>PlaceFinder.Hospital</c>).</summary>
        Hospital,
        /// <summary><c>Profile.LastSafehouse</c>, else the nearest owned property; none → help <see cref="PhoneText.NoSafehouse"/>.</summary>
        Safehouse,
    }

    /// <summary>
    /// The player's phone: contacts (unlocked as met), texts, calls, the Jobs app and GPS shortcuts, and the
    /// interaction menu. Input (each step, from <c>world.Input</c> pressed edges): Phone toggles Home/Closed;
    /// InteractionMenu toggles QuickMenu/Closed; while open MenuUp/MenuDown move the selection (wrapping), MenuAccept
    /// activates the row, MenuBack goes to Home (from a sub-screen), to Messages (from a message) or closes (from Home
    /// or QuickMenu). Opening is ignored while <c>world.Menu.IsOpen</c> or the player is not alive.
    /// </summary>
    public sealed class PhoneService
    {
        /// <summary>Messages kept (oldest dropped).</summary>
        public const int MaxMessages = 30;

        private readonly List<PhoneContact> _contacts = new List<PhoneContact>();
        private readonly List<PhoneMessage> _messages = new List<PhoneMessage>();

        /// <summary>Creates an empty phone (Closed, no contacts).</summary>
        public PhoneService()
        {
            Model = new PhoneModel();
        }

        /// <summary>HUD model.</summary>
        public PhoneModel Model { get; private set; }

        /// <summary>Unlocked contacts in unlock order.</summary>
        public IList<PhoneContact> ContactList { get { return _contacts; } }

        /// <summary>Messages, newest first.</summary>
        public IList<PhoneMessage> Messages { get { return _messages; } }

        /// <summary>Unread messages.</summary>
        public int UnreadCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _messages.Count; i++) if (!_messages[i].Read) n++;
                return n;
            }
        }

        /// <summary>Unlocks a contact (no-op when already unlocked). Returns true when added.</summary>
        public bool Unlock(ContactId id)
        {
            if (HasContact(id)) return false;
            _contacts.Add(new PhoneContact { Id = id, Name = Contacts.Name(id) });
            Rebuild();
            return true;
        }

        /// <summary>True when unlocked.</summary>
        public bool HasContact(ContactId id)
        {
            for (int i = 0; i < _contacts.Count; i++) if (_contacts[i].Id == id) return true;
            return false;
        }

        /// <summary>
        /// Receives a text: newest first (dropping beyond <see cref="MaxMessages"/>), unread, and a HUD notification
        /// (<c>Messages.Notify(sender, text, NotificationKind.Text)</c>).
        /// </summary>
        public void ReceiveText(GameWorld world, string sender, string text)
        {
            _messages.Insert(0, new PhoneMessage { Sender = sender, Text = text, GameTime = world.Clock.GameSeconds });
            while (_messages.Count > MaxMessages) _messages.RemoveAt(_messages.Count - 1);
            world.Messages.Notify(sender, text, NotificationKind.Text);
            Rebuild();
        }

        /// <summary>True when a message with the same sender and text is already in the phone (unlock texts are sent once).</summary>
        public bool HasText(string sender, string text)
        {
            for (int i = 0; i < _messages.Count; i++)
                if (_messages[i].Sender == sender && _messages[i].Text == text) return true;
            return false;
        }

        /// <summary>
        /// Calls a contact: when it has a next available mission, queues the contact's <c>CallLine</c> as a subtitle and
        /// sets the map waypoint (<c>world.HasWaypoint</c>/<c>Waypoint</c>) to its start marker, returning true;
        /// otherwise queues <see cref="PhoneText.NoWork"/> from the contact and returns false.
        /// </summary>
        public bool Call(GameWorld world, Campaign campaign, ContactId id)
        {
            string name = Contacts.Name(id);
            MissionDefinition next = campaign == null ? null : campaign.NextFor(id, world.Profile);
            if (next == null)
            {
                world.Messages.QueueSubtitle(name, PhoneText.NoWork, 4f);
                return false;
            }
            world.Messages.QueueSubtitle(name, next.CallLine, 4f);
            world.HasWaypoint = true;
            world.Waypoint = next.StartMarker;
            return true;
        }

        /// <summary>Sets the waypoint to a shortcut's destination (from the player's position). False when there is none.</summary>
        public bool GpsTo(GameWorld world, PlaceFinder places, Campaign campaign, GpsShortcut shortcut)
        {
            Vec3 at = world.Player.Position;
            CampaignPlace place = null;
            switch (shortcut)
            {
                case GpsShortcut.Shop:
                    place = places.Nearest(ServiceKind.Commercial, InteriorArchetype.ConvenienceStore, at, 0f);
                    break;
                case GpsShortcut.Garage:
                    place = places.Nearest(ServiceKind.Industrial, InteriorArchetype.Garage, at, 0f);
                    if ((place == null || place.Archetype != InteriorArchetype.Garage) && campaign != null) place = campaign.Places.Garage;
                    break;
                case GpsShortcut.Hospital:
                    place = places.Hospital(at);
                    break;
                default:
                    place = Safehouse(world, places, at);
                    if (place == null)
                    {
                        world.Messages.ShowHelp(PhoneText.NoSafehouse, 2, 5f);
                        return false;
                    }
                    break;
            }
            if (place == null) return false;
            world.HasWaypoint = true;
            world.Waypoint = place.Door;
            return true;
        }

        private static CampaignPlace Safehouse(GameWorld world, PlaceFinder places, Vec3 at)
        {
            PlayerProfile profile = world.Profile;
            if (profile.LastSafehouse != 0) return places.Describe((ushort)profile.LastSafehouse);
            CampaignPlace best = null;
            for (int i = 0; i < profile.Properties.Count; i++)
            {
                CampaignPlace p = places.Describe((ushort)profile.Properties[i]);
                if (p != null && (best == null || Vec3.DistanceXZ(p.Door, at) < Vec3.DistanceXZ(best.Door, at))) best = p;
            }
            return best;
        }

        /// <summary>Opens a screen (Selected 0, rows rebuilt).</summary>
        public void Open(PhoneScreen screen)
        {
            Model.Screen = screen;
            Model.Selected = 0;
            Model.Body = null;
            Model.Header = HeaderOf(screen);
            Rebuild();
            Model.Revision++;
        }

        /// <summary>Closes the phone.</summary>
        public void Close()
        {
            if (Model.Screen == PhoneScreen.Closed) return;
            Model.Screen = PhoneScreen.Closed;
            Model.RowCount = 0;
            Model.Selected = 0;
            Model.Body = null;
            Model.Header = null;
            Model.Revision++;
        }

        /// <summary>Applies this step's input (see the class); row actions call into <paramref name="runner"/>.</summary>
        public void HandleInput(GameWorld world, MissionRunner runner)
        {
            InputFrame input = world.Input;
            bool canOpen = !world.Menu.IsOpen && world.PlayerState != null && world.PlayerState.Life == LifeState.Alive;
            if (input.WasPressed(GameButton.Phone))
            {
                if (Model.IsOpen) Close();
                else if (canOpen) Open(PhoneScreen.Home);
                return;
            }
            if (input.WasPressed(GameButton.InteractionMenu))
            {
                if (Model.IsOpen) Close();
                else if (canOpen)
                {
                    Open(PhoneScreen.QuickMenu);
                    RefreshModel(world, runner);
                }
                return;
            }
            if (!Model.IsOpen) return;
            int n = Model.RowCount;
            if (input.WasPressed(GameButton.MenuUp) && n > 0)
            {
                Model.Selected = (Model.Selected + n - 1) % n;
                Model.Revision++;
            }
            if (input.WasPressed(GameButton.MenuDown) && n > 0)
            {
                Model.Selected = (Model.Selected + 1) % n;
                Model.Revision++;
            }
            if (input.WasPressed(GameButton.MenuBack)) Back();
            else if (input.WasPressed(GameButton.MenuAccept)) Accept(world, runner);
        }

        private void Back()
        {
            switch (Model.Screen)
            {
                case PhoneScreen.Home:
                case PhoneScreen.QuickMenu:
                    Close();
                    break;
                case PhoneScreen.Message:
                    Open(PhoneScreen.Messages);
                    Model.Selected = _messageIndex < Model.RowCount ? _messageIndex : 0;
                    break;
                default:
                    Open(PhoneScreen.Home);
                    break;
            }
        }

        private void Accept(GameWorld world, MissionRunner runner)
        {
            int i = Model.Selected;
            switch (Model.Screen)
            {
                case PhoneScreen.Home:
                    if (i < HomeScreens.Length) Open(HomeScreens[i]);
                    break;
                case PhoneScreen.Contacts:
                    if (i < _contacts.Count) Call(world, runner.Campaign, _contacts[i].Id);
                    break;
                case PhoneScreen.Messages:
                    if (i >= _messages.Count) break;
                    _messageIndex = i;
                    _messages[i].Read = true;
                    Model.Screen = PhoneScreen.Message;
                    Model.Header = _messages[i].Sender;
                    Model.Body = _messages[i].Text;
                    Model.RowCount = 0;
                    Model.Selected = 0;
                    Model.Unread = UnreadCount;
                    Model.Revision++;
                    break;
                case PhoneScreen.Jobs:
                    if (i < SideJobTable.KindCount) JobAction(world, runner, (SideJobKind)i);
                    break;
                case PhoneScreen.Gps:
                    if (runner.Places == null) break;
                    if (i < 4) GpsTo(world, runner.Places, runner.Campaign, (GpsShortcut)i);
                    else world.HasWaypoint = false;
                    break;
                case PhoneScreen.QuickMenu:
                    int count = QuickEntries(world, runner);
                    if (i >= count) break;
                    int code = _quick[i];
                    if (code == QuickPhone) Open(PhoneScreen.Home);
                    else if (code == QuickEnd) runner.EndSideJob(world, FailReasons.Cancelled);
                    else
                    {
                        runner.TryStartSideJob(world, (SideJobKind)code);
                        Close();
                    }
                    break;
            }
        }

        private static void JobAction(GameWorld world, MissionRunner runner, SideJobKind kind)
        {
            if (runner.ActiveJob != null && runner.ActiveJob.Kind == kind)
            {
                runner.EndSideJob(world, FailReasons.Cancelled);
                return;
            }
            if (kind == SideJobKind.StreetRace && runner.TryStartSideJob(world, kind) != SideJobStartResult.Started)
            {
                world.HasWaypoint = true;
                world.Waypoint = runner.RaceMarker;
                return;
            }
            SideJobStartResult r = runner.TryStartSideJob(world, kind);
            if (r == SideJobStartResult.WrongVehicle) world.Messages.ShowHelp(SideJobText.WrongVehicle[(int)kind], 2, 5f);
            else if (r == SideJobStartResult.MissionRunning) world.Messages.ShowHelp(SideJobText.MissionRunning, 2, 5f);
        }

        private const int QuickEnd = 100;
        private const int QuickPhone = 200;
        private readonly int[] _quick = new int[SideJobTable.KindCount + 2];
        private int _messageIndex;

        private static readonly PhoneScreen[] HomeScreens = { PhoneScreen.Contacts, PhoneScreen.Messages, PhoneScreen.Jobs, PhoneScreen.Gps };

        private static string HeaderOf(PhoneScreen screen)
        {
            switch (screen)
            {
                case PhoneScreen.Contacts: return PhoneText.Apps[0];
                case PhoneScreen.Messages: return PhoneText.Apps[1];
                case PhoneScreen.Jobs: return PhoneText.Apps[2];
                case PhoneScreen.Gps: return PhoneText.Apps[3];
                case PhoneScreen.QuickMenu: return PhoneText.QuickMenu;
                case PhoneScreen.Closed: return null;
                default: return PhoneText.Home;
            }
        }

        /// <summary>Fills <c>_quick</c> with the interaction menu's entries (job kinds, end shift, phone); returns the count.</summary>
        private int QuickEntries(GameWorld world, MissionRunner runner)
        {
            int n = 0;
            if (runner != null && runner.ActiveJob != null) _quick[n++] = QuickEnd;
            else
            {
                Car car = world.Player == null || world.Player.VehicleId == 0 ? null : world.FindCar(world.Player.VehicleId);
                if (car != null && world.Player.Seat == 0)
                    for (int k = 0; k < SideJobTable.KindCount - 1; k++)
                        if (SideJobTable.AcceptsVehicle((SideJobKind)k, car.Model.Kind)) _quick[n++] = k;
            }
            _quick[n++] = QuickPhone;
            return n;
        }

        /// <summary>Rebuilds rows that need no world (home, contacts, messages, GPS).</summary>
        private void Rebuild()
        {
            Model.Unread = UnreadCount;
            switch (Model.Screen)
            {
                case PhoneScreen.Home:
                    Model.RowCount = 0;
                    for (int i = 0; i < PhoneText.Apps.Length; i++) SetRow(i, PhoneText.Apps[i], null, true, i == 1 && Model.Unread > 0);
                    Model.RowCount = PhoneText.Apps.Length;
                    break;
                case PhoneScreen.Contacts:
                    for (int i = 0; i < _contacts.Count && i < PhoneModel.MaxRows; i++) SetRow(i, _contacts[i].Name, null, true, false);
                    Model.RowCount = Math.Min(_contacts.Count, PhoneModel.MaxRows);
                    break;
                case PhoneScreen.Messages:
                    for (int i = 0; i < _messages.Count && i < PhoneModel.MaxRows; i++) SetRow(i, _messages[i].Sender, _messages[i].Text, true, !_messages[i].Read);
                    Model.RowCount = Math.Min(_messages.Count, PhoneModel.MaxRows);
                    break;
                case PhoneScreen.Gps:
                    for (int i = 0; i < PhoneText.GpsRows.Length; i++) SetRow(i, PhoneText.GpsRows[i], null, true, false);
                    Model.RowCount = PhoneText.GpsRows.Length;
                    break;
            }
            if (Model.Selected >= Model.RowCount) Model.Selected = 0;
        }

        private bool SetRow(int i, string title, string detail, bool enabled, bool badge)
        {
            PhoneRow r = Model.Rows[i];
            if (ReferenceEquals(r.Title, title) && ReferenceEquals(r.Detail, detail) && r.Enabled == enabled && r.Badge == badge) return false;
            Model.Rows[i] = new PhoneRow { Title = title, Detail = detail, Enabled = enabled, Badge = badge };
            return true;
        }

        /// <summary>
        /// Refreshes <see cref="Model"/> rows for the current screen (Jobs/QuickMenu rows depend on the player's vehicle
        /// and the running job) and <c>Unread</c>; increments Revision only when something changed. No allocation.
        /// </summary>
        public void RefreshModel(GameWorld world, MissionRunner runner)
        {
            if (!Model.IsOpen)
            {
                int unread = UnreadCount;
                if (unread != Model.Unread) { Model.Unread = unread; Model.Revision++; }
                return;
            }
            int oldCount = Model.RowCount, oldUnread = Model.Unread;
            bool changed = false;
            switch (Model.Screen)
            {
                case PhoneScreen.Jobs:
                    Car car = world.Player == null || world.Player.VehicleId == 0 ? null : world.FindCar(world.Player.VehicleId);
                    for (int k = 0; k < SideJobTable.KindCount; k++)
                    {
                        var kind = (SideJobKind)k;
                        bool running = runner.ActiveJob != null && runner.ActiveJob.Kind == kind;
                        int level = SideJobTable.Clamp(world.Profile.GetSideJobLevel(SideJobTable.Id(kind)));
                        string detail = kind == SideJobKind.StreetRace ? PhoneText.RaceDetail : PhoneText.Levels[level - 1];
                        bool enabled = running || kind == SideJobKind.StreetRace
                            || (car != null && world.Player.Seat == 0 && SideJobTable.AcceptsVehicle(kind, car.Model.Kind));
                        changed |= SetRow(k, running ? PhoneText.EndShift : SideJobTable.Name(kind), detail, enabled, false);
                    }
                    Model.RowCount = SideJobTable.KindCount;
                    break;
                case PhoneScreen.QuickMenu:
                    int n = QuickEntries(world, runner);
                    for (int i = 0; i < n; i++)
                    {
                        int code = _quick[i];
                        string title = code == QuickPhone ? PhoneText.OpenPhone : code == QuickEnd ? PhoneText.EndShift : SideJobTable.Name((SideJobKind)code);
                        changed |= SetRow(i, title, null, true, false);
                    }
                    Model.RowCount = n;
                    break;
                case PhoneScreen.Message:
                    Model.Unread = UnreadCount;
                    break;
                default:
                    Rebuild();
                    break;
            }
            if (Model.Selected >= Model.RowCount && Model.RowCount > 0) Model.Selected = 0;
            if (changed || oldCount != Model.RowCount || oldUnread != Model.Unread) Model.Revision++;
        }
    }

    /// <summary>Authored phone UI text.</summary>
    public static class PhoneText
    {
        /// <summary>Home header.</summary>
        public const string Home = "Phone";
        /// <summary>App names in Home row order.</summary>
        public static readonly string[] Apps = { "Contacts", "Messages", "Jobs", "GPS" };
        /// <summary>GPS rows in <see cref="GpsShortcut"/> order, then "Clear waypoint".</summary>
        public static readonly string[] GpsRows = { "Nearest shop", "Nearest garage", "Nearest hospital", "Safehouse", "Clear waypoint" };
        /// <summary>Interaction menu header.</summary>
        public const string QuickMenu = "Interaction";
        /// <summary>Row to end the running shift.</summary>
        public const string EndShift = "End shift";
        /// <summary>QuickMenu row that opens the phone.</summary>
        public const string OpenPhone = "Phone";
        /// <summary>Level detail strings, index = level − 1.</summary>
        public static readonly string[] Levels =
        {
            "Level 1", "Level 2", "Level 3", "Level 4", "Level 5", "Level 6", "Level 7", "Level 8", "Level 9", "Level 10",
        };
        /// <summary>Jobs-app detail for the race row (accept sets the waypoint to the race marker).</summary>
        public const string RaceDetail = "Drive to the race marker";
        /// <summary>Contact with no work.</summary>
        public const string NoWork = "Nothing for you right now. I'll text you.";
        /// <summary>No safehouse owned.</summary>
        public const string NoSafehouse = "You don't own a safehouse yet.";
        /// <summary>Empty messages list.</summary>
        public const string NoMessages = "No messages";
    }
}
