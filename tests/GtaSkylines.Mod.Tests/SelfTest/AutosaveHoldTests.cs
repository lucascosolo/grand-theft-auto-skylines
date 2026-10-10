using System;
using GtaSkylines.Mod.SelfTest;
using Xunit;

namespace GtaSkylines.Mod.Tests
{
    public class AutosaveHoldTests
    {
        private int _pauses;
        private int _unpauses;

        private AutosaveHold Make() { return new AutosaveHold(() => _pauses++, () => _unpauses++); }

        [Fact]
        public void Null_arguments_throw()
        {
            Assert.Throws<ArgumentNullException>(() => new AutosaveHold(null, () => { }));
            Assert.Throws<ArgumentNullException>(() => new AutosaveHold(() => { }, null));
        }

        [Fact]
        public void New_hold_is_not_held_and_calls_nothing()
        {
            AutosaveHold h = Make();
            Assert.False(h.Held);
            Assert.Equal(0, _pauses);
            Assert.Equal(0, _unpauses);
        }

        [Fact]
        public void Hold_pauses_exactly_once()
        {
            AutosaveHold h = Make();
            Assert.True(h.Hold());
            Assert.True(h.Held);
            Assert.False(h.Hold());
            Assert.False(h.Hold());
            Assert.Equal(1, _pauses);
            Assert.Equal(0, _unpauses);
        }

        [Fact]
        public void Release_without_hold_does_nothing()
        {
            AutosaveHold h = Make();
            Assert.False(h.Release());
            Assert.False(h.Held);
            Assert.Equal(0, _unpauses);
        }

        [Fact]
        public void Release_unpauses_exactly_once()
        {
            AutosaveHold h = Make();
            h.Hold();
            Assert.True(h.Release());
            Assert.False(h.Held);
            Assert.False(h.Release());
            Assert.Equal(1, _pauses);
            Assert.Equal(1, _unpauses);
        }

        [Fact]
        public void Repeated_sequences_stay_balanced()
        {
            AutosaveHold h = Make();
            for (int i = 0; i < 5; i++)
            {
                h.Hold(); h.Hold(); h.Release(); h.Release(); h.Release();
            }
            Assert.Equal(5, _pauses);
            Assert.Equal(5, _unpauses);
            h.Hold();
            Assert.True(h.Held);
            h.Release();
            Assert.Equal(_pauses, _unpauses);
        }
    }
}
