using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Session
{
    /// <summary>The Wasted and Busted flows and the respawn (order 90). Rules on <see cref="GameSession"/>.</summary>
    internal sealed class RespawnSystem : IGameSystem
    {
        private const float FadeSeconds = 0.5f;

        private readonly GameSession _session;
        private readonly InteriorSystem _interiors;
        private readonly List<ushort> _buildings = new List<ushort>();

        public RespawnSystem(GameSession session, InteriorSystem interiors)
        {
            _session = session;
            _interiors = interiors;
        }

        public int Order { get { return SystemOrder.Respawn; } }

        public void Step(GameWorld w, float dt)
        {
            Ped p = w.Player;
            if (p == null) return;
            PlayerState ps = w.PlayerState;
            if (ps.Life == LifeState.Alive)
            {
                if (p.Health <= 0f || p.IsDead)
                {
                    p.Health = 0f;
                    p.Flags |= PedFlags.Dead;
                    p.Flags &= ~(PedFlags.Aiming | PedFlags.HandsUp);
                    p.Anim = PedAnim.LieDown;
                    Start(w, ps, LifeState.Wasted, BigScreenKind.Wasted, StatNames.Deaths);
                    w.Emit(GameEventType.PlayerDied, p.Position, p.LastAttackerTag, p.Tag, 0f, null);
                }
                else if (w.Wanted.BustedRequested)
                {
                    w.Wanted.BustedRequested = false;
                    p.Flags |= PedFlags.Arrested;
                    Start(w, ps, LifeState.Busted, BigScreenKind.Busted, StatNames.Arrests);
                    w.Emit(GameEventType.PlayerArrested, p.Position, w.Wanted.BustedByTag, p.Tag, 0f, null);
                }
                return;
            }
            ps.LifeSeconds += dt;
            w.FadeAlpha = MathUtil.Clamp01((ps.LifeSeconds - (PlayerRules.DeathScreenSeconds - FadeSeconds)) / FadeSeconds);
            if (ps.LifeSeconds >= PlayerRules.DeathScreenSeconds) Respawn(w, p, ps);
        }

        private static void Start(GameWorld w, PlayerState ps, LifeState life, BigScreenKind screen, string stat)
        {
            ps.Life = life;
            ps.LifeSeconds = 0f;
            ps.Transition = VehicleTransition.None;
            w.Menu.Close();
            w.Profile.AddStat(stat, 1);
            w.Messages.ShowBigScreen(screen, null, 0, PlayerRules.DeathScreenSeconds);
        }

        private void Respawn(GameWorld w, Ped p, PlayerState ps)
        {
            bool busted = ps.Life == LifeState.Busted;
            Vec3 from = w.Interior.IsInside ? w.Interior.Door : p.Position;
            if (p.VehicleId != 0) w.TakeOutOfCar(p, p.Position, p.YawDeg);
            _interiors.Unload(w);

            Vec3 spot;
            ushort building;
            if (!ServiceDoors.Nearest(w.City, busted ? ServiceKind.Police : ServiceKind.Health, from, DoorSearch.Any, _buildings,
                    out building, out spot))
                spot = _session.BeginPosition;

            p.Motor.Teleport(spot);
            p.Motor.Velocity = Vec3.Zero;
            p.PrevPosition = spot;
            p.Flags = PedFlags.None;
            p.Health = 100f;
            p.Anim = PedAnim.Idle;
            p.LastAttackerTag = 0;
            ps.Sneaking = false;
            ps.Stamina = PlayerRules.MaxStamina;
            PlayerProfile prof = w.Profile;
            if (busted)
            {
                p.Armour = 0f;
                w.Wallet.Fine(PlayerRules.BustedFine, "Bail");
                prof.StripWeapons();
                p.CurrentWeapon = 0;
                p.MagazineAmmo = 0;
            }
            else
            {
                w.Wallet.Fine(PlayerRules.HospitalFee, "Hospital");
                WeaponId cur = (WeaponId)MathUtil.Clamp(p.CurrentWeapon, 0, WeaponInfo.Count - 1);
                if (WeaponInfo.UsesAmmo(cur)) prof.SetAmmo(cur, 0);
                p.MagazineAmmo = 0;
            }
            w.Wanted.Reset();
            ps.Life = LifeState.Alive;
            ps.LifeSeconds = 0f;
            w.FadeAlpha = 0f;
            w.Emit(GameEventType.PlayerRespawned, spot, p.Tag, 0, busted ? 1f : 0f, null);
        }
    }
}
