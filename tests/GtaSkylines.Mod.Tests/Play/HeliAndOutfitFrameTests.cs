using GtaSkylines.Game.Mathx;
using GtaSkylines.Mod.Play;
using GtaSkylines.Mod.Rendering;
using Xunit;

namespace GtaSkylines.Mod.Tests.Play
{
    public class HeliAndOutfitFrameTests
    {
        private static RenderFrame Fill(WorldFrameBuilder b, PlayGame g, float dt = 0.1f)
        {
            var f = new RenderFrame();
            b.Fill(f, g.Parts, dt);
            return f;
        }

        [Fact]
        public void No_helicopter_entry_while_none_is_flying()
        {
            var g = new PlayGame();
            Assert.False(Fill(new WorldFrameBuilder(), g).Heli.Visible);
        }

        [Fact]
        public void A_flying_helicopter_is_emitted_with_model_pose_and_spotlight()
        {
            var g = new PlayGame();
            g.Parts.Police.Helicopter.Spawn(g.W, 7, new Vec3(100, 40, 50), 90f);
            g.Parts.Police.Helicopter.SpotlightTarget = new Vec3(100, 0, 60);
            g.Parts.Police.Helicopter.Velocity = new Vec3(10, 0, 0);
            var b = new WorldFrameBuilder();
            HeliDraw h = Fill(b, g).Heli;
            Assert.True(h.Visible);
            Assert.Equal(7, h.ModelId);
            Assert.Equal(new Vec3(100, 40, 50), h.Position);
            Assert.Equal(90f, h.YawDeg);
            Assert.True(h.Spotlight);
            Assert.Equal(new Vec3(100, 0, 60), h.SpotlightTarget);
            Assert.True(h.PrevPosition.X < h.Position.X);
            float first = h.RotorTravel;
            Assert.True(Fill(b, g).Heli.RotorTravel > first);
        }

        [Fact]
        public void An_inactive_helicopter_or_one_without_a_model_is_not_emitted()
        {
            var g = new PlayGame();
            g.Parts.Police.Helicopter.Spawn(g.W, -1, new Vec3(100, 40, 50), 0f);
            Assert.False(Fill(new WorldFrameBuilder(), g).Heli.Visible);
            g.Parts.Police.Helicopter.Spawn(g.W, 3, new Vec3(100, 40, 50), 0f);
            g.Parts.Police.Helicopter.Active = false;
            Assert.False(Fill(new WorldFrameBuilder(), g).Heli.Visible);
        }

        [Fact]
        public void Changing_the_player_model_changes_the_drawn_model_on_the_next_frame()
        {
            var g = new PlayGame();
            g.Parts.Session.Camera.FootView = GtaSkylines.Game.Camera.FootView.ThirdMid;
            g.Tick();
            var b = new WorldFrameBuilder();
            int before = Fill(b, g).Peds.Find(p => p.Key == g.Player.Id).ModelId;
            int next = before == 2 ? 3 : 2;
            g.Player.Model = next;
            Assert.Equal(next, Fill(b, g).Peds.Find(p => p.Key == g.Player.Id).ModelId);
        }
    }
}
