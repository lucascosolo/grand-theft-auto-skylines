namespace GtaSkylines.Game.World
{
    /// <summary>
    /// AI for one non-player ped (G7 supplies the implementations; G8 the cop brain). Called by the Peds system
    /// (order 30) once per step for every living ped that has one, before that ped's motor is moved.
    /// </summary>
    public interface IPedBrain
    {
        /// <summary>Short name for the debug overlay ("Flee", "CallPolice").</summary>
        string Name { get; }

        /// <summary>
        /// True while the brain is doing something that must not be interrupted by demotion (fleeing, calling, fighting).
        /// G6 demotes a ped only when this is false (or the ped has no brain) and the other demotion rules hold.
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Decides this step: writes <see cref="Ped.Move"/>, <see cref="Ped.YawDeg"/>, <see cref="Ped.Anim"/> and flags;
        /// may replace <c>ped.Brain</c> with the next brain (it takes effect next step). Allocates nothing in steady state.
        /// </summary>
        void Step(GameWorld world, Ped ped, float dt);
    }

    /// <summary>
    /// AI control of a Game car (G8 police, G9 missions; G6's <see cref="RouteDriver"/>). Called by the Vehicles system
    /// (order 20) once per step for each car whose <see cref="Car.Driver"/> is set and which the player is not driving.
    /// A driver acts only while the car has a living driver ped in seat 0, except when <see cref="Driverless"/> is true.
    /// </summary>
    public interface IDriver
    {
        /// <summary>Short name for the debug overlay.</summary>
        string Name { get; }

        /// <summary>True when the car may be driven without a ped in the driver seat (race rivals, scripted cars).</summary>
        bool Driverless { get; }

        /// <summary>This step's controls for <paramref name="car"/>. May also set the car's siren, light bar and horn.</summary>
        Vehicles.VehicleControls Drive(GameWorld world, Car car, float dt);
    }
}
