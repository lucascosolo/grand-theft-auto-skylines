using System;

namespace GtaSkylines.Game.Missions
{
    /// <summary>
    /// Authored campaign text. Templates use <see cref="TextVars"/> placeholders (<c>{contact}</c>, <c>{place}</c>,
    /// <c>{street}</c>, <c>{district}</c>, <c>{place2}</c>, <c>{street2}</c>, <c>{district2}</c>, <c>{person}</c>,
    /// <c>{vehicle}</c>, <c>{city}</c>, <c>{contact_street}</c>, <c>{contact_district}</c>) and objective markup
    /// (<see cref="RichText"/>). Help texts use HUD button tokens (<c>{Sprint}</c>, <c>{EnterExit}</c>…), which are
    /// GameButton names and are not <see cref="TextVars"/> keys, so <see cref="TextVars.Fill"/> leaves them alone.
    /// </summary>
    public static class CampaignText
    {
        /// <summary>Vince's phone and speaker name.</summary>
        public const string VinceName = "Vince";
        /// <summary>Reyes's phone and speaker name.</summary>
        public const string ReyesName = "Sgt. Reyes";
        /// <summary>The Mayor's Office phone name.</summary>
        public const string MayorName = "Mayor's Office";
        /// <summary>The Mayor's Office speaker in subtitles.</summary>
        public const string HaleName = "Deputy Mayor Hale";
        /// <summary>The aide in C3.</summary>
        public const string TheoName = "Theo";
        /// <summary>Police radio speaker.</summary>
        public const string DispatchName = "Dispatch";

        /// <summary>Shown while the player is out of the vehicle a step needs.</summary>
        public const string GetBackInCar = "Get back in the ~b~vehicle~s~.";
        /// <summary>PickUp with a full car.</summary>
        public const string NoRoom = "There's no free seat in this vehicle.";
        /// <summary>Backyard respray without the cash.</summary>
        public const string NeedCashForRespray = "You can't afford the respray.";
        /// <summary>Steps that need a car while the player is on foot.</summary>
        public const string NeedACar = "You'll need a car for this. Take one with {EnterExit}.";

        private static readonly string[] FireProgressText =
        {
            "Fire out: 0%", "Fire out: 10%", "Fire out: 20%", "Fire out: 30%", "Fire out: 40%", "Fire out: 50%",
            "Fire out: 60%", "Fire out: 70%", "Fire out: 80%", "Fire out: 90%", "Fire out: 100%",
        };

        private static readonly string[] RacePositionText =
        {
            "Position 1/1",
            "Position 1/2", "Position 2/2",
            "Position 1/3", "Position 2/3", "Position 3/3",
            "Position 1/4", "Position 2/4", "Position 3/4", "Position 4/4",
        };

        /// <summary>"Fire out: N%" for tenths 0-10 (clamped). Cached.</summary>
        public static string FireProgress(int tenths)
        {
            return FireProgressText[tenths < 0 ? 0 : tenths > 10 ? 10 : tenths];
        }

        /// <summary>"Position rank/racers" for 1 ≤ rank ≤ racers ≤ 4 (clamped into that range). Cached.</summary>
        public static string RacePosition(int rank, int racers)
        {
            int n = racers < 1 ? 1 : racers > 4 ? 4 : racers;
            int r = rank < 1 ? 1 : rank > n ? n : rank;
            return RacePositionText[n * (n - 1) / 2 + r - 1];
        }

        /// <summary>Wallet reason for a mission reward.</summary>
        public static string RewardReason(string title)
        {
            if (title == null) throw new ArgumentNullException("title");
            return "Mission: " + title;
        }

        private static DialogueLine L(string speaker, string text) { return new DialogueLine(speaker, text, 4f); }

        /// <summary>V1 "Fresh Start".</summary>
        public static class V1
        {
            /// <summary>Title.</summary>
            public const string Title = "Fresh Start";
            /// <summary>Vince's first text (the campaign's opening).</summary>
            public const string Unlock = "Hey, it's Vince. Heard you're new in {city}. Come see me at {contact} on {contact_street}. Bring an appetite.";
            /// <summary>Briefing ({place} = the store).</summary>
            public static readonly DialogueLine[] Briefing =
            {
                L(VinceName, "So you're the new face. {city} chews up new faces, you know that?"),
                L(VinceName, "Rule one: nobody works hungry. Grab a snack at {place}."),
                L(VinceName, "Then bring me a car. Any car. I'm not picky, I'm broke."),
            };
            /// <summary>Walk to the store.</summary>
            public const string GoToStore = "Go to ~y~{place}~s~ on {street}.";
            /// <summary>Tutorial: moving.</summary>
            public const string GoToStoreHelp = "Hold {Sprint} to run. Press {Jump} to jump or climb.";
            /// <summary>Buy the snack.</summary>
            public const string BuySnack = "Buy a ~g~snack~s~ at the counter.";
            /// <summary>Tutorial: shops.</summary>
            public const string BuySnackHelp = "Walk up to the counter and press {Interact} to shop.";
            /// <summary>Steal a parked car.</summary>
            public const string StealCar = "Steal a ~b~parked car~s~.";
            /// <summary>Tutorial: cars.</summary>
            public const string StealCarHelp = "Press {EnterExit} next to a car to get in. Parked cars are the easy ones.";
            /// <summary>Drive to the garage.</summary>
            public const string Deliver = "Take the car to ~y~{contact}~s~ in {contact_district}.";
            /// <summary>Tutorial: GPS.</summary>
            public const string DeliverHelp = "Follow the GPS line on the minimap. Press {Headlights} for lights.";
            /// <summary>Wrap-up.</summary>
            public static readonly DialogueLine[] Outro =
            {
                L(VinceName, "Not bad. Not good either. But not bad."),
                L(VinceName, "Come back when you want real work."),
            };
            /// <summary>Text after passing.</summary>
            public const string Passed = "Car's already in pieces. Nice work, rookie.";
            /// <summary>Phone call line.</summary>
            public const string Call = "Garage. {contact_street}. You know where. Get moving.";
        }

        /// <summary>V2 "Hot Wheels".</summary>
        public static class V2
        {
            /// <summary>Title.</summary>
            public const string Title = "Hot Wheels";
            /// <summary>Unlock text.</summary>
            public const string Unlock = "Got a buyer who wants something fast and shiny. Swing by the garage.";
            /// <summary>Briefing ({vehicle}, {district} = where the car is parked).</summary>
            public static readonly DialogueLine[] Briefing =
            {
                L(VinceName, "A collector wants a {vehicle}. There's one parked over in {district}."),
                L(VinceName, "It's got a nosy neighbour, so the cops will hear about it."),
                L(VinceName, "And no scratches. He pays for paint, not for dents."),
            };
            /// <summary>Steal it.</summary>
            public const string Steal = "Steal the ~r~{vehicle}~s~ on {street} in {district}.";
            /// <summary>Bring it back.</summary>
            public const string Deliver = "Bring the ~b~{vehicle}~s~ to ~y~{contact}~s~. Keep it in one piece.";
            /// <summary>Help after the witness.</summary>
            public const string DeliverHelp = "Someone saw you. Outrun the cops, and mind the paint.";
            /// <summary>Wrap-up.</summary>
            public static readonly DialogueLine[] Outro =
            {
                L(VinceName, "Look at that shine. The collector's going to cry."),
            };
            /// <summary>Text after passing.</summary>
            public const string Passed = "Collector paid double. Don't let it go to your head.";
            /// <summary>Phone call line.</summary>
            public const string Call = "The {vehicle} won't steal itself. It's in {district}.";
        }

        /// <summary>V3 "Clean Getaway".</summary>
        public static class V3
        {
            /// <summary>Title.</summary>
            public const string Title = "Clean Getaway";
            /// <summary>Unlock text.</summary>
            public const string Unlock = "Things got hot after that last job. Come by, I'll explain.";
            /// <summary>Briefing ({place} = the respray garage).</summary>
            public static readonly DialogueLine[] Briefing =
            {
                L(VinceName, "Bad news. The cops tied that car to you. They're coming."),
                L(VinceName, "Shake them off, get your ride a fresh coat at {place}, then come back."),
            };
            /// <summary>Lose the police.</summary>
            public const string LoseCops = "Lose the ~r~cops~s~.";
            /// <summary>Tutorial: wanted level.</summary>
            public const string LoseCopsHelp = "Break line of sight and stay out of the search zone until the stars fade.";
            /// <summary>Respray.</summary>
            public const string Respray = "Get the car resprayed at ~y~{place}~s~.";
            /// <summary>Back to the garage.</summary>
            public const string Return = "Return to ~y~{contact}~s~.";
            /// <summary>Wrap-up.</summary>
            public static readonly DialogueLine[] Outro =
            {
                L(VinceName, "Fresh paint, fresh start. You're learning."),
            };
            /// <summary>Text after passing.</summary>
            public const string Passed = "Heat's off. Go spend some money, you earned it.";
            /// <summary>Phone call line.</summary>
            public const string Call = "They know your car. Get to the garage before they know your face.";
        }

        /// <summary>R1 "Ride Along".</summary>
        public static class R1
        {
            /// <summary>Title.</summary>
            public const string Title = "Ride Along";
            /// <summary>Reyes's first text.</summary>
            public const string Unlock = "This is Sergeant Reyes. I know about the car. Come to {contact} and we'll talk about your future.";
            /// <summary>Briefing ({district} = the hot district).</summary>
            public static readonly DialogueLine[] Briefing =
            {
                L(ReyesName, "Relax. I don't want you in a cell. I want you useful."),
                L(ReyesName, "Take a cruiser from the lot. There's trouble brewing in {district}."),
            };
            /// <summary>Get in the cruiser.</summary>
            public const string EnterCruiser = "Get in the ~b~police car~s~.";
            /// <summary>Tutorial: siren.</summary>
            public const string EnterCruiserHelp = "Tap {Interact} to switch the siren on or off.";
            /// <summary>Drive to the district.</summary>
            public const string GoToDistrict = "Drive to ~y~{district}~s~.";
            /// <summary>Radio call ({street2} = where the suspect starts).</summary>
            public static readonly DialogueLine[] Radio =
            {
                L(DispatchName, "All units, suspect vehicle fleeing on {street2}. Respond."),
            };
            /// <summary>Chase.</summary>
            public const string Chase = "Stop the ~r~suspect~s~.";
            /// <summary>Tutorial: takedown.</summary>
            public const string ChaseHelp = "Ram the car until it stops, then take the driver down.";
            /// <summary>Wrap-up.</summary>
            public static readonly DialogueLine[] Outro =
            {
                L(ReyesName, "Clean takedown. Maybe you're worth the paperwork."),
            };
            /// <summary>Text after passing.</summary>
            public const string Passed = "Nice driving. Keep your phone on.";
            /// <summary>Phone call line.</summary>
            public const string Call = "Station lot, {contact_street}. The cruiser's waiting.";
        }

        /// <summary>R2 "Evidence Locker".</summary>
        public static class R2
        {
            /// <summary>Title.</summary>
            public const string Title = "Evidence Locker";
            /// <summary>Unlock text.</summary>
            public const string Unlock = "Need a favour. The quiet kind. Meet me at the station.";
            /// <summary>Briefing ({place2} = the warehouse).</summary>
            public static readonly DialogueLine[] Briefing =
            {
                L(ReyesName, "There's a box in our locker room that makes a friend of mine look bad."),
                L(ReyesName, "Walk in, take it, walk out. If the duty officers see you, you're on your own."),
                L(ReyesName, "Drop it at {place2} in {district2}. No cops on your tail."),
            };
            /// <summary>Outside the station.</summary>
            public const string GoInside = "Go into ~y~{contact}~s~.";
            /// <summary>Inside.</summary>
            public const string TakeBox = "Take the ~g~evidence box~s~ from the locker room.";
            /// <summary>Interaction prompt.</summary>
            public const string Prompt = "Press {Interact} to take the evidence box";
            /// <summary>Tutorial: sneaking.</summary>
            public const string SneakHelp = "Press {Sneak} to move quietly. Officers who see you take it will remember you.";
            /// <summary>Deliver the box.</summary>
            public const string Deliver = "Take the box to ~y~{place2}~s~ on {street2}. Stay off the radar.";
            /// <summary>Wrap-up.</summary>
            public static readonly DialogueLine[] Outro =
            {
                L(ReyesName, "Box is gone, my friend sleeps easy. You never saw me."),
            };
            /// <summary>Text after passing.</summary>
            public const string Passed = "Good work. Our little secret.";
            /// <summary>Phone call line.</summary>
            public const string Call = "Locker room's in the back. Don't get seen.";
        }

        /// <summary>R3 "Takedown".</summary>
        public static class R3
        {
            /// <summary>Title.</summary>
            public const string Title = "Takedown";
            /// <summary>Unlock text ({place2} = the hideout).</summary>
            public const string Unlock = "The crew running guns out of {place2} just shot at my officers. I want them gone.";
            /// <summary>Briefing.</summary>
            public static readonly DialogueLine[] Briefing =
            {
                L(ReyesName, "Three armed men at {place2} in {district2}. Officially, I never sent you."),
                L(ReyesName, "Take them out, then get clear before the real units roll in."),
            };
            /// <summary>Go to the hideout.</summary>
            public const string GoTo = "Go to ~y~{place2}~s~ on {street2}.";
            /// <summary>Fight.</summary>
            public const string Eliminate = "Take out the ~r~gunmen~s~.";
            /// <summary>Tutorial: combat.</summary>
            public const string EliminateHelp = "Hold {Aim} to aim and press {Attack} to fire. Use cover.";
            /// <summary>Escape.</summary>
            public const string Escape = "Lose the ~r~cops~s~.";
            /// <summary>Wrap-up.</summary>
            public static readonly DialogueLine[] Outro =
            {
                L(ReyesName, "That's three fewer problems in {city}. I owe you one."),
            };
            /// <summary>Text after passing.</summary>
            public const string Passed = "The Mayor's Office asked for a problem solver. I gave them your number.";
            /// <summary>Phone call line.</summary>
            public const string Call = "{place2}. Three of them. Be smart about it.";
        }

        /// <summary>C1 "Code Red".</summary>
        public static class C1
        {
            /// <summary>Title.</summary>
            public const string Title = "Code Red";
            /// <summary>First text from the Mayor's Office.</summary>
            public const string Unlock = "Mayor's Office here. The city needs a driver who doesn't panic. Come to {contact}.";
            /// <summary>Briefing ({person} = patient, {place} = pick-up, {place2} = hospital).</summary>
            public static readonly DialogueLine[] Briefing =
            {
                L(HaleName, "Ambulances are stretched thin and {person} can't wait."),
                L(HaleName, "Take the ambulance out front, collect them at {place}, get them to {place2}. Fast."),
            };
            /// <summary>Get in.</summary>
            public const string EnterAmbulance = "Get in the ~b~ambulance~s~.";
            /// <summary>Pick-up.</summary>
            public const string PickUp = "Pick up ~b~{person}~s~ at ~y~{place}~s~.";
            /// <summary>Drive to hospital.</summary>
            public const string Deliver = "Get ~b~{person}~s~ to ~y~{place2}~s~.";
            /// <summary>Tutorial: timer.</summary>
            public const string DeliverHelp = "Watch the timer. Smooth driving keeps the patient stable.";
            /// <summary>Wrap-up.</summary>
            public static readonly DialogueLine[] Outro =
            {
                L(HaleName, "They'll pull through. The city noticed. So did the Mayor."),
            };
            /// <summary>Text after passing.</summary>
            public const string Passed = "The hospital sent flowers. To us. Nice touch.";
            /// <summary>Phone call line.</summary>
            public const string Call = "The ambulance is out front of {contact}. Move.";
        }

        /// <summary>C2 "Five-Alarm".</summary>
        public static class C2
        {
            /// <summary>Title.</summary>
            public const string Title = "Five-Alarm";
            /// <summary>Unlock text.</summary>
            public const string Unlock = "Fire crews are swamped all over {city}. Interested in a little heroism?";
            /// <summary>Briefing for a real fire ({place} = the burning building).</summary>
            public static readonly DialogueLine[] Briefing =
            {
                L(HaleName, "{place} is burning on {street}. Grab the fire engine and put it out."),
                L(HaleName, "Get close, hold steady, and don't stop until it's out."),
            };
            /// <summary>Briefing when nothing burns (a reported fire).</summary>
            public static readonly DialogueLine[] BriefingReported =
            {
                L(HaleName, "Someone reported a fire at {place} on {street}. Probably nothing."),
                L(HaleName, "Take the fire engine and check it anyway. That's the job."),
            };
            /// <summary>Get in.</summary>
            public const string EnterEngine = "Get in the ~b~fire engine~s~.";
            /// <summary>Drive to the fire.</summary>
            public const string GoToFire = "Drive to the fire at ~y~{place}~s~.";
            /// <summary>Hose.</summary>
            public const string Hose = "Hose down the ~r~fire~s~.";
            /// <summary>Tutorial: hose.</summary>
            public const string HoseHelp = "Hold {Attack} to spray water. Stay close.";
            /// <summary>Inspect (reported fire).</summary>
            public const string Inspect = "Inspect ~y~{place}~s~ on foot.";
            /// <summary>Reported fire outcome.</summary>
            public static readonly DialogueLine[] FalseAlarm =
            {
                L(HaleName, "False alarm. Burnt toast. Good to know the response time works."),
            };
            /// <summary>Wrap-up after a real fire.</summary>
            public static readonly DialogueLine[] Outro =
            {
                L(HaleName, "{place} is still standing. That's a win for {city}."),
            };
            /// <summary>Text after passing.</summary>
            public const string Passed = "The fire chief wants to know who you are. I said nobody.";
            /// <summary>Phone call line.</summary>
            public const string Call = "Fire engine's at {contact}. Smoke's at {place}.";
        }

        /// <summary>C3 "Gridlock" (finale).</summary>
        public static class C3
        {
            /// <summary>Title.</summary>
            public const string Title = "Gridlock";
            /// <summary>Unlock text.</summary>
            public const string Unlock = "The Mayor's aide needs a ride. It's not just a ride. Come to {contact}.";
            /// <summary>Briefing ({place2} = destination).</summary>
            public static readonly DialogueLine[] Briefing =
            {
                L(HaleName, "Theo is carrying the budget half of {city} wants buried."),
                L(HaleName, "Pick him up out front and get him to {place2} in {district2}. People will try to stop you."),
            };
            /// <summary>Need a car.</summary>
            public const string GetCar = "Get a ~b~car~s~.";
            /// <summary>Pick up Theo.</summary>
            public const string PickUp = "Pick up ~b~Theo~s~.";
            /// <summary>Theo gets in.</summary>
            public static readonly DialogueLine[] Ambush =
            {
                L(TheoName, "They're onto us. Floor it!"),
            };
            /// <summary>Deliver Theo.</summary>
            public const string Deliver = "Get ~b~Theo~s~ to ~y~{place2}~s~ in {district2}.";
            /// <summary>Help when the chase starts.</summary>
            public const string DeliverHelp = "Four stars. Don't stop, don't get boxed in, don't lose Theo.";
            /// <summary>Wrap-up.</summary>
            public static readonly DialogueLine[] Outro =
            {
                L(TheoName, "We made it. Remind me never to take the bus again."),
                L(HaleName, "{city} owes you. I mean it."),
            };
            /// <summary>Text after passing.</summary>
            public const string Passed = "The budget passed. You're the best-kept secret in {city}.";
            /// <summary>Phone call line.</summary>
            public const string Call = "Theo's waiting at {contact}. Bring something fast.";
        }
    }
}
