using System;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Crime
{
    /// <summary>
    /// Turns <see cref="GameEvent"/>s caused by the player into crimes (F-HEAT-1). "The player" as actor A means the
    /// player ped's tag, or the tag of the car the player drives (seat 0) at classification time.
    /// <para>Mapping (first match wins):</para>
    /// <list type="bullet">
    /// <item>CarStolen, A = player: Value 1 → <see cref="CrimeType.PoliceCarStolen"/>, else <see cref="CrimeType.CarStolen"/>.</item>
    /// <item>CarJacked, A = player: car B with Owner Police → PoliceCarStolen, else <see cref="CrimeType.CarJacked"/>.</item>
    /// <item>Punched or Shove, A = player, B a person: B a cop → <see cref="CrimeType.AssaultOnCop"/>, else <see cref="CrimeType.Assault"/>.</item>
    /// <item>Shot, A = player, B a person: cop → AssaultOnCop (Loud), else <see cref="CrimeType.ShotAtPed"/> (Loud). Shot with B not a person → nothing (the Gunshot event covers it).</item>
    /// <item>Gunshot, A = player → <see cref="CrimeType.Gunshot"/> (Loud).</item>
    /// <item>Explosion, A = player or a Game car with Owner Player → <see cref="CrimeType.Explosion"/> (Loud).</item>
    /// <item>PedKnockedDown, A = player's car (or the player), B a cop → AssaultOnCop; else A = a car → <see cref="CrimeType.PedKnockedDown"/>; A = the player on foot → Assault.</item>
    /// <item>PedInjured, A = player or player's car: B a cop → AssaultOnCop; A a car → <see cref="CrimeType.HitAndRun"/>; else <see cref="CrimeType.PedInjured"/>.</item>
    /// <item>PedKilled, A = player or player's car: B a cop → <see cref="CrimeType.CopKilled"/>, else <see cref="CrimeType.PedKilled"/>.</item>
    /// <item>Trespass → <see cref="CrimeType.Trespass"/>; Robbery → <see cref="CrimeType.Robbery"/>.</item>
    /// <item>AimedAt, A = player, B a person who is not a cop, the player's CurrentWeapon ≠ 0 (Fists) → <see cref="CrimeType.Threatening"/>.</item>
    /// <item>RecklessDriving, A = player's car: each event weighs <see cref="RecklessEventWeight"/>; the classifier keeps
    /// the times of the last three; when the third falls within <see cref="RecklessWindowSeconds"/> of the first, it
    /// yields <see cref="CrimeType.RecklessDriving"/> (severity 1) and forgets them (the next crime needs three new events).</item>
    /// </list>
    /// Escalation on one victim: when a crime's victim tag equals that of a crime classified within
    /// <see cref="EscalationSeconds"/> before, <see cref="CrimeSystem"/> raises the earlier record in place
    /// (type and severity) instead of adding a new one, if the new severity is higher; a lower or equal one is dropped.
    /// "Cop" means a Game ped of <see cref="PedKind.Cop"/>. Allocates nothing.
    /// </summary>
    public sealed class CrimeClassifier
    {
        /// <summary>Weight of one RecklessDriving event (documentation of the contract's 0.5; three make one crime).</summary>
        public const float RecklessEventWeight = 0.5f;
        /// <summary>Window for three reckless events, game seconds.</summary>
        public const float RecklessWindowSeconds = 20f;
        /// <summary>Window in which a later crime on the same victim escalates the earlier one, game seconds.</summary>
        public const float EscalationSeconds = 10f;

        private static readonly float[] Severities =
        {
            0f,   // None
            1f,   // CarStolen
            2f,   // CarJacked
            1f,   // Assault
            1f,   // PedKnockedDown
            2f,   // HitAndRun
            2f,   // PedInjured
            3f,   // PedKilled
            1f,   // Gunshot
            2f,   // ShotAtPed
            3f,   // Explosion
            1f,   // Trespass
            3f,   // Robbery
            1f,   // RecklessDriving (three events of 0.5 in 20 s)
            3f,   // AssaultOnCop
            4f,   // CopKilled
            2f,   // ResistingArrest
            3f,   // PoliceCarStolen
            1f,   // Threatening
        };

        /// <summary>Severity of a crime type (table above; 0 for None or an unknown value).</summary>
        public static float SeverityOf(CrimeType type)
        {
            int i = (int)type;
            return i >= 0 && i < Severities.Length ? Severities[i] : 0f;
        }

        /// <summary>True for types the police hear within 20 m regardless of sight (Gunshot, ShotAtPed, Explosion).</summary>
        public static bool IsLoud(CrimeType type)
        {
            return type == CrimeType.Gunshot || type == CrimeType.ShotAtPed || type == CrimeType.Explosion;
        }

        private readonly double[] _reckless = new double[3];
        private int _recklessCount;

        /// <summary>
        /// Classifies one event. Returns <see cref="CrimeType.None"/> when it is not a crime by the player (or is a
        /// reckless event that did not complete a triple). <paramref name="victimTag"/> is B (0 for Gunshot, Explosion,
        /// Trespass, RecklessDriving); <paramref name="loud"/> follows the mapping. Updates the reckless-driving memory.
        /// </summary>
        public CrimeType Classify(GameWorld world, GameEvent e, out int victimTag, out bool loud)
        {
            victimTag = 0;
            loud = false;
            Ped player = world.Player;
            if (player == null) return CrimeType.None;
            int a = e.ActorA, b = e.ActorB;
            bool byPlayer = a == player.Tag;
            bool byCar = !byPlayer && IsPlayerCar(world, a);
            bool cop = IsCop(world, b);
            switch (e.Type)
            {
                case GameEventType.CarStolen:
                    if (!byPlayer) return CrimeType.None;
                    victimTag = b;
                    return e.Value >= 0.5f ? CrimeType.PoliceCarStolen : CrimeType.CarStolen;
                case GameEventType.CarJacked:
                {
                    if (!byPlayer) return CrimeType.None;
                    victimTag = b;
                    Car car = world.CarFromTag(b, false, 0);
                    Ped driver = e.Value > 0f ? world.FindPed((int)e.Value) : null;
                    bool police = (car != null && car.Owner == CarOwner.Police) || (driver != null && driver.Kind == PedKind.Cop);
                    return police ? CrimeType.PoliceCarStolen : CrimeType.CarJacked;
                }
                case GameEventType.Punched:
                case GameEventType.Shove:
                    if (!byPlayer || !WorldTag.IsPerson(b)) return CrimeType.None;
                    victimTag = b;
                    return cop ? CrimeType.AssaultOnCop : CrimeType.Assault;
                case GameEventType.Shot:
                    if (!byPlayer || !WorldTag.IsPerson(b)) return CrimeType.None;
                    victimTag = b;
                    loud = true;
                    return cop ? CrimeType.AssaultOnCop : CrimeType.ShotAtPed;
                case GameEventType.Gunshot:
                    if (!byPlayer) return CrimeType.None;
                    loud = true;
                    return CrimeType.Gunshot;
                case GameEventType.Explosion:
                {
                    bool mine = byPlayer;
                    if (!mine && WorldTag.KindOf(a) == TagKind.Car)
                    {
                        Car car = world.FindCar(WorldTag.IdOf(a));
                        mine = car != null && car.Owner == CarOwner.Player;
                    }
                    if (!mine) return CrimeType.None;
                    loud = true;
                    return CrimeType.Explosion;
                }
                case GameEventType.PedKnockedDown:
                    if (!(byPlayer || byCar) || !WorldTag.IsPerson(b)) return CrimeType.None;
                    victimTag = b;
                    if (cop) return CrimeType.AssaultOnCop;
                    return byCar ? CrimeType.PedKnockedDown : CrimeType.Assault;
                case GameEventType.PedInjured:
                    if (!(byPlayer || byCar) || !WorldTag.IsPerson(b)) return CrimeType.None;
                    victimTag = b;
                    if (cop) return CrimeType.AssaultOnCop;
                    return byCar ? CrimeType.HitAndRun : CrimeType.PedInjured;
                case GameEventType.PedKilled:
                    if (!(byPlayer || byCar) || !WorldTag.IsPerson(b) || b == player.Tag) return CrimeType.None;
                    victimTag = b;
                    return cop ? CrimeType.CopKilled : CrimeType.PedKilled;
                case GameEventType.Trespass:
                    return byPlayer ? CrimeType.Trespass : CrimeType.None;
                case GameEventType.Robbery:
                    if (!byPlayer) return CrimeType.None;
                    victimTag = b;
                    return CrimeType.Robbery;
                case GameEventType.AimedAt:
                    if (!byPlayer || cop || !WorldTag.IsPerson(b) || player.CurrentWeapon == 0) return CrimeType.None;
                    victimTag = b;
                    return CrimeType.Threatening;
                case GameEventType.RecklessDriving:
                    if (!(byCar || byPlayer)) return CrimeType.None;
                    return Reckless(e.GameTime);
                default:
                    return CrimeType.None;
            }
        }

        /// <summary>Forgets the reckless-driving memory (called on a new wanted episode and on <c>WantedStatus.Reset</c>).</summary>
        public void Reset()
        {
            _recklessCount = 0;
        }

        private CrimeType Reckless(double time)
        {
            if (_recklessCount == 3)
            {
                _reckless[0] = _reckless[1];
                _reckless[1] = _reckless[2];
                _recklessCount = 2;
            }
            _reckless[_recklessCount++] = time;
            if (_recklessCount == 3 && time - _reckless[0] <= RecklessWindowSeconds)
            {
                _recklessCount = 0;
                return CrimeType.RecklessDriving;
            }
            return CrimeType.None;
        }

        private static bool IsPlayerCar(GameWorld world, int tag)
        {
            if (WorldTag.KindOf(tag) != TagKind.Car) return false;
            int id = WorldTag.IdOf(tag);
            return id == world.Player.VehicleId || (world.PlayerState != null && id == world.PlayerState.LastCarId);
        }

        private static bool IsCop(GameWorld world, int tag)
        {
            if (WorldTag.KindOf(tag) != TagKind.Ped) return false;
            Ped p = world.FindPed(WorldTag.IdOf(tag));
            return p != null && p.Kind == PedKind.Cop;
        }
    }
}
