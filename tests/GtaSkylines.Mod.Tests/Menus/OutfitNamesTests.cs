using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Mod.Menus;
using Xunit;

namespace GtaSkylines.Mod.Tests.Menus
{
    public class OutfitNamesTests
    {
        [Theory]
        [InlineData("Business Man", 3, "Business")]
        [InlineData("OFFICE worker", 3, "Business")]
        [InlineData("a suit", 0, "Business")]
        [InlineData("Tourist", 3, "Tourist")]
        [InlineData("Police Officer", 3, "Workwear")]
        [InlineData("Garbage", 0, "Workwear")]
        [InlineData("Jogger runner", 3, "Sport")]
        [InlineData("Gym Bro", 3, "Sport")]
        [InlineData("Hipster", 0, "Street")]
        [InlineData("Goth", 3, "Street")]
        [InlineData("Citizen", 0, "Kids")]
        [InlineData("Citizen", 1, "Street")]
        [InlineData("Citizen", 2, "Casual")]
        [InlineData("Citizen", 3, "Casual")]
        [InlineData("Citizen", 4, "Classic")]
        [InlineData("Citizen", 9, "Casual")]
        [InlineData(null, 4, "Classic")]
        public void StyleOf_matches_rules(string name, int age, string expected)
        {
            Assert.Equal(expected, OutfitNames.StyleOf(name, age));
        }

        [Fact]
        public void StyleOf_first_matching_rule_wins()
        {
            Assert.Equal("Business", OutfitNames.StyleOf("tourist suit", 3));
            Assert.Equal("Tourist", OutfitNames.StyleOf("tourist police", 3));
            Assert.Equal("Workwear", OutfitNames.StyleOf("police jogger", 3));
            Assert.Equal("Sport", OutfitNames.StyleOf("jogger punk", 3));
        }

        private static CitizenModel M(string name, int age)
        {
            return new CitizenModel { Name = name, AgeGroup = age };
        }

        [Fact]
        public void Build_numbers_each_style_from_one_in_model_order()
        {
            var models = new List<CitizenModel> { M("Citizen", 2), M("Citizen", 4), M("Business", 2), M("Citizen", 3), M("Citizen", 4) };
            Assert.Equal(new[] { "Casual 1", "Classic 1", "Business 1", "Casual 2", "Classic 2" }, OutfitNames.Build(models));
        }

        [Fact]
        public void Build_null_entry_is_an_unnamed_age_3_citizen_and_null_list_is_empty()
        {
            Assert.Equal(new[] { "Casual 1", "Casual 2" }, OutfitNames.Build(new List<CitizenModel> { null, M("x", 3) }));
            Assert.Empty(OutfitNames.Build(null));
        }

        [Fact]
        public void Build_is_deterministic_and_never_uses_Outfit_N()
        {
            var models = new List<CitizenModel>();
            for (int i = 0; i < 20; i++) models.Add(M("Citizen" + i, i % 5));
            string[] a = OutfitNames.Build(models), b = OutfitNames.Build(models);
            Assert.Equal(a, b);
            Assert.Equal(20, a.Length);
            foreach (string s in a) Assert.False(s.StartsWith("Outfit"), s);
        }
    }
}
