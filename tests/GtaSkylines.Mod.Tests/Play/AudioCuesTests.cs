using GtaSkylines.Game.City;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.World;
using GtaSkylines.Mod.Audio;
using GtaSkylines.Mod.Play;
using Xunit;

namespace GtaSkylines.Mod.Tests.Play
{
    public class AudioCuesTests
    {
        private static GameEvent Ev(GameEventType t, int b = 0, float value = 0f, string text = null)
        {
            return new GameEvent { Type = t, ActorB = b, Value = value, Text = text };
        }

        [Theory]
        [InlineData(GameEventType.Gunshot, AudioCue.Gunshot)]
        [InlineData(GameEventType.Explosion, AudioCue.Explosion)]
        [InlineData(GameEventType.Fell, AudioCue.Land)]
        [InlineData(GameEventType.Purchase, AudioCue.Purchase)]
        [InlineData(GameEventType.PedKilled, AudioCue.None)]
        [InlineData(GameEventType.Bumped, AudioCue.None)]
        public void ForEvent_simple_mappings(GameEventType type, AudioCue expected)
        {
            Assert.Equal(expected, AudioCues.ForEvent(Ev(type), "Bat"));
        }

        [Fact]
        public void Shot_is_an_impact_only_when_it_hit_an_actor()
        {
            Assert.Equal(AudioCue.BulletImpact, AudioCues.ForEvent(Ev(GameEventType.Shot, b: 5), "Bat"));
            Assert.Equal(AudioCue.None, AudioCues.ForEvent(Ev(GameEventType.Shot, b: 0), "Bat"));
        }

        [Fact]
        public void Punched_is_a_bat_hit_only_when_the_text_names_the_bat()
        {
            Assert.Equal(AudioCue.BatHit, AudioCues.ForEvent(Ev(GameEventType.Punched, text: "Bat"), "Bat"));
            Assert.Equal(AudioCue.Punch, AudioCues.ForEvent(Ev(GameEventType.Punched, text: "Fists"), "Bat"));
            Assert.Equal(AudioCue.Punch, AudioCues.ForEvent(Ev(GameEventType.Punched, text: null), "Bat"));
        }

        [Theory]
        [InlineData(GameEventType.CarCollision)]
        [InlineData(GameEventType.PedHitByVehicle)]
        public void Vehicle_impacts_are_silent_below_the_minimum_speed(GameEventType type)
        {
            Assert.Equal(AudioCue.Crash, AudioCues.ForEvent(Ev(type, value: AudioCues.MinCrashSpeed), "Bat"));
            Assert.Equal(AudioCue.Crash, AudioCues.ForEvent(Ev(type, value: 15f), "Bat"));
            Assert.Equal(AudioCue.None, AudioCues.ForEvent(Ev(type, value: AudioCues.MinCrashSpeed - 0.1f), "Bat"));
        }

        [Theory]
        [InlineData(BigScreenKind.Wasted, UiSound.Wasted)]
        [InlineData(BigScreenKind.Busted, UiSound.Busted)]
        [InlineData(BigScreenKind.MissionPassed, UiSound.MissionPassed)]
        [InlineData(BigScreenKind.MissionFailed, UiSound.MissionFailed)]
        public void BigScreen_appearing_plays_its_jingle(BigScreenKind kind, UiSound expected)
        {
            UiSound s;
            Assert.True(AudioCues.ForBigScreen(BigScreenKind.None, kind, out s));
            Assert.Equal(expected, s);
        }

        [Fact]
        public void BigScreen_unchanged_or_cleared_plays_nothing()
        {
            UiSound s;
            Assert.False(AudioCues.ForBigScreen(BigScreenKind.Wasted, BigScreenKind.Wasted, out s));
            Assert.False(AudioCues.ForBigScreen(BigScreenKind.Wasted, BigScreenKind.None, out s));
        }

        [Fact]
        public void WantedUp_and_NewText_fire_only_on_a_rise()
        {
            Assert.True(AudioCues.WantedUp(1, 2));
            Assert.False(AudioCues.WantedUp(2, 2));
            Assert.False(AudioCues.WantedUp(3, 0));
            Assert.True(AudioCues.NewText(0, 1));
            Assert.False(AudioCues.NewText(2, 2));
            Assert.False(AudioCues.NewText(2, 0));
        }

        [Theory]
        [InlineData(VehicleKind.Ambulance, SirenKind.AmbulanceHiLo)]
        [InlineData(VehicleKind.FireTruck, SirenKind.FireHorn)]
        [InlineData(VehicleKind.Police, SirenKind.PoliceWail)]
        [InlineData(VehicleKind.Car, SirenKind.PoliceWail)]
        public void SirenFor_picks_the_pattern_by_kind(VehicleKind kind, SirenKind expected)
        {
            Assert.Equal(expected, AudioCues.SirenFor(kind));
        }

        // ---- FootstepClock -------------------------------------------------------------------------------------

        [Fact]
        public void First_call_never_steps()
        {
            var c = new FootstepClock();
            Assert.False(c.Advance(new Vec3(100, 0, 100), true, 2f));
        }

        [Fact]
        public void Walking_steps_once_per_walk_stride()
        {
            var c = new FootstepClock();
            c.Advance(Vec3.Zero, true, 2f);
            Assert.False(c.Advance(new Vec3(0, 0, FootstepClock.WalkStride - 0.1f), true, 2f));
            Assert.True(c.Advance(new Vec3(0, 0, FootstepClock.WalkStride + 0.1f), true, 2f));
        }

        [Fact]
        public void Running_needs_the_longer_stride()
        {
            var c = new FootstepClock();
            c.Advance(Vec3.Zero, true, 6f);
            Assert.False(c.Advance(new Vec3(0, 0, FootstepClock.WalkStride + 0.1f), true, 6f));
            Assert.True(c.Advance(new Vec3(0, 0, FootstepClock.RunStride + 0.1f), true, 6f));
        }

        [Fact]
        public void Distance_while_off_the_ground_is_not_counted()
        {
            var c = new FootstepClock();
            c.Advance(Vec3.Zero, true, 2f);
            Assert.False(c.Advance(new Vec3(0, 0, 5f), false, 2f));
            Assert.False(c.Advance(new Vec3(0, 0, 5.1f), true, 2f));
        }

        [Fact]
        public void Remainder_carries_over_to_the_next_stride()
        {
            var c = new FootstepClock();
            c.Advance(Vec3.Zero, true, 2f);
            Assert.True(c.Advance(new Vec3(0, 0, 0.9f), true, 2f));
            // 0.2 m left over, so 0.55 m more completes the next 0.7 m stride.
            Assert.True(c.Advance(new Vec3(0, 0, 1.45f), true, 2f));
        }

        [Fact]
        public void Reset_forgets_position_and_distance()
        {
            var c = new FootstepClock();
            c.Advance(Vec3.Zero, true, 2f);
            c.Advance(new Vec3(0, 0, 0.6f), true, 2f);
            c.Reset();
            Assert.False(c.Advance(new Vec3(50, 0, 50), true, 2f));
            Assert.False(c.Advance(new Vec3(50, 0, 50.2f), true, 2f));
        }
    }
}
