using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Physics;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Session;
using GtaSkylines.Game.Settings;
using GtaSkylines.Game.Tests.Fakes;
using GtaSkylines.Game.Tests.PhysicsTests;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Tests.Combat
{
    /// <summary>
    /// A flat 600 m square of ground at y = 0 with a GameSession (and so a GameWorld) on it. The player stands far to the
    /// south (z = -250) unless a test moves him. Shared by the Combat and Peds tests.
    /// </summary>
    internal sealed class Arena
    {
        public const float Dt = 1f / 60f;

        public readonly FakeCity City = new FakeCity();
        public readonly PlayerProfile Profile = new PlayerProfile();
        public readonly GameSession Session;
        public readonly GameWorld World;
        private long _nextRegion = 10;

        public Arena(uint seed = 1234u)
        {
            Session = new GameSession(City, new GameSettings(), Profile, seed);
            World = Session.World;
            new MeshBuilder().Floor(-300f, -300f, 300f, 300f, 0f).Apply(World.Collision, 1);
            Session.Begin(new Vec3(0f, 0f, -250f), 0f);
        }

        public Ped Player { get { return World.Player; } }

        /// <summary>A civilian ped standing at (x, 0, z) facing +Z unless a yaw is given.</summary>
        public Ped Spawn(float x, float z, float yaw = 0f, PedKind kind = PedKind.Civilian)
        {
            return World.SpawnPed(kind, 3, new Vec3(x, 0f, z), yaw);
        }

        /// <summary>A vertical 5 m high wall along the segment.</summary>
        public void Wall(float x0, float z0, float x1, float z1)
        {
            new MeshBuilder().Wall(x0, z0, x1, z1, 0f, 5f).Apply(World.Collision, _nextRegion++);
        }

        /// <summary>An ambient CS1 citizen at (x, 0, z); the capsule is rebuilt.</summary>
        public AmbientCitizen AddAmbient(ushort instance, uint citizen, float x, float z, float yaw = 0f)
        {
            var c = new AmbientCitizen { Instance = instance, Citizen = citizen, Position = new Vec3(x, 0f, z), YawDeg = yaw, Model = 3 };
            World.Ambient.Citizens.Add(c);
            World.RebuildDynamicShapes(true);
            return c;
        }

        public void Rebuild() { World.RebuildDynamicShapes(true); }

        public Vec3 Chest(Ped p) { return p.Position + new Vec3(0f, 1.2f, 0f); }

        public static InputFrame Held(params GameButton[] buttons)
        {
            var f = new InputFrame();
            foreach (var b in buttons) f.Held |= InputFrame.Bit(b);
            return f;
        }

        public static InputFrame HeldAndPressed(params GameButton[] buttons)
        {
            var f = Held(buttons);
            f.Pressed = f.Held;
            return f;
        }

        public List<GameEvent> EventsSince(long seq)
        {
            var list = new List<GameEvent>();
            long cursor = seq;
            GameEvent e;
            while (World.Events.Read(ref cursor, out e)) list.Add(e);
            return list;
        }

        public static int Count(IEnumerable<GameEvent> events, GameEventType type)
        {
            int n = 0;
            foreach (var e in events) if (e.Type == type) n++;
            return n;
        }

        public int CountEffects(CityEffectKind kind)
        {
            int n = 0;
            foreach (var e in World.Effects.Items) if (e.Kind == kind) n++;
            return n;
        }

        /// <summary>Runs a ped system for the given seconds at 60 Hz.</summary>
        public void Run(IGameSystem system, float seconds)
        {
            int n = (int)(seconds / Dt + 0.5f);
            for (int i = 0; i < n; i++) system.Step(World, Dt);
        }
    }
}
