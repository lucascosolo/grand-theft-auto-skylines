using GtaSkylines.Game.Missions;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.Missions
{
    public class RichTextTests
    {
        [Fact]
        public void PlaceSpanIsParsedAndMarkupRemoved()
        {
            RichText r = RichText.Parse("Go to ~y~Vince's garage~s~.");
            Assert.Equal("Go to Vince's garage.", r.Text);
            Assert.Single(r.Spans);
            Assert.Equal(6, r.Spans[0].Start);
            Assert.Equal(14, r.Spans[0].Length);
            Assert.Equal(HighlightKind.Place, r.Spans[0].Kind);
        }

        [Fact]
        public void EveryColourCodeMapsToItsKind()
        {
            RichText r = RichText.Parse("~y~a~s~ ~b~b~s~ ~r~c~s~ ~g~d~s~");
            Assert.Equal("a b c d", r.Text);
            Assert.Equal(new[] { HighlightKind.Place, HighlightKind.Friendly, HighlightKind.Target, HighlightKind.Goal },
                new[] { r.Spans[0].Kind, r.Spans[1].Kind, r.Spans[2].Kind, r.Spans[3].Kind });
        }

        [Fact]
        public void NewColourClosesTheOpenSpan()
        {
            RichText r = RichText.Parse("~y~one~b~two");
            Assert.Equal("onetwo", r.Text);
            Assert.Equal(2, r.Spans.Length);
            Assert.Equal(0, r.Spans[0].Start);
            Assert.Equal(3, r.Spans[0].Length);
            Assert.Equal(3, r.Spans[1].Start);
            Assert.Equal(3, r.Spans[1].Length);
        }

        [Fact]
        public void SpanStillOpenAtTheEndEndsThere()
        {
            RichText r = RichText.Parse("see ~r~it");
            Assert.Equal("see it", r.Text);
            Assert.Equal(4, r.Spans[0].Start);
            Assert.Equal(2, r.Spans[0].Length);
        }

        [Fact]
        public void EmptySpansAreDropped()
        {
            RichText r = RichText.Parse("a~y~~s~b");
            Assert.Equal("ab", r.Text);
            Assert.Empty(r.Spans);
        }

        [Fact]
        public void UnknownCodesAndLoneTildesStayLiteral()
        {
            Assert.Equal("a ~x~ b", RichText.Parse("a ~x~ b").Text);
            Assert.Equal("about ~5 km", RichText.Parse("about ~5 km").Text);
            Assert.Empty(RichText.Parse("a ~x~ b").Spans);
        }

        [Fact]
        public void NullParsesAsEmpty()
        {
            RichText r = RichText.Parse(null);
            Assert.Equal(string.Empty, r.Text);
            Assert.Empty(r.Spans);
        }

        [Fact]
        public void WriteObjectiveFillsTheMissionStatus()
        {
            var status = new MissionStatus();
            status.ObjectiveSpans.Add(new TextSpan { Start = 99, Length = 1 });
            RichText.Parse("Go to ~y~Vince~s~.").WriteObjective(status);
            Assert.Equal("Go to Vince.", status.Objective);
            Assert.Single(status.ObjectiveSpans);
            Assert.Equal(6, status.ObjectiveSpans[0].Start);
        }

        [Fact]
        public void WriteObjectiveOfEmptyTextSetsNull()
        {
            var status = new MissionStatus { Objective = "old" };
            RichText.Empty.WriteObjective(status);
            Assert.Null(status.Objective);
            Assert.Empty(status.ObjectiveSpans);
        }
    }

    public class TextVarsTests
    {
        [Fact]
        public void FillReplacesKnownKeysAndLeavesUnknownVisible()
        {
            var v = new TextVars().Set(TextVars.City, "Testville").Set(TextVars.Place, "Quick Stop");
            Assert.Equal("Quick Stop in Testville, {nothing} and {EnterExit}", v.Fill("{place} in {city}, {nothing} and {EnterExit}"));
        }

        [Fact]
        public void SetNullClearsAndGetReturnsValue()
        {
            var v = new TextVars().Set("a", "1");
            Assert.Equal("1", v.Get("a"));
            v.Set("a", null);
            Assert.Null(v.Get("a"));
            Assert.Equal("{a}", v.Fill("{a}"));
        }

        [Fact]
        public void FillOfNullIsEmpty()
        {
            Assert.Equal(string.Empty, new TextVars().Fill(null));
        }

        [Fact]
        public void SetPlaceFillsPlaceStreetDistrictOrTheSecondSet()
        {
            var place = new CampaignPlace { Name = "Rusty Bolt", Street = "Elm Street", District = "Old Town" };
            var v = new TextVars().SetPlace(place, false);
            Assert.Equal("Rusty Bolt", v.Get(TextVars.Place));
            Assert.Equal("Elm Street", v.Get(TextVars.Street));
            Assert.Equal("Old Town", v.Get(TextVars.District));
            Assert.Null(v.Get(TextVars.Place2));
            v.SetPlace(place, true);
            Assert.Equal("Rusty Bolt", v.Get(TextVars.Place2));
            Assert.Equal("Elm Street", v.Get(TextVars.Street2));
            Assert.Equal("Old Town", v.Get(TextVars.District2));
        }

        [Fact]
        public void RichFillsThenParses()
        {
            var v = new TextVars().Set(TextVars.Place, "Quick Stop");
            RichText r = v.Rich("Go to ~y~{place}~s~.");
            Assert.Equal("Go to Quick Stop.", r.Text);
            Assert.Equal(10, r.Spans[0].Length);
        }
    }
}
