using System;
using System.Collections.Generic;
using GtaSkylines.Game.Missions;
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
            get { throw new NotImplementedException(); }
        }

        /// <summary>Unlocks a contact (no-op when already unlocked). Returns true when added.</summary>
        public bool Unlock(ContactId id)
        {
            throw new NotImplementedException();
        }

        /// <summary>True when unlocked.</summary>
        public bool HasContact(ContactId id)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Receives a text: newest first (dropping beyond <see cref="MaxMessages"/>), unread, and a HUD notification
        /// (<c>Messages.Notify(sender, text, NotificationKind.Text)</c>).
        /// </summary>
        public void ReceiveText(GameWorld world, string sender, string text)
        {
            throw new NotImplementedException();
        }

        /// <summary>True when a message with the same sender and text is already in the phone (unlock texts are sent once).</summary>
        public bool HasText(string sender, string text)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Calls a contact: when it has a next available mission, queues the contact's <c>CallLine</c> as a subtitle and
        /// sets the map waypoint (<c>world.HasWaypoint</c>/<c>Waypoint</c>) to its start marker, returning true;
        /// otherwise queues <see cref="PhoneText.NoWork"/> from the contact and returns false.
        /// </summary>
        public bool Call(GameWorld world, Campaign campaign, ContactId id)
        {
            throw new NotImplementedException();
        }

        /// <summary>Sets the waypoint to a shortcut's destination (from the player's position). False when there is none.</summary>
        public bool GpsTo(GameWorld world, PlaceFinder places, Campaign campaign, GpsShortcut shortcut)
        {
            throw new NotImplementedException();
        }

        /// <summary>Opens a screen (Selected 0, rows rebuilt).</summary>
        public void Open(PhoneScreen screen)
        {
            throw new NotImplementedException();
        }

        /// <summary>Closes the phone.</summary>
        public void Close()
        {
            throw new NotImplementedException();
        }

        /// <summary>Applies this step's input (see the class); row actions call into <paramref name="runner"/>.</summary>
        public void HandleInput(GameWorld world, MissionRunner runner)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Refreshes <see cref="Model"/> rows for the current screen (Jobs/QuickMenu rows depend on the player's vehicle
        /// and the running job) and <c>Unread</c>; increments Revision only when something changed. No allocation.
        /// </summary>
        public void RefreshModel(GameWorld world, MissionRunner runner)
        {
            throw new NotImplementedException();
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
