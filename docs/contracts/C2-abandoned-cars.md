# C2 contract: abandoned cars block CS1 traffic (F-VEH-8)

Owner request and decisions, 2026-10-11: a car the player leaves in a traffic lane makes CS1 traffic queue behind it
and reroute, bounded to while the player is within the snapshot radius and at most 3 minutes; abandoned-car lifetime
stays F-VEH-4 (never saved); the Game keeps drawing its own car (no CS1 parked car, which moving traffic ignores).
No Harmony; CS1 writes on the simulation thread via `SimulationManager.AddAction`.

## Game (BCL, tested)

- `LaneBlocks.TryFindLane(RoadGraph, Vec3 position, out RoadPoint)`: the nearest drivable segment point
  (`TryNearestPoint`, 25 m) where |`LateralOffset`| ≤ lanes × lane width ÷ 2 (lanes = forward + backward; width ≤ 0
  → `RoadDefaults.LaneWidth`), i.e. the car centre is within half a lane width of some car lane, and
  |position.Y − point.Y| ≤ 3 m (not a road above or below).
- Per step (Ambient system), a car **blocks** when: `Owner == Abandoned`, no ped in any seat, not the player's vehicle,
  not destroyed, horizontal speed < 0.5 m/s, within `Ambient.Radius` (XZ) of the player, on a lane (above), and its
  accumulated block time < `LaneBlocks.MaxSeconds` (180 s). It then emits **one** `CityEffect.BlockLane(tag, pivot,
  yawDeg, model, segment)`; pivot = body position − rotation × model `CenterOffset` (CS1's ground pivot).
- A blocking car emits **one** `CityEffect.UnblockLane(tag)` when: it is no longer Abandoned or anyone sits in it
  (taken), it is > 2 m (XZ) from where it blocked (pushed), the player is beyond `Ambient.Radius`, its accumulated block
  time reaches 180 s, or it is removed (`GameWorld.RemoveCar`). Block time accumulates over one abandonment and resets
  when the car stops being Abandoned. After the 180 s it never blocks again in that abandonment.
- `CityEffect` gains `Tag`, `Model`, `Segment` (int) and kinds `BlockLane`, `UnblockLane`.

## Mod (CS1; bookkeeping BCL-pure and tested)

- `LaneBlockers` (Mod/City, thread-safe): tag → record {pivot, yaw, model, segment, CS1 vehicle id, traffic-only}.
  `Add` (replaces a tag), `Remove(tag, out record)`, `IsBlocker(vehicle)`, `SetVehicle(tag, vehicle, trafficOnly)`,
  `CopyTo(list)`, `DetachAll(list)` (lists vehicle ids, records stay with vehicle 0), `Clear(list)` (lists vehicle ids,
  forgets all), `Count`. Vehicle 0 is never a blocker.
- Blocker (sim thread): `VehicleManager.CreateVehicle` of the car's prefab when its AI is `PassengerCarAI`, else the
  first catalogued `PassengerCarAI` prefab; no citizens, no path; all four frames at the pivot and yaw with
  `m_insideBuilding = true` (render skip, Vehicle.cs:299-305; CarAI never writes it); `Stopped`; `Spawn` (grid).
  Every frame an AddAction re-poses it, resets `m_waitCounter`/`m_blockCounter`, re-sets `Stopped`, and adds traffic
  to its segment (clamped below 65535 so CS1 never flags it Blocked). Snapshot and departure tracker skip blockers.
  `UnblockLane` releases it. Saving (`SavePanel.isSaving`): vehicles released, records kept, recreated after.
  Play-mode end / unload: all released. If the vehicle cannot be created or CS1 drops it: traffic-only (reroute, no
  queue), logged. One log line per block / unblock.

## Verification

`tools/check.sh` green. Tests by a test-author: Game session tests (FakeCity + road graph) for every emit / no-emit
rule above; `LaneBlocks.TryFindLane`; `LaneBlockers` bookkeeping. Manual line F-VEH-8 in docs/TESTING.md.
