namespace GtaSkylines.Game.Phone
{
    /// <summary>Which phone screen is shown.</summary>
    public enum PhoneScreen
    {
        /// <summary>Phone put away.</summary>
        Closed,
        /// <summary>Home: rows Contacts, Messages, Jobs, GPS (in this order).</summary>
        Home,
        /// <summary>Unlocked contacts (row = contact; accept calls).</summary>
        Contacts,
        /// <summary>Messages, newest first (accept opens one).</summary>
        Messages,
        /// <summary>One message: Header = sender, <see cref="PhoneModel.Body"/> = text, no rows.</summary>
        Message,
        /// <summary>The six side jobs with levels (accept starts; the running one shows "End shift").</summary>
        Jobs,
        /// <summary>GPS shortcuts: Shop, Garage, Hospital, Safehouse, Clear waypoint.</summary>
        Gps,
        /// <summary>The interaction menu (M): side jobs the current vehicle accepts, "End shift" while one runs, "Phone".</summary>
        QuickMenu,
    }

    /// <summary>A row of a phone list.</summary>
    public struct PhoneRow
    {
        /// <summary>Main text ("Vince", "Taxi").</summary>
        public string Title;
        /// <summary>Secondary text ("Level 3", the message preview), or null.</summary>
        public string Detail;
        /// <summary>False rows are drawn greyed and cannot be accepted.</summary>
        public bool Enabled;
        /// <summary>Unread marker.</summary>
        public bool Badge;
    }

    /// <summary>
    /// The phone as plain data for the HUD (M4 draws it). Written only by <see cref="PhoneService"/>; rows are refilled in
    /// place (fixed capacity, cached strings), and <see cref="Revision"/> increments whenever anything visible changes.
    /// </summary>
    public sealed class PhoneModel
    {
        /// <summary>Row capacity.</summary>
        public const int MaxRows = 32;

        /// <summary>Screen.</summary>
        public PhoneScreen Screen;
        /// <summary>Header line (screen name or sender).</summary>
        public string Header;
        /// <summary>Body text of <see cref="PhoneScreen.Message"/>, else null.</summary>
        public string Body;
        /// <summary>Rows, valid [0, <see cref="RowCount"/>).</summary>
        public readonly PhoneRow[] Rows = new PhoneRow[MaxRows];
        /// <summary>Valid rows.</summary>
        public int RowCount;
        /// <summary>Highlighted row (0 when there are none).</summary>
        public int Selected;
        /// <summary>Unread messages (shown on the Messages row and the closed-phone badge).</summary>
        public int Unread;
        /// <summary>Change counter.</summary>
        public int Revision;

        /// <summary>True while open.</summary>
        public bool IsOpen { get { return Screen != PhoneScreen.Closed; } }
    }
}
