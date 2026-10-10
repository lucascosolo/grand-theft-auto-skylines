# M7 contract: wiring the game into the mod

Owns `src/GtaSkylines.Mod/Play/` (new), `src/GtaSkylines.Game/Session/GameFactory.cs` (new) and
the adapters listed below; may make the small shared-file changes listed under "Shared changes".
Replaces M1's temporary `FreeCameraSession` with the real game. Everything else already exists
and is tested; this chunk connects it. Read each module's public API before wiring it.

## Pieces and their seams

| Piece | Where | Seam to use |
|---|---|---|
| Mod shell, lifecycle | `Mod/Lifecycle/PlayController`, `IPlaySession`, `SessionServices`, `ModRuntime` | implement `IPlaySession` as `Play/GtaPlaySession`; register it where `FreeCameraSession` is created today |
| Input | `Mod/Input/InputMapper` | its `InputFrame` goes into `GameSession.Tick` |
| City adapters | `Mod/City/CityServices` (CityAdapter: ICity, SnapshotBuilder, CityEffectApplier, Catalogues, TakenIds), `Mod/World/CollisionStreamer`, `RoadGraphBuilder` | construct once per play session; tick snapshot + streaming + road graph refresh each frame within budget; drain `GameSession` effects into the applier |
| Game session | `Game/Session/GameSession`, `Game/World/*`, `Game/Camera/CameraRig`, `Game/Hud/HudModel` | `GameFactory.Create(ICity, GameSettings, PlayerProfile, uint seed, ...)` builds the session and registers every system in order |
| Peds and combat | `Game/Peds/*`, `Game/Combat/*` (G7) | `PedBrainFactory` → `GameHooks` brain factory; `CombatSystem`, `PedsSystem`; TimeScale source; `Hooks.Ammo` |
| Crime and police | `Game/Crime/*`, `Game/Police/*` (G8) | `CrimeSystem`, `PolicySystem`; implement `ICrimePedServices` over G7 (`Perception.WitnessesOf`, `CanSee`, CallPolice brain with crime id and seconds, `Weapons.Fire(world, ...)`) in `GameFactory` or `Game/Session/CrimePedServices.cs`; cop brain slot `PedBrainFactory.Cops` |
| Missions | `Game/Missions/*`, `Phone/*`, `Dialogue/*` (G9) | `MissionRunner`; its G7 brain factory delegate (HostileBrain) and its `IWantedControl`-style seam routed to G8 (`ReportKnownCrime`, stars); `DialogueLines` as G6's `IDialogueHook`; `PhoneService` |
| Rendering | `Mod/Rendering/Renderer`, `RenderFrame` and draw structs (M3) | fill `RenderFrame` each frame from the world (player/peds → PedDraw with PedAnim; cars → CarDraw with body pose, wheels, lights, siren, damage; interior → InteriorDraw; effects from `Shot`/`Explosion`/impact events; markers from `MissionStatus`/door prompts; camera from `CameraRig`); resolver delegates for model id → `CitizenInfo`/`VehicleInfo` via `Catalogues` |
| HUD and menus | `Mod/Hud/HudRenderer`, `Mod/Menus/GtaUi`, `UiViews`, `IMenuCommands`, `IUiSounds` (M4) | `OnGUI` draws from `HudModel`; implement `IMenuCommands` over `GameSession` (menu choices, waypoint, settings, save via CS1's own save, return to city builder); fill `PhoneView` from `PhoneService`/`PhoneModel` and `WeaponWheelView` from G7's `WeaponWheelModel` |
| Audio | `Mod/Audio/AudioEvents`, `AudioPlayer`, `AmbienceDucker`, `Cs1VehicleEngine` (M5) | translate `GameEvents` (footsteps from motor steps and surface flags, shots, impacts, explosions, purchases, wanted level up, mission passed/failed, wasted, busted, phone texts) and continuous state (engine rpm, sirens, screech, interior room tone + ducking) into calls; implement `IUiSounds` |
| Persistence | `Mod/Persistence/ProfileStore`, serializable data extension | profile in and out of the session; take a fresh profile snapshot at CS1 save time instead of the 5 s cadence |
| Self-test | `tools/lib-paths.sh` (paths), launch.cfg keys `selftest`, `selftest_quit`, `autoload` in `<localApplicationData>/GtaSkylines/` | M6b writes `selftest-report.json`, `.txt` and `selftest-screenshots/` there |

## Behaviour to get right

- Promotion timing: the frame a CS1 vehicle/citizen is taken is the first frame our copy is drawn;
  no flicker, no double.
- Interior transitions: on entering, the renderer builds the interior and the indoor lighting
  override and audio ducking start during the fade; all restored on exit, on `End`, on level
  unload and on mod disable.
- Camera: apply `CameraRig` output before CS1's render manager culls (order our LateUpdate
  correctly; research script execution order or use the render hook).
- `CityEffect.Chirp(0, text)`: the applier picks a nearby walking citizen as the sender.
- Pause menu, weapon wheel and phone pause or slow the fixed step as DESIGN.md says; CS1's
  simulation rate follows the settings and pauses with the pause menu.
- Frame budget: simulation ≤ 4 ms average with 30 peds and 8 police cars; streaming ≤ 2 ms;
  log a one-line performance summary every 10 s at debug level.
- Every Unity callback guarded; one bad frame never takes the city down.

## Shared changes allowed in this chunk

- `Catalogues`: id → `VehicleInfo` / `CitizenInfo` accessors; citizen model display names for
  outfits.
- `GameSettings`: `VehiclesVolume`, `UiVolume`, `MinimapNorthUp` (with defaults, ranges,
  parse/serialise and options/pause-menu entries).
- `VehicleTuning`: cap car top speed at 50 m/s (CS1's prefab max × 1.4 can exceed 200 km/h).
- `ShopItem`/menus: an owned flag for weapons and properties.

## Verification

`tools/check.sh` green; pure helpers of the wiring (event → audio mapping, world → RenderFrame
conversion, menu command routing) tested in `tests/GtaSkylines.Mod.Tests`; a headless
integration test in `tests/GtaSkylines.Game.Tests` builds the full `GameFactory` session on a
`FakeCity` and plays a scripted minute (walk, steal a car, drive, crash, get a wanted level,
enter a shop, buy, leave, start mission V1) without exceptions and with all systems ticking.
