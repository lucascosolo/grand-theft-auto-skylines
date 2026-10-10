# G5 contract: vehicle physics

Owns `src/GtaSkylines.Game/Vehicles/` and `tests/GtaSkylines.Game.Tests/Vehicles/`. Pure C#, BCL
only, net35 + net10.0. Uses `GtaSkylines.Game.Physics.CollisionWorld` (G1) for every query and
`GtaSkylines.Game.City.VehicleModel` for sizes and CS1's own speed figures.

## What it is for

The player's car (and every police or mission car driven by AI through `RouteFollower`, G2) is a
`VehicleBody` stepped at a fixed 1/60 s. It must feel like GTA V on keyboard: quick to top
speed, grippy until the handbrake or a hard turn at speed breaks the rear loose, stable over
kerbs and road seams, satisfying crashes that never explode the simulation.

## Required API (names may be refined; behaviour may not)

- `VehicleTuning` built from a `VehicleModel` by a static factory: mass (kg) from the bounding
  box volume (car ≈ 1400, bus ≈ 11000, truck ≈ 9000 by kind; clamp 800-15000), box inertia,
  wheelbase and track from the half extents, wheel radius by kind (0.32 car .. 0.5 bus/truck),
  suspension rest length 0.35, spring and damper giving ~1.6 Hz with damping ratio ~0.35, top
  speed `max(1.4 × model.MaxSpeed, 25)` m/s for cars (emergency vehicles +15 %, buses and trucks
  `max(model.MaxSpeed, 20)`), engine force from a 0-27 m/s time of ~6 s for cars, reverse top
  speed 9 m/s, brake deceleration ~9 m/s², max steer 35° falling to 8° at top speed, lateral
  grip coefficient ~1.6 (handbrake: rear lateral grip × 0.35 and rear longitudinal locked).
- `VehicleControls` struct: Throttle 0..1, Brake 0..1 (on a stopped or reversing car S means
  reverse: the body handles the GTA convention "S brakes, then reverses"), Steer −1..1,
  Handbrake bool. Steering input is smoothed inside the body (rate-limited), not by callers.
- `VehicleBody`: Position (world centre of the bounding box), Rotation (Quat), Velocity,
  AngularVelocity, Speed (signed along forward), Health 0..100, DamageState (Intact, Smoking
  < 40, OnFire < 15, Exploded), FireSeconds, Upright (dot of up with world up), per-wheel
  state for rendering (contact, compression, spin angle, steer angle, ground point),
  EngineRpm 0..1 and Gear for audio, Teleport(position, rotation), and
  `Step(float dt, VehicleControls controls, CollisionWorld world, int selfTag)` returning a
  `VehicleStepResult` with up to 4 `VehicleImpact`s (point, normal, relative normal speed m/s,
  other tag or −1 for static geometry, hit kind) and booleans Exploded, LeftGround, Landed.
- Wheels: raycasts down the suspension from the wheel mount; spring+damper force at contact;
  tyre forces from slip (longitudinal from drive/brake, lateral from slip angle with a
  saturating curve); rolling resistance and air drag so coasting slows naturally; anti-roll
  bars; extra downforce proportional to speed²; airborne angular damping and mild air control
  (steer input yaws a little in the air, throttle pitches a little).
- Body collision: the bounding box (from the model's half extents) against static triangles
  and dynamic boxes/capsules through CollisionWorld queries (several sphere/box sample casts or
  a box-triangle contact routine; your choice), producing contacts resolved with impulses
  (restitution 0.1, friction 0.4) at the contact points, so a wall hit at an angle scrapes
  along and a head-on hit stops the car. Continuous enough to never tunnel at 50 m/s through
  a wall 0.1 m thick (sub-step when fast). The body is never left inside static geometry.
- Pairwise: `VehicleBody.ResolveCollision(VehicleBody a, VehicleBody b)` for two Game-driven
  vehicles (box-box, impulse exchange by mass); vehicles that are CS1 ambient traffic are
  dynamic boxes in the CollisionWorld (immovable from the body's point of view; the session
  turns big impacts into promotions and pushes).
- Pedestrians: dynamic capsules in the world are hit as obstacles; the impact event (tag,
  relative speed) is what the combat system uses to knock them down. A ped capsule must not
  stop a car above 5 m/s (treat as soft: impulse capped at 80 kg-equivalent).
- Damage: impacts above 4 m/s relative normal speed cost health ∝ (v − 4)² scaled by mass
  ratio; health ≤ 15 → OnFire with a 6 s timer → Exploded (the result flags it once); an
  exploded body ignores controls. Flip assist: upside down and nearly stopped for 3 s → the
  session may call `Recover()` which rights the car in place.
- Zero allocation per step in steady state.

## Tests the test-author must cover (in addition to anything the API docs promise)

Flat ground built from two big triangles in a CollisionWorld: settles at rest without
drifting or jittering (velocity < 0.01 m/s after 2 s); full throttle reaches ≥ 90 % of top
speed within the expected time and never exceeds top speed by > 2 %; braking from 25 m/s
stops within 40 m; S on a stopped car reverses and caps at reverse top speed; steady steer at
10 m/s follows a circle of roughly constant radius; handbrake at 20 m/s with steer yaws faster
than without and the car slides (lateral speed > 2 m/s); a 15° ramp is climbed and the car
rests on a 10° slope with the brake; driving off a 2 m ledge lands upright with Landed set;
a head-on wall hit at 20 m/s produces an impact ≈ 20 m/s, health drops, the car stops and is
not inside the wall; a 45° glancing wall hit keeps most of the tangential speed; 50 m/s into a
0.1 m wall never tunnels; two bodies colliding head-on at 10 m/s each with equal mass end with
roughly opposite and small velocities (momentum conserved within 10 %); a heavy truck hitting a
car pushes it; damage thresholds (Smoking, OnFire, Exploded after 6 s); a dynamic capsule
(pedestrian) hit at 15 m/s produces an impact event with its tag and barely slows the car;
10 000 steps on bumpy terrain without NaN; zero allocation per Step after warm-up (net10
`GC.GetAllocatedBytesForCurrentThread`).
