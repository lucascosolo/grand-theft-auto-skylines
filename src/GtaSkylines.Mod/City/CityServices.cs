using System;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Physics;
using GtaSkylines.Game.Roads;
using GtaSkylines.Mod.World;
using Skylines.Host;
using UnityEngine;

namespace GtaSkylines.Mod.City
{
    /// <summary>
    /// The M2 adapters for one loaded city, for the play session (M7) to construct after the level loads and dispose
    /// on unload: <see cref="ICity"/>, the per-frame snapshot, collision streaming, the road graph and the effect
    /// applier. Main thread only.
    /// </summary>
    public sealed class CityServices : IDisposable
    {
        /// <summary>Builds the catalogues and the road graph now (allocates; once per city load).</summary>
        public CityServices(HostLog log, CollisionWorld collision, RoadGraph roads)
            : this(log)
        {
            Attach(collision, roads);
        }

        /// <summary>
        /// Builds the catalogues, city adapter, snapshot builder and effect applier only; <see cref="Attach"/> the game
        /// world's collision world and road graph before the first <see cref="Tick"/> (the world needs <see cref="City"/> first).
        /// </summary>
        public CityServices(HostLog log)
        {
            _log = log;
            Catalogues = new Catalogues(log);
            City = new CityAdapter(log, Catalogues);
            Effects = new CityEffectApplier(log, Catalogues);
            Snapshot = new SnapshotBuilder(Catalogues, Effects.Taken, Effects.Blockers, log);
        }

        private readonly HostLog _log;

        /// <summary>Starts streaming collision into <paramref name="collision"/> and builds <paramref name="roads"/> now. Once.</summary>
        public void Attach(CollisionWorld collision, RoadGraph roads)
        {
            if (Collision != null) throw new InvalidOperationException("already attached");
            Collision = new CollisionStreamer(_log, collision);
            Roads = new RoadGraphBuilder(_log, roads);
            Roads.Rebuilt += Collision.InvalidateAll;
            Roads.Rebuild();
        }

        /// <summary>Vehicle and citizen models.</summary>
        public Catalogues Catalogues { get; private set; }
        /// <summary>The city as the Game reads it.</summary>
        public CityAdapter City { get; private set; }
        /// <summary>Fills the per-frame <see cref="CitySnapshot"/>.</summary>
        public SnapshotBuilder Snapshot { get; private set; }
        /// <summary>Streams static collision into the Game's world.</summary>
        public CollisionStreamer Collision { get; private set; }
        /// <summary>Keeps the Game's road graph in step with CS1.</summary>
        public RoadGraphBuilder Roads { get; private set; }
        /// <summary>Applies the Game's effects to CS1.</summary>
        public CityEffectApplier Effects { get; private set; }

        /// <summary>
        /// One frame while playing, before the Game tick: road graph check, collision streaming within
        /// <paramref name="collisionBudgetMs"/>, debug viewer key, snapshot around <paramref name="feet"/>.
        /// </summary>
        public void Tick(float dt, Vector3 feet, Vector3 velocity, CitySnapshot snapshot, double collisionBudgetMs)
        {
            Roads.Tick(dt);
            Collision.Viewer.Update();
            Collision.Tick(feet, velocity, collisionBudgetMs);
            Snapshot.Fill(snapshot, feet);
        }

        /// <summary>Unsubscribes and empties the collision world (play mode end or city unload).</summary>
        public void Dispose()
        {
            if (Collision != null)
            {
                Roads.Rebuilt -= Collision.InvalidateAll;
                Collision.Clear();
                Collision.Dispose();
            }
            Effects.Taken.Clear();
            Effects.ReleaseBlockers();
        }
    }
}
