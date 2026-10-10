using System;
using System.Collections.Generic;
using ColossalFramework;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Roads;
using Skylines.Host;
using UnityEngine;

namespace GtaSkylines.Mod.World
{
    /// <summary>
    /// Copies CS1's road network (segments of <c>RoadBaseAI</c> nets: roads, bridges, tunnels; never rail, paths or
    /// power lines) into the Game's <see cref="RoadGraph"/>: Bézier control points from
    /// <c>NetSegment.CalculateMiddlePoints</c>, car lanes per direction, lane width, speed limit, name and flags.
    /// Rebuilt on load and, while playing, at most every 5 s when the network changed (segment/node counts or the
    /// build index moved). Main thread only.
    /// </summary>
    public sealed class RoadGraphBuilder
    {
        /// <summary>Minimum seconds between two rebuilds while playing.</summary>
        public const float MinInterval = 5f;

        private readonly HostLog _log;
        private readonly RoadGraph _graph;
        private readonly List<RoadNode> _nodes = new List<RoadNode>();
        private readonly List<RoadSegment> _segments = new List<RoadSegment>();
        private readonly HashSet<ushort> _usedNodes = new HashSet<ushort>();
        private LaneSpec[] _lanes = new LaneSpec[16];
        private int _segCount = -1, _nodeCount = -1;
        private uint _buildIndex;
        private float _sinceRebuild = float.MaxValue;

        /// <summary>Fills <paramref name="graph"/>.</summary>
        public RoadGraphBuilder(HostLog log, RoadGraph graph)
        {
            _log = log;
            _graph = graph;
        }

        /// <summary>Raised on the main thread after each rebuild (the collision streamer re-streams then).</summary>
        public event Action Rebuilt;

        /// <summary>The graph being filled.</summary>
        public RoadGraph Graph { get { return _graph; } }

        /// <summary>Per frame while playing: rebuilds when the network changed and the interval has passed.</summary>
        public void Tick(float dt)
        {
            _sinceRebuild += dt;
            if (_sinceRebuild < MinInterval || !Changed()) return;
            Rebuild();
        }

        /// <summary>True when CS1's network counters moved since the last rebuild.</summary>
        public bool Changed()
        {
            if (!NetManager.exists || !SimulationManager.exists) return false;
            NetManager nm = Singleton<NetManager>.instance;
            return nm.m_segmentCount != _segCount || nm.m_nodeCount != _nodeCount
                || Singleton<SimulationManager>.instance.m_currentBuildIndex != _buildIndex;
        }

        /// <summary>Rebuilds the whole graph now (allocates; on load and on change only).</summary>
        public void Rebuild()
        {
            _sinceRebuild = 0f;
            try
            {
                NetManager nm = Singleton<NetManager>.instance;
                _segCount = nm.m_segmentCount;
                _nodeCount = nm.m_nodeCount;
                _buildIndex = Singleton<SimulationManager>.instance.m_currentBuildIndex;
                Collect(nm);
                bool lht = Singleton<SimulationManager>.instance.m_metaData != null
                    && Singleton<SimulationManager>.instance.m_metaData.m_invertTraffic == SimulationMetaData.MetaBool.True;
                int accepted = _graph.Rebuild(_nodes, _segments, lht);
                _log.Info("roads: graph rebuilt, " + accepted + " segments, " + _nodes.Count + " nodes" + (lht ? ", left-hand traffic" : ""));
                if (Rebuilt != null) Rebuilt();
            }
            catch (Exception e)
            {
                _log.Error("roads: graph rebuild failed", e);
            }
        }

        private void Collect(NetManager nm)
        {
            _nodes.Clear();
            _segments.Clear();
            _usedNodes.Clear();
            NetSegment[] segs = nm.m_segments.m_buffer;
            NetNode[] nodes = nm.m_nodes.m_buffer;
            for (int i = 1; i < segs.Length; i++)
            {
                if ((segs[i].m_flags & (NetSegment.Flags.Created | NetSegment.Flags.Deleted)) != NetSegment.Flags.Created) continue;
                NetInfo info = segs[i].Info;
                if (info == null || !(info.m_netAI is RoadBaseAI) || info.m_lanes == null) continue;
                ushort a = segs[i].m_startNode, b = segs[i].m_endNode;
                if (a == 0 || b == 0) continue;
                Vector3 pa = nodes[a].m_position, pd = nodes[b].m_position;
                Vector3 pb, pc;
                NetSegment.CalculateMiddlePoints(pa, segs[i].m_startDirection, pd, segs[i].m_endDirection,
                    (nodes[a].m_flags & NetNode.Flags.Middle) != 0, (nodes[b].m_flags & NetNode.Flags.Middle) != 0, out pb, out pc);
                if (_lanes.Length < info.m_lanes.Length) _lanes = new LaneSpec[info.m_lanes.Length];
                for (int l = 0; l < info.m_lanes.Length; l++)
                {
                    NetInfo.Lane lane = info.m_lanes[l];
                    _lanes[l] = new LaneSpec
                    {
                        Car = (lane.m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0
                            && (lane.m_vehicleType & VehicleInfo.VehicleType.Car) != 0,
                        Direction = (byte)lane.m_finalDirection,
                        Width = lane.m_width,
                        SpeedLimit = lane.m_speedLimit,
                    };
                }
                LaneSummary s = LaneCounter.Count(_lanes, info.m_lanes.Length, (segs[i].m_flags & NetSegment.Flags.Invert) != 0);
                var roadAi = (RoadBaseAI)info.m_netAI;
                bool tunnel = info.m_netAI is RoadTunnelAI;
                bool bridge = info.m_netAI is RoadBridgeAI;
                bool pedestrian = s.Forward == 0 && s.Backward == 0;
                _segments.Add(new RoadSegment
                {
                    Id = i,
                    StartNode = a,
                    EndNode = b,
                    A = ToVec(pa), B = ToVec(pb), C = ToVec(pc), D = ToVec(pd),
                    ForwardLanes = s.Forward,
                    BackwardLanes = s.Backward,
                    LaneWidth = s.LaneWidth,
                    SpeedLimit = GtaSkylines.Mod.City.CityMath.SpeedLimitMs(s.SpeedLimit),
                    Name = nm.GetSegmentName((ushort)i),
                    Flags = LaneCounter.Flags(roadAi.m_highwayRules, s.Forward, s.Backward, bridge, tunnel, pedestrian),
                });
                if (_usedNodes.Add(a)) _nodes.Add(new RoadNode(a, ToVec(pa)));
                if (_usedNodes.Add(b)) _nodes.Add(new RoadNode(b, ToVec(pd)));
            }
        }

        private static Vec3 ToVec(Vector3 v)
        {
            return new Vec3(v.x, v.y, v.z);
        }
    }
}
