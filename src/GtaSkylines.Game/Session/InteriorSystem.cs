using System;
using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Interiors;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Physics;
using GtaSkylines.Game.Profile;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Session
{
    /// <summary>Interior transitions, occupants and the menus raised by interactions (order 40). Rules on <see cref="GameSession"/>.</summary>
    internal sealed class InteriorSystem : IGameSystem
    {
        private const string ClosedHelp = "Closed";
        private const int MaxWardrobeOutfits = 12;

        private enum Pending { None, Enter, Exit, Elevator }

        private readonly GameSession _session;
        private readonly PedVitals _vitals = new PedVitals();
        private readonly List<Occupant> _occupants = new List<Occupant>();
        private Pending _pending;
        private BuildingDescriptor _pendingBuilding;
        private InteriorLayout _pendingLayout;
        private Vec3 _pendingDoor;

        public InteriorSystem(GameSession session)
        {
            _session = session;
        }

        public int Order { get { return SystemOrder.Interiors; } }

        internal void RequestEnter(GameWorld w, BuildingDescriptor b, Vec3 door)
        {
            if (w.Interior.Phase != InteriorPhase.Outside) return;
            InteriorLayout layout = b.Abandoned ? null : InteriorGenerator.Generate(b);
            if (layout == null)
            {
                w.Messages.ShowHelp(ClosedHelp, 2, 3f);
                return;
            }
            _pending = Pending.Enter;
            _pendingBuilding = b;
            _pendingLayout = layout;
            _pendingDoor = door;
            w.Interior.Phase = InteriorPhase.FadingOut;
            w.Interior.PhaseSeconds = 0f;
        }

        internal void UsePoint(GameWorld w, int index)
        {
            InteriorState it = w.Interior;
            if (it.Phase != InteriorPhase.Inside || it.Layout == null || index < 0 || index >= it.Layout.Interactions.Count) return;
            InteractionPoint pt = it.Layout.Interactions[index];
            BuildingDescriptor b = it.Building;
            bool owned = Owned(w, b);
            switch (pt.Kind)
            {
                case InteractionKind.ExitDoor:
                    StartLeave(w, it.Layout.Archetype == InteriorArchetype.Apartment ? Pending.Elevator : Pending.Exit);
                    break;
                case InteractionKind.ElevatorDoor:
                    StartLeave(w, Pending.Elevator);
                    break;
                case InteractionKind.ShopCounter:
                    OpenShop(w, MenuRequest.ShopKindFor(pt.Shop, b.Level));
                    break;
                case InteractionKind.HospitalDesk:
                case InteractionKind.PoliceDesk:
                case InteractionKind.RespraySpot:
                    OpenShop(w, MenuRequest.ShopKindFor(pt.Kind));
                    break;
                case InteractionKind.Bed:
                    if (owned) Open(w, MenuKind.Bed);
                    break;
                case InteractionKind.Wardrobe:
                    if (!owned) break;
                    Open(w, MenuKind.Wardrobe);
                    int n = Math.Min(MaxWardrobeOutfits, w.City.CitizenModelCount);
                    for (int i = 0; i < n; i++) w.Menu.Outfits.Add(i);
                    break;
            }
        }

        private static bool Owned(GameWorld w, BuildingDescriptor b)
        {
            return b != null && w.Profile.OwnsProperty(b.Id);
        }

        private static void Open(GameWorld w, MenuKind kind)
        {
            MenuRequest m = w.Menu;
            m.Close();
            m.Kind = kind;
            m.Building = w.Interior.Building != null ? w.Interior.Building.Id : (ushort)0;
            m.Title = w.Interior.Building != null ? w.Interior.Building.Name : null;
            m.LastResult = PurchaseResult.Ok;
            m.Revision++;
        }

        private static void OpenShop(GameWorld w, ShopKind kind)
        {
            Open(w, MenuKind.Shop);
            w.Menu.Shop = kind;
            w.Menu.Items.AddRange(Shop.ItemsFor(kind));
        }

        private void StartLeave(GameWorld w, Pending what)
        {
            _pending = what;
            w.Interior.Phase = InteriorPhase.FadingOutToExit;
            w.Interior.PhaseSeconds = 0f;
        }

        public void Step(GameWorld w, float dt)
        {
            InteriorState it = w.Interior;
            if (it.Phase == InteriorPhase.Outside || it.Phase == InteriorPhase.Inside) return;
            it.PhaseSeconds += dt;
            float t = MathUtil.Clamp01(it.PhaseSeconds / InteriorState.FadeSeconds);
            switch (it.Phase)
            {
                case InteriorPhase.FadingOut:
                case InteriorPhase.FadingOutToExit:
                    w.FadeAlpha = t;
                    if (t >= 1f) Arrive(w);
                    break;
                case InteriorPhase.FadingIn:
                case InteriorPhase.FadingInOutside:
                    w.FadeAlpha = 1f - t;
                    if (t >= 1f)
                    {
                        it.Phase = it.Phase == InteriorPhase.FadingIn ? InteriorPhase.Inside : InteriorPhase.Outside;
                        it.PhaseSeconds = 0f;
                        w.FadeAlpha = 0f;
                    }
                    break;
            }
        }

        private void Arrive(GameWorld w)
        {
            InteriorState it = w.Interior;
            Ped p = w.Player;
            Pending what = _pending;
            _pending = Pending.None;
            it.PhaseSeconds = 0f;
            if (what == Pending.Enter)
            {
                BuildingDescriptor b = _pendingBuilding;
                it.Door = _pendingDoor;
                it.Trespassing = b.Service == ServiceKind.Residential && !Owned(w, b);
                Load(w, b, _pendingLayout, false);
                _pendingLayout = null;
                it.Phase = InteriorPhase.FadingIn;
                w.Emit(GameEventType.EnteredInterior, it.Door, p.Tag, 0, b.Id, null);
                if (it.Trespassing) w.Emit(GameEventType.Trespass, it.Door, p.Tag, 0, b.Id, null);
                w.Profile.AddStat(PlayerRules.StatInteriorsVisited, 1);
            }
            else if (what == Pending.Elevator && it.Layout != null)
            {
                BuildingDescriptor b = it.Building;
                bool toApartment = it.Layout.Archetype != InteriorArchetype.Apartment;
                InteriorLayout next = toApartment
                    ? InteriorGenerator.Generate(b, InteriorArchetype.Apartment, 1 + b.Id % InteriorState.ApartmentVariants)
                    : InteriorGenerator.Generate(b);
                if (next == null) { it.Phase = InteriorPhase.FadingIn; return; }
                RemoveOccupants(w);
                Load(w, b, next, true);
                it.Phase = InteriorPhase.FadingIn;
            }
            else
            {
                BuildingDescriptor b = it.Building;
                Unload(w);
                Vec3 outward = b != null ? (it.Door - b.Position).Flat : Vec3.Zero;
                outward = outward.Length > 1e-4f ? outward.Normalized : Vec3.Forward;
                Vec3 spot = it.Door + outward;
                p.Motor.Teleport(spot);
                p.PrevPosition = spot;
                p.YawDeg = Vec3.YawDegOf(outward);
                w.PlayerState.ViewYawDeg = p.YawDeg;
                it.Phase = InteriorPhase.FadingInOutside;
                if (b != null) w.Emit(GameEventType.ExitedInterior, it.Door, p.Tag, 0, b.Id, null);
            }
        }

        private void Load(GameWorld w, BuildingDescriptor b, InteriorLayout layout, bool atElevator)
        {
            InteriorState it = w.Interior;
            it.Building = b;
            it.Layout = layout;
            it.Origin = InteriorState.OriginFor(b);
            it.YawDeg = b.YawDeg;
            it.CustomInteractions.Clear();

            int n = layout.CollisionTriangleCount;
            float[] src = layout.CollisionTriangles;
            var pos = new float[n * 9];
            var flags = new ushort[n];
            for (int i = 0; i < n * 3; i++)
            {
                Vec3 v = it.ToWorld(new Vec3(src[i * 3], src[i * 3 + 1], src[i * 3 + 2]));
                pos[i * 3] = v.X;
                pos[i * 3 + 1] = v.Y;
                pos[i * 3 + 2] = v.Z;
            }
            for (int i = 0; i < n; i++) flags[i] = (ushort)SurfaceFlags.Interior;
            w.Collision.SetRegion(InteriorState.RegionKey, pos, flags, n);

            Ped p = w.Player;
            Vec3 spawn = it.ToWorld(atElevator ? layout.ElevatorSpawn : layout.EntranceSpawn);
            float yaw = MathUtil.WrapDeg((atElevator ? layout.ElevatorSpawnYawDeg : layout.EntranceSpawnYawDeg) + it.YawDeg);
            p.Motor.Teleport(spawn);
            p.PrevPosition = spawn;
            p.YawDeg = yaw;
            p.InteriorId = b.Id;
            w.PlayerState.ViewYawDeg = yaw;
            w.PlayerState.ViewPitchDeg = 0f;
            SpawnOccupants(w, b, layout);
        }

        private void SpawnOccupants(GameWorld w, BuildingDescriptor b, InteriorLayout layout)
        {
            InteriorState it = w.Interior;
            w.City.Occupants(b.Id, _occupants);
            bool home = b.Service == ServiceKind.Residential;
            int next = 0;
            int models = Math.Max(1, w.City.CitizenModelCount);
            for (int i = 0; i < layout.NpcSpots.Count; i++)
            {
                NpcSpot spot = layout.NpcSpots[i];
                uint citizen = 0;
                int model = -1;
                while (next < _occupants.Count)
                {
                    Occupant o = _occupants[next++];
                    if (o.Resident != home || w.FindPedByCitizen(o.Citizen) != null) continue;
                    citizen = o.Citizen;
                    model = o.Model;
                    break;
                }
                if (model < 0) model = new Rng(Rng.Seed(b.Id, (uint)i, w.Seed)).Range(0, models);
                Ped ped = w.SpawnPed(PedKind.Npc, model, it.ToWorld(spot.Position), MathUtil.WrapDeg(spot.YawDeg + it.YawDeg));
                ped.InteriorId = b.Id;
                ped.Citizen = citizen;
                w.RegisterCitizen(ped);
                ped.Anim = spot.Seated ? PedAnim.Sit : PedAnim.Idle;
                ped.Brain = w.CreateBrain(ped, PedOrigin.InteriorOccupant, 0, spot);
                it.Occupants.Add(ped.Id);
            }
        }

        private static void RemoveOccupants(GameWorld w)
        {
            List<int> ids = w.Interior.Occupants;
            for (int i = ids.Count - 1; i >= 0; i--)
            {
                if (i >= ids.Count) continue;
                Ped p = w.FindPed(ids[i]);
                if (p != null) w.RemovePed(p, false);
            }
            ids.Clear();
        }

        /// <summary>Removes the loaded interior (region, occupants, state) without moving the player.</summary>
        internal void Unload(GameWorld w)
        {
            InteriorState it = w.Interior;
            _pending = Pending.None;
            _pendingLayout = null;
            if (it.Layout == null && it.Phase == InteriorPhase.Outside) return;
            RemoveOccupants(w);
            w.Collision.RemoveRegion(InteriorState.RegionKey);
            it.Layout = null;
            it.Building = null;
            it.Trespassing = false;
            it.CustomInteractions.Clear();
            it.Phase = InteriorPhase.Outside;
            it.PhaseSeconds = 0f;
            w.Menu.Close();
            if (w.Player != null) w.Player.InteriorId = -1;
        }

        // ---- menus -------------------------------------------------------------------------------------------

        internal PurchaseResult Choose(GameWorld w, int index)
        {
            MenuRequest m = w.Menu;
            Ped p = w.Player;
            switch (m.Kind)
            {
                case MenuKind.Shop:
                    if (index < 0 || index >= m.Items.Count) return PurchaseResult.UnknownItem;
                    return Buy(w, m, m.Items[index]);
                case MenuKind.Bed:
                    if (index == 0)
                    {
                        w.Profile.LastSafehouse = m.Building;
                        _session.ProfileForSave();
                        _session.SaveRequested = true;
                        return PurchaseResult.Ok;
                    }
                    if (index == 1)
                    {
                        w.Effects.Add(CityEffect.AdvanceTime(PlayerRules.SleepHours));
                        p.Health = 100f;
                        w.Emit(GameEventType.Slept, p.Position, p.Tag, 0, PlayerRules.SleepHours, null);
                        m.Close();
                        return PurchaseResult.Ok;
                    }
                    if (index == 2) { m.Close(); return PurchaseResult.Ok; }
                    return PurchaseResult.UnknownItem;
                case MenuKind.Wardrobe:
                    if (index < 0 || index >= m.Outfits.Count) return PurchaseResult.UnknownItem;
                    int model = m.Outfits[index];
                    w.Profile.OutfitId = model;
                    p.Model = model;
                    w.Emit(GameEventType.OutfitChanged, p.Position, p.Tag, 0, model, null);
                    return PurchaseResult.Ok;
                case MenuKind.Property:
                    if (index == 1) { m.Close(); return PurchaseResult.Ok; }
                    if (index != 0) return PurchaseResult.UnknownItem;
                    BuildingDescriptor b;
                    if (!w.City.TryGetBuilding(m.Building, out b)) return PurchaseResult.InvalidProperty;
                    PurchaseResult pr = Property.Buy(w.Wallet, b.Id, b.Level, b.Width * b.Length);
                    m.LastResult = pr;
                    if (pr == PurchaseResult.Ok) m.Close();
                    return pr;
                default:
                    return PurchaseResult.UnknownItem;
            }
        }

        private PurchaseResult Buy(GameWorld w, MenuRequest m, ShopItem item)
        {
            Ped p = w.Player;
            PlayerProfile prof = w.Profile;
            Car car = item.Effect == ItemEffect.Respray ? RespraySubject(w, m) : null;
            if (item.Effect == ItemEffect.Respray && car == null) { m.LastResult = PurchaseResult.UnknownItem; return m.LastResult; }
            long before = prof.Cash;
            _vitals.Ped = p;
            PurchaseResult r = Shop.Buy(w.Wallet, m.Shop, item.Id, _vitals);
            m.LastResult = r;
            if (r != PurchaseResult.Ok) return r;
            w.Emit(GameEventType.Purchase, p.Position, p.Tag, 0, before - prof.Cash, item.Id);
            p.Model = prof.OutfitId;
            if (car != null)
            {
                int colour = car.ColourRgb;
                while (colour == car.ColourRgb) colour = (int)(w.Rng.NextUInt() & 0xFFFFFF);
                car.ColourRgb = colour;
                if (w.Wanted.Description.CarId == car.Id) w.Wanted.Description.CarColour = colour;
                w.Emit(GameEventType.Resprayed, car.Position, p.Tag, car.Tag, colour, null);
            }
            return r;
        }

        private static Car RespraySubject(GameWorld w, MenuRequest m)
        {
            Ped p = w.Player;
            if (p.VehicleId != 0) return w.FindCar(p.VehicleId);
            Car last = w.FindCar(w.PlayerState.LastCarId);
            if (last == null || last.Destroyed) return null;
            Vec3 door = w.Interior.Building != null ? w.Interior.Door : p.Position;
            return Vec3.DistanceXZ(last.Position, door) <= PlayerRules.RespraySearchRadius ? last : null;
        }

        /// <summary>The live player ped as the shop's <see cref="IVitals"/> (one instance, no allocation per purchase).</summary>
        private sealed class PedVitals : IVitals
        {
            public Ped Ped;

            public float Health { get { return Ped.Health; } set { Ped.Health = MathUtil.Clamp(value, 0f, PlayerProfile.MaxVital); } }

            public float Armour { get { return Ped.Armour; } set { Ped.Armour = MathUtil.Clamp(value, 0f, PlayerProfile.MaxVital); } }
        }
    }
}
