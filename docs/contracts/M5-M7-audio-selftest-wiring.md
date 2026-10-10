# M5, M6, M7 contracts: audio, self-test and tooling, wiring

## M5 Audio — owns `src/GtaSkylines.Mod/Audio/`

Procedural sound (no audio assets shipped): clips synthesised once at load with
`AudioClip.Create` + `SetData` (verify the Unity 5.6 signatures) and played through a small pool
of `AudioSource`s (2D for UI, 3D with rolloff for world sounds) attached to our controller
object; the listener is CS1's (the main camera). Sounds: footsteps by surface (`SurfaceFlags`:
road/pavement hard, terrain soft, interior wood/tile), jump/land, punches and bat hits,
gunshots per weapon (layered noise burst + low thump + tail; shotgun boom; SMG rattle), shell
clicks and reloads, bullet impacts, car crash by impact speed, horn (two-tone), sirens (police
wail and yelp, ambulance hi-lo, fire horn) as loops with Doppler, engine as a loop whose pitch
and volume follow `EngineRpm`/throttle (use the vehicle's own CS1 engine effect instead when it
can be driven for our car — research `VehicleInfo.m_effects` / `EngineSoundEffect`), tyre
screech from lateral slip, explosion, UI (menu move/select/back, purchase, cash, phone ring and
text tone, mission passed jingle, mission failed, wasted and busted stingers, wanted level up),
interior room tone with CS1's ambient city sound ducked while inside (research `AudioManager`
volume controls; restore exactly). Volume groups from `GameSettings` (master, effects, vehicles,
UI). Pure synthesis functions get net10 tests (length, peak level, no NaN, loop seamless).

## M6 Self-test, install and docs — owns `src/GtaSkylines.Mod/SelfTest/`, `tools/`, `docs/TESTING.md`, `docs/STATUS.md`

- In-game self-test (Ctrl+Shift+T, and unattended through a launch config file in the mod's
  data folder: `selftest = city_load`, `selftest_quit = true`, `autoload = new:<map>` — port
  the reference's `SaveAutoloader`, `LaunchConfig` and `SelfTestController` patterns) that
  drives the real session with scripted inputs on a new city on a built-in map: enter play on a
  road; walk 10 m; jump; sprint; enter the nearest parked car; drive 150 m along a road with the
  route follower feeding inputs; crash into a wall at speed (damage); exit; enter the nearest
  enterable building, use the exit; punch an ambient citizen (promotion, reaction); fire a
  pistol (witness call, stars); wait for a police unit to spawn and pursue; hands up → busted;
  respawn; start mission V1 and complete it with scripted moves; open and close every menu;
  profile save → load round trip; leave play mode with the camera and UI restored. Each step
  has a timeout, pass/fail, timings and an `Application.CaptureScreenshot` at key moments; the
  report (text + JSON) and screenshots go to the mod's data folder; FPS and per-system ms are
  logged. Nothing is saved over a real city.
- `tools/install-mod.sh` (copies the four DLLs into the CS1 local mods folder
  `~/.local/share/Colossal Order/Cities_Skylines/Addons/Mods/GtaSkylines/`, never the game
  install, `--normal`/`--selftest` launch config modes), `tools/selftest.sh` (port the
  reference's `remote-selftest.sh`: start the game, wait for the report, copy evidence to
  `~/.cache/gta-skylines/evidence/selftest-<UTC>/`, restore normal launch config; never kills
  or deletes), `tools/collect-evidence.sh`.
- `docs/TESTING.md`: automated checks, the owner's install and self-test procedure, then a
  manual playtest checklist with one line per DESIGN.md feature ID; `docs/STATUS.md` filled
  per feature with the four columns.

## M7 Wiring — owns `src/GtaSkylines.Mod/Play/` and `src/GtaSkylines.Game/Session/GameFactory.cs`

Replace M1's `FreeCameraSession` with the real play session: `GameFactory` builds a
`GameSession` with every Game system (G6 core, G7 peds and combat, G8 crime and police, G9
missions) in order; the Mod session adapter feeds the InputFrame, `CitySnapshot`
(SnapshotBuilder), streamed collision regions and the road graph (M2), applies drained
`CityEffect`s (M2), drives the renderers (M3), HUD and menus (M4) and audio (M5), routes menu
commands back to the session, saves and loads the profile through the serializable data
extension, handles interiors' lighting override on transitions, and enforces a frame budget
(simulation ≤ 4 ms average with 30 peds and 8 police cars; logged once a second at debug level).
Pause menu, weapon wheel and phone pause or slow the fixed step as DESIGN.md says.
