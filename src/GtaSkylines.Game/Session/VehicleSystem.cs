using System;
using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Vehicles;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Session
{
    /// <summary>Steps every Game car's body and driver (order 20). Rules on <see cref="GameSession"/>.</summary>
    internal sealed class VehicleSystem : IGameSystem
    {
        private const float SirenTapSeconds = 0.3f;
        private const float ExplosionRadius = 8f;

        private readonly PlayerControlSystem _control;
        private float _interactHeld;
        private bool _tapArmed;

        public VehicleSystem(PlayerControlSystem control)
        {
            _control = control;
        }

        public int Order { get { return SystemOrder.Vehicles; } }

        public void Step(GameWorld w, float dt)
        {
            List<Car> cars = w.CarList;
            if (cars.Count == 0) return;
            w.RebuildDynamicShapes(false);
            Ped player = w.Player;
            for (int i = 0; i < cars.Count; i++)
            {
                Car car = cars[i];
                bool playerDrives = player != null && car.DriverId == player.Id && player.Has(PedFlags.InVehicle);
                VehicleControls c;
                if (playerDrives) c = PlayerControls(w, car, dt);
                else
                {
                    car.Horn = false;
                    Ped driver = car.DriverId != 0 ? w.FindPed(car.DriverId) : null;
                    bool canDrive = car.Driver != null && (car.Driver.Driverless || (driver != null && !driver.IsDead));
                    c = canDrive ? car.Driver.Drive(w, car, dt) : new VehicleControls { Throttle = 1f, Brake = 1f, Handbrake = driver == null };
                }
                car.Controls = c;
                VehicleStepResult r = car.Body.Step(dt, c, w.Collision, car.Tag);
                HandleImpacts(w, car, ref r, playerDrives);
                if (r.Exploded) Explode(w, car);
            }
            for (int i = 0; i < cars.Count; i++)
            {
                for (int j = i + 1; j < cars.Count; j++)
                {
                    Car a = cars[i], b = cars[j];
                    float reach = a.Model.HalfExtents.Length + b.Model.HalfExtents.Length;
                    if ((a.Position - b.Position).LengthSquared > reach * reach) continue;
                    VehicleImpact ia, ib;
                    if (!VehicleBody.ResolveCollision(a.Body, a.Tag, b.Body, b.Tag, out ia, out ib)) continue;
                    w.Emit(GameEventType.CarCollision, ia.Point, a.Tag, b.Tag, ia.RelativeNormalSpeed, null);
                    if (ia.Damage > 0f) w.Emit(GameEventType.CarDamaged, ia.Point, b.Tag, a.Tag, ia.Damage, null);
                    if (ib.Damage > 0f) w.Emit(GameEventType.CarDamaged, ib.Point, a.Tag, b.Tag, ib.Damage, null);
                }
            }
            for (int i = 0; i < cars.Count; i++)
            {
                Car car = cars[i];
                for (int s = 0; s < Car.MaxSeats; s++)
                {
                    if (car.Seats[s] == 0) continue;
                    Ped p = w.FindPed(car.Seats[s]);
                    if (p == null) continue;
                    p.Motor.Teleport(car.Position);
                    p.YawDeg = car.Rotation.YawDeg;
                }
            }
            w.AddCarShapes();
        }

        private VehicleControls PlayerControls(GameWorld w, Car car, float dt)
        {
            InputFrame inp = w.Input;
            PlayerState ps = w.PlayerState;
            bool alive = ps.Life == LifeState.Alive && !w.Player.IsDead && !w.Menu.IsOpen;
            var c = new VehicleControls();
            if (alive)
            {
                c.Throttle = Math.Max(inp.MoveY, 0f);
                c.Brake = Math.Max(-inp.MoveY, 0f);
                c.Steer = MathUtil.Clamp(inp.MoveX, -1f, 1f);
                c.Handbrake = inp.IsHeld(GameButton.Jump);
            }
            if (!alive || PlayerControlSystem.WantsBrake(w)) { c.Throttle = 1f; c.Brake = 1f; }

            if (inp.WasPressed(GameButton.Headlights)) car.Headlights = !car.Headlights;
            bool held = inp.IsHeld(GameButton.Interact);
            if (inp.WasPressed(GameButton.Interact)) { _tapArmed = true; _interactHeld = 0f; }
            if (held)
            {
                _interactHeld += dt;
                car.Horn = !car.Model.HasSiren || _interactHeld >= SirenTapSeconds;
            }
            else
            {
                if (_tapArmed && car.Model.HasSiren && _interactHeld < SirenTapSeconds)
                {
                    car.Siren = !car.Siren;
                    car.LightBar = car.Siren;
                }
                _tapArmed = false;
                _interactHeld = 0f;
                car.Horn = false;
            }
            return c;
        }

        private static void HandleImpacts(GameWorld w, Car car, ref VehicleStepResult r, bool playerDrives)
        {
            for (int k = 0; k < r.ImpactCount; k++)
            {
                VehicleImpact imp = r.GetImpact(k);
                int other = imp.OtherTag > 0 ? imp.OtherTag : 0;
                if (imp.Damage > 0f) w.Emit(GameEventType.CarDamaged, imp.Point, other, car.Tag, imp.Damage, null);
                if (imp.Kind == ImpactKind.Capsule)
                {
                    w.Emit(GameEventType.PedHitByVehicle, imp.Point, car.Tag, other, imp.RelativeNormalSpeed, null);
                    if (playerDrives) w.Emit(GameEventType.RecklessDriving, imp.Point, car.Tag, other, imp.RelativeNormalSpeed, null);
                }
                else if (imp.Kind == ImpactKind.Box)
                {
                    TagKind kind = WorldTag.KindOf(other);
                    bool ambient = kind == TagKind.AmbientVehicle || kind == TagKind.AmbientParkedVehicle;
                    if (ambient && imp.RelativeNormalSpeed >= PlayerRules.PromoteOnImpactSpeed)
                        w.CarFromTag(other, true, car.Tag);
                    w.Emit(GameEventType.CarCollision, imp.Point, car.Tag, other, imp.RelativeNormalSpeed, null);
                    if (playerDrives) w.Emit(GameEventType.RecklessDriving, imp.Point, car.Tag, other, imp.RelativeNormalSpeed, null);
                }
            }
        }

        private static void Explode(GameWorld w, Car car)
        {
            int driverTag = car.DriverId != 0 ? WorldTag.ForPed(car.DriverId) : 0;
            w.Emit(GameEventType.CarDestroyed, car.Position, driverTag, car.Tag, 0f, null);
            w.Emit(GameEventType.Explosion, car.Position, car.Tag, 0, ExplosionRadius, null);
            car.Siren = car.LightBar = car.Horn = false;
            for (int s = 0; s < Car.MaxSeats; s++)
            {
                if (car.Seats[s] == 0) continue;
                Ped p = w.FindPed(car.Seats[s]);
                if (p == null || p.IsDead) continue;
                p.Health = 0f;
                p.Flags |= PedFlags.Dead;
                p.LastAttackerTag = car.Tag;
                w.Emit(GameEventType.PedKilled, car.Position, car.Tag, p.Tag, 0f, null);
            }
        }
    }
}
