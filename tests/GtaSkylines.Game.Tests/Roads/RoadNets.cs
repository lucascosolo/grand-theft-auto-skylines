using System;
using System.Collections.Generic;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Roads;
using Xunit;

namespace GtaSkylines.Game.Tests.Roads
{
    /// <summary>Hand-built road networks and assertion helpers shared by the G2 tests. x = east, z = north, heading 0 = +z.</summary>
    internal sealed class Net
    {
        public readonly List<RoadNode> Nodes = new List<RoadNode>();
        public readonly List<RoadSegment> Segments = new List<RoadSegment>();

        public Net Node(int id, float x, float z)
        {
            Nodes.Add(new RoadNode(id, new Vec3(x, 0f, z)));
            return this;
        }

        Vec3 Pos(int nodeId)
        {
            foreach (RoadNode n in Nodes) if (n.Id == nodeId) return n.Position;
            throw new InvalidOperationException("test net: unknown node " + nodeId);
        }

        /// <summary>Straight segment from node a (t = 0) to node b (t = 1).</summary>
        public Net Road(int id, int a, int b, int fwd = 1, int back = 1, float speed = 10f, RoadFlags flags = RoadFlags.None, float laneWidth = 3f)
        {
            Vec3 pa = Pos(a), pb = Pos(b);
            Segments.Add(new RoadSegment
            {
                Id = id, StartNode = a, EndNode = b,
                A = pa, B = pa + (pb - pa) / 3f, C = pa + (pb - pa) * (2f / 3f), D = pb,
                ForwardLanes = fwd, BackwardLanes = back, LaneWidth = laneWidth, SpeedLimit = speed, Flags = flags,
            });
            return this;
        }

        /// <summary>Segment with explicit Bézier control points (A and D must equal the node positions).</summary>
        public Net Curve(int id, int a, int b, Vec3 pb, Vec3 pc, int fwd = 1, int back = 1, float speed = 10f, float laneWidth = 3f)
        {
            Segments.Add(new RoadSegment
            {
                Id = id, StartNode = a, EndNode = b, A = Pos(a), B = pb, C = pc, D = Pos(b),
                ForwardLanes = fwd, BackwardLanes = back, LaneWidth = laneWidth, SpeedLimit = speed,
            });
            return this;
        }

        public RoadGraph Build(bool leftHand = false)
        {
            var g = new RoadGraph();
            g.Rebuild(Nodes, Segments, leftHand);
            return g;
        }
    }

    internal static class Nets
    {
        public const float Kappa = 0.5523f;

        /// <summary>Node 1 at the origin, node 2 at (0,0,length); segment 1 runs along +z. Right of travel is +x.</summary>
        public static Net Straight(float length = 200f, int fwd = 1, int back = 1, float speed = 10f)
        {
            return new Net().Node(1, 0, 0).Node(2, 0, length).Road(1, 1, 2, fwd, back, speed);
        }

        /// <summary>n × n two-way grid, node id = row * n + col + 1 at (col * spacing, 0, row * spacing). Horizontal segments 100+, vertical 200+.</summary>
        public static Net Grid(int n = 3, float spacing = 100f, float speed = 10f)
        {
            var net = new Net();
            for (int r = 0; r < n; r++)
                for (int c = 0; c < n; c++)
                    net.Node(r * n + c + 1, c * spacing, r * spacing);
            for (int r = 0; r < n; r++)
                for (int c = 0; c + 1 < n; c++)
                    net.Road(100 + r * n + c, r * n + c + 1, r * n + c + 2, 1, 1, speed);
            for (int r = 0; r + 1 < n; r++)
                for (int c = 0; c < n; c++)
                    net.Road(200 + r * n + c, r * n + c + 1, (r + 1) * n + c + 1, 1, 1, speed);
            return net;
        }

        /// <summary>
        /// Main road along z through junction node 2 at the origin: segment 1 from node 1 (0,0,-200) to node 2,
        /// segment 2 from node 2 to node 3 (0,0,200), branch segment 3 from node 2 to node 4 (200,0,0).
        /// </summary>
        public static Net TJunction()
        {
            return new Net().Node(1, 0, -200).Node(2, 0, 0).Node(3, 0, 200).Node(4, 200, 0)
                .Road(1, 1, 2).Road(2, 2, 3).Road(3, 2, 4);
        }

        /// <summary>
        /// Junction node 2 at the origin: segment 1 (node 1 at (0,0,-200) to node 2), segment 2 straight on to node 3
        /// (0,0,200) and segment 3 to node 4 at 200 m heading 60 degrees. Arriving from node 4, the most aligned
        /// continuation is segment 1.
        /// </summary>
        public static Net YJunction()
        {
            return new Net().Node(1, 0, -200).Node(2, 0, 0).Node(3, 0, 200).Node(4, 173.205f, 100f)
                .Road(1, 1, 2).Road(2, 2, 3).Road(3, 2, 4);
        }

        /// <summary>Clockwise (north, east, south, west) one-way square of 100 m sides: nodes 1 (0,0), 2 (0,100), 3 (100,100), 4 (100,0); segment i goes node i to the next.</summary>
        public static Net OneWayLoop(float speed = 10f)
        {
            return new Net().Node(1, 0, 0).Node(2, 0, 100).Node(3, 100, 100).Node(4, 100, 0)
                .Road(1, 1, 2, 1, 0, speed, RoadFlags.OneWay).Road(2, 2, 3, 1, 0, speed, RoadFlags.OneWay)
                .Road(3, 3, 4, 1, 0, speed, RoadFlags.OneWay).Road(4, 4, 1, 1, 0, speed, RoadFlags.OneWay);
        }

        /// <summary>One quarter-circle segment, radius r, centre (r,0,0): from node 1 (0,0,0) heading north to node 2 (r,0,r) heading east.</summary>
        public static Net QuarterArc(float r = 100f, int fwd = 1, int back = 1, float speed = 10f)
        {
            return new Net().Node(1, 0, 0).Node(2, r, r)
                .Curve(1, 1, 2, new Vec3(0, 0, Kappa * r), new Vec3(r - Kappa * r, 0, r), fwd, back, speed);
        }

        /// <summary>
        /// A to B (1000 m apart on the x axis) by a slow direct street (segment 1) or by a detour A-C-D-B
        /// (segments 2, 3, 4; 1600 m) whose speed limit is detourSpeed.
        /// </summary>
        public static Net HighwayDetour(float detourSpeed)
        {
            return new Net().Node(1, 0, 0).Node(2, 1000, 0).Node(3, 0, 300).Node(4, 1000, 300)
                .Road(1, 1, 2, 1, 1, 5f)
                .Road(2, 1, 3, 2, 2, detourSpeed, RoadFlags.Highway).Road(3, 3, 4, 2, 2, detourSpeed, RoadFlags.Highway)
                .Road(4, 4, 2, 2, 2, detourSpeed, RoadFlags.Highway);
        }

        /// <summary>
        /// Highway along z (nodes 1 (0,0,0), 2 (0,0,500), 3 (0,0,1000); segments 1 and 2, speed 30) with a one-way
        /// on-ramp (segment 3) from node 4 at (150,0,350) merging into node 2.
        /// </summary>
        public static Net HighwayWithRamp()
        {
            return new Net().Node(1, 0, 0).Node(2, 0, 500).Node(3, 0, 1000).Node(4, 150, 350)
                .Road(1, 1, 2, 2, 2, 30f, RoadFlags.Highway).Road(2, 2, 3, 2, 2, 30f, RoadFlags.Highway)
                .Curve(3, 4, 2, new Vec3(150, 0, 430), new Vec3(0, 0, 420), 1, 0, 15f);
        }

        /// <summary>
        /// Horizontal street along x through nodes 1 (0,0), 2 (100,0), 3 (200,0), 4 (300,0); nodes 12/22 and 13/23
        /// are side-street ends 100 m north/south of nodes 2 and 3. Nodes 2 and 3 are four-way junctions.
        /// </summary>
        public static Net CrossStreets()
        {
            return new Net().Node(1, 0, 0).Node(2, 100, 0).Node(3, 200, 0).Node(4, 300, 0)
                .Node(12, 100, 100).Node(22, 100, -100).Node(13, 200, 100).Node(23, 200, -100)
                .Road(1, 1, 2).Road(2, 2, 3).Road(3, 3, 4)
                .Road(4, 12, 2).Road(5, 2, 22).Road(6, 13, 3).Road(7, 3, 23);
        }

        /// <summary>Straight 300 m, right-turn quarter arc of radius 40 (centre (40,0,300)), straight 200 m east at z = 340.</summary>
        public static Net StraightThenRightCurve(float speed = 20f)
        {
            const float r = 40f;
            return new Net().Node(1, 0, 0).Node(2, 0, 300).Node(3, r, 340).Node(4, 240, 340)
                .Road(1, 1, 2, 1, 1, speed)
                .Curve(2, 2, 3, new Vec3(0, 0, 300 + Kappa * r), new Vec3(r - Kappa * r, 0, 340), 1, 1, speed)
                .Road(3, 3, 4, 1, 1, speed);
        }
    }

    internal static class Check
    {
        public static void Near(float expected, float actual, float tolerance, string what = null)
        {
            Assert.True(Math.Abs(expected - actual) <= tolerance,
                (what ?? "value") + ": expected " + expected + " +/- " + tolerance + " but was " + actual);
        }

        public static void Near(Vec3 expected, Vec3 actual, float tolerance, string what = null)
        {
            Assert.True(Vec3.Distance(expected, actual) <= tolerance,
                (what ?? "position") + ": expected " + expected + " +/- " + tolerance + " but was " + actual);
        }

        public static void NearAngle(float expectedDeg, float actualDeg, float tolerance, string what = null)
        {
            Assert.True(Math.Abs(MathUtil.DeltaAngleDeg(expectedDeg, actualDeg)) <= tolerance,
                (what ?? "heading") + ": expected " + expectedDeg + " +/- " + tolerance + " but was " + actualDeg);
        }

        public static Route Route(RoadGraph g, Vec3 from, Vec3 to)
        {
            var r = new Route();
            RouteStatus s = g.FindRoute(from, to, r);
            Assert.Equal(RouteStatus.Ok, s);
            return r;
        }

        public static List<int> SegmentIds(Route r)
        {
            var ids = new List<int>();
            for (int i = 0; i < r.SegmentCount; i++) ids.Add(r.GetSegmentId(i));
            return ids;
        }
    }
}
