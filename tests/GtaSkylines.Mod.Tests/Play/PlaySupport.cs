using System.Collections.Generic;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Physics;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Session;
using GtaSkylines.Game.Settings;
using GtaSkylines.Game.Tests.Fakes;
using GtaSkylines.Game.World;
using GtaSkylines.Mod.Play;

namespace GtaSkylines.Mod.Tests.Play
{
    /// <summary>A running game on a flat floor at y = 0 over a <see cref="FakeCity"/>.</summary>
    internal sealed class PlayGame
    {
        public readonly FakeCity City = new FakeCity();
        public readonly PlayerProfile Profile = new PlayerProfile();
        public readonly GameParts Parts;

        public PlayGame(bool begin = true)
        {
            Parts = GameFactory.Create(City, new GameSettings(), Profile, 12345u);
            const float h = 400f;
            float[] p =
            {
                -h, 0, -h, h, 0, -h, h, 0, h,
                -h, 0, -h, h, 0, h, -h, 0, h,
            };
            var flags = new ushort[] { (ushort)SurfaceFlags.Terrain, (ushort)SurfaceFlags.Terrain };
            Parts.World.Collision.SetRegion(7001, p, flags, 2);
            if (begin)
            {
                Parts.Session.Begin(new Vec3(350f, 0f, 350f), 0f);
                Tick(20);
            }
        }

        public GameWorld W { get { return Parts.World; } }
        public Ped Player { get { return Parts.World.Player; } }

        public void Tick(int steps = 1)
        {
            for (int i = 0; i < steps; i++) Parts.Session.Tick(GameSession.FixedDt, default(InputFrame), null);
        }

        public Car SpawnCar(int model, Vec3 at, int colour = 0x112233)
        {
            return W.SpawnCar(model, at, 0f, CarOwner.Civilian, colour);
        }
    }

    internal sealed class RecordingHost : IPlayHost
    {
        public readonly List<string> Calls = new List<string>();
        public void Resume() { Calls.Add("Resume"); }
        public void ApplySettings() { Calls.Add("ApplySettings"); }
        public void SaveGame() { Calls.Add("SaveGame"); }
        public void ReturnToCityBuilder() { Calls.Add("ReturnToCityBuilder"); }
    }

    internal sealed class FakeGate : IPromotionGate
    {
        public bool Cars = true, Peds = true;
        public bool CarReady(Car car) { return Cars; }
        public bool PedReady(Ped ped) { return Peds; }
    }
}
