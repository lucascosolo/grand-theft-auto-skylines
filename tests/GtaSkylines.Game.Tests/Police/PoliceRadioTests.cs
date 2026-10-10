using GtaSkylines.Game.Crime;
using GtaSkylines.Game.Police;
using GtaSkylines.Game.Tests.CrimeTests;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.PoliceTests
{
    public class PoliceRadioTests
    {
        private static Scene Radio()
        {
            var s = new Scene(policeCars: false);
            s.City.StreetFunc = p => "Main St";
            s.World.Clock.GameSeconds = 100.0;
            s.MakeWanted(2);
            return s;
        }

        [Fact]
        public void A_reported_crime_is_announced_with_its_words_and_the_street()
        {
            var s = Radio();
            var radio = new PoliceRadio();
            Assert.True(radio.Say(s.World, RadioCue.CrimeReported, CrimeType.CarStolen));
            Assert.Equal("Dispatch, we have a report of a car theft near Main St.", radio.LastLine);
            Assert.True(s.World.Messages.TryGetSubtitle(out Subtitle sub));
            Assert.Equal(PoliceRadio.Speaker, sub.Speaker);
            Assert.Equal(radio.LastLine, sub.Text);
            Assert.Equal(PoliceRadio.LineSeconds, sub.Seconds, 3);
        }

        [Fact]
        public void An_unnamed_street_becomes_the_area()
        {
            var s = Radio();
            s.City.StreetFunc = null;
            var radio = new PoliceRadio();
            radio.Say(s.World, RadioCue.Roadblock, CrimeType.None);
            Assert.Equal("Roadblock in position on the area.", radio.LastLine);
        }

        [Fact]
        public void Spotted_names_the_street_and_the_description_on_foot_or_in_a_red_sedan()
        {
            var s = Radio();
            var onFoot = new PoliceRadio();
            onFoot.Say(s.World, RadioCue.Spotted, CrimeType.None);
            Assert.Equal("Suspect spotted on Main St, on foot. Moving in.", onFoot.LastLine);

            s.Wanted.Description = new SuspectDescription { OutfitModel = -1, CarModel = 0, CarColour = 0xC0392B };
            var inCar = new PoliceRadio();
            inCar.Say(s.World, RadioCue.Spotted, CrimeType.None);
            Assert.Equal("Suspect spotted on Main St, in a red sedan. Moving in.", inCar.LastLine);
        }

        [Fact]
        public void LastSeen_gives_the_heading_street_and_description()
        {
            var s = Radio();
            s.Player.YawDeg = 90f;
            var radio = new PoliceRadio();
            radio.Say(s.World, RadioCue.LastSeen, CrimeType.None);
            Assert.Equal("Suspect last seen heading east on Main St on foot.", radio.LastLine);
        }

        [Fact]
        public void The_fixed_lines_for_lost_visual_air_unit_and_cleared()
        {
            var s = Radio();
            var r = new PoliceRadio();
            r.Say(s.World, RadioCue.LostVisual, CrimeType.None);
            Assert.Equal("We've lost visual. Searching the area.", r.LastLine);
            s.World.Clock.GameSeconds += 9.0;
            r.Say(s.World, RadioCue.AirUnit, CrimeType.None);
            Assert.Equal("Air unit en route, spotlight on.", r.LastLine);
            s.World.Clock.GameSeconds += 9.0;
            r.Say(s.World, RadioCue.Cleared, CrimeType.None);
            Assert.Equal("No sign of the suspect. All units, resume patrol.", r.LastLine);
        }

        [Fact]
        public void At_most_one_line_every_eight_game_seconds()
        {
            var s = Radio();
            var r = new PoliceRadio();
            Assert.True(r.Say(s.World, RadioCue.LostVisual, CrimeType.None));
            s.World.Clock.GameSeconds += 3.0;
            Assert.False(r.Say(s.World, RadioCue.AirUnit, CrimeType.None));
            Assert.Equal("We've lost visual. Searching the area.", r.LastLine);
            s.World.Clock.GameSeconds += 5.5;
            Assert.True(r.Say(s.World, RadioCue.AirUnit, CrimeType.None));
        }
    }
}
