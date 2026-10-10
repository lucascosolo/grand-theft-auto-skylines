using System.Collections.Generic;
using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Game.City
{
    /// <summary>What the Game asks the Mod to do to the city.</summary>
    public enum CityEffectKind
    {
        /// <summary>
        /// A walking citizen became a Game ped actor: remove its CS1 instance from the street (the citizen itself stays
        /// alive in CS1). Fields: <see cref="CityEffect.Citizen"/>, <see cref="CityEffect.Instance"/>.
        /// </summary>
        TakeCitizen,

        /// <summary>
        /// A ped actor is handed back: the citizen continues its CS1 life as if it had reached where it was going (or
        /// home). Fields: Citizen, Position.
        /// </summary>
        ReturnCitizen,

        /// <summary>A moving CS1 vehicle became a Game vehicle: remove it from CS1's traffic. Fields: Vehicle.</summary>
        TakeVehicle,

        /// <summary>A CS1 parked vehicle became a Game vehicle: remove it from CS1. Fields: Vehicle.</summary>
        TakeParkedVehicle,

        /// <summary>The citizen is injured: CS1 marks it sick so an ambulance comes. Fields: Citizen, Building (where it is taken; 0 = its home).</summary>
        InjureCitizen,

        /// <summary>The citizen dies through CS1's death path so a hearse comes. Fields: Citizen, Building (0 = its home).</summary>
        KillCitizen,

        /// <summary>A still-walking CS1 citizen panics and heads home. Fields: Instance.</summary>
        PanicCitizen,

        /// <summary>Reduce a building's fire by <see cref="CityEffect.Amount"/> (0-1 of its maximum); 1 puts it out. Fields: Building, Amount.</summary>
        DouseFire,

        /// <summary>Post a Chirper message as a citizen. Fields: Citizen (sender, 0 = anonymous), Text.</summary>
        Chirp,

        /// <summary>Move CS1's clock forward by <see cref="CityEffect.Amount"/> hours (sleeping in a safehouse).</summary>
        AdvanceTime,
    }

    /// <summary>One request from the Game to the city. Applied by the Mod on CS1's simulation thread, in order.</summary>
    public struct CityEffect
    {
        /// <summary>What to do.</summary>
        public CityEffectKind Kind;
        /// <summary>CS1 citizen id, when relevant.</summary>
        public uint Citizen;
        /// <summary>CS1 citizen instance id, when relevant.</summary>
        public ushort Instance;
        /// <summary>CS1 vehicle or parked vehicle id, when relevant.</summary>
        public ushort Vehicle;
        /// <summary>CS1 building id, when relevant.</summary>
        public ushort Building;
        /// <summary>A world position, when relevant.</summary>
        public Vec3 Position;
        /// <summary>An amount, when relevant.</summary>
        public float Amount;
        /// <summary>Text, when relevant.</summary>
        public string Text;

        /// <summary>See <see cref="CityEffectKind.TakeCitizen"/>.</summary>
        public static CityEffect TakeCitizen(uint citizen, ushort instance) { return new CityEffect { Kind = CityEffectKind.TakeCitizen, Citizen = citizen, Instance = instance }; }

        /// <summary>See <see cref="CityEffectKind.ReturnCitizen"/>.</summary>
        public static CityEffect ReturnCitizen(uint citizen, Vec3 position) { return new CityEffect { Kind = CityEffectKind.ReturnCitizen, Citizen = citizen, Position = position }; }

        /// <summary>See <see cref="CityEffectKind.TakeVehicle"/> and <see cref="CityEffectKind.TakeParkedVehicle"/>.</summary>
        public static CityEffect TakeVehicle(ushort vehicle, bool parked) { return new CityEffect { Kind = parked ? CityEffectKind.TakeParkedVehicle : CityEffectKind.TakeVehicle, Vehicle = vehicle }; }

        /// <summary>See <see cref="CityEffectKind.InjureCitizen"/>.</summary>
        public static CityEffect InjureCitizen(uint citizen, ushort building) { return new CityEffect { Kind = CityEffectKind.InjureCitizen, Citizen = citizen, Building = building }; }

        /// <summary>See <see cref="CityEffectKind.KillCitizen"/>.</summary>
        public static CityEffect KillCitizen(uint citizen, ushort building) { return new CityEffect { Kind = CityEffectKind.KillCitizen, Citizen = citizen, Building = building }; }

        /// <summary>See <see cref="CityEffectKind.PanicCitizen"/>.</summary>
        public static CityEffect PanicCitizen(ushort instance) { return new CityEffect { Kind = CityEffectKind.PanicCitizen, Instance = instance }; }

        /// <summary>See <see cref="CityEffectKind.DouseFire"/>.</summary>
        public static CityEffect DouseFire(ushort building, float amount) { return new CityEffect { Kind = CityEffectKind.DouseFire, Building = building, Amount = amount }; }

        /// <summary>See <see cref="CityEffectKind.Chirp"/>.</summary>
        public static CityEffect Chirp(uint sender, string text) { return new CityEffect { Kind = CityEffectKind.Chirp, Citizen = sender, Text = text }; }

        /// <summary>See <see cref="CityEffectKind.AdvanceTime"/>.</summary>
        public static CityEffect AdvanceTime(float hours) { return new CityEffect { Kind = CityEffectKind.AdvanceTime, Amount = hours }; }
    }

    /// <summary>The queue of effects produced by a Game tick; the Mod drains it after each tick.</summary>
    public sealed class CityEffectQueue
    {
        private readonly List<CityEffect> _items = new List<CityEffect>();

        /// <summary>Queued effects, oldest first.</summary>
        public IList<CityEffect> Items { get { return _items; } }

        /// <summary>Number queued.</summary>
        public int Count { get { return _items.Count; } }

        /// <summary>Queues an effect.</summary>
        public void Add(CityEffect effect) { _items.Add(effect); }

        /// <summary>Copies every queued effect into <paramref name="into"/> (appending) and empties the queue.</summary>
        public void DrainTo(List<CityEffect> into)
        {
            into.AddRange(_items);
            _items.Clear();
        }
    }
}
