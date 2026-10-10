using GtaSkylines.Game.Interiors;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Peds;
using GtaSkylines.Game.Tests.Combat;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.Peds
{
    /// <summary>PedSteering on open ground and walls, PedPath on a hand-made nav grid.</summary>
    public class SteeringTests
    {
        [Fact]
        public void ChooseDirectionKeepsTheWantedWayWhenClear()
        {
            var a = new Arena();
            var p = a.Spawn(0f, 0f);
            Vec3 chosen;
            Assert.True(PedSteering.ChooseDirection(a.World, p, Vec3.Forward, 1.4f, out chosen));
            Assert.True(Vec3.Distance(chosen, Vec3.Forward) < 0.01f);
        }

        [Fact]
        public void ChooseDirectionTurnsFortyFiveDegreesAroundAWallAhead()
        {
            var a = new Arena();
            var p = a.Spawn(0f, 0f);
            a.Wall(-0.2f, 1.2f, 0.2f, 1.2f);
            a.Rebuild();
            Vec3 chosen;
            Assert.True(PedSteering.ChooseDirection(a.World, p, Vec3.Forward, 1.4f, out chosen));
            Assert.True(Vec3.Dot(chosen, Vec3.Forward) < 0.9f);
        }

        [Fact]
        public void ChooseDirectionFailsWhenBoxedIn()
        {
            var a = new Arena();
            var p = a.Spawn(0f, 0f);
            a.Wall(-1f, 1f, 1f, 1f);
            a.Wall(-1f, -1f, 1f, -1f);
            a.Wall(1f, -1f, 1f, 1f);
            a.Wall(-1f, -1f, -1f, 1f);
            a.Rebuild();
            Vec3 chosen;
            Assert.False(PedSteering.ChooseDirection(a.World, p, Vec3.Forward, 1.4f, out chosen));
        }

        [Fact]
        public void SeekWalksToTheTargetAndReportsArrival()
        {
            var a = new Arena();
            var p = a.Spawn(0f, 0f);
            var target = new Vec3(0f, 0f, 6f);
            bool arrived = false;
            for (int i = 0; i < 60 * 10 && !arrived; i++)
            {
                arrived = PedSteering.Seek(a.World, p, target, PedSteering.WalkSpeed, PedAnim.Walk, Arena.Dt);
                a.World.MovePed(p, Arena.Dt);
            }
            Assert.True(arrived);
            Assert.True(Vec3.DistanceXZ(p.Position, target) <= 0.6f);
        }

        [Fact]
        public void FleeMovesAwayFromTheThreatAtTheGivenSpeed()
        {
            var a = new Arena();
            var p = a.Spawn(0f, 0f);
            for (int i = 0; i < 60; i++)
            {
                PedSteering.Flee(a.World, p, new Vec3(0f, 0f, -5f), 5.5f, PedAnim.Panic, Arena.Dt);
                a.World.MovePed(p, Arena.Dt);
            }
            Assert.InRange(p.Position.Z, 3.5f, 6f);
            Assert.Equal(PedAnim.Panic, p.Anim);
        }

        [Fact]
        public void StopZeroesTheMoveAndTurnTowardIsRateLimited()
        {
            var a = new Arena();
            var p = a.Spawn(0f, 0f);
            p.Move.DesiredVelocity = new Vec3(1f, 0f, 0f);
            PedSteering.Stop(p, PedAnim.Idle);
            Assert.Equal(0f, p.Move.DesiredVelocity.LengthXZ);
            p.YawDeg = 0f;
            PedSteering.TurnToward(p, 180f, Arena.Dt);
            Assert.InRange(p.YawDeg, 8.9f, 9.1f);
        }

        private static InteriorNavGrid WallGrid()
        {
            var g = new InteriorNavGrid(new Rect2(0f, 0f, 10f, 10f));
            for (float x = 0.125f; x < 10f; x += 0.25f)
                for (float z = 0.125f; z < 10f; z += 0.25f)
                    g.Set(x, z, true);
            for (float z = 0.125f; z < 8f; z += 0.25f)
                for (float x = 4.875f; x < 5.2f; x += 0.25f)
                    g.Set(x, z, false);
            return g;
        }

        private static Arena InteriorArena(InteriorNavGrid grid)
        {
            var a = new Arena();
            var layout = new InteriorLayout { Width = 10f, Depth = 10f, Nav = grid };
            a.World.Interior.Layout = layout;
            a.World.Interior.Origin = Vec3.Zero;
            a.World.Interior.YawDeg = 0f;
            a.World.Interior.Phase = InteriorPhase.Inside;
            return a;
        }

        [Fact]
        public void PedPathGoesAroundTheWallToTheGoal()
        {
            var a = InteriorArena(WallGrid());
            var p = a.World.SpawnPed(PedKind.Civilian, 3, new Vec3(2f, 0f, 2f), 0f);
            var path = new PedPath();
            var goal = new Vec3(8f, 0f, 2f);
            bool arrived = false;
            float maxZ = 0f;
            for (int i = 0; i < 60 * 30 && !arrived; i++)
            {
                arrived = path.Follow(a.World, p, goal, PedSteering.WalkSpeed, PedAnim.Walk, Arena.Dt);
                a.World.MovePed(p, Arena.Dt);
                if (p.Position.Z > maxZ) maxZ = p.Position.Z;
            }
            Assert.True(arrived);
            Assert.True(maxZ > 8f, "detoured above the wall's end");
        }

        [Fact]
        public void PedPathWithNoRouteStandsStillAndReturnsFalse()
        {
            var g = WallGrid();
            for (float z = 8f; z < 10f; z += 0.25f)
                for (float x = 4.875f; x < 5.2f; x += 0.25f)
                    g.Set(x, z + 0.125f, false);
            var a = InteriorArena(g);
            var p = a.World.SpawnPed(PedKind.Civilian, 3, new Vec3(2f, 0f, 2f), 0f);
            var path = new PedPath();
            Assert.False(path.Follow(a.World, p, new Vec3(8f, 0f, 2f), PedSteering.WalkSpeed, PedAnim.Walk, Arena.Dt));
            Assert.Equal(0f, p.Move.DesiredVelocity.LengthXZ);
            Assert.Equal(0, path.Remaining);
        }

        [Fact]
        public void PedPathClearForgetsTheRoute()
        {
            var a = InteriorArena(WallGrid());
            var p = a.World.SpawnPed(PedKind.Civilian, 3, new Vec3(2f, 0f, 2f), 0f);
            var path = new PedPath();
            path.Follow(a.World, p, new Vec3(8f, 0f, 2f), PedSteering.WalkSpeed, PedAnim.Walk, Arena.Dt);
            Assert.True(path.Remaining > 0);
            path.Clear();
            Assert.Equal(0, path.Remaining);
        }
    }
}
