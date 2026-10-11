# C1 contract: coherent citizens and parked-car departures (F-PED-7, F-VEH-7)

Owner request, 2026-10-10: "Citizens should behave coherently and cars shouldn't disappear when they go
to take off from where they were parked." No Harmony (DECISIONS.md): CS1 is read and written only through
its data buffers and public methods, writes on the simulation thread via `SimulationManager.AddAction`.

## What CS1 does (research 2026-10-10; every member must still be re-read and logged in CS1-API-NOTES.md)

Paths under `~/.cache/minecraft-skylines/decompile/Assembly-CSharp/`.

- Departure: the resident's path starts at a lane position within 32 m of its parked car
  (CitizenAI.cs:1081-1086, :1115); on reaching the vehicle lane `ResidentAI.SpawnVehicle` (ResidentAI.cs:2780-2924)
  creates the moving `Vehicle` at the parked car's position and rotation when the car is within 32 m of the
  citizen's `m_targetPos` (:2858-2879), else at the citizen's own position; then `ChangeInstance` (:2905-2908) and
  `Citizen.SetParkedVehicle(citizen, 0)` releases the parked car in the same step (:2911; Citizen.cs:841-846;
  VehicleManager.cs:1887-1911). The new car carries `Stopped` while the citizen boards (:2886-2915).
- Visibility: `Vehicle.RenderInstance` draws nothing unless `Flags.Spawned` (Vehicle.cs:276). `CarAI.TrySpawn`
  (CarAI.cs:1361-1375) sets `WaitingSpace` and stays unspawned while `CheckOverlap` finds traffic, retried every
  step (:62-65) with no time limit. So the parked car disappears and the moving one is invisible until the lane
  is clear: this is the vanishing car.
- Citizens: a `Moving` citizen whose instance is released with no vehicle is set `Home` (ResidentAI.cs:2003-2010).
  An instance can continue a journey from any point: `CitizenManager.CreateCitizenInstance` (CitizenManager.cs:1354),
  set `m_frame0..3` and `m_targetPos` (w = 1) to the point, `CurrentLocation = Moving`, then
  `CitizenAI.SetTarget(instance, ref data, targetBuilding)`, which path-finds from `m_targetPos`
  (ResidentAI.cs:3099-3105, :2689-2692; CitizenAI.cs:1130) and spawns on success (HumanAI.cs:210-213).
  No CS1 flag freezes an instance while keeping it alive.

## Part A: parked-car departures (F-VEH-7), Mod only

- A tracker (pure logic, BCL, tested) remembers parked CS1 cars near the player (within the snapshot radius)
  with pose, prefab and owner citizen. When a remembered parked id disappears and the owner's
  `Citizen.m_vehicle` is a created, unspawned vehicle, the car becomes a **departing ghost** at the remembered
  pose. It ends when that vehicle is `Spawned` within 2 m of the pose (CS1 now draws it in place), when that
  vehicle no longer exists, or, if CS1 spawned it more than 2 m away (or there is no new vehicle), only when
  the ghost's pose is out of the player's sight and beyond 60 m (reuse the Game's sight rule thresholds).
- While a ghost lives: our renderer draws the parked prefab at the pose (the existing CS1-prefab vehicle draw
  path used for Game cars; parked colour/variation as CS1 would draw it), and the snapshot emits it to the Game
  as an ambient vehicle with the **moving vehicle's id**, the pose, zero velocity and `DriverCitizen` = owner, so
  it has collision and the player can take it (taking it releases CS1's unspawned car and pulls the owner out).
- No CS1 state is written. One-line log per ghost start and end with the reason; the F-QA-3 overlay labels
  ghosts.

## Part B: citizens keep their errand (F-PED-7), Game + Mod

- The snapshot reports each ambient citizen's CS1 errand: target building (none when `TargetIsNode`) and
  whether it is the citizen's home, work, or a visit; carjackable vehicles report their driver's target building.
- A promoted citizen and a carjacked driver remember the errand. Their calm behaviour (after any reaction ends)
  is to walk to the errand building's door on foot (existing ped navigation and `ServiceDoors`/building door
  positions; pavements preferred if the Game has them) at walk speed, and, on reaching the door, go in: the ped
  is removed and the city is told the citizen arrived there. No errand → walk toward home; no home → wander,
  as today's idle brains do. A ped that is mid-reaction keeps today's reactions.
- Handing back (demotion out of sight, mission cleanup): the return effect carries the ped's position, yaw and
  errand. The applier recreates a CS1 instance there and sets its target (above), `CurrentLocation = Moving`;
  if CS1 cannot path from there, fall back to placing the citizen at the errand building (today's behaviour).
  Dead and injured citizens keep their existing effects.
- Holding: while the Game holds a citizen, any CS1 instance that appears for that citizen (CS1 sends them out
  of the building we placed them in) is released again on the simulation thread and the citizen put back in
  that building, so no second copy is ever drawn. Taking a citizen keeps today's placement (work / visit /
  home by the instance's target) so CS1's counts stay sane while held.
- The Game layer stays BCL-only: errands are building ids and door positions through `ICity`; no CS1 types.

## Verification

`tools/check.sh` green. Tests by a test-author: the ghost tracker (start, end on in-place spawn, end on
release, out-of-sight rule for far spawns, takeable id); errand brains in a `FakeCity` session (walk to door,
arrive → return effect with arrival, demote mid-walk → return effect with position and errand, no errand →
home, carjacked driver flees then walks on); return effect fields. Manual checklist lines in docs/TESTING.md
for F-PED-7 and F-VEH-7.
