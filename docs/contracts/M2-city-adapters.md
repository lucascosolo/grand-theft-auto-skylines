# M2 contract: CS1 city adapters

Owns `src/GtaSkylines.Mod/City/` and `src/GtaSkylines.Mod/World/` (net35, CS1 + Unity). Every
CS1 member used is first read in the decompile (`~/.cache/minecraft-skylines/decompile/`) and
logged in `docs/CS1-API-NOTES.md` with file:line; reuse what `minecraft-skylines/docs/CS1-API-NOTES.md`
already verified (and its in-game status). Main-thread reads only; every write into CS1 goes
through `SimulationManager.AddAction`.

## 1. `CityAdapter : ICity`

- Ground: `TerrainManager` height sampling (the reference's `TerrainSampler`); water: the
  reference's `WaterSampler` (surface above terrain); time: CS1's day-time hour; districts:
  `DistrictManager` (id at a point, names, city name); street names: the nearest named road
  segment within the radius (`NetManager` segment grid + `GetSegmentName`), cached per 8 m cell.
- Buildings: `BuildingManager` building grid for `BuildingsNear`; a `BuildingDescriptor` from a
  building id: name (`GetBuildingName`), prefab name, service mapping from `ItemClass`
  (service, sub-service, level → `ServiceKind`, sub-service string, level), position, front
  yaw (the building's angle, front = +z local as CS1's own building matrix defines; verify the
  sign in the decompile and test the mapping with a pure helper), width/length from the
  prefab's cell size (8 m cells), height from the mesh bounds, doors: every
  `BuildingInfo.m_enterDoors` prop position transformed by the building's matrix, else the
  front centre on the ground (lot edge), district, burning (`m_fireIntensity > 0`), abandoned
  or collapsed or not completed → `Abandoned`. Owned-by-player is set by the Game, not here.
- `ServiceBuildings`: every created, working building of the service (cached, refreshed every
  10 s).
- `Occupants`: citizens whose current location is that building (home or work/visit units),
  with their citizen model (the citizen's `CitizenInfo` index in our model catalogue).
- `Patients`: sick citizens currently in buildings (not already in an ambulance), nearest first,
  pickup = that building's first door. `BurningBuildings` likewise.
- Catalogues built once per city load: vehicle models from `PrefabCollection<VehicleInfo>`
  restricted to road vehicles a player could drive (cars, vans, trucks, buses, taxis, police,
  ambulance, fire, hearse, garbage, other city service road vehicles) with `VehicleKind` from the
  AI type, size from the mesh bounds, CS1 max speed/acceleration/braking from the prefab;
  `PoliceHelicopter` from a police helicopter AI if the game has one. Citizen models from
  `PrefabCollection<CitizenInfo>` restricted to human citizens (resident/tourist/service
  person AIs), with gender and age group. Stable ids for the session; a reverse map from
  prefab to id for the snapshot and renderers.
- `CitizenProfile` from `Citizen` data: name (`CitizenManager.GetCitizenName`), age (CS1 age
  units to years: verify the formula), gender, home and work buildings, happiness, wealth,
  education, health, sick, tourist, employed/student.

## 2. `SnapshotBuilder`

Each frame while playing (within the time budget), fills `CitySnapshot` around the player
(150 m): walking citizen instances (citizen grid) with position, velocity, yaw, model id;
moving road vehicles (vehicle grid) and parked vehicles (parked grid) with bounds-centre
position, rotation, velocity, model id, kind, driver citizen, colour (`VehicleInfo` colour for
that vehicle). Skips anything the Game has taken (ids the effect applier released). No
allocation in steady state (reused lists, no LINQ).

## 3. `CollisionStreamer`

Port the reference's collision streaming (`minecraft-skylines/cs1/src/MinecraftSkylines.Mod/
CollisionStreamer.cs` and the Host geometry classes already in `Skylines.Host/Geometry`)
without the Minecraft conversion: regions of 16 m around the player within 160 m (evict at
220 m), nearest first, prioritising the direction of travel at speed, 2 ms per frame budget,
into `CollisionWorld.SetRegion` with `SurfaceFlags` mapped from the geometry's own flags
(terrain, road, pavement, building, prop, tree, bridge). Rebuild regions touched by city edits
(the reference's re-stream signals). A debug toggle (Ctrl+Shift+F9) draws the loaded triangles
as GL lines (port the reference's `CollisionViewer`).

## 4. `RoadGraphBuilder`

From `NetManager` segments and nodes of road networks (not rail, pedestrian paths or power
lines): positions, Bézier control points (`NetSegment.CalculateMiddlePoints` with the segment's
start/end directions), lane counts per direction from the `NetInfo` vehicle lanes, lane width,
speed limit (CS1 speed unit 1.0 = 50 km/h → m/s), name, flags (highway, one-way, bridge,
tunnel, pedestrian-only), left-hand traffic from the city's metadata. Rebuilt into the Game's
`RoadGraph` on load and when the network changes (detect by segment/node counts and a
modification counter), at most every 5 s while playing.

## 5. `CityEffectApplier`

Applies `CityEffect`s on the simulation thread, in order, each guarded and logged:
TakeCitizen (remove the walking instance without killing the citizen or leaving it stuck:
research how CS1 handles a released instance for a citizen in transit and set its location so
it resumes life, e.g. at its target or home), ReturnCitizen, TakeVehicle (release the moving
vehicle; its passengers/cargo handled as CS1's own release does), TakeParkedVehicle,
InjureCitizen (sick flag and low health so CS1 dispatches an ambulance; verify which path makes
`HealthcareAI` pick them up), KillCitizen (the reference's verified `CitizenFate.Kill`),
PanicCitizen (`CitizenFate.Panic`), DouseFire (lower the building's fire intensity; at 0 clear
the burning state the way the fire engine AI does), Chirp (post through CS1's message manager
as a citizen; verify the API), AdvanceTime (move CS1's clock as the reference's verified
TIME_SET does). Taken ids are remembered so the snapshot skips them until CS1 reuses the id.

## Tests

Pure helpers (service mapping table, speed unit conversion, yaw mapping, lane counting from a
lane list abstraction, age conversion) get net10 tests in `tests/GtaSkylines.Mod.Tests`. The
rest is compile-verified and exercised by the in-game self-test (M6).
