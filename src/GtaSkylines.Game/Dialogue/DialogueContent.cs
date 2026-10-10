namespace GtaSkylines.Game.Dialogue
{
    /// <summary>Authored citizen lines, indexed by <see cref="LineGroup"/>. Every group has at least five lines.</summary>
    public static class DialogueContent
    {
        /// <summary>Lines per group, index = (int)<see cref="LineGroup"/>.</summary>
        public static readonly string[][] Groups =
        {
            // Happy
            new[]
            {
                "Beautiful day, isn't it? I love this city.",
                "Life's good. Don't you dare ruin it.",
                "I just got a raise. Ask me anything.",
                "Have you seen the park? Best thing the Mayor ever did.",
                "Honestly? Can't complain. So I won't.",
                "Smile, stranger. It's free.",
                "Everything's coming up me today.",
            },
            // Content
            new[]
            {
                "Hey. Not bad, not bad.",
                "Another day, another dollar.",
                "Can I help you with something?",
                "The traffic's fine today. Weird, right?",
                "Just heading home. Long day.",
                "You look lost. Most people here do.",
                "It's a living.",
            },
            // Unhappy
            new[]
            {
                "Do I look like I want to chat?",
                "Rent went up again. Of course it did.",
                "This neighbourhood used to be nice.",
                "The bus was late. The bus is always late.",
                "Whatever you're selling, I'm not buying.",
                "Some days I think about moving to the next town over.",
                "Ask me again when the potholes are fixed.",
            },
            // Miserable
            new[]
            {
                "Leave me alone. Please.",
                "Nothing works in this city. Nothing.",
                "Garbage hasn't been picked up in a week. Smell that?",
                "I haven't slept since they built the highway outside my window.",
                "If you're from city hall, I have some complaints.",
                "Go bother somebody happy. If you can find one.",
            },
            // Wealthy
            new[]
            {
                "Mind the shoes. They cost more than your car.",
                "My driver's around here somewhere.",
                "Lovely to meet you. Now, if you'll excuse me.",
                "I'd invite you to the club, but there's a dress code.",
                "Property's up twelve percent this quarter. Delightful.",
                "Is this street always so... public?",
            },
            // Poor
            new[]
            {
                "Got any spare change? No? Figures.",
                "Two jobs and I still can't make rent.",
                "Don't suppose you're hiring?",
                "Groceries cost a fortune now.",
                "I'm saving up for a bike. Long way to go.",
                "You look like you've got money. Must be nice.",
            },
            // Educated
            new[]
            {
                "Did you know this whole district was farmland once?",
                "Fascinating. Truly. Anyway, I have a lecture.",
                "The zoning in this city is a case study in chaos.",
                "I'm writing a paper on traffic flow. Send help.",
                "Statistically, you shouldn't be standing that close to me.",
                "Read anything good lately? No? Shame.",
            },
            // Uneducated
            new[]
            {
                "Huh? Oh. Hey.",
                "I fix stuff. Cars, fences, whatever.",
                "School wasn't for me. Turned out fine.",
                "You need something lifted? I lift things.",
                "Don't use big words with me, I'm on break.",
                "Work's work. Pays the bills, mostly.",
            },
            // Child
            new[]
            {
                "Are you a spy? You look like a spy.",
                "My mom says not to talk to strangers.",
                "I can run faster than you. Bet.",
                "Do you have a dog? I want a dog.",
                "When I grow up I'm gonna drive a fire engine.",
                "Is that your car? It's so cool!",
            },
            // Teen
            new[]
            {
                "Ugh. What?",
                "You're kind of old to be hanging around here.",
                "My phone's at two percent. Don't talk to me.",
                "Nice jacket. That was sarcasm.",
                "Got school tomorrow. Don't remind me.",
                "Are you famous or something? No? Okay.",
            },
            // Senior
            new[]
            {
                "In my day, this was all fields.",
                "Speak up, young one. I can't hear you over the traffic.",
                "Forty years I've lived here. Seen it all.",
                "Have you eaten? You look thin.",
                "Back in my day, people said hello properly.",
                "My knees say it's going to rain.",
                "Slow down. Life's not a race. Except to the pharmacy.",
            },
            // Tourist
            new[]
            {
                "Excuse me, do you know where the famous landmark is?",
                "Could you take our photo? No? Okay.",
                "We're only here for the weekend. It's lovely!",
                "Is it always this busy here?",
                "I can't read this map at all.",
                "Which way to the beach? There's a beach, right?",
                "The hotel said this was the safe part of town.",
            },
            // Unemployed
            new[]
            {
                "Still looking for work. Know anyone?",
                "Sent out thirty applications this week. Nothing.",
                "I'm between jobs. Have been for a while.",
                "Got all the time in the world. That's the problem.",
                "If you hear about anything, tell me first.",
                "They closed the factory. Just like that.",
            },
            // Student
            new[]
            {
                "I have an exam tomorrow and I'm outside. Great.",
                "Student discount? Anyone? No?",
                "Coffee. I need coffee.",
                "My thesis is due and I've written the title.",
                "Library closes at nine. That's my whole life.",
                "Don't ask about my grades.",
            },
            // Worker
            new[]
            {
                "On my way to work. Can't stop.",
                "Boss wants overtime again.",
                "Lunch break. Fifteen minutes. Make it quick.",
                "My commute is longer than my shift, I swear.",
                "Friday can't come soon enough.",
                "Clocked in, clocked out. Repeat.",
                "If I'm late, I'm blaming you.",
            },
            // Sick
            new[]
            {
                "Don't get too close. I'm not feeling great.",
                "Waiting on the ambulance. Has been a while.",
                "I think I need a doctor. Or three.",
                "Cough. Sorry. Cough.",
                "The clinic's packed. Everyone's sick.",
                "Is it hot out here or is it me?",
            },
            // Recognised
            new[]
            {
                "Hey, aren't you the one from the news?",
                "I know you. You're trouble.",
                "Please don't hurt me. I didn't see anything.",
                "My cousin says you stole his car. Just saying.",
                "The cops are looking for someone who looks just like you.",
                "Oh no. Oh no no no. Not you.",
                "Saw you on Chirper. You're kind of a big deal.",
            },
            // Generic
            new[]
            {
                "Hey.",
                "Can I help you?",
                "Nice day for it.",
                "Sorry, I'm in a hurry.",
                "Do I know you?",
                "Busy, busy, busy.",
            },
            // Bumped
            new[]
            {
                "Watch it!",
                "Hey! Eyes up!",
                "Excuse YOU.",
                "Do you mind?!",
                "Walk much?",
                "Ow! Seriously?",
                "Plenty of sidewalk, pal.",
            },
            // Carjacked
            new[]
            {
                "Hey! That's my car!",
                "No, no, no! I just paid that off!",
                "Somebody stop them! Thief!",
                "Take it! Just don't hurt me!",
                "My groceries are in there!",
                "I'm calling the cops, you hear me?",
            },
            // Robbed
            new[]
            {
                "Okay, okay! Take it, it's all yours!",
                "Please, I've got a family!",
                "It's not even my money! I just work here!",
                "Easy! Easy with that thing!",
                "The register's open. Just go.",
                "You won't get away with this.",
            },
            // WitnessCalling
            new[]
            {
                "Police? There's a crazy person on the street!",
                "Yes, I saw everything. Send someone, quick!",
                "Hello, 911? They went that way!",
                "I can describe them. Hurry!",
                "Officer, you need to get down here right now.",
                "They're still here! Please hurry!",
            },
            // CopOrder
            new[]
            {
                "Police! Hands where I can see them!",
                "Freeze! Don't move!",
                "Get down on the ground!",
                "Stop right there!",
                "Drop it! Now!",
                "You're under arrest. Don't make this harder.",
                "Stay where you are!",
            },
        };
    }
}
