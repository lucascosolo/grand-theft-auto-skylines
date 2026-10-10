# Architecture

One CS1 mod. CS1 (Unity 5.6.7f1, Mono, .NET 3.5 profile) keeps simulating and drawing the city;
our code adds a player, vehicles we control, ped and police actors, interiors, HUD and jobs.

## Layers (dependencies point down only)

```
GtaSkylines.Mod   (net35, CS1 + Unity)  IUserMod, lifecycle, adapters CS1 -> Game, renderers, HUD,
                                        audio, effects applied to the city, self-test
GtaSkylines.Game  (net35 + net10.0, BCL only)  the whole game simulation: collision world,
                                        character and vehicle physics, actors, combat, crime/heat
                                        and police AI, road graph and AI driving, interiors
                                        generator, missions, economy, save record, settings, HUD model
Skylines.Host     (net35, CS1 + Unity)  reusable CS1 integration lifted from minecraft-skylines:
                                        camera takeover, input capture, shortcut blocking, world
                                        geometry export (terrain, nets, buildings, props, trees,
                                        moving objects), mesh cache, backups, autoload, log
Skylines.Core     (net35 + net10.0, BCL only)  geometry and streaming maths from minecraft-skylines
```

`Skylines.Core` and `Skylines.Host` come from `minecraft-skylines` (owner's project, same
author) with the Minecraft-only parts removed; they stay free of GTA knowledge so either project
can lift them. `GtaSkylines.Game` never references Unity or CS1: everything the game knows about
the city arrives through the `ICity` interface and everything it wants done to the city leaves as
`CityEffect` records. That makes the full game loop runnable headless in tests with a fake city.

## Ownership: one authority per piece of state

| State | Authority |
|---|---|
| Player, ped actors, police actors, our vehicles: position, velocity, health, AI state | `GtaSkylines.Game` |
| Ambient citizens and traffic (not yet interacted with) | CS1 simulation; the Game sees snapshots |
| Promotion of a CS1 citizen or vehicle to an actor | Game decides (`CityEffect.Promote...`), Mod executes (CS1 instance released, actor created with the same model) |
| City consequences (sick, dead, panic, fire put out, Chirper post) | Game requests via `CityEffect`, Mod applies on the simulation thread |
| Static world geometry | CS1; streamed into the Game's `CollisionWorld` as triangle regions |
| Road graph | CS1 `NetManager`; extracted into the Game's `RoadGraph`, refreshed on change |
| Interior geometry | Game (`InteriorGenerator`, deterministic from building descriptor); Mod builds meshes |
| Time of day, weather | CS1 |
| Player profile, settings | Game records; Mod stores them in the save / settings file |

## Frame loop

Mod `Update` (main thread) per frame while playing:
1. Read input → `InputFrame` (Game type: move axes, look deltas, buttons pressed/held).
2. Refresh the city snapshot cheaply (`CitySnapshot`: nearby citizens, vehicles, time of day,
   water) and stream collision regions around the player within a time budget.
3. `GameSession.Tick(dt, input)` runs every system in a fixed order at a fixed 60 Hz step with an
   accumulator (physics determinism): player → vehicles → peds → combat → crime/heat → police →
   missions → economy → HUD model.
4. Apply `CityEffect`s (queued onto CS1's simulation thread with `SimulationManager.AddAction`).
5. Render: camera from the Game's camera rig, player avatar, our vehicles, actors, interior,
   effects; HUD in `OnGUI` from the Game's `HudModel`.

The Game step is pure: given the same inputs and city snapshots it produces the same state.

## Coordinates and units

The Game uses CS1/Unity world coordinates directly: metres, y up, left-handed, +z north on the
map. `Vec3` and `Quat` in `GtaSkylines.Game.Mathx` mirror Unity's semantics (Quat rotates like
`UnityEngine.Quaternion`; yaw is about +y with `Euler(0, yaw, 0) * (0,0,1) = (sin yaw, 0, cos yaw)`),
so conversion in the Mod is a field copy. Angles in degrees at API boundaries, radians inside maths.

## Physics

Our own kinematic and rigid-body code in `GtaSkylines.Game.Physics` against
`CollisionWorld` (static triangles in a uniform grid per streamed region, plus dynamic boxes and
capsules for CS1 vehicles and citizens). Chosen over Unity PhysX because nothing in CS1 is known
to exercise PhysX, mesh-collider cooking of streamed regions risks frame spikes, and our own code
is testable without the game (see `DECISIONS.md`).

## Rendering

- Player and ped actors: the citizen's own `CitizenInfo` prefab instance from CS1's pool
  (`PrefabInfo.ObtainPrefabInstance<CitizenInfo>(InstanceID, 255)`) posed with
  `CitizenInfo.SetRenderParameters(position, rotation, velocity, color, AnimationState, false)`,
  which drives the game's own animator (idle/walk/run/sprint/panic).
- Vehicles: the static `Vehicle.RenderInstance(cameraInfo, VehicleInfo, position, rotation, ...)`
  inside a registered render callback, so colour, lights, wheels and LOD are the game's own.
- Interiors and procedural meshes: `Graphics.DrawMesh` with the game's
  `Custom/Props/Prop/Default` shader and runtime-generated textures (verified usable by the
  reference project); lines and HUD primitives with `Hidden/Internal-Colored`. Unity's particle
  shaders are not in CS1's build.
- Interior space: the active interior is placed at the building's x, z and y = −1500 m, sealed,
  so nothing of the city is visible; while inside, the Mod overrides CS1's sun, ambient and fog
  and restores them on exit (GTA's "interior cell" approach: one interior loaded at a time).

## HUD and menus

Unity IMGUI (`OnGUI`) for layout and text, with GL drawing into `RenderTexture`s for the
minimap and the big map (rasterised once from CS1 roads, buildings and water, then rotated and
overlaid per frame). CS1's own UI is hidden while playing (camera culling, as the reference
project does), so ours is the only UI on screen.

## Threading

All Game code runs on Unity's main thread. CS1 data that the simulation thread writes is read
with the reference project's established patterns (snapshots of buffers on the main thread);
every write into CS1 goes through `SimulationManager.AddAction`. Every mod callback is
exception-guarded so a bug can never abort a city load or save.

## Persistence

`ISerializableDataExtension` stores the profile record (`GtaSkylines.Profile`, versioned, CRC-32,
`Skylines.Core.Saves.BlobRecord`) in the city save. Settings live in
`<CS1 user data>/GtaSkylines/settings.cfg`.

## Verification levels

Each feature is reported in `docs/STATUS.md` as implemented / built (compiles against the owner's
CS1 assemblies) / sandbox-tested (unit or headless session tests) / verified in game (owner run).
Mocked tests are never evidence of engine integration.
