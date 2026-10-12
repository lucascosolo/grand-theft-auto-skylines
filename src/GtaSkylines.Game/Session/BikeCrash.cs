using GtaSkylines.Game.Combat;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Session
{
    /// <summary>Throws the player off a two-wheeler that crashed (<see cref="Vehicles.VehicleBody.Fallen"/>).</summary>
    internal static class BikeCrash
    {
        /// <summary>Health lost per m/s of <see cref="Vehicles.VehicleBody.CrashSpeed"/>, on top of the bail damage. 3.</summary>
        public const float DamagePerSpeed = 3f;

        /// <summary>
        /// When the player is seated in a fallen vehicle: a bail exit (<see cref="PlayerControlSystem.ExitCar"/>), then
        /// <c>DamagePerSpeed · CrashSpeed</c> vehicle damage with a knock-down. Otherwise nothing.
        /// </summary>
        public static void ThrowRider(GameWorld w, PlayerControlSystem control)
        {
            Ped p = w.Player;
            if (p == null || p.IsDead || !p.Has(PedFlags.InVehicle)) return;
            Car car = w.FindCar(p.VehicleId);
            if (car == null || !car.Body.Fallen) return;
            float speed = car.Body.CrashSpeed;
            control.ExitCar(w, p, w.PlayerState, car, true);
            Damage.ApplyToPed(w, p, DamagePerSpeed * speed, car.Tag, DamageKind.Vehicle, true, speed);
        }
    }
}
