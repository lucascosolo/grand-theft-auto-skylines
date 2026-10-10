using System;
using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Crime;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Police;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.Settings;
using GtaSkylines.Game.Tests.Fakes;
using GtaSkylines.Game.Roads;
using GtaSkylines.Game.Tests.Roads;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Tests.CrimeTests
{
    /// <summary>A scripted G7 stand-in: witnesses, sight, call brains the harness steps, recorded Fire calls.</summary>
    internal sealed class FakePedServices : ICrimePedServices
    {
        public struct FireCall { public int ShooterId; public Vec3 Origin; public Vec3 Direction; public double Time; }

        public sealed class CallRecord
        {
            public int PedId, CrimeId;
            public float CallSeconds;
            public FakeCallBrain Brain;
        }

        /// <summary>Returned (nearest first) for every event.</summary>
        public readonly List<CrimeWitness> Witnesses = new List<CrimeWitness>();
        /// <summary>Peds that CanSee the player (by ped id).</summary>
        public readonly HashSet<int> Seeing = new HashSet<int>();
        public readonly List<CallRecord> Calls = new List<CallRecord>();
        public readonly List<FireCall> Fires = new List<FireCall>();

        public int WitnessesOf(GameWorld world, GameEvent e, List<CrimeWitness> into)
        {
            into.Clear();
            for (int i = 0; i < Witnesses.Count; i++)
            {
                CrimeWitness w = Witnesses[i];
                w.Distance = Vec3.DistanceXZ(w.Position, e.Position);
                into.Add(w);
            }
            return into.Count;
        }

        public bool CanSee(GameWorld world, Ped observer, Vec3 point) { return Seeing.Contains(observer.Id); }

        public IPedBrain CreateCallPolice(GameWorld world, Ped witness, int crimeId, float callSeconds)
        {
            var b = new FakeCallBrain { CrimeId = crimeId, Seconds = callSeconds };
            Calls.Add(new CallRecord { PedId = witness.Id, CrimeId = crimeId, CallSeconds = callSeconds, Brain = b });
            return b;
        }

        public void Fire(GameWorld world, Ped shooter, Vec3 origin, Vec3 direction)
        {
            Fires.Add(new FireCall { ShooterId = shooter.Id, Origin = origin, Direction = direction, Time = world.Clock.GameSeconds });
        }

        public void InterruptAll() { foreach (CallRecord c in Calls) c.Brain.Interrupted = true; }
    }

    /// <summary>CallStarted on the first step, CallCompleted after Seconds, or CallInterrupted when interrupted.</summary>
    internal sealed class FakeCallBrain : IPedBrain
    {
        public int CrimeId;
        public float Seconds, Elapsed;
        public bool Interrupted, Started, Finished;
        public string Name { get { return "FakeCall"; } }
        public bool IsActive { get { return !Finished; } }

        public void Step(GameWorld world, Ped ped, float dt)
        {
            if (Finished) return;
            if (!Started) { Started = true; world.Emit(GameEventType.CallStarted, ped.Position, ped.Tag, 0, CrimeId, null); }
            Elapsed += dt;
            if (Interrupted) { Finished = true; world.Emit(GameEventType.CallInterrupted, ped.Position, ped.Tag, 0, CrimeId, null); }
            else if (Elapsed >= Seconds) { Finished = true; world.Emit(GameEventType.CallCompleted, ped.Position, ped.Tag, 0, CrimeId, null); }
        }
    }

    /// <summary>A world with a flat FakeCity, both G8 systems and the fake G7 services; steps everything in system order.</summary>
    internal sealed class Scene
    {
        public const float Dt = 0.1f;
        public readonly FakeCity City = new FakeCity();
        public readonly GameWorld World;
        public readonly FakePedServices Peds = new FakePedServices();
        public readonly CrimeSystem Crime;
        public readonly PoliceSystem Police;
        public readonly uint Seed;

        public Scene(uint seed = 7u, PoliceDifficulty difficulty = PoliceDifficulty.Normal, bool policeCars = true, bool helicopter = false, bool grid = false)
        {
            Seed = seed;
            if (!policeCars) City.VehicleModels.Remove(1);
            if (helicopter) City.AddVehicleModel(new VehicleModel { Id = 20, Name = "Police Helicopter", Kind = VehicleKind.PoliceHelicopter });
            var settings = new GameSettings();
            settings.Police = difficulty;
            World = new GameWorld(City, settings, new PlayerProfile(), seed);
            Crime = new CrimeSystem(Peds, seed);
            Police = new PoliceSystem(Crime, seed);
            World.Player.Model = 3;
            World.Player.Motor.Teleport(Vec3.Zero);
            if (grid) UseGrid(11, 100f, -500f, -500f);
        }

        public Ped Player { get { return World.Player; } }
        public WantedStatus Wanted { get { return World.Wanted; } }

        /// <summary>An n x n two-way grid (nodes spaced <paramref name="spacing"/>), shifted so node 1 sits at (ox, oz).</summary>
        public void UseGrid(int n, float spacing, float ox, float oz)
        {
            Net net = Nets.Grid(n, spacing);
            var nodes = new List<RoadNode>();
            foreach (RoadNode nd in net.Nodes) nodes.Add(new RoadNode(nd.Id, nd.Position + new Vec3(ox, 0f, oz)));
            var segs = new List<RoadSegment>();
            foreach (RoadSegment s in net.Segments)
            {
                RoadSegment t = s;
                Vec3 o = new Vec3(ox, 0f, oz);
                t.A = s.A + o; t.B = s.B + o; t.C = s.C + o; t.D = s.D + o;
                segs.Add(t);
            }
            World.Roads.Rebuild(nodes, segs, false);
        }

        public void PlacePlayer(float x, float z) { World.Player.Motor.Teleport(new Vec3(x, 0f, z)); }

        public static float YawToward(Vec3 from, Vec3 to)
        {
            return (float)(Math.Atan2(to.X - from.X, to.Z - from.Z) * 180.0 / Math.PI);
        }

        public Ped Civilian(float x, float z, bool witness = false, bool seesPlayer = false)
        {
            Ped p = World.SpawnPed(PedKind.Civilian, 1, new Vec3(x, 0f, z), 0f);
            if (witness) Peds.Witnesses.Add(new CrimeWitness { Tag = p.Tag, Position = p.Position });
            if (seesPlayer) Peds.Seeing.Add(p.Id);
            return p;
        }

        /// <summary>A cop on foot facing the player (or the given yaw).</summary>
        public Ped Cop(float x, float z, float? yaw = null)
        {
            Vec3 pos = new Vec3(x, 0f, z);
            float y = yaw ?? YawToward(pos, World.Player.Position);
            Ped p = World.SpawnPed(PedKind.Cop, 2, pos, y);
            p.YawDeg = y;
            return p;
        }

        public long Punch(Ped victim)
        {
            return World.Emit(GameEventType.Punched, victim.Position, World.Player.Tag, victim.Tag, 0f, null);
        }

        public long Kill(Ped victim)
        {
            return World.Emit(GameEventType.PedKilled, victim.Position, World.Player.Tag, victim.Tag, 0f, null);
        }

        public long Gunshot(Vec3 at)
        {
            return World.Emit(GameEventType.Gunshot, at, World.Player.Tag, 0, 0f, null);
        }

        public Car PlayerInCar(float x, float z, float yaw = 0f, int colour = 0xC0392B)
        {
            Car c = World.SpawnCar(0, new Vec3(x, 0f, z), yaw, CarOwner.Player, colour);
            World.PutInCar(World.Player, c, 0);
            return c;
        }

        /// <summary>One simulation step: clock, Crime, the call brains, cop brains (+ motor), Police.</summary>
        public void Step(float dt = Dt, bool police = true)
        {
            World.Clock.GameSeconds += dt;
            World.Clock.RealSeconds += dt;
            World.Clock.Steps++;
            Crime.Step(World, dt);
            foreach (FakePedServices.CallRecord c in Peds.Calls)
            {
                Ped p = World.FindPed(c.PedId);
                if (p != null && !p.IsDead) c.Brain.Step(World, p, dt);
            }
            if (police) Police.Step(World, dt);
            var peds = new List<Ped>(World.Peds);
            foreach (Ped p in peds)
            {
                if (p.Kind != PedKind.Cop || p.Brain == null || p.IsDead || p.VehicleId != 0) continue;
                p.Brain.Step(World, p, dt);
                World.MovePed(p, dt);
            }
        }

        public void Run(float seconds, float dt = Dt, bool police = true)
        {
            int n = (int)Math.Round(seconds / dt);
            for (int i = 0; i < n; i++) Step(dt, police);
        }

        /// <summary>Steps until the predicate holds or <paramref name="maxSeconds"/> pass; returns the seconds stepped, -1 on timeout.</summary>
        public float RunUntil(Func<bool> done, float maxSeconds, float dt = Dt)
        {
            float t = 0f;
            while (t < maxSeconds)
            {
                Step(dt);
                t += dt;
                if (done()) return t;
            }
            return -1f;
        }

        /// <summary>Makes the player wanted as if a cop had just seen a crime.</summary>
        public void MakeWanted(int stars)
        {
            World.Wanted.Stars = stars;
            World.Wanted.HasLastKnownPosition = true;
            World.Wanted.LastKnownPosition = World.Player.Position;
            World.Wanted.Description = new SuspectDescription { OutfitModel = World.Player.Model, CarModel = -1 };
        }
    }

    internal static class Near
    {
        public static void Equal(float expected, float actual, float tol)
        {
            Xunit.Assert.InRange(actual, expected - tol, expected + tol);
        }

        public static void Equal(Vec3 expected, Vec3 actual, float tol)
        {
            Xunit.Assert.InRange(Vec3.DistanceXZ(expected, actual), 0f, tol);
        }
    }
}
