using System.Collections.Generic;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Session
{
    /// <summary>Rebuilds dynamic shapes, demotes peds, removes abandoned cars and steps their lane blocks (order 5). Rules on <see cref="GameSession"/>.</summary>
    internal sealed class AmbientSystem : IGameSystem
    {
        private static readonly Vec3 Head = new Vec3(0f, 1.6f, 0f);

        public int Order { get { return SystemOrder.Ambient; } }

        public void Step(GameWorld w, float dt)
        {
            w.RebuildDynamicShapes(true);
            Ped player = w.Player;
            if (player == null) return;
            Vec3 at = player.Position;

            List<Ped> peds = w.PedList;
            for (int i = peds.Count - 1; i >= 0; i--)
            {
                if (i >= peds.Count) continue;
                Ped p = peds[i];
                if (p == player || p.Kind != PedKind.Civilian || p.VehicleId != 0 || p.Has(PedFlags.Persistent)) continue;
                float d = Vec3.DistanceXZ(p.Position, at);
                if (p.IsDead || p.Has(PedFlags.Injured))
                {
                    if (d > PlayerRules.RemoveBodiesDistance) w.RemovePed(p, false);
                    continue;
                }
                if (p.Brain != null && p.Brain.IsActive) { p.IdleUnseenSeconds = 0f; continue; }
                bool far = d > PlayerRules.DemoteAlwaysDistance;
                bool unseen = d > PlayerRules.DemoteUnseenDistance && !w.CanPlayerSee(p.Position + Head);
                if (far || unseen) w.RemovePed(p, true);
                else p.IdleUnseenSeconds = 0f;
            }

            List<Car> cars = w.CarList;
            for (int i = cars.Count - 1; i >= 0; i--)
            {
                if (i >= cars.Count) continue;
                Car c = cars[i];
                LaneBlocks.Step(w, c, at, dt);
                if (c.Owner != CarOwner.Abandoned) { c.AbandonedSeconds = 0f; continue; }
                if (player.VehicleId == c.Id) continue;
                if (Vec3.DistanceXZ(c.Position, at) > PlayerRules.AbandonedRemoveDistance && !w.CanPlayerSee(c.Position))
                {
                    c.AbandonedSeconds += dt;
                    if (c.AbandonedSeconds >= PlayerRules.AbandonedUnseenSeconds) w.RemoveCar(c);
                }
                else c.AbandonedSeconds = 0f;
            }
        }
    }
}
