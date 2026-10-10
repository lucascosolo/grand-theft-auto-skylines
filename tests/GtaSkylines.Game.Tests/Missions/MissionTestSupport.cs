using System;
using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Interiors;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Missions;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Roads;
using GtaSkylines.Game.Session;
using GtaSkylines.Game.Settings;
using GtaSkylines.Game.Tests.Fakes;
using GtaSkylines.Game.Tests.Roads;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Tests.Missions
{
    /// <summary>Named buildings of the test cities, so tests can assert that texts contain the chosen names.</summary>
    internal sealed class TestCity
    {
        public FakeCity City = new FakeCity();
        public ushort Garage, Garage2, Warehouse, Industrial2, Police, Hospital, CityHall, Store, Store2, Office, Home1, Home2, Home3, Shop2;

        private ushort _nextId = 10;

        /// <summary>Adds a building whose real interior archetype is <paramref name="archetype"/> (searches ids until it matches).</summary>
        public ushort Add(ServiceKind service, InteriorArchetype archetype, string name, float x, float z, byte district = 0)
        {
            for (int guard = 0; guard < 5000; guard++)
            {
                var b = Interiors.InteriorTestSupport.Building(_nextId++, service, "");
                if (InteriorGenerator.ArchetypeFor(b) != archetype) continue;
                b.Name = name;
                b.Position = new Vec3(x, 0f, z);
                b.Doors = new[] { new Vec3(x, 0f, z + 8f) };
                b.District = district;
                City.AddBuilding(b);
                return b.Id;
            }
            throw new InvalidOperationException("no building id gives archetype " + archetype);
        }

        public static void Common(FakeCity c)
        {
            c.Name = "Testville";
            c.Districts[1] = "Old Town";
            c.Districts[2] = "Harbor";
            c.DistrictFunc = (x, z) => x < 1000f ? (byte)1 : (byte)2;
            c.StreetFunc = p => "Main Street";
            c.Profiles[77] = new CitizenProfile { Citizen = 77, Name = "Ada Kowalski", Age = 40, Happiness = 50, Health = 20, Sick = true };
        }

        /// <summary>A city with every service the campaign prefers, spread over about 2 km, two districts.</summary>
        public static TestCity Full()
        {
            var t = new TestCity();
            Common(t.City);
            t.Garage = t.Add(ServiceKind.Industrial, InteriorArchetype.Garage, "Rusty Bolt Motors", 600, 600, 1);
            t.Garage2 = t.Add(ServiceKind.Industrial, InteriorArchetype.Garage, "Second Gear Garage", 2400, 2200, 2);
            t.Warehouse = t.Add(ServiceKind.Industrial, InteriorArchetype.Warehouse, "Dockside Warehouse", 1500, 300, 2);
            t.Industrial2 = t.Add(ServiceKind.Industrial, InteriorArchetype.Warehouse, "Steelworks Annex", 1700, 1200, 2);
            t.Police = t.Add(ServiceKind.Police, InteriorArchetype.PoliceStation, "Central Precinct", 700, 500, 1);
            t.Hospital = t.Add(ServiceKind.Health, InteriorArchetype.Hospital, "St. Mercy Hospital", 300, 900, 1);
            t.CityHall = t.Add(ServiceKind.Government, InteriorArchetype.ServiceHall, "Testville City Hall", 650, 650, 1);
            t.Store = t.Add(ServiceKind.Commercial, InteriorArchetype.ConvenienceStore, "Quick Stop Market", 500, 700, 1);
            t.Store2 = t.Add(ServiceKind.Commercial, InteriorArchetype.ConvenienceStore, "Corner Pantry", 1400, 900, 2);
            t.Shop2 = t.Add(ServiceKind.Commercial, InteriorArchetype.ClothesShop, "Threadbare Boutique", 1300, 1000, 2);
            t.Office = t.Add(ServiceKind.Office, InteriorArchetype.Office, "Meridian Tower", 800, 800, 1);
            t.Home1 = t.Add(ServiceKind.Residential, InteriorArchetype.House, "Elm House", 450, 550, 1);
            t.Home2 = t.Add(ServiceKind.Residential, InteriorArchetype.House, "Harbor Cottage", 1450, 500, 2);
            t.Home3 = t.Add(ServiceKind.Residential, InteriorArchetype.House, "Ridge House", 1000, 1500, 2);
            return t;
        }

        /// <summary>A city with homes and two shops only: no police station, hospital, city hall or industry.</summary>
        public static TestCity Sparse()
        {
            var t = new TestCity();
            Common(t.City);
            t.Store = t.Add(ServiceKind.Commercial, InteriorArchetype.ConvenienceStore, "Lonely Mart", 500, 500, 1);
            t.Shop2 = t.Add(ServiceKind.Commercial, InteriorArchetype.ClothesShop, "Far Fashions", 1300, 900, 2);
            t.Home1 = t.Add(ServiceKind.Residential, InteriorArchetype.House, "Elm House", 400, 450, 1);
            t.Home2 = t.Add(ServiceKind.Residential, InteriorArchetype.House, "Harbor Cottage", 1350, 600, 2);
            t.Home3 = t.Add(ServiceKind.Residential, InteriorArchetype.House, "Ridge House", 900, 1300, 1);
            return t;
        }

        /// <summary>A city with no buildings at all.</summary>
        public static TestCity Empty()
        {
            var t = new TestCity();
            Common(t.City);
            return t;
        }

        /// <summary>A 15 x 15 two-way grid of roads, 200 m apart (2.8 km square).</summary>
        public static Net Grid() { return Nets.Grid(15, 200f, 14f); }
    }

    /// <summary>A running session with the G9 system installed, for scenario tests.</summary>
    internal sealed class Scene
    {
        public const uint Seed = 4242u;
        public TestCity Test;
        public FakeCity City;
        public GameSession Session;
        public GameWorld World;
        public MissionRunner Runner;
        public PlayerProfile Profile;
        public CitySnapshot Snapshot = new CitySnapshot();
        public InputFrame Input;
        /// <summary>Every city effect produced since the scene was created.</summary>
        public readonly List<CityEffect> AllEffects = new List<CityEffect>();

        public static Scene Create(TestCity test = null, Action<PlayerProfile> setup = null, bool roads = true)
        {
            var s = new Scene();
            s.Test = test ?? TestCity.Full();
            s.City = s.Test.City;
            s.Profile = new PlayerProfile();
            if (setup != null) setup(s.Profile);
            s.Session = new GameSession(s.City, new GameSettings(), s.Profile, Seed);
            s.World = s.Session.World;
            if (roads)
            {
                Net net = TestCity.Grid();
                s.World.Roads.Rebuild(net.Nodes, net.Segments, false);
            }
            s.Runner = new MissionRunner(s.World, Seed);
            s.Session.AddSystem(s.Runner);
            s.Session.Begin(new Vec3(100f, 0f, 100f), 0f);
            return s;
        }

        /// <summary>Runs <paramref name="steps"/> fixed steps with the current <see cref="Input"/> (pressed edges only on the first).</summary>
        public void Tick(int steps = 1)
        {
            for (int i = 0; i < steps; i++)
            {
                InputFrame f = Input;
                if (i > 0) f.Pressed = 0;
                Session.Tick(GameSession.FixedDt, f, Snapshot);
                foreach (CityEffect e in Session.Effects) AllEffects.Add(e);
            }
            Input.Pressed = 0;
        }

        public void Seconds(float seconds) { Tick((int)Math.Ceiling(seconds / GameSession.FixedDt)); }

        /// <summary>One step with a button pressed (and held) once.</summary>
        public void Press(GameButton b)
        {
            Input.Pressed = InputFrame.Bit(b);
            Input.Held |= InputFrame.Bit(b);
            Tick(1);
            Input.Held &= ~InputFrame.Bit(b);
            Input.Pressed = 0;
        }

        public void Hold(GameButton b, bool on)
        {
            if (on) Input.Held |= InputFrame.Bit(b); else Input.Held &= ~InputFrame.Bit(b);
        }

        public void Teleport(Vec3 feet) { World.Player.Motor.Teleport(feet); }

        /// <summary>Puts the player in the driver seat of a new Player-owned car of the kind, at <paramref name="ground"/>.</summary>
        public Car Drive(VehicleKind kind, Vec3 ground)
        {
            Car car = World.SpawnCar(City.FindVehicleModel(kind, 0), ground, 0f, CarOwner.Player, 0x336699);
            World.PutInCar(World.Player, car, 0);
            return car;
        }

        public Car Spawn(VehicleKind kind, Vec3 ground, CarOwner owner = CarOwner.Civilian)
        {
            return World.SpawnCar(City.FindVehicleModel(kind, 0), ground, 0f, owner, 0x884422);
        }

        public Vec3 Door(ushort building) { return City.Buildings[building].Doors[0]; }

        /// <summary>Steps once so the runner generates the campaign, then returns it.</summary>
        public Campaign Prime()
        {
            Tick(1);
            return Runner.Campaign;
        }

        /// <summary>Replaces mission <paramref name="id"/> in the campaign with a hand-made definition (same id, title, contact, reward) and returns it.</summary>
        public MissionDefinition Replace(string id, params MissionStep[] steps)
        {
            Campaign c = Prime();
            int i = Array.IndexOf(MissionIds.All, id);
            MissionDefinition old = c.Missions[i];
            var def = new MissionDefinition
            {
                Id = id, Title = old.Title, Contact = old.Contact, Requires = old.Requires, StartPlace = old.StartPlace,
                StartMarker = old.StartMarker, Steps = steps, Reward = old.Reward, UnlockText = old.UnlockText,
            };
            c.Missions[i] = def;
            return def;
        }

        public int CountEffects(CityEffectKind kind)
        {
            int n = 0;
            foreach (CityEffect e in AllEffects) if (e.Kind == kind) n++;
            return n;
        }

        /// <summary>A fresh step context bound to the world (no definition: a side-job style run).</summary>
        public MissionContext Context()
        {
            var ctx = new MissionContext();
            ctx.Reset(World, null);
            return ctx;
        }

        /// <summary>Begins <paramref name="step"/> in a fresh context and returns the context.</summary>
        public MissionContext Begin(MissionStep step)
        {
            MissionContext ctx = Context();
            step.Begin(ctx);
            return ctx;
        }

        /// <summary>Steps <paramref name="step"/> once with <paramref name="dt"/>, advancing the context's timers like the runner does.</summary>
        public StepState StepOnce(MissionStep step, MissionContext ctx, float dt = 1f / 60f)
        {
            ctx.StepSeconds += dt;
            ctx.RunSeconds += dt;
            return step.Step(ctx, dt);
        }

        /// <summary>Puts one event into the context for the next <see cref="StepOnce"/>.</summary>
        public static void Event(MissionContext ctx, GameEventType type, int a, int b, float value = 0f, string text = null)
        {
            ctx.Events[ctx.EventCount++] = new GameEvent { Type = type, ActorA = a, ActorB = b, Value = value, Text = text };
        }
    }

    internal static class Txt
    {
        public static bool Has(string text, string part) { return text != null && text.IndexOf(part, StringComparison.Ordinal) >= 0; }

        /// <summary>True when no known template placeholder is left in the text.</summary>
        public static bool Filled(string text)
        {
            string[] keys = { "{contact", "{place", "{street", "{district", "{person", "{vehicle", "{city", "{number" };
            foreach (string k in keys) if (Has(text, k)) return false;
            return true;
        }
    }
}
