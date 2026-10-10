using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Dialogue;
using GtaSkylines.Game.Hud;
using GtaSkylines.Game.Tests.Fakes;
using GtaSkylines.Game.Tests.Missions;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.Dialogue
{
    public class DialogueLinesTests
    {
        private static CitizenProfile Profile(uint id, int age = 35, int happiness = 60, int wealth = 1, int education = 1,
            bool employed = true, bool student = false, bool tourist = false, int health = 90, bool sick = false)
        {
            return new CitizenProfile
            {
                Citizen = id, Name = "Citizen " + id, Age = age, Happiness = happiness, Wealth = wealth, Education = education,
                Employed = employed, Student = student, Tourist = tourist, Health = health, Sick = sick,
            };
        }

        private static Scene WithProfiles(params CitizenProfile[] profiles)
        {
            Scene s = Scene.Create();
            foreach (CitizenProfile p in profiles) s.City.Profiles[p.Citizen] = p;
            return s;
        }

        [Fact]
        public void AtLeastOneHundredTwentyAuthoredLinesAndEveryGroupHasSome()
        {
            Assert.True(DialogueLines.TotalLines >= 120, "lines: " + DialogueLines.TotalLines);
            foreach (LineGroup g in System.Enum.GetValues(typeof(LineGroup)))
            {
                string[] lines = DialogueLines.Lines(g);
                Assert.NotEmpty(lines);
                foreach (string l in lines) Assert.False(string.IsNullOrWhiteSpace(l));
            }
        }

        [Fact]
        public void GroupForMapsReactionContexts()
        {
            Assert.Equal(LineGroup.Bumped, DialogueLines.GroupFor(DialogueContext.Bumped));
            Assert.Equal(LineGroup.Carjacked, DialogueLines.GroupFor(DialogueContext.Carjacked));
            Assert.Equal(LineGroup.Robbed, DialogueLines.GroupFor(DialogueContext.Robbed));
            Assert.Equal(LineGroup.WitnessCalling, DialogueLines.GroupFor(DialogueContext.WitnessCalling));
            Assert.Equal(LineGroup.CopOrder, DialogueLines.GroupFor(DialogueContext.CopOrder));
            Assert.Equal(LineGroup.Generic, DialogueLines.GroupFor(DialogueContext.Greeting));
        }

        [Fact]
        public void CandidatesForAnOrdinaryWorkingAdult()
        {
            var into = new LineGroup[DialogueLines.MaxCandidates];
            int n = DialogueLines.Candidates(Profile(1, age: 35, happiness: 60, wealth: 2, education: 3), into);
            var got = new List<LineGroup>();
            for (int i = 0; i < n; i++) got.Add(into[i]);
            Assert.Equal(new[] { LineGroup.Content, LineGroup.Worker, LineGroup.Wealthy, LineGroup.Educated }, got);
        }

        [Fact]
        public void CandidatesCoverHappinessBandsSickAndAgeGroups()
        {
            var into = new LineGroup[DialogueLines.MaxCandidates];
            Assert.Equal(LineGroup.Happy, First(Profile(1, happiness: 80), into));
            Assert.Equal(LineGroup.Unhappy, First(Profile(1, happiness: 30), into));
            Assert.Equal(LineGroup.Miserable, First(Profile(1, happiness: 10), into));
            Assert.Equal(LineGroup.Sick, First(Profile(1, sick: true), into));
            Assert.Equal(LineGroup.Sick, First(Profile(1, health: 39), into));
            Assert.Contains(LineGroup.Child, All(Profile(1, age: 8, employed: false), into));
            Assert.Contains(LineGroup.Teen, All(Profile(1, age: 15, employed: false, student: true), into));
            Assert.Contains(LineGroup.Student, All(Profile(1, age: 15, employed: false, student: true), into));
            Assert.Contains(LineGroup.Senior, All(Profile(1, age: 70, employed: false), into));
            Assert.Contains(LineGroup.Unemployed, All(Profile(1, age: 30, employed: false), into));
            Assert.Contains(LineGroup.Poor, All(Profile(1, wealth: 0), into));
            Assert.Contains(LineGroup.Uneducated, All(Profile(1, education: 0), into));
        }

        [Fact]
        public void TouristsStopAtTheTouristGroup()
        {
            var into = new LineGroup[DialogueLines.MaxCandidates];
            List<LineGroup> g = All(Profile(1, tourist: true, wealth: 2, education: 3), into);
            Assert.Equal(LineGroup.Tourist, g[g.Count - 1]);
            Assert.DoesNotContain(LineGroup.Wealthy, g);
            Assert.DoesNotContain(LineGroup.Educated, g);
            Assert.DoesNotContain(LineGroup.Worker, g);
        }

        private static LineGroup First(CitizenProfile p, LineGroup[] into) { DialogueLines.Candidates(p, into); return into[0]; }

        private static List<LineGroup> All(CitizenProfile p, LineGroup[] into)
        {
            int n = DialogueLines.Candidates(p, into);
            var l = new List<LineGroup>();
            for (int i = 0; i < n; i++) l.Add(into[i]);
            return l;
        }

        [Fact]
        public void TheSameCitizenAndContextAlwaysGiveTheSameLine()
        {
            Scene s = WithProfiles(Profile(10), Profile(11, age: 70, happiness: 10));
            var d = new DialogueLines();
            foreach (uint id in new uint[] { 10, 11 })
            {
                string first = d.Line(s.World, DialogueContext.Greeting, id, null);
                Assert.False(string.IsNullOrEmpty(first));
                for (int i = 0; i < 20; i++) Assert.Same(first, d.Line(s.World, DialogueContext.Greeting, id, null));
            }
        }

        [Fact]
        public void LinesVaryAcrossCitizensWithDifferentProfiles()
        {
            var profiles = new List<CitizenProfile>();
            for (uint i = 1; i <= 60; i++)
                profiles.Add(Profile(i, age: 8 + (int)i, happiness: (int)(i * 7 % 100), wealth: (int)(i % 3), education: (int)(i % 4), employed: i % 2 == 0, tourist: i % 11 == 0));
            Scene s = WithProfiles(profiles.ToArray());
            var d = new DialogueLines();
            var seen = new HashSet<string>();
            foreach (CitizenProfile p in profiles) seen.Add(d.Line(s.World, DialogueContext.Greeting, p.Citizen, null));
            Assert.True(seen.Count >= 20, "distinct lines: " + seen.Count);
        }

        [Fact]
        public void ALineBelongsToOneOfTheProfilesGroups()
        {
            CitizenProfile p = Profile(3, age: 70, happiness: 90, employed: false);
            Scene s = WithProfiles(p);
            var into = new LineGroup[DialogueLines.MaxCandidates];
            var groups = All(p, into);
            groups.Add(LineGroup.Recognised);
            string line = new DialogueLines().Line(s.World, DialogueContext.Greeting, 3, null);
            bool found = false;
            foreach (LineGroup g in groups) if (System.Array.IndexOf(DialogueLines.Lines(g), line) >= 0) found = true;
            Assert.True(found, line);
        }

        [Fact]
        public void HighHeatMakesSomeCitizensRecogniseThePlayer()
        {
            var profiles = new List<CitizenProfile>();
            for (uint i = 1; i <= 40; i++) profiles.Add(Profile(i));
            Scene s = WithProfiles(profiles.ToArray());
            var d = new DialogueLines();
            s.World.Wanted.PlayerHeat = 0f;
            int calm = CountRecognised(s, d, profiles);
            s.World.Wanted.PlayerHeat = DialogueLines.RecognisedHeat;
            int hot = CountRecognised(s, d, profiles);
            Assert.Equal(0, calm);
            Assert.InRange(hot, 8, 32);
        }

        private static int CountRecognised(Scene s, DialogueLines d, List<CitizenProfile> profiles)
        {
            int n = 0;
            foreach (CitizenProfile p in profiles)
                if (System.Array.IndexOf(DialogueLines.Lines(LineGroup.Recognised), d.Line(s.World, DialogueContext.Greeting, p.Citizen, null)) >= 0) n++;
            return n;
        }

        [Fact]
        public void UnknownCitizensAndSpawnedPedsGetGenericLines()
        {
            Scene s = Scene.Create();
            var d = new DialogueLines();
            string unknown = d.Line(s.World, DialogueContext.Greeting, 999, null);
            Assert.Contains(unknown, DialogueLines.Lines(LineGroup.Generic));
            Ped ped = s.World.SpawnPed(PedKind.Civilian, 0, new GtaSkylines.Game.Mathx.Vec3(5f, 0f, 5f), 0f);
            string spawned = d.Line(s.World, DialogueContext.Greeting, 0, ped);
            Assert.Contains(spawned, DialogueLines.Lines(LineGroup.Generic));
            Assert.Same(spawned, d.Line(s.World, DialogueContext.Greeting, 0, ped));
        }

        [Theory]
        [InlineData(DialogueContext.Bumped, LineGroup.Bumped)]
        [InlineData(DialogueContext.Carjacked, LineGroup.Carjacked)]
        [InlineData(DialogueContext.Robbed, LineGroup.Robbed)]
        [InlineData(DialogueContext.WitnessCalling, LineGroup.WitnessCalling)]
        [InlineData(DialogueContext.CopOrder, LineGroup.CopOrder)]
        public void ReactionContextsPickFromTheirGroup(DialogueContext context, LineGroup group)
        {
            Scene s = WithProfiles(Profile(5));
            var d = new DialogueLines();
            string line = d.Line(s.World, context, 5, null);
            Assert.Contains(line, DialogueLines.Lines(group));
            Assert.Same(line, d.Line(s.World, context, 5, null));
        }

        [Fact]
        public void TheGreetingContextIsDifferentFromReactionContextsForTheSameCitizen()
        {
            Scene s = WithProfiles(Profile(5));
            var d = new DialogueLines();
            Assert.NotEqual(d.Line(s.World, DialogueContext.Greeting, 5, null), d.Line(s.World, DialogueContext.Bumped, 5, null));
        }
    }

    public class PedCardTextTests
    {
        private static FakeCity City()
        {
            var c = new FakeCity { Name = "Testville" };
            c.AddBuilding(new BuildingDescriptor { Id = 20, Name = "Acme Offices", Service = ServiceKind.Office });
            c.AddBuilding(new BuildingDescriptor { Id = 21, Name = "Elm Street 4", Service = ServiceKind.Residential });
            c.AddBuilding(new BuildingDescriptor { Id = 22, Name = "North High", Service = ServiceKind.Education });
            return c;
        }

        [Theory]
        [InlineData(0, "Miserable")]
        [InlineData(19, "Miserable")]
        [InlineData(20, "Unhappy")]
        [InlineData(40, "Okay")]
        [InlineData(79, "Happy")]
        [InlineData(80, "Thrilled")]
        [InlineData(100, "Thrilled")]
        [InlineData(500, "Thrilled")]
        [InlineData(-4, "Miserable")]
        public void HappinessWordsByBandsOfTwenty(int happiness, string word)
        {
            Assert.Equal(word, PedCardText.HappinessWord(happiness));
        }

        [Fact]
        public void AWorkerLivesAndWorksSomewhere()
        {
            PedCardLines l = PedCardText.From(new CitizenProfile { Name = "Ada Kowalski", Age = 34, Employed = true, WorkBuilding = 20, HomeBuilding = 21, Happiness = 85 }, City());
            Assert.Equal("Ada Kowalski", l.Name);
            Assert.Equal("Age 34", l.Age);
            Assert.Equal("Works at Acme Offices", l.Occupation);
            Assert.Equal("Lives at Elm Street 4", l.Home);
            Assert.Equal("Thrilled", l.Mood);
        }

        [Fact]
        public void StudentsRetireesAndTheUnemployed()
        {
            FakeCity c = City();
            Assert.Equal("Student at North High", PedCardText.From(new CitizenProfile { Age = 16, Student = true, WorkBuilding = 22 }, c).Occupation);
            Assert.Equal("Retired", PedCardText.From(new CitizenProfile { Age = 70 }, c).Occupation);
            Assert.Equal("Unemployed", PedCardText.From(new CitizenProfile { Age = 30 }, c).Occupation);
            Assert.Equal("Unemployed", PedCardText.From(new CitizenProfile { Age = 30, Employed = true, WorkBuilding = 999 }, c).Occupation);
        }

        [Fact]
        public void TouristsVisitAndHomelessHaveNoFixedAddress()
        {
            FakeCity c = City();
            PedCardLines t = PedCardText.From(new CitizenProfile { Age = 30, Tourist = true, HomeBuilding = 21 }, c);
            Assert.Equal("Tourist", t.Occupation);
            Assert.Equal("Visiting Testville", t.Home);
            Assert.Equal("No fixed address", PedCardText.From(new CitizenProfile { Age = 30 }, c).Home);
            Assert.Equal("No fixed address", PedCardText.From(new CitizenProfile { Age = 30, HomeBuilding = 999 }, c).Home);
        }

        [Fact]
        public void FormatWordsTheHudCardAndCachesPerCitizen()
        {
            var text = new PedCardText();
            var data = new PedCardData { Citizen = 5, Name = "Ada", Age = 40, WorkName = "Acme", HomeName = "Elm", Happiness = 50, Employed = true };
            PedCardLines a = text.Format(data, "Testville");
            Assert.Equal("Works at Acme", a.Occupation);
            Assert.Equal("Lives at Elm", a.Home);
            Assert.Equal("Okay", a.Mood);
            PedCardLines b = text.Format(data, "Testville");
            Assert.Same(a.Occupation, b.Occupation);
            Assert.Same(a.Name, b.Name);
            Assert.Same(a.Home, b.Home);
            data.Happiness = 90;
            Assert.Equal("Thrilled", text.Format(data, "Testville").Mood);
            data.Citizen = 6;
            data.Name = "Bo";
            Assert.Equal("Bo", text.Format(data, "Testville").Name);
        }

        [Fact]
        public void FormatHandlesRetiredStudentTouristAndUnemployedCards()
        {
            var text = new PedCardText();
            Assert.Equal("Retired", text.Format(new PedCardData { Citizen = 1, Age = 70, Retired = true }, "T").Occupation);
            Assert.Equal("Student at North High", text.Format(new PedCardData { Citizen = 2, Age = 15, Student = true, WorkName = "North High" }, "T").Occupation);
            PedCardLines t = text.Format(new PedCardData { Citizen = 3, Age = 30, Tourist = true }, "Testville");
            Assert.Equal("Tourist", t.Occupation);
            Assert.Equal("Visiting Testville", t.Home);
            Assert.Equal("Unemployed", text.Format(new PedCardData { Citizen = 4, Age = 30 }, "T").Occupation);
        }
    }
}
