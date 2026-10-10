# G6 contract: world model, session, cameras, HUD model

Owns `src/GtaSkylines.Game/World/`, `Session/`, `Camera/`, `Hud/` and their test folders.
Pure C# (net35 + net10.0). This is the integration core: later chunks (G7 combat and peds, G8
crime and police, G9 missions) add **systems** that read and write the world defined here, so
its shared state must be designed for them, not just for G6's own tests.

Inputs available: `City` (ICity, CitySnapshot, CityEffect, BuildingDescriptor...), `Input`
(InputFrame, GameButton), `Physics` (CollisionWorld, CharacterMotor, SurfaceFlags...), `Roads`
(RoadGraph, Route, RouteFollower, GpsRoute), `Interiors` (InteriorGenerator, InteriorLayout),
`Profile` and `Settings` (PlayerProfile, Wallet, Shop, Property, GameSettings), `Vehicles`
(VehicleBody, VehicleTuning, VehicleControls; G5).

## World (shared state, "blackboard")

- `GameWorld`: owns `CollisionWorld Collision`, `RoadGraph Roads`, `ICity City`,
  `CityEffectQueue Effects`, `GameEvents Events`, `GameClock Clock` (game seconds, real seconds,
  CS1 hour from the city), `Rng Rng`, `PlayerProfile Profile`, `GameSettings Settings`,
  registries of `Ped` and `Car` with stable int ids (never reused within a session), the
  `Player` ped, `InteriorState Interior`, and blackboards for later systems:
  `WantedStatus Wanted` (Stars 0-5, Evading, LastKnownPosition, SearchCentre, SearchRadius,
  PlayerHeat 0-100, DistrictHeat map, Description {OutfitModel, CarModel, CarColour}),
  `MissionStatus Mission` (Active, Title, Objective text with highlight spans, timer,
  blips list, GPS target), `HudMessages` (help text with priority and expiry, subtitles queue,
  notifications feed, big-screen requests: Wasted, Busted, MissionPassed{reward},
  MissionFailed{reason}). G6 creates and clears these; G8 and G9 write them; the HUD reads them.
- Ambient interop: `CitySnapshot` arrives each frame; `GameWorld.Ambient` keeps the latest
  snapshot. Ambient CS1 vehicles and citizens are put into the CollisionWorld as dynamic boxes
  and capsules each frame with tags that encode what they are (e.g. high bits for kind), so
  queries and impacts can tell a CS1 car from a Game car from a ped.
- `Ped`: Id, Kind (Player, Civilian, Cop, Npc for interior occupants), Model (citizen model),
  Citizen (CS1 id, 0 none), Motor (CharacterMotor), YawDeg, Health 0-100, Armour 0-100, Flags (Dead, KnockedDown, HandsUp, OnPhone, Panicking,
  Sneaking, Aiming, Arrested, InVehicle, Invisible), VehicleId and Seat, `Anim` (Idle, Walk,
  Run, Sprint, Panic, Sit, LieDown, Phone, HandsUp, Fall, Climb, Swim, EnterVehicle,
  ExitVehicle, Punch, Aim, Shoot) for the renderer, `IPedBrain Brain` (null for the player;
  G7 supplies brains), `int CurrentWeapon` slot for G7, `PedInteriorId` (−1 outside).
- `Car`: Id, Model (VehicleModel), Body (VehicleBody), ColourRgb, Owner (Player, Police,
  Civilian, Mission, Abandoned), Driver ped id, passenger ped ids, Siren, LightBar, Headlights,
  Horn, `IDriver Driver` interface for AI control (G8 police, G9 missions provide drivers;
  G6 provides `RouteDriver` over RouteFollower for simple "drive to" use), SourceCs1Vehicle
  (ushort, 0 if spawned), Locked flag, AbandonedSeconds, Plate string.
- `GameEvents`: append-only ring buffer (capacity 256) of `GameEvent` {Type, GameTime,
  Position, ActorA, ActorB, Value, Text}; types at least: Bumped, Shove, Punched, Shot,
  Gunshot (noise), Explosion, PedKnockedDown, PedInjured, PedKilled, CarJacked, CarStolen
  (parked), CarDamaged, CarDestroyed, PlayerEnteredCar, PlayerExitedCar, EnteredInterior,
  ExitedInterior, Purchase, PlayerDied, PlayerArrested, Trespass, RecklessDriving (near
  misses and collisions with traffic), HandsUp. Systems read events by sequence number
  (each keeps its own cursor), so no consumer misses one between frames.
- `IGameSystem { int Order { get; } void Step(GameWorld world, float dt); }` and
  `GameSession.AddSystem`. G6's own systems: PlayerControl (10), Vehicles (20), Ambient (5),
  Interiors (40), Respawn (90), Camera (95), Hud (100). Reserved orders for later chunks:
  Peds 30, Combat 50, Crime 60, Police 70, Missions 80.

## Session

- `GameSession(ICity city, GameSettings settings, PlayerProfile profile)`: `Begin(Vec3 feet,
  float yawDeg)`, `Tick(float realDt, InputFrame input, CitySnapshot snapshot)` → fixed 60 Hz
  steps with an accumulator (max 5 per frame, remainder carried), input "pressed" edges are
  delivered to exactly one fixed step, `Paused` (pause menu) freezes the simulation but not
  the HUD/menus, `End()` returns the profile (position not kept; last safehouse is), drained
  `CityEffect`s exposed for the Mod after each Tick, `Interpolation` alpha for rendering.
- Player on foot (GTA feel): camera-relative WASD; jog 3.6 m/s default, walk 1.6 when only
  lightly moving (no analogue on keyboard: walk via sneak toggle), sprint 6.8 m/s while Shift
  held with stamina 0-100 (drains 18/s, refills 25/s after 1 s), turn smoothly toward the
  move direction in third person, face the camera yaw in first person and while aiming; Space
  jumps or climbs (motor ledge climb); fall damage from motor landing ImpactSpeed: none below
  9 m/s, then (v − 9) × 9 health; Ctrl toggles sneaking (walk speed, quieter, used by G8).
- Interaction targeting (each step, priority order): when in a car → exit; on foot: the car
  door within 3 m in front of the player (parked, ambient moving under 8 m/s, abandoned or
  Game car) → "Press F to enter <Name>"; interaction points inside an interior within 1.2 m;
  building doors within 2.5 m outside → "Press E to enter <Building>"; peds within 2.5 m in
  front → "Press E to talk" (G9 supplies the line through a hook); the HUD help box shows the
  prompt with the bound key name (from KeyBindings).
- Entering a vehicle: walk-to-door phase (max 1.2 s, the player is moved along a path to the
  driver door, cancelled by moving), then: parked ambient → emit `TakeVehicle(id, parked:true)`,
  create a Car with the same model/colour/pose, event CarStolen; moving ambient → stop it
  (TakeVehicle), its driver citizen becomes a Civilian ped placed at the door (TakeCitizen not
  needed: drivers are not CS1 instances on the street; create the ped with the driver's model
  from the snapshot's DriverCitizen if known, else a random model), event CarJacked with 1.6 s
  pull-out; Game car → get in (if a driver ped is inside, carjack it the same way). The player
  ped becomes InVehicle (hidden capsule, not in collision). F again: car slows, player exits
  at the driver door (or the nearest free side) when speed < 3 m/s; F at speed > 8 m/s makes
  the player bail out (roll, small damage).
- Driving: VehicleControls from InputFrame (W throttle, S brake/reverse, A/D steer, Space
  handbrake), Interact toggles siren on emergency vehicles (tap) / horn (hold), Headlights
  toggles lights; car impacts with ambient CS1 traffic above 6 m/s → promote that vehicle
  (TakeVehicle + spawn Car with a shocked civilian driver brain hook) so it can be pushed and
  damaged; impacts with pedestrians → event for G7; explosion → player in the car dies.
- Promotion/demotion of citizens: `PromoteCitizen(AmbientCitizen)` creates a Civilian ped
  from the snapshot and emits TakeCitizen; peds with no brain activity, out of the player's
  sight and > 60 m away (or > 150 m) are demoted: removed, `ReturnCitizen` emitted if alive.
  Dead or injured peds stay until 120 m away, then removed (their KillCitizen/InjureCitizen
  effects were emitted by G7 when it happened).
- Abandoned Game cars: removed when > 300 m from the player and not in view for 10 s.
- Interiors: E at a building door → fade-out 0.4 s → `InteriorGenerator.Generate` (null →
  "Closed" help text) → the layout's collision triangles are loaded into the CollisionWorld as
  region key `InteriorRegionKey` with `SurfaceFlags.Interior`, placed at the building's x, z,
  y = −1500 and the building's yaw (`InteriorLayout.ToWorld`); the player is teleported to
  EntranceSpawn; ambient snapshot is ignored while inside; `InteriorState` exposes the layout,
  origin, yaw, building, occupants (NPC peds created from the layout's NpcSpots with models
  from `ICity.Occupants` for residents/workers, random otherwise) for the renderer and G7;
  ExitDoor interaction → fade → back at the building door outside facing away from it;
  ElevatorDoor in a lobby → Apartment variant (1 + building id % 20) at ElevatorSpawn;
  ShopCounter/Bed/Wardrobe/HospitalDesk/PoliceDesk/RespraySpot interactions raise a
  `MenuRequest` (shop kind, item list from `Shop`) that the Mod's menus present; purchases go
  through `Wallet`/`Shop`; Bed in an owned property saves (MenuRequest Save) and sleeps
  (AdvanceTime 6 h effect). Entering a home you do not own is a Trespass event.
- Death and arrest: health 0 → Wasted big screen 4 s → respawn at the nearest Health building
  door (`ICity.ServiceBuildings(Health)`, else the entry point) with full health, minus a
  hospital fee (Wallet.Fine $500 or what there is), current weapon ammo lost (G7 applies via
  an event). Busted (G8 sets a request) → 4 s → nearest Police building door, fine, weapons
  lost. Both clear the wanted level through `WantedStatus` (G8 owns the rules; G6 calls a
  `Reset()` on it).
- Profile round trip: stats counters updated (distance walked/driven, vehicles stolen, deaths,
  arrests, interiors visited); `End()` writes position-independent profile state.

## Cameras (`CameraRig`)

Outputs Position, Rotation, FovDeg, NearClip each frame (interpolated). Modes cycled by
CameraView separately on foot and in vehicles: on foot FirstPerson (eye 1.62 m, small walk
bob, head turns with mouse, body follows), ThirdNear (2.2 m), ThirdMid (3.5 m), ThirdFar
(5 m); aiming forces an over-the-shoulder view (1.2 m, right offset 0.5 m, FOV 50); in
vehicles FirstPerson (driver seat from the model's half extents: 0.35 m left of centre,
0.55 × height, 0.1 × length forward), Hood, ChaseNear (from model length), ChaseFar. Mouse
look with pitch limits (−80..+80 on foot, −30..+45 chase), chase cameras recentre behind the
velocity after 1.5 s without mouse input, LookBehind holds a reversed view. Spring arm: a
sphere cast (radius 0.2) from the pivot to the desired position against the CollisionWorld
(ignoring the player's own tag, Interior allowed, NoCamera flags ignored) with smoothing that
pulls in fast and eases out slowly; never inside geometry. Death cam: slow orbit. FOV from
settings, +8° at sprint and with speed in cars.

## HUD model (`HudModel`, filled by the Hud system every frame)

Everything listed under "HUD" in DESIGN.md as plain data: health and armour fractions,
stamina, breath (when swimming), stars and flashing, cash and the last delta with age, weapon
name and ammo (G7 fills via world fields), help text, objective with highlight spans,
subtitle line, notification feed (last 4), street and district banner (from ICity names,
shown 4 s on change), vehicle name banner, speed (km/h), big screen state with progress,
fade alpha, crosshair kind, ped card (name, age, job, home, happiness when looking at an
ambient citizen within 6 m), blips (kind, world position, rotation for the player arrow,
flashing), GPS polyline (from GpsRoute, simplified), search-zone circle, minimap zoom
(from speed), MenuRequest pass-through. No strings allocated per frame when nothing changed
(cache formatted text by value).

## Tests (test-author)

Headless sessions on a `FakeCity` with a flat floor region and a straight road graph:
begin → player stands on the floor; walking with W moves forward relative to camera yaw at jog
speed; sprint drains stamina; fall from a 6 m box damages; F near a snapshot parked car →
TakeVehicle effect, CarStolen event, player in car, driving W moves the car; F near a moving
ambient car → TakeVehicle + a Civilian ped near the door + CarJacked; exiting at speed 0 puts
the player beside the car; E at a FakeCity building door → fade → player inside at
EntranceSpawn with the interior's collision loaded (raycast down hits the interior floor at
y ≈ −1500), ExitDoor → back outside at the door; shop counter interaction raises a MenuRequest
with the convenience catalogue; health 0 → Wasted → respawn at the FakeCity hospital door,
cash reduced; promotion/demotion emits TakeCitizen/ReturnCitizen; abandoned cars far away
are removed; camera never ends inside a wall placed between the player and the camera;
first-person camera sits at eye height; HUD model shows the enter-vehicle prompt, street
banner from FakeCity.StreetFunc, cash delta after a purchase; fixed-step determinism (same
inputs → same state hash); zero allocation per Tick in a steady walking state.
