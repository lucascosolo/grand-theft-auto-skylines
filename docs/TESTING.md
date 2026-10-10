# Testing

Three layers, from cheapest to most real. Only the last proves anything about CS1 integration.

## 1. Automated checks: `tools/check.sh`

Builds the whole solution (net35 for CS1, net10.0 for tests) with warnings as errors, runs every unit test
(reports in `reports/cs/*.trx`), then runs the shell tests in `tests/tools/` (the installer against a fake
data folder). Run it from the repository root; it must print `All checks passed.`

Covers: everything in `GtaSkylines.Game` (physics, combat, heat, police, interiors, missions, economy),
pure helpers in `Skylines.Core`/`Skylines.Host`/the Mod (mesh and audio synthesis, serialization formats), and
that the mod compiles against the owner's CS1 assemblies (Steam build 22724702). The installer script is
tested for file placement, `launch.cfg` contents and refusals.

Cannot cover: anything that needs the running game (Unity, CS1 managers, rendering, audio output, input,
saving and loading through CS1, the simulation thread, performance). A passing `check.sh` says the logic is
sound and the code builds, not that the mod works in the game. `docs/STATUS.md` keeps these separate.

## 2. Install procedure (owner)

1. `tools/check.sh` (builds the Release outputs).
2. Quit Cities: Skylines (the installer refuses to copy while `Cities.x64` runs).
3. `tools/install-mod.sh` copies `GtaSkylines.dll`, `GtaSkylines.Game.dll`, `Skylines.Core.dll`,
   `Skylines.Host.dll` (and `.pdb`/`.mdb`) to
   `~/.local/share/Colossal Order/Cities_Skylines/Addons/Mods/GtaSkylines/` and writes `launch.cfg` for normal play.
   `tools/install-mod.sh --check` shows what would happen. It never touches other mods, the game install or saves.
   To uninstall, move that folder away.
4. Start the game, Content Manager, Mods, enable "GtaSkylines", load a city (use a copy of a save).

Paths are defined once in `tools/lib-paths.sh`. The mod data folder is
`~/.local/share/Colossal Order/Cities_Skylines/GtaSkylines/`; it holds `launch.cfg`, the settings file and the
self-test output. The mod log is `~/.local/share/Colossal Order/Cities_Skylines/ModLogs/GtaSkylines.log`.

`launch.cfg` keys (`key = value`, `#` comment): `selftest` (`off` | `city_load`), `selftest_quit` (`true` | `false`),
`autoload` (`new:<map>` or a save name; absent = off). `--selftest [<map>]` and `--selftest-save "<save>"`
set them for an unattended run; `--normal` sets `selftest = off`, `selftest_quit = false`, no autoload.

## 3. Unattended self-test: `tools/selftest.sh`

Needs Steam running and logged in on the desktop, and no game running.

```
tools/selftest.sh                       # new city on Green Plains, nothing saved
tools/selftest.sh --new-game "<map>"    # another built-in map
tools/selftest.sh --save "<save name>"  # an existing save (self-test never saves over it)
```

It installs with `--selftest`, starts `Cities.x64` directly (`GTA_LAUNCH=steam` uses `steam -applaunch 255710`
instead, which opens the Paradox Launcher and needs a click), polls the mod log, waits for the report, waits for
the game to quit itself, copies the evidence to `~/.cache/gta-skylines/evidence/selftest-<UTC>/` and resets
`launch.cfg` to normal play. It never kills a process and never deletes. Exit codes: 0 report found, 1 refused
(game running) or install failed, 2 game did not start, 3 timeout or game exited without a report, 4 bad usage.
Timeouts are set by `GTA_TIMEOUT_SECONDS` (default 2400) and `GTA_START_SECONDS` (180); other overrides are in
the script header.

Evidence folder: `selftest-report.json`, `selftest-report.txt`, `selftest-screenshots/`, `GtaSkylines.log`,
`Player.log`, `launch.cfg`. The report is written by the mod to
`<mod data folder>/selftest-report.json` (screenshots to `selftest-screenshots/`).

Reading the report: the end of the run prints `Self-test: N pass, M fail, K skip, E error`. In the JSON,
`summary` has those counts; each step lists its name, result (pass/fail/skip/error), duration and, on failure, the
reason and the timeout it hit; `aborted` with a reason means the run stopped early (open the screenshot of the
last step and `GtaSkylines.log` around it). Any `fail`/`error` is a bug to report; a `skip` names what it needed.
FPS and per-system milliseconds are in the log. Look at the screenshots: a green step with a black or
wrong-looking screenshot still counts as a problem. A self-test pass is sandbox evidence for the owner's run,
not a substitute for the checklist below.

## 4. Manual playtest checklist

Use a copy of a save you do not mind losing. Controls are in `docs/DESIGN.md`. For each line: do, expect, note.
Tick it or write what happened (what you did, what you saw, rough time).

**F-MODE**
- F-MODE-1: drag the figure onto the map, or aim and press Ctrl+Shift+G. Expect to appear on foot at that spot. Note: spawn height, camera.
- F-MODE-2: while playing, press city shortcuts (B, Esc menus) and move the mouse. Expect CS1 UI gone, cursor locked, traffic at about half speed. Note: any CS1 UI leaking through.
- F-MODE-3: press Esc or P; try Resume, Map (pan, zoom, waypoint), Brief, Stats, Settings, Controls, Return to City Builder. Expect the game paused, and the city view and UI restored exactly. Note: camera position after return.
- F-MODE-4: load a city, do not enter play, save and compare; then enter play the first time. Expect no city change before entry and a backup offer on first entry. Note: the backup file name.

**F-FOOT**
- F-FOOT-1: walk, jog, sprint until tired, jump, climb a 2 m ledge with Space, drop from height, stand on a steep slope. Expect stamina drain, fall damage, sliding on steep ground. Note: anything that feels stuck or floaty.
- F-FOOT-2: walk into terrain, a bridge, a building, trees, props, a parked car, a citizen. Expect to be blocked, never fall through. Note: where you clipped.
- F-FOOT-3: walk into deep water and stay. Expect swimming, a breath indicator, drowning when it runs out. Note: surface height.
- F-FOOT-4: watch your character move. Expect a CS1 citizen model with idle, walk, run, sprint animations. Note: sliding feet, T-poses.
- F-FOOT-5: cycle first person, three third-person distances, aim over the shoulder, back against a wall. Expect no camera through walls. Note: clipping.

**F-VEH**
- F-VEH-1: press F near a parked car, a moving car (carjack), a police car, bus, truck, ambulance; try a train or boat. Expect road vehicles enterable, the driver pulled out; trains and ships refused. Note: which kinds failed.
- F-VEH-2: drive; use handbrake drifts, reverse. Expect the car drawn with its own model and colour, arcade handling, and the CS1 vehicle gone from the road. Note: handling, wrong model.
- F-VEH-3: hit walls, cars, pedestrians. Expect traffic shoved, peds knocked down, smoke at 40 % health, fire at 15 %, explosion 6 s after fire. Note: damage thresholds seen.
- F-VEH-4: get out and walk away 300 m. Expect the car to stay where it stopped, then vanish unseen. Note: distance when it vanished.
- F-VEH-5: horn (E), siren and light bar in a police car, headlights (H), engine sound. Expect each to work and sound right. Note: missing sounds.
- F-VEH-6: chase camera, hood camera (V), look behind (C). Expect sensible cameras for a big and a small vehicle. Note: camera inside the model.

**F-PED**
- F-PED-1: look at a citizen up close. Expect a card with name, age, job, home, happiness. Note: wrong or empty fields.
- F-PED-2: press E on several citizens. Expect short lines that fit their state. Note: lines that make no sense.
- F-PED-3: bump hard, threaten, punch a citizen. Expect stumble, flee, cower, hands up, some fight back, some call the police. Note: reaction seen.
- F-PED-4: injure and kill citizens. Expect an ambulance for the injured and a hearse for the dead; panicked citizens head home. Note: did the vehicle come (CS1 info panel).
- F-PED-5: leave an incident out of sight. Expect the citizens to go back to normal walking. Note: any stuck or frozen citizen.
- F-PED-6: take a Taxi or Paramedic job. Expect real waiting citizens and sick citizens as passengers. Note: citizen names match the city.

**F-CMB**
- F-CMB-1: Tab weapon wheel; select fists, bat, pistol, SMG, shotgun, carbine; fire and reload (R). Expect ammo counts and reload. Note: missing weapons.
- F-CMB-2: melee a citizen; shoot walls, a car, a ped. Expect hit reactions, muzzle flash, tracers, recoil, impacts. Note: hits that pass through things.
- F-CMB-3: take damage, wait; buy armour and health. Expect regeneration to 50 %, armour bar. Note: regen speed.
- F-CMB-4: die. Expect WASTED, respawn at the nearest hospital, cash fee, ammo lost. Note: fee amount.

**F-HEAT**
- F-HEAT-1: commit each crime type (theft, assault, hit-and-run, murder, firing, threatening, trespassing, robbery, reckless driving, resisting, attacking and killing police). Expect each recognised. Note: any that did nothing.
- F-HEAT-2: commit a crime seen only by a citizen; stop the witness before the call ends, then let one finish. Expect no stars until the call lands (witness visibly on the phone). Note: call duration.
- F-HEAT-3: repeat crimes while wanted. Expect 1 to 5 stars escalating. Note: stars per crime.
- F-HEAT-4: break line of sight. Expect stars flashing, a search zone on the minimap widening, then the wanted level clearing. Note: time to clear.
- F-HEAT-5: change car or clothes unseen, sneak in cover. Expect police to need to re-identify you; lower detection. Note: how fast they found you.
- F-HEAT-6: lose the stars, then look at the pause menu. Expect heat for the district, more patrols nearby. Note: heat meter values.
- F-HEAT-7: at 3, 4, 5 stars. Expect units from police stations by road, roles (pursue, intercept, search, roadblock, box-in, PIT), helicopter at 4+ if the city has one. Note: stuck or teleporting police cars.
- F-HEAT-8: at 1 to 2 stars press X or stand still; at 3+ or armed. Expect arrest attempts at low stars, shooting at high. Note: BUSTED trigger.
- F-HEAT-9: get BUSTED, then hide in an interior at 4+ stars. Expect respawn at the police station, weapons lost, a fine; cops may follow in. Note: fine amount.

**F-INT**
- F-INT-1: walk to several building types. Expect a marker and "Press E to enter <name>" within 2 m. Note: buildings with no door.
- F-INT-2: enter, leave, re-enter the same building. Expect the same layout, a fade, own lighting. Note: layout differences.
- F-INT-3: enter house, apartment, shop, office, factory, police station, hospital, fire station, school. Expect furnished, connected rooms. Note: empty or broken interiors.
- F-INT-4: enter a home and a workplace at day and night. Expect residents at home, workers at work, clerks at counters; a home entry counts as trespassing. Note: occupant names.
- F-INT-5: use a shop, safehouse (save, outfit, sleep), hospital, police station (pay off heat), respray garage. Expect each service to work. Note: costs.
- F-INT-6: exit. Expect to stand on the street at the door. Note: wrong side.

**F-JOB**
- F-JOB-1: start a mission from a marker. Expect briefing subtitles, objectives, MISSION PASSED or FAILED with reason and reward. Note: fail conditions that surprised you.
- F-JOB-2: play the contact campaign (8+ missions, three contacts). Expect real places from your city. Note: the mission that blocked you.
- F-JOB-3: Taxi, Paramedic, Firefighter, Vigilante, Delivery, Street race. Expect each to use real city data. Note: which have no valid targets.
- F-JOB-4: open the phone (Up arrow). Expect contacts, offers, texts, Chirper reacting to your actions. Note: missing messages.

**F-ECO**
- F-ECO-1: earn (job, robbery) and spend (weapons, food, clothes, respray, fines). Expect cash deltas on the HUD. Note: odd prices.
- F-ECO-2: buy a residential building. Expect a safehouse with save and spawn. Note: price.
- F-ECO-3: open Stats. Expect distance, vehicles stolen, crimes, arrests, deaths, jobs, money. Note: counters that do not move.

**F-SAVE**
- F-SAVE-1: save from the pause menu, quit, load the city. Expect cash, health, weapons, outfit, properties, mission progress, stats, heat, last safehouse to come back. Note: anything lost.
- F-SAVE-2: change a setting and a key binding, load another city. Expect them unchanged. Note: per-city leaks.
- F-SAVE-3: disable the mod and load the saved city. Expect a normal load. Note: any error.

**F-SET**
- F-SET: change mouse sensitivity, invert Y, FOV, camera defaults, HUD scale, minimap, subtitles, sim rate, police difficulty, volumes, a key binding, from CS1 mod options and from the pause menu. Expect each to apply. Note: settings with no effect.

**F-AUD**
- F-AUD: listen to footsteps on road and soil, engine, horn, siren, gunshots, impacts, UI blips, phone, mission jingles. Expect all present and not clipping. Note: missing or harsh sounds.

**F-QA**
- F-QA-1: `tools/check.sh` passes.
- F-QA-2: press Ctrl+Shift+T in a new city. Expect the self-test to run and write its report. Note: failed steps.
- F-QA-3: toggle the debug overlay. Expect collision, actor states, police roles and search zone, FPS budget. Note: missing layers.

## 5. Sending evidence back

Right after a playtest, with the game closed:

```
tools/collect-evidence.sh
```

It copies `GtaSkylines.log`, `Player.log`, `launch.cfg`, the latest self-test report and screenshots into
`~/.cache/gta-skylines/evidence/collected-<UTC>/`. Tell the agent the folder name and paste your checklist notes
(feature ID, what you did, what happened). For a crash, also say what you were doing in the last minute and
whether the game closed or hung.
