using System;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Hud
{
    /// <summary>
    /// Fills a <see cref="HudModel"/> from the world every presentation step (order 100, runs while paused). Sources:
    /// player ped and PlayerState (vitals, crosshair, speed), Profile and Wallet transactions (cash and delta),
    /// WantedStatus (stars, search zone), MissionStatus (objective, timer, blips, GPS target), HudMessages (help with
    /// <c>{Button}</c> tokens replaced by <c>~Key~</c> from the primary binding, subtitles, notifications, big screen;
    /// it calls <c>HudMessages.Advance(dt)</c>), the city (street within 30 m and district names, ped card through
    /// <c>ICity.TryGetCitizenProfile</c> and <c>TryGetBuilding</c> for the ambient citizen or citizen-backed ped hit by a
    /// 6 m raycast from the camera along its forward). The GPS target is the mission's when set, else the waypoint; the
    /// system sets it on <c>world.Gps</c> and ticks it. Ped card lookups are cached by citizen id.
    /// </summary>
    public sealed class HudSystem : IGameSystem
    {
        /// <summary>Creates the system writing into <paramref name="model"/>.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
        public HudSystem(HudModel model)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public int Order { get { return SystemOrder.Hud; } }

        /// <inheritdoc />
        public void Step(GameWorld world, float dt)
        {
            throw new NotImplementedException();
        }
    }
}
