using System;
using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Interiors;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Physics;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Session;
using GtaSkylines.Game.Settings;
using GtaSkylines.Game.Tests.Fakes;
using GtaSkylines.Game.Tests.PhysicsTests;
using GtaSkylines.Game.Tests.Roads;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.SessionTests
{
    /// <summary>Scripted input frames. Held buttons stay in Held; "Pressed" also sets the edge bit for that one frame.</summary>
    internal static class In
    {
        public static InputFrame Idle { get { return default(InputFrame); } }

        public static InputFrame Move(float x, float y, params GameButton[] held)
        {
            var f = new InputFrame { MoveX = x, MoveY = y };
            foreach (GameButton b in held) f.Held |= InputFrame.Bit(b);
            return f;
        }

        public static InputFrame Forward { get { return Move(0f, 1f); } }

        public static InputFrame Hold(params GameButton[] held) { return Move(0f, 0f, held); }

        public static InputFrame Press(params GameButton[] pressed)
        {
            var f = new InputFrame();
            foreach (GameButton b in pressed) { f.Held |= InputFrame.Bit(b); f.Pressed |= InputFrame.Bit(b); }
            return f;
        }

        public static InputFrame Look(float yawDeg, float pitchDeg)
        {
            return new InputFrame { LookYawDeg = yawDeg, LookPitchDeg = pitchDeg };
        }

        public static InputFrame WithPress(this InputFrame f, GameButton b)
        {
            f.Held |= InputFrame.Bit(b);
            f.Pressed |= InputFrame.Bit(b);
            return f;
        }

        public static InputFrame WithHold(this InputFrame f, params GameButton[] held)
        {
            foreach (GameButton b in held) f.Held |= InputFrame.Bit(b);
            return f;
        }

        /// <summary>The same frame as a later frame of a held input: no edges, no look delta.</summary>
        public static InputFrame Continued(this InputFrame f)
        {
            f.Pressed = 0; f.Released = 0; f.LookYawDeg = 0; f.LookPitchDeg = 0;
            return f;
        }
    }

    /// <summary>
    /// A headless session on a <see cref="FakeCity"/>: a flat floor at y = 0 (±<see cref="FloorHalf"/> m) in the collision
    /// world, a straight road along +z from the origin, scripted input at the fixed step, and logs of every event and
    /// city effect seen so far.
    /// </summary>
    internal sealed class Sim
    {
        public const float Dt = GameSession.FixedDt;
        public const float FloorHalf = 400f;
        public const long FloorKey = 7001;

        public readonly FakeCity City;
        public readonly GameSettings Settings;
        public readonly PlayerProfile Profile;
        public readonly GameSession S;
        public readonly CitySnapshot Snap = new CitySnapshot();
        public readonly List<CityEffect> Effects = new List<CityEffect>();
        public readonly List<GameEvent> Log = new List<GameEvent>();
        private long _cursor = 1;

        public Sim(Action<FakeCity> city = null, Action<GameSettings> settings = null, Action<PlayerProfile> profile = null,
            bool begin = true, Vec3? feet = null, float yaw = 0f, uint seed = 12345u, bool road = true)
        {
            City = new FakeCity();
            if (city != null) city(City);
            Settings = new GameSettings();
            if (settings != null) settings(Settings);
            Profile = new PlayerProfile();
            if (profile != null) profile(Profile);
            S = new GameSession(City, Settings, Profile, seed);
            new MeshBuilder().Floor(-FloorHalf, -FloorHalf, FloorHalf, FloorHalf, 0f).Apply(S.World.Collision, FloorKey);
            if (road)
            {
                Net n = Nets.Straight(400f);
                S.World.Roads.Rebuild(n.Nodes, n.Segments, false);
            }
            if (begin) Begin(feet ?? Vec3.Zero, yaw);
        }

        public GameWorld W { get { return S.World; } }
        public Ped Player { get { return S.World.Player; } }
        public PlayerState PS { get { return S.World.PlayerState; } }

        public void Begin(Vec3 feet, float yaw)
        {
            S.Begin(feet, yaw);
            Run(0.3, In.Idle);
        }

        public void Tick(InputFrame f, float dt = Dt)
        {
            S.Tick(dt, f, Snap);
            Collect();
        }

        private void Collect()
        {
            IList<CityEffect> fx = S.Effects;
            for (int i = 0; i < fx.Count; i++) Effects.Add(fx[i]);
            GameEvent e;
            while (S.World.Events.Read(ref _cursor, out e)) Log.Add(e);
        }

        /// <summary>Runs whole seconds of fixed ticks; the frame's edges apply to the first tick only.</summary>
        public void Run(double seconds, InputFrame f)
        {
            int n = (int)Math.Round(seconds * 60.0);
            for (int i = 0; i < n; i++) Tick(i == 0 ? f : f.Continued());
        }

        /// <summary>Runs until the condition holds; returns whether it did within <paramref name="maxSeconds"/>.</summary>
        public bool Until(Func<bool> cond, double maxSeconds, InputFrame f)
        {
            int n = (int)Math.Round(maxSeconds * 60.0);
            for (int i = 0; i < n; i++)
            {
                if (cond()) return true;
                Tick(i == 0 ? f : f.Continued());
            }
            return cond();
        }

        public int Count(GameEventType t)
        {
            int c = 0;
            foreach (GameEvent e in Log) if (e.Type == t) c++;
            return c;
        }

        public GameEvent Last(GameEventType t)
        {
            for (int i = Log.Count - 1; i >= 0; i--) if (Log[i].Type == t) return Log[i];
            Assert.Fail("no " + t + " event was emitted");
            return default(GameEvent);
        }

        public int CountEffects(CityEffectKind k)
        {
            int c = 0;
            foreach (CityEffect e in Effects) if (e.Kind == k) c++;
            return c;
        }

        public CityEffect FirstEffect(CityEffectKind k)
        {
            foreach (CityEffect e in Effects) if (e.Kind == k) return e;
            Assert.Fail("no " + k + " effect was produced");
            return default(CityEffect);
        }

        /// <summary>Teleports the player and turns the body and the view to <paramref name="yawDeg"/>.</summary>
        public void Place(Vec3 feet, float yawDeg)
        {
            Player.Motor.Teleport(feet);
            Player.YawDeg = yawDeg;
            PS.ViewYawDeg = yawDeg;
            PS.ViewPitchDeg = 0f;
        }

        // ---- snapshot builders -----------------------------------------------------------------------------------

        public AmbientVehicle AddParkedCar(ushort id, Vec3 centre, float yawDeg, int model = 0, int colour = 0x3366CC)
        {
            var v = new AmbientVehicle { Id = id, Parked = true, Position = centre, Rotation = Quat.Yaw(yawDeg), Model = model, Kind = City.GetVehicleModel(model).Kind, ColorRgb = colour };
            Snap.Vehicles.Add(v);
            return v;
        }

        public AmbientVehicle AddMovingCar(ushort id, Vec3 centre, float yawDeg, Vec3 velocity, uint driver, int model = 0, int colour = 0xAA2222)
        {
            var v = new AmbientVehicle { Id = id, Parked = false, Position = centre, Rotation = Quat.Yaw(yawDeg), Velocity = velocity, Model = model, Kind = City.GetVehicleModel(model).Kind, DriverCitizen = driver, ColorRgb = colour };
            Snap.Vehicles.Add(v);
            return v;
        }

        public AmbientCitizen AddCitizen(ushort instance, uint citizen, Vec3 feet, int model = 3, float yawDeg = 0f)
        {
            var c = new AmbientCitizen { Instance = instance, Citizen = citizen, Position = feet, YawDeg = yawDeg, Model = model };
            Snap.Citizens.Add(c);
            return c;
        }

        // ---- buildings and interiors -----------------------------------------------------------------------------

        /// <summary>A building whose front (local +z) faces <paramref name="yawDeg"/>, with one door at the front centre.</summary>
        public static BuildingDescriptor Building(int id, ServiceKind service, string sub, Vec3 pos, float yawDeg, string name = null, float level = 1)
        {
            var b = new BuildingDescriptor
            {
                Id = (ushort)id, Name = name ?? ("Building " + id), InfoName = "Prefab" + id, Service = service, SubService = sub,
                Level = (int)level, Position = pos, YawDeg = yawDeg, Width = 16f, Length = 16f, Height = 10f,
            };
            b.Doors = new[] { pos + Quat.Yaw(yawDeg) * new Vec3(0f, 0f, b.Length * 0.5f) };
            return b;
        }

        /// <summary>The first building id from <paramref name="firstId"/> whose generated archetype is the wanted one.</summary>
        public static BuildingDescriptor FindBuilding(InteriorArchetype want, ServiceKind service, string sub, Vec3 pos, float yawDeg, int firstId = 10, string name = null)
        {
            for (int id = firstId; id < firstId + 3000; id++)
            {
                BuildingDescriptor b = Building(id, service, sub, pos, yawDeg, name);
                if (InteriorGenerator.ArchetypeFor(b) == want) return b;
            }
            throw new InvalidOperationException("no building of archetype " + want);
        }

        /// <summary>World position of the first interaction of a kind in the loaded interior.</summary>
        public Vec3 InteractionWorld(InteractionKind kind)
        {
            InteriorLayout l = W.Interior.Layout;
            Assert.NotNull(l);
            for (int i = 0; i < l.Interactions.Count; i++)
                if (l.Interactions[i].Kind == kind) return W.Interior.ToWorld(l.Interactions[i].Position);
            Assert.Fail("layout has no " + kind);
            return Vec3.Zero;
        }

        /// <summary>Stands the player on an interaction point of the loaded interior, facing in, ready to press Interact.</summary>
        public void GoTo(InteractionKind kind)
        {
            Vec3 p = InteractionWorld(kind);
            Place(p, PS.ViewYawDeg);
            Run(0.2, In.Idle);
        }

        /// <summary>Puts the player at the building door facing it and presses Interact; returns after the transition ended.</summary>
        public void EnterBuilding(BuildingDescriptor b)
        {
            Vec3 door = b.Doors[0];
            Vec3 outward = (door - b.Position).Flat.Normalized;
            Place(door + outward * 1.2f, Vec3.YawDegOf(-outward));
            Run(0.1, In.Idle);
            Tick(In.Press(GameButton.Interact));
            Assert.True(Until(() => W.Interior.Phase == InteriorPhase.Inside, 3.0, In.Idle), "never reached Inside; phase " + W.Interior.Phase);
        }

        public static void Near(float expected, float actual, float tol, string what = null)
        {
            Assert.True(Math.Abs(expected - actual) <= tol, (what ?? "value") + ": expected " + expected + " +/- " + tol + " but was " + actual);
        }

        public static void Near(Vec3 expected, Vec3 actual, float tol, string what = null)
        {
            Assert.True((expected - actual).Length <= tol, (what ?? "position") + ": expected " + expected + " +/- " + tol + " but was " + actual);
        }

        public static float AngleDiff(float a, float b)
        {
            float d = (a - b) % 360f;
            if (d > 180f) d -= 360f;
            if (d < -180f) d += 360f;
            return Math.Abs(d);
        }

        /// <summary>Strips the typographic minus so "−$5" and "-$5" compare equal.</summary>
        public static string Ascii(string s) { return s == null ? null : s.Replace('−', '-'); }
    }
}
