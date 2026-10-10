using System.Collections.Generic;
using GtaSkylines.Game.Settings;
using GtaSkylines.Mod.Input;
using Xunit;

namespace GtaSkylines.Mod.Tests
{
    public class KeyNamesTests
    {
        [Fact]
        public void All_has_78_distinct_valid_names()
        {
            Assert.Equal(78, KeyNames.All.Length);
            Assert.Equal(78, new HashSet<string>(KeyNames.All).Count);
            foreach (string n in KeyNames.All) Assert.True(KeyBindings.IsValidKeyName(n), n);
        }

        [Theory]
        [InlineData("A")]
        [InlineData("Keypad9")]
        [InlineData("F12")]
        [InlineData("Mouse2")]
        [InlineData("RightArrow")]
        [InlineData("WheelDown")]
        public void All_contains_known_names(string name)
        {
            Assert.Contains(name, KeyNames.All);
        }

        [Theory]
        [InlineData(null, KeySource.None)]
        [InlineData("", KeySource.None)]
        [InlineData("WheelUp", KeySource.WheelUp)]
        [InlineData("WheelDown", KeySource.WheelDown)]
        [InlineData("W", KeySource.Key)]
        [InlineData("Mouse1", KeySource.Key)]
        [InlineData("LeftShift", KeySource.Key)]
        [InlineData("w", KeySource.None)]
        [InlineData("Mouse3", KeySource.None)]
        [InlineData("Foo", KeySource.None)]
        public void Classify(string name, KeySource expected)
        {
            Assert.Equal(expected, KeyNames.Classify(name));
        }

        [Fact]
        public void Classify_every_listed_name_is_not_None()
        {
            foreach (string n in KeyNames.All) Assert.NotEqual(KeySource.None, KeyNames.Classify(n));
        }
    }
}
