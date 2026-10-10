# Delivery plan

Feature contract: `docs/DESIGN.md`. Architecture: `docs/ARCHITECTURE.md`. Ledger of finished
chunks: `.agent/ledger.md` (local) and `git log`. Each chunk owns the folders listed; concurrent
chunks never share a folder. Shared files (`src/GtaSkylines.Game/City`, `Mathx`, `Input`,
`tests/GtaSkylines.Game.Tests/Fakes/FakeCity.cs`) change only through the coordinator; a chunk
that needs a new `ICity` member reports it instead of adding it.

Pipeline per Game chunk: (1) `deep-implementer` writes the public API skeleton with XML docs
(bodies throw `NotImplementedException`), (2) `test-author` writes failing tests from this
contract and the skeleton, (3) the same implementer makes them pass without editing the tests.
Mod chunks (CS1 integration) have little a unit test can reach: their pure helpers get tests,
the rest is compile-verified against the CS1 assemblies, every CS1 member read in the decompile
and logged in `docs/CS1-API-NOTES.md`, and covered by the in-game self-test (M6).

Verification for every chunk: `tools/check.sh` green (build 0 warnings, all tests pass).

Detailed contracts (they supersede the summaries below where they differ): `docs/contracts/`
G5-vehicles, G6-world-session, G7-combat-peds, G8-crime-police, G9-missions, M2-city-adapters,
M3-rendering, M4-hud-menus, M5-M7-audio-selftest-wiring. M7 (wiring the real session into the
Mod) is the chunk that joins everything; it follows M2-M5 and G7-G9.

## Wave 1 — Game layer, independent

### G1 Physics: collision world and character motor
Owns `src/GtaSkylines.Game/Physics/`, `tests/GtaSkylines.Game.Tests/Physics/`.
- `CollisionWorld`: static triangle regions keyed by a `long` (set/replace/remove, copied in),
  per-triangle `SurfaceFlags` (Terrain, Road, Pavement, Building, Prop, Tree, Bridge, Interior,
  NoCamera); a uniform grid (4 m cells on x, z) for queries; dynamic oriented boxes and vertical
  capsules (each with an int tag) replaced every frame. Queries: raycast, sphere cast, capsule
  cast (vertical capsule swept along a direction), sphere overlap with contact normals and
  depths; each takes a mask (static / dynamic / flag filter) and returns the nearest hit with
  point, normal, distance, flags and tag. No allocation per query in steady state.
- `CharacterMotor`: kinematic capsule (radius 0.35, height 1.8; crouch/sneak keeps the
  height). Per fixed step: collide-and-slide (max 4 iterations), ground snap down 0.5 m when
  grounded, step-up 0.45 m, walkable slope ≤ 50° (steeper slides down), jump (apex ≈ 1.1 m),
  gravity 9.81 m/s², terminal velocity 55 m/s, air control 30 %, ledge climb (ledge top 0.6-2.0 m
  above the feet within 0.6 m ahead, clear headroom: a timed climb moves the feet onto the ledge),
  swimming when the feet are 1.2 m below a water surface from `ICity.TryWaterSurface` (float at
  the surface, slow swim, breath 20 s, drown damage after), landing events with impact speed (the
  combat system turns impacts above 8 m/s into damage), pushes from dynamic capsules/boxes
  (never ends inside one). Moving platforms are out of scope.
- Done when: tests cover flat walk, wall slide, corner, step-up allowed/refused, slope
  walk/slide, jump apex, fall + landing event, ledge climb allowed/refused (no headroom), water
  float + breath, box push-out, region replace/remove, query masks, grid boundaries (shapes
  spanning cells/regions), no tunnelling at 55 m/s against a thin wall.

### G2 Roads: graph, routes, AI driving
Owns `src/GtaSkylines.Game/Roads/`, `tests/GtaSkylines.Game.Tests/Roads/`.
- `RoadGraph` built by the Mod: nodes (id, position) and segments (id, start/end node, cubic
  Bézier control points, forward/backward lane counts, lane width, speed limit m/s, name,
  `RoadFlags`: Highway, OneWay, Bridge, Tunnel, PedestrianOnly). Versioned; rebuilt wholesale.
- Queries: nearest point on a drivable road (segment, t, lateral offset, heading); A* route
  between two world points over segments (travel time cost) returning a polyline with per-point
  heading and the segment list; route length/ETA; points ahead along a route; "predict where a
  vehicle at P heading H at speed V is in N seconds" by following the most-aligned road through
  junctions (police intercepts and roadblocks); the junctions within N metres ahead along the
  predicted path; a random wander route for patrols.
- `RouteFollower`: given a route and a vehicle's pose/speed, produce steering (−1..1), throttle,
  brake to follow the right-hand lane (CS1 drives on the right by default; `LeftHandTraffic` flag
  flips it), slowing for curves and junctions, with lateral offset input for overtaking; stopping
  at a point; arrival detection.
- `GpsRoute`: the HUD's route from the player to a target, recomputed when the player strays
  > 25 m from it or every 3 s at most.
- Done when: tests on hand-built graphs cover nearest point, route correctness and one-way
  respect, unreachable target, prediction through a junction, follower converging on a straight
  and a curve, stopping distance, left-hand traffic.

### G3 Interiors: generator
Owns `src/GtaSkylines.Game/Interiors/`, `tests/GtaSkylines.Game.Tests/Interiors/`.
- `InteriorArchetype` chosen from a `BuildingDescriptor` (service, sub-service, level, size,
  name): House, ApartmentLobby (+ one apartment reached by an elevator door), Shop variants
  (ConvenienceStore, ClothesShop, GunStore, Restaurant/Bar; chosen deterministically among
  commercial buildings, with at least the convenience store common and gun stores ~10 %),
  Office, Warehouse/Factory, PoliceStation, Hospital, FireStation, School, ServiceHall (other
  services), Garage (industrial with respray), Generic. Parks and abandoned buildings: none.
- `InteriorGenerator.Generate(BuildingDescriptor, seed)` → `InteriorLayout` in interior-local
  metres (origin at the entrance door on the floor, +z into the building): footprint scaled from
  the building's (clamped 8-40 m wide, 8-40 m deep; ceilings 2.8-4.5 m by archetype), rooms by
  recursive subdivision with archetype room programs (e.g. police: lobby with desk, offices,
  locker room, two cells with bars, armoury; hospital: reception, ward with beds, treatment room),
  doorways ≥ 1.0 m wide between connected rooms, every room reachable from the entrance, the
  entrance door and (for lobbies) an elevator door as `Portal`s, furniture placed against walls
  or in room centres from a procedural kit with ≥ 0.8 m walkable clearance along a path from the
  entrance to every room's doorway and to every interaction point.
- Interaction points with a kind: ShopCounter (with shop type), Bed (sleep/save), Wardrobe,
  HospitalDesk, PoliceDesk, RespraySpot, ExitDoor, ElevatorDoor, plus NPC spots (stand/sit, with
  a role: Clerk, Resident, Worker, Cop, Doctor, Patient).
- Geometry out: a collision triangle list (floor, walls with door gaps, ceiling, furniture
  boxes) with `SurfaceFlags.Interior`, and render mesh data per material slot (vertices, normals,
  UVs, colours, indices; slots: Floor, Wall, Ceiling, Wood, Fabric, Metal, Glass, Tile, Plant,
  Emissive), with texture "recipes" (`TextureRecipe`: base colour, pattern kind: Plain, Planks,
  Tiles, Carpet, Bricks, Concrete, Stripes; scale) the Mod turns into textures.
- Deterministic: same descriptor and seed → identical layout and mesh (tests compare hashes).
- Done when: tests over every archetype and many seeds/sizes check: rooms inside the footprint,
  no overlapping rooms, every room and interaction point reachable (grid flood fill at 0.25 m
  with the 0.35 m player radius), doors not blocked, furniture inside rooms and not overlapping
  each other, collision mesh closed around walkable space (rays from walkable points toward any
  horizontal direction hit a wall within the footprint), counts sane, determinism.

### G4 Profile, economy, settings
Owns `src/GtaSkylines.Game/Profile/`, `src/GtaSkylines.Game/Settings/`,
`tests/GtaSkylines.Game.Tests/Profile/`, `tests/GtaSkylines.Game.Tests/Settings/`.
- `PlayerProfile`: cash (long, never negative; fines take what there is), health 0-100, armour
  0-100, weapons owned + ammo per weapon (`WeaponId`: Fists, Bat, Pistol, Smg, Shotgun, Carbine),
  outfit (citizen model id), owned properties (building ids), last safehouse, mission progress
  (completed mission ids, campaign step, side-job levels), stats (named counters), heat (player
  heat value and per-district heat map), play time.
- `ProfileRecord`: binary codec, `u8 version` (1), fields, CRC-32 through
  `Skylines.Core.Saves.BlobRecord`; decode of corrupt data → error result, never an exception
  that escapes; forward compatibility: unknown trailing fields ignored.
- `Shop` catalog: items per shop kind with prices (weapons, ammo packs, armour, food → health,
  clothes → outfit, respray, hospital heal, police "pay off heat"), purchase rules
  (insufficient cash, already owned, max ammo), `Wallet` transactions with reasons for the HUD
  (+/− deltas).
- `Property`: price from building level and size; buy; safehouse list.
- `GameSettings`: all settings in DESIGN.md F-SET with ranges and defaults; `KeyBindings`
  (GameButton → primary and secondary key names as Unity `KeyCode` names, mouse buttons
  `Mouse0..Mouse2`, wheel `WheelUp/WheelDown`); parse/serialise a `key = value` text file;
  unknown keys and bad values are reported problems, never exceptions; defaults per DESIGN.md.
- Done when: round-trip tests for profile and settings, corruption tests, purchase rules,
  defaults table matches DESIGN.md's control table.

## Wave 2 — Game layer, on wave 1

### G5 Vehicle physics
Owns `src/GtaSkylines.Game/Vehicles/` (physics part), tests alongside.
Rigid body (mass from model size, inertia box), 4 raycast wheels (suspension spring/damper,
tyre longitudinal/lateral friction with a slip curve; handbrake drops rear lateral grip for
drift), engine with top speed scaled from the CS1 model's `MaxSpeed` (≥ 1.4× so player cars feel
fast) and reverse, brakes, steering speed-sensitive, anti-roll, downforce; body collision as a
box against `CollisionWorld` (contact points, impulse with restitution 0.1 and friction) and
against other `VehicleBody`s and dynamic boxes; impact events (other body, relative speed, point)
for damage and for pushing CS1 traffic; damage 0-100 with smoke/fire/explode thresholds
(DESIGN F-VEH-3); upside-down detection; stuck flip assist. AI input uses the same body.
Done when: tests for straight acceleration to top speed, braking distance, steady turn,
handbrake turn rotates more, resting on slope, driving off a ledge and landing, wall impact
event and bounce, car-car collision conserves momentum approximately, no tunnelling at 50 m/s.

### G6 World model and session
Owns `src/GtaSkylines.Game/World/`, `src/GtaSkylines.Game/Session/`,
`src/GtaSkylines.Game/Camera/`, `src/GtaSkylines.Game/Hud/` (model only), tests alongside.
- Actors: `Ped` (player, civilian, cop; model, citizen id, motor, health, armour, flags,
  current vehicle and seat, brain slot), `Car` (model, body, health, colour, siren, lights,
  driver, passengers, owner: Player/Police/Civilian/Mission, abandoned timer), registries with
  stable int ids, events bus (typed events with a ring buffer for the HUD/crime/missions).
- Promotion: ambient citizen → ped (emit `TakeCitizen`), ambient vehicle → car (emit
  `TakeVehicle`/`TakeParkedVehicle`; its driver becomes a ped placed beside it), demotion of
  peds out of sight > 60 m (emit `ReturnCitizen`), despawn of abandoned cars > 300 m unseen.
- `GameSession`: fixed 60 Hz step with accumulator and max 5 steps/frame; ordered systems
  (player control → vehicles → peds → combat → crime → police → missions → economy → camera →
  HUD model); pause; player on-foot control (GTA speeds: walk 1.6, jog 3.6, sprint 6.8 m/s with
  stamina), enter/exit vehicle flow (walk to door 1 s, carjack 2 s with the driver pulled out),
  interaction targeting (nearest vehicle door / building door / ped / interaction point within
  reach, highest priority wins, with the prompt text), interior transitions (fade out 0.4 s,
  load layout, place player at the entrance portal, fade in; exit reverses; cops can follow at
  4+ stars), death/arrest respawn flow, respawn points (nearest hospital / police station door).
- `CameraRig`: first person (eye 1.62 m, bob), third-person near/mid/far orbit with spring arm
  against `CollisionWorld` (NoCamera flag ignored), aim over-shoulder, vehicle chase (follows
  velocity, recentres after 1.5 s), hood view, look-behind; outputs position, rotation, FOV,
  near clip.
- `HudModel`: everything DESIGN.md's HUD shows, as data (bars, stars and flashing, cash and
  deltas, weapon, help text, objective, subtitles queue, notifications feed, street/district
  banner, vehicle name, big-screen state, crosshair state, ped card, blips with kinds and
  positions, GPS polyline, search zone).
Done when: headless session tests with `FakeCity` drive a player through walking, entering a
parked car, carjacking a moving one, driving, exiting, entering and leaving an interior, dying
and respawning; HUD model fields asserted at each step.

## Wave 3 — Game layer, on G6

### G7 Combat and ped AI
Owns `src/GtaSkylines.Game/Combat/`, `src/GtaSkylines.Game/Peds/`, tests alongside.
Weapons table (damage, range, rate, spread, magazine, reload time, melee arc), aiming (camera
ray, assisted lock-on within 6° on peds when aiming), hitscan against world/cars/peds with
tracers as events, melee hits, damage with armour first, regeneration to 50 %, knock-down from
vehicle impacts (> 4 m/s) and melee, injury vs death thresholds → `InjureCitizen`/`KillCitizen`
effects, fall damage from motor landing events. Ped brains: Wander (stand/walk near spot),
Flee (away from threat along open ground), Cower, HandsUp, FightBack (some adults, fists),
CallPolice (phone for 4-10 s; interrupted if threatened or hit), Stumble/complain when bumped,
GetUp, ReturnToCity; carjacked drivers: flee or fight (seeded). Threat perception: sight cone
40 m, hearing gunshots 60 m.
Done when: tests for damage maths, armour, regen, hitscan blocked by walls, melee arc, ped
reactions to each stimulus, witness call completes/interrupted, deaths produce effects.

### G8 Crime, heat and police
Owns `src/GtaSkylines.Game/Crime/`, `src/GtaSkylines.Game/Police/`, tests alongside.
Exactly DESIGN.md F-HEAT-1..9: crime events, witness reporting (via G7's CallPolice), wanted
level rules, last known position and description, evasion timer and widening search zone,
heat per player and per district with decay, recognition model (line of sight via
`CollisionWorld` raycasts, distance, description match, sneaking, darkness at night),
dispatcher (unit budget by stars and heat: 1★ 1 car, 2★ 2-3, 3★ 4 + roadblock, 4★ 6 +
helicopter if available, 5★ 8 + aggressive), units spawned at the city's police stations or,
when none is within 1.5 km, at a road point 250-400 m from the player out of sight, travelling
by `RoadGraph`; roles Pursue, Intercept (ahead on predicted route), Search (sector of the
zone), Roadblock (cars across a junction ahead), BoxIn, PIT, OnFoot; arrest and shooting rules;
BUSTED and pay-off. Police cars use G5 bodies driven by G2 followers.
Done when: scenario tests: unwitnessed crime → no stars; witnessed crime → stars after the call;
interrupted call → none; cop sight → instant; losing sight → search zone and expiry; outfit
change → not recognised; heat decay; dispatcher role assignment for each star level;
roadblock placed ahead on the predicted route; arrest at 1★ when stopped.

### G9 Missions, jobs, phone, dialogue
Owns `src/GtaSkylines.Game/Missions/`, `src/GtaSkylines.Game/Phone/`,
`src/GtaSkylines.Game/Dialogue/`, tests alongside.
Mission framework (DESIGN F-JOB-1); campaign of ≥ 8 missions over three contacts generated
from the city's places (`ICity.ServiceBuildings`, districts) with authored text templates;
side jobs Taxi, Paramedic, Firefighter, Vigilante, Delivery, Street race (F-JOB-3) with levels
and pay; phone contacts, texts, job offers; Chirper reactions to crimes and feats (rate
limited); ped dialogue lines from `CitizenProfile` (F-PED-2) and the ped card.
Done when: tests start, progress, pass and fail each mission and job type in a `FakeCity`;
campaign generation works for a city with no police station or hospital (falls back sensibly).

## Wave 4 — CS1 integration (Mod)

### M1 Mod shell and lifecycle
Owns `src/GtaSkylines.Mod/` root files, `Lifecycle/`, `Input/`, `Persistence/`.
`IUserMod` (name, description, options UI with the settings and key rebinding), loading
extension, serializable data extension (profile in the save), threading extension hooks;
enter/exit play (toolbar walk-in button and Ctrl+Shift+G, from the reference's `WalkInButton`,
`MapDropButton`, `CameraTakeover`, `ShortcutBlocker`, `InputCapture`), CS1 UI hidden through
the camera (reference approach), simulation rate while playing, pause integration, first-entry
backup prompt (reference `CityBackup`), exception guards and `HostLog`.

### M2 City adapters
Owns `src/GtaSkylines.Mod/City/`, `src/GtaSkylines.Mod/World/`.
`ICity` over CS1 managers; per-frame `CitySnapshot`; collision streaming into `CollisionWorld`
(the reference's `CollisionStreamer` geometry, regions around the player, budgeted per frame);
`RoadGraph` extraction from `NetManager` (rebuilt on network changes); `CityEffect` applier on
the simulation thread; vehicle and citizen model catalogues; door positions from
`BuildingInfo.m_enterDoors`.

### M3 Rendering
Owns `src/GtaSkylines.Mod/Rendering/`.
Player and ped actors as CS1 citizen prefab instances with the game's animations; our vehicles
with `Vehicle.RenderInstance` (lights, siren, colour); interior meshes and generated textures
with lighting/fog override while inside; markers, blips in the world, tracers, muzzle flashes,
fire/smoke for damaged cars; first-person weapon view model; camera application.

### M4 HUD and menus
Owns `src/GtaSkylines.Mod/Hud/`, `src/GtaSkylines.Mod/Menus/`.
IMGUI HUD from `HudModel` (DESIGN.md HUD list), minimap and big map rasterised from the road
graph, buildings and water into render textures, pause menu (Map with waypoint, Brief, Stats,
Settings, Controls, Return to City Builder), weapon wheel, phone, interaction menu, shop and
property menus, full-screen state screens, fade.

### M5 Audio
Owns `src/GtaSkylines.Mod/Audio/`.
Procedural clips (footsteps, gunshots, impacts, horn, siren, UI blips, phone, jingles) and the
vehicles' own CS1 engine effects where available; volumes from settings.

### M6 Self-test, install, docs
Owns `src/GtaSkylines.Mod/SelfTest/`, `tools/` scripts, `docs/TESTING.md`, `docs/STATUS.md`.
In-game self-test (Ctrl+Shift+T and unattended launch config) covering every F-ID that can be
exercised automatically, with a report file; install script into the CS1 mods folder;
remote/unattended test script; owner's playtest checklist.

## Wave 5 — review

Architecture review, `/code-review`, fixes, then an independent readiness review against
`docs/DESIGN.md` and `docs/STATUS.md`.
