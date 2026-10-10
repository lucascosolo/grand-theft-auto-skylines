using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GtaSkylines.Game.Camera;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Interiors;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Physics;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Hud
{
    /// <summary>
    /// Fills a <see cref="HudModel"/> from the world every presentation step (order 100, runs while paused). Sources:
    /// player ped and PlayerState (vitals, crosshair, speed), Profile and Wallet transactions (cash and delta),
    /// WantedStatus (stars, search zone), MissionStatus (objective, timer, blips, GPS target), HudMessages (help with
    /// <c>{Button}</c> tokens replaced by <c>~Key~</c> from the primary binding, subtitles, notifications, big screen;
    /// it calls <c>HudMessages.Advance(dt)</c> after copying them), the city (street within 30 m and district names, ped
    /// card through <c>ICity.TryGetCitizenProfile</c> and <c>TryGetBuilding</c> for the ambient citizen or citizen-backed
    /// ped hit by a 6 m raycast from the camera along its forward), <c>Hooks.Ammo</c> for the ammo line when installed.
    /// The GPS target is the mission's when set, else the waypoint; the system sets it on <c>world.Gps</c> and ticks it.
    /// Ped card lookups are cached by citizen id; strings are rebuilt only when their value changes.
    /// </summary>
    public sealed class HudSystem : IGameSystem
    {
        private const float StreetRadius = 30f;
        private const int LocationEverySteps = 6;
        /// <summary>Seconds between rescans of the shops and safehouses near the player.</summary>
        internal const float PlacesRefreshSeconds = 3f;
        /// <summary>Distance the player may move before the places are rescanned early, metres.</summary>
        internal const float PlacesRefreshDistance = 80f;
        private static readonly string[] WeaponNames = { null, "Bat", "Pistol", "SMG", "Shotgun", "Carbine" };
        private static Dictionary<string, GameButton> _buttons;

        private readonly HudModel _m;
        private readonly List<WalletTransaction> _tx = new List<WalletTransaction>();
        private bool _cashInit;
        private long _lastCash;
        private long _lastDelta;
        private string _lastHelpRaw;
        private string _lastHelp;
        private int _lastTimer = int.MinValue;
        private int _locationCountdown;
        private bool _locationInit;
        private int _lastVehicleId;
        private bool _gpsHasTarget;
        private Vec3 _gpsTarget;
        private float _gpsBuiltAt = float.NaN;
        private uint _cardCitizen;
        private PedCardData _card;
        private readonly List<ushort> _nearIds = new List<ushort>(128);
        private readonly List<Vec3> _shops = new List<Vec3>(64);
        private readonly List<Vec3> _safehouses = new List<Vec3>(8);
        private float _placesAge = float.MaxValue;
        private Vec3 _placesAt;

        /// <summary>Creates the system writing into <paramref name="model"/>.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
        public HudSystem(HudModel model)
        {
            if (model == null) throw new ArgumentNullException("model");
            _m = model;
        }

        /// <summary>The camera rig (for the first-person crosshair); set by the session.</summary>
        public CameraRig Rig { get; set; }

        /// <inheritdoc />
        public int Order { get { return SystemOrder.Hud; } }

        /// <inheritdoc />
        public void Step(GameWorld world, float dt)
        {
            HudModel m = _m;
            Ped p = world.Player;
            m.Menu = world.Menu;
            if (p == null) return;
            PlayerState ps = world.PlayerState;
            Car car = p.VehicleId != 0 ? world.FindCar(p.VehicleId) : null;

            m.Health = MathUtil.Clamp01(p.Health / 100f);
            m.Armour = MathUtil.Clamp01(p.Armour / 100f);
            m.Stamina = MathUtil.Clamp01(ps.Stamina / 100f);
            float breathMax = p.Motor.Settings.BreathSeconds;
            m.Breath = breathMax > 0f ? MathUtil.Clamp01(p.Motor.Breath / breathMax) : 1f;
            m.ShowBreath = car == null && (p.Motor.State == MotorState.Swimming || m.Breath < 0.999f);
            m.Heat = MathUtil.Clamp01(world.Wanted.PlayerHeat / 100f);
            m.DistrictHeat = MathUtil.Clamp01(world.Wanted.GetDistrictHeat(world.City.DistrictAt(p.Position.X, p.Position.Z)) / 100f);
            m.Stars = world.Wanted.Stars;
            m.StarsFlashing = world.Wanted.Stars > 0 && world.Wanted.Evading;

            Cash(world, dt);
            Weapon(world, p);
            Texts(world);
            Location(world, p, dt);

            if (car != null && p.VehicleId != _lastVehicleId)
            {
                m.VehicleName = car.Model.Name;
                m.VehicleBannerAge = 0f;
            }
            else m.VehicleBannerAge += dt;
            _lastVehicleId = p.VehicleId;
            m.ShowSpeed = car != null;
            m.SpeedKmh = car != null ? (int)Math.Round(car.Body.Speed * 3.6f) : 0;

            m.BigScreen = world.Messages.BigScreen;
            m.BigScreenText = world.Messages.BigScreenText;
            m.BigScreenReward = world.Messages.BigScreenReward;
            m.BigScreenProgress = world.Messages.BigScreenProgress;
            m.FadeAlpha = world.FadeAlpha;
            m.InInterior = world.Interior.IsInside;

            Crosshair(world, p, car);
            PedCard(world, p, car);
            Map(world, p, car, dt);
            Gps(world, p, dt);

            m.ShowSearchZone = world.Wanted.Stars > 0 && world.Wanted.SearchRadius > 0f;
            m.SearchCentre = world.Wanted.SearchCentre;
            m.SearchRadius = world.Wanted.SearchRadius;

            world.Messages.Advance(dt);
        }

        private void Cash(GameWorld w, float dt)
        {
            HudModel m = _m;
            long cash = w.Profile.Cash;
            if (!_cashInit || cash != _lastCash)
            {
                m.Cash = cash;
                m.CashText = HudModel.FormatCash(cash);
                _lastCash = cash;
                _cashInit = true;
            }
            _tx.Clear();
            w.Wallet.Drain(_tx);
            if (m.CashDeltaAge < float.MaxValue) m.CashDeltaAge += dt;
            for (int i = 0; i < _tx.Count; i++)
            {
                if (m.CashDeltaAge >= HudModel.CashDeltaSeconds) m.CashDelta = 0;
                m.CashDelta += _tx[i].Delta;
                m.CashDeltaAge = 0f;
            }
            if (m.CashDeltaAge >= HudModel.CashDeltaSeconds) m.CashDelta = 0;
            if (m.CashDelta != _lastDelta || (m.CashDelta == 0 && m.CashDeltaText != null))
            {
                m.CashDeltaText = m.CashDelta == 0 ? null : m.CashDelta > 0 ? "+" + HudModel.FormatCash(m.CashDelta) : HudModel.FormatCash(m.CashDelta);
                _lastDelta = m.CashDelta;
            }
        }

        private void Weapon(GameWorld w, Ped p)
        {
            HudModel m = _m;
            int wi = MathUtil.Clamp(p.CurrentWeapon, 0, WeaponInfo.Count - 1);
            WeaponId id = (WeaponId)wi;
            m.WeaponName = WeaponNames[wi];
            m.ShowAmmo = WeaponInfo.UsesAmmo(id);
            int mag, reserve;
            if (w.Hooks.Ammo != null && w.Hooks.Ammo.TryGetAmmo(w, out mag, out reserve))
            {
                m.AmmoInMagazine = mag;
                m.AmmoReserve = reserve;
            }
            else
            {
                m.AmmoInMagazine = m.ShowAmmo ? p.MagazineAmmo : 0;
                m.AmmoReserve = m.ShowAmmo ? w.Profile.GetAmmo(id) : 0;
            }
        }

        private void Texts(GameWorld w)
        {
            HudModel m = _m;
            string raw = w.Messages.HelpText;
            if (!ReferenceEquals(raw, _lastHelpRaw))
            {
                _lastHelpRaw = raw;
                _lastHelp = raw == null ? null : Substitute(raw, w);
            }
            m.HelpText = _lastHelp;

            MissionStatus ms = w.Mission;
            m.Objective = ms.Objective;
            m.ObjectiveSpans.Clear();
            for (int i = 0; i < ms.ObjectiveSpans.Count; i++) m.ObjectiveSpans.Add(ms.ObjectiveSpans[i]);
            int secs = ms.TimerSeconds < 0f ? -1 : (int)Math.Ceiling(ms.TimerSeconds);
            if (secs != _lastTimer)
            {
                m.TimerText = secs < 0 ? null : (secs / 60).ToString(CultureInfo.InvariantCulture) + ":" + (secs % 60).ToString("D2", CultureInfo.InvariantCulture);
                _lastTimer = secs;
            }
            m.StatusLine = ms.StatusLine;

            Subtitle sub;
            if (w.Messages.TryGetSubtitle(out sub)) { m.SubtitleSpeaker = sub.Speaker; m.SubtitleText = sub.Text; }
            else { m.SubtitleSpeaker = null; m.SubtitleText = null; }
            m.Notifications.Clear();
            for (int i = 0; i < w.Messages.NotificationCount; i++) m.Notifications.Add(w.Messages.GetNotification(i));
        }

        private static string Substitute(string raw, GameWorld w)
        {
            if (raw.IndexOf('{') < 0) return raw;
            if (_buttons == null)
            {
                var map = new Dictionary<string, GameButton>();
                foreach (GameButton b in Enum.GetValues(typeof(GameButton))) map[b.ToString()] = b;
                _buttons = map;
            }
            var sb = new StringBuilder(raw.Length + 8);
            int i = 0;
            while (i < raw.Length)
            {
                int open = raw.IndexOf('{', i);
                int close = open < 0 ? -1 : raw.IndexOf('}', open + 1);
                if (open < 0 || close < 0) { sb.Append(raw, i, raw.Length - i); break; }
                sb.Append(raw, i, open - i);
                GameButton button;
                string name = raw.Substring(open + 1, close - open - 1);
                if (_buttons.TryGetValue(name, out button))
                {
                    string key = w.Settings.Bindings.Get(button, 0);
                    sb.Append('~').Append(KeyLabel(key)).Append('~');
                }
                else sb.Append(raw, open, close - open + 1);
                i = close + 1;
            }
            return sb.ToString();
        }

        /// <summary>Short on-screen name of a Unity key name ("UpArrow" → "Up", "LeftShift" → "Shift", "Mouse0" → "LMB").</summary>
        internal static string KeyLabel(string key)
        {
            if (string.IsNullOrEmpty(key)) return "?";
            switch (key)
            {
                case "Mouse0": return "LMB";
                case "Mouse1": return "RMB";
                case "Mouse2": return "MMB";
                case "Return": return "Enter";
                case "Escape": return "Esc";
                case "LeftControl": case "RightControl": return "Ctrl";
                case "LeftShift": case "RightShift": return "Shift";
                case "LeftAlt": case "RightAlt": return "Alt";
            }
            if (key.Length > 5 && key.EndsWith("Arrow", StringComparison.Ordinal)) return key.Substring(0, key.Length - 5);
            return key;
        }

        private void Location(GameWorld w, Ped p, float dt)
        {
            HudModel m = _m;
            m.LocationBannerAge += dt;
            if (w.Interior.IsInside) return;
            if (_locationInit && --_locationCountdown > 0) return;
            _locationCountdown = LocationEverySteps;
            Vec3 pos = p.Position;
            string street = w.City.StreetNameNear(pos, StreetRadius);
            string district = w.City.DistrictName(w.City.DistrictAt(pos.X, pos.Z));
            if (!_locationInit || !string.Equals(street, m.StreetName) || !string.Equals(district, m.DistrictName))
            {
                m.StreetName = street;
                m.DistrictName = district;
                m.LocationBannerAge = 0f;
            }
            _locationInit = true;
        }

        private void Crosshair(GameWorld w, Ped p, Car car)
        {
            HudModel m = _m;
            if (car != null || w.Menu.IsOpen || m.BigScreen != BigScreenKind.None || p.IsDead) { m.Crosshair = CrosshairKind.None; return; }
            if (p.Has(PedFlags.Aiming))
            {
                Ped target = w.PlayerState.AimTargetTag != 0 ? w.PedFromTag(w.PlayerState.AimTargetTag, false, 0) : null;
                m.Crosshair = target != null && target.Has(PedFlags.Hostile) ? CrosshairKind.Hostile : CrosshairKind.Dot;
                return;
            }
            m.Crosshair = Rig != null && Rig.FootView == FootView.FirstPerson ? CrosshairKind.Dot : CrosshairKind.None;
        }

        private void PedCard(GameWorld w, Ped p, Car car)
        {
            HudModel m = _m;
            m.HasPedCard = false;
            if (car != null) return;
            PhysicsHit hit;
            QueryMask mask = QueryMask.DynamicOnly.Ignoring(p.Tag);
            if (!w.Collision.Raycast(w.ViewPosition, w.ViewRotation * Vec3.Forward, HudModel.PedCardRange, mask, out hit)) return;
            uint citizen = 0;
            TagKind kind = WorldTag.KindOf(hit.Tag);
            if (kind == TagKind.AmbientCitizen)
            {
                AmbientCitizen c;
                if (w.TryGetAmbientCitizen((ushort)WorldTag.IdOf(hit.Tag), out c)) citizen = c.Citizen;
            }
            else if (kind == TagKind.Ped)
            {
                Ped o = w.FindPed(WorldTag.IdOf(hit.Tag));
                if (o != null) citizen = o.Citizen;
            }
            if (citizen == 0) return;
            if (citizen != _cardCitizen)
            {
                CitizenProfile prof;
                if (!w.City.TryGetCitizenProfile(citizen, out prof)) return;
                BuildingDescriptor b;
                _card = new PedCardData
                {
                    Citizen = citizen,
                    Name = prof.Name,
                    Age = prof.Age,
                    WorkName = prof.WorkBuilding != 0 && w.City.TryGetBuilding(prof.WorkBuilding, out b) ? b.Name : null,
                    HomeName = prof.HomeBuilding != 0 && w.City.TryGetBuilding(prof.HomeBuilding, out b) ? b.Name : null,
                    Happiness = prof.Happiness,
                    Student = prof.Student,
                    Tourist = prof.Tourist,
                    Employed = prof.Employed,
                    Retired = prof.Age >= 65 && !prof.Employed,
                };
                _cardCitizen = citizen;
            }
            m.PedCard = _card;
            m.HasPedCard = true;
        }

        private void Map(GameWorld w, Ped p, Car car, float dt)
        {
            HudModel m = _m;
            m.PlayerPosition = p.Position;
            m.MapRotationDeg = Vec3.YawDegOf(w.ViewRotation * Vec3.Forward);
            float speed = car != null ? Math.Abs(car.Body.Speed) : p.Motor.Velocity.Flat.Length;
            float target = MathUtil.Lerp(HudModel.MinimapRadiusMin, HudModel.MinimapRadiusMax, MathUtil.Clamp01(speed / 30f));
            m.MinimapRadius += (target - m.MinimapRadius) * MathUtil.ApproachFactor(0.5f, dt);

            List<HudBlip> blips = m.Blips;
            blips.Clear();
            blips.Add(new HudBlip { Kind = BlipKind.Player, Position = p.Position, YawDeg = car != null ? car.Rotation.YawDeg : p.YawDeg });
            if (w.HasWaypoint) blips.Add(new HudBlip { Kind = BlipKind.Waypoint, Position = w.Waypoint });
            List<Blip> mb = w.Mission.Blips;
            for (int i = 0; i < mb.Count; i++)
            {
                Blip b = mb[i];
                Vec3 at = b.Position;
                if (b.FollowTag != 0)
                {
                    Vec3 followed;
                    if (w.TryGetTagPosition(b.FollowTag, out followed)) at = followed;
                }
                blips.Add(new HudBlip { Kind = b.Kind, Position = at, Flashing = b.Flashing });
            }
            Places(w, p.Position, dt);
            float range = m.MinimapRadius * 1.2f;
            for (int i = 0; i < _shops.Count; i++)
                if (Vec3.DistanceXZ(_shops[i], p.Position) <= range) blips.Add(new HudBlip { Kind = BlipKind.Shop, Position = _shops[i] });
            for (int i = 0; i < _safehouses.Count; i++)
                blips.Add(new HudBlip { Kind = BlipKind.Safehouse, Position = _safehouses[i] });
            if (w.Wanted.Stars > 0)
            {
                List<Ped> peds = w.PedList;
                for (int i = 0; i < peds.Count; i++)
                    if (peds[i].Kind == PedKind.Cop && peds[i].VehicleId == 0 && !peds[i].IsDead) blips.Add(new HudBlip { Kind = BlipKind.Cop, Position = peds[i].Position });
                List<Car> cars = w.CarList;
                for (int i = 0; i < cars.Count; i++)
                    if (cars[i].Owner == CarOwner.Police) blips.Add(new HudBlip { Kind = BlipKind.Cop, Position = cars[i].Position });
            }
        }

        /// <summary>Rescans shop buildings (commercial buildings with a shop interior archetype) and owned safehouses every few seconds or after the player moved far.</summary>
        private void Places(GameWorld w, Vec3 pos, float dt)
        {
            _placesAge += dt;
            if (_placesAge < PlacesRefreshSeconds && Vec3.DistanceXZ(pos, _placesAt) < PlacesRefreshDistance) return;
            _placesAge = 0f;
            _placesAt = pos;
            _shops.Clear();
            _nearIds.Clear();
            w.City.ServiceBuildings(ServiceKind.Commercial, _nearIds);
            float range = HudModel.MinimapRadiusMax * 1.2f;
            BuildingDescriptor b;
            for (int i = 0; i < _nearIds.Count; i++)
            {
                if (!w.City.TryGetBuilding(_nearIds[i], out b) || Vec3.DistanceXZ(b.Position, pos) > range) continue;
                InteriorArchetype a = InteriorGenerator.ArchetypeFor(b);
                if (a == InteriorArchetype.ConvenienceStore || a == InteriorArchetype.ClothesShop || a == InteriorArchetype.GunStore) _shops.Add(b.Position);
            }
            _safehouses.Clear();
            IList<uint> props = w.Profile.Properties;
            for (int i = 0; i < props.Count; i++)
                if (props[i] <= ushort.MaxValue && w.City.TryGetBuilding((ushort)props[i], out b)) _safehouses.Add(b.Position);
        }

        private void Gps(GameWorld w, Ped p, float dt)
        {
            HudModel m = _m;
            bool has = w.Mission.HasGpsTarget || w.HasWaypoint;
            Vec3 target = w.Mission.HasGpsTarget ? w.Mission.GpsTarget : w.Waypoint;
            if (!has)
            {
                if (_gpsHasTarget)
                {
                    w.Gps.ClearTarget();
                    m.GpsPoints.Clear();
                    m.GpsRevision++;
                    _gpsHasTarget = false;
                    _gpsBuiltAt = float.NaN;
                }
                return;
            }
            bool changed = !_gpsHasTarget || target != _gpsTarget;
            if (changed)
            {
                w.Gps.ClearTarget();
                w.Gps.SetTarget(target);
                _gpsTarget = target;
                _gpsHasTarget = true;
            }
            bool recomputed = w.Gps.Tick(dt, p.Position);
            float progress = w.Gps.Progress;
            if (changed || recomputed || float.IsNaN(_gpsBuiltAt) || Math.Abs(progress - _gpsBuiltAt) >= HudModel.GpsPointSpacing)
            {
                w.Gps.Route.PointsAhead(progress, HudModel.GpsPointSpacing, HudModel.MaxGpsPoints, m.GpsPoints);
                _gpsBuiltAt = progress;
                m.GpsRevision++;
            }
        }
    }
}
