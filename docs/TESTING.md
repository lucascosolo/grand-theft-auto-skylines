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
3. `tools/install-mod.sh` copies `GtaSkylines.dll`, `GtaSkylines.Game.dll`, `GtaSkylines.Core.dll`,
   `GtaSkylines.Host.dll` (and `.pdb`/`.mdb`) to
   `~/.local/share/Colossal Order/Cities_Skylines/Addons/Mods/GtaSkylines/` and writes `launch.cfg` for normal play.
   `tools/install-mod.sh --check` shows what would happen. It never touches other mods, the game install or saves.
   To uninstall, move that folder away. The shared libraries carry GtaSkylines names because CS1 binds assemblies
   by simple name across all mods: the MinecraftSkylines mod ships `Skylines.Core.dll`/`Skylines.Host.dll`, and with
   the old names one mod ran against the other's copy (2026-10-11: MissingMethodException, driving dead). Leftover
   `Skylines.Core.*`/`Skylines.Host.*` files in the mod folder are moved to `~/.archive/<date>/gta-skylines-stale-install/`.
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

What the self-test does (`src/GtaSkylines.Mod/SelfTest/`): Ctrl+Shift+T in a loaded city (press again to abort), or
`selftest = city_load` 8 s after a city load, enters play on the road segment nearest the camera and drives the real
session with scripted inputs, one step at a time, each with a timeout: `enter_play`, `walk` (10 m), `jump`, `sprint`
(stamina drains), `enter_car` (nearest parked CS1 car within 80 m, else a Game car spawned next to the player), `drive`
(150 m along a wander route, inputs from the Game's `RouteFollower`), `crash` (full throttle into a Game car spawned 45 m
ahead), `exit_car`, `building` (nearest building with a door within 300 m, then its exit door), `punch` (nearest CS1
citizen within 80 m, else a spawned Game ped), `pistol` (one is granted, three shots; needs stars), `police` (a unit that
sees the player within 40 m), `busted` (hands up), `respawn`, `mission_v1` (started directly, then walked/driven along
road routes, shop counter and parked-car steal scripted; on failure the report says which mission step it reached),
`menus` (pause, phone, interaction, weapon wheel, big map: each opened, screenshotted, closed), `profile` (record encode
→ decode → re-encode identical, fields match the live profile; nothing written to disk) and `leave_play` (camera within
5 m of where it was, cursor and CS1 UI back, no modal left). A step whose prerequisite failed is skipped with
`needs <step>`; a step that lacks city content (no building, no campaign places) is skipped with the reason. Fixtures are
Game-side only; the self-test never changes CS1 state beyond what play mode itself does.

**Guarantee: a self-test never saves over a city, and never writes a save at all.** The mod issues no save during a run;
CS1's autosave is paused (its `AutoSaveTimer` pause counter, paired by `AutosaveHold`) from the city load in an
unattended run (`selftest = city_load`) or from the start of a Ctrl+Shift+T run, and stays paused until the run ends
(Ctrl+Shift+T) or the game quits (unattended; releasing earlier would let an overdue autosave fire at once). In the new
game the autoloader started, the first-entry backup offer is skipped; in an unattended run on an existing save it is
auto-declined (logged, no dialog, no backup save); in a Ctrl+Shift+T run on a real city the backup dialog appears as
usual and is yours to answer. The unattended vsync-off/60 fps setting is restored when the run ends. A brand-new city on a map has no buildings, citizens or police stations,
so most content steps skip there: run `--save "<save name>"` on a copy of a developed city for full coverage.

Reading the report: the end of the run prints `Self-test: N pass, M fail, K skip, E error`. In the JSON,
`summary` has those counts; each step lists its name, result (pass/fail/skip/error), duration and, on failure, the
reason and the timeout it hit; `aborted` with a reason means the run stopped early (open the screenshot of the
last step and `GtaSkylines.log` around it). Any `fail`/`error` is a bug to report; a `skip` names what it needed.
`performance` has the average FPS, worst frame, frames over 33 ms and per-phase averages (`sim_ms_avg`, `stream_ms_avg`,
`render_ms_avg`, `audio_ui_ms_avg`); `exceptions` counts the ERROR lines the mod log gained during the run. Look at the screenshots: a green step with a black or
wrong-looking screenshot still counts as a problem. A self-test pass is sandbox evidence for the owner's run,
not a substitute for the checklist below.

## 4. Manual playtest checklist

Use a copy of a save you do not mind losing. Controls are in `docs/DESIGN.md`. For each line: do, expect, note.
Tick it or write what happened (what you did, what you saw, rough time).

**F-MODE**
- F-MODE-1: drag the figure onto the map, or aim and press Ctrl+Shift+G (also aim at a bridge deck and a flat roof). Expect to appear on foot at that spot, on the road, bridge or roof the cursor was on rather than the terrain below it. Note: spawn height, camera.
- F-MODE-2: while playing, press city shortcuts (B, Esc menus) and move the mouse. Expect CS1 UI gone, cursor locked, traffic at about half speed. Note: any CS1 UI leaking through.
- F-MODE-3: press Esc or P; try Resume, Map (pan, zoom, waypoint), Brief, Stats, Settings, Controls, Return to City Builder. Expect the game paused, and the city view and UI restored exactly. Note: camera position after return.
- F-MODE-4: load a city, do not enter play, save and compare; then enter play the first time. Expect no city change before entry and a backup offer on first entry. Note: the backup file name.
- F-MODE-5 (render fix 1): play at dusk and night on foot, in chase and in hood/first-person views, looking at lit streets near and far. Expect steady lighting and shadows with no flicker; the far city may now end at 4 km. Note: any flicker, near geometry clipped away, distant city missing that matters.

**F-HUD**
- F-HUD: play a minute on foot and driving, commit a crime, lose the stars. Expect the rotating minimap bottom-left (roads, buildings, water, parks; player, waypoint, mission, shop, safehouse and, while wanted, cop blips; GPS route; zoom out with speed; flashing search circle), green health and blue armour bars under it, top-right wanted stars (flashing grey while unseen), cash with +/- deltas, weapon and ammo, top-left help box, bottom-centre objective and subtitles, bottom-right street and district on change and vehicle name on entry, all scaled to the resolution. Note: missing or misplaced elements.

**F-FOOT**
- F-FOOT-1: walk, jog, sprint until tired, jump, climb a 2 m ledge with Space, drop from height, stand on a steep slope. Expect stamina drain, fall damage, sliding on steep ground. Note: anything that feels stuck or floaty.
- F-FOOT-2: walk into terrain, a bridge, a building, trees, props, a parked car, a citizen. Expect to be blocked, never fall through. Note: where you clipped.
- F-FOOT-3: walk into deep water and stay. Expect swimming, a breath indicator, drowning when it runs out. Note: surface height.
- F-FOOT-4: watch your character move. Expect a CS1 citizen model with idle, walk, run, sprint animations. Note: sliding feet, T-poses.
- F-FOOT-5: cycle first person, three third-person distances, aim over the shoulder, back against a wall. Expect no camera through walls. Note: clipping.

**F-VEH**
- F-VEH-1: press F near a parked car, a moving car (carjack), a police car, bus, truck, ambulance; try a train or boat. Expect road vehicles enterable, the driver pulled out; trains and ships refused. Note: which kinds failed.
- F-VEH-2: drive; use handbrake drifts, reverse. Expect the car drawn with its own model and colour, arcade handling, and the CS1 vehicle gone from the road. Note: handling, wrong model.
- F-VEH-2b (render fix 1): drive a car, a narrow vehicle (scooter, bike) and a truck over kerbs and through turns. Expect the body upright on its wheels, never sunk into the road or tilted more than the physics lean. Note: model, how far it sank or tilted.
- F-VEH-2 two-wheelers: with a scooter or motorbike asset subscribed, ride it in tight and wide circles at several speeds, stop, then ride into a wall at full speed and at walking pace. Expect visible lean into each turn growing with speed, upright when stopped or parked, never tipping over while riding; the full-speed hit throws you off (knocked down, health lost) and the bike lies on its side; the slow bump does nothing. Note: lean too much or too little, wobble, steering too quick or slow, braking feel.
- F-VEH-3: hit walls, cars, pedestrians. Expect traffic shoved, peds knocked down, smoke at 40 % health, fire at 15 %, explosion 6 s after fire. Note: damage thresholds seen.
- F-VEH-4: get out and walk away 300 m. Expect the car to stay where it stopped, then vanish unseen. Note: distance when it vanished.
- F-VEH-5: horn (E), siren and light bar in a police car, headlights (H), engine sound. Expect each to work and sound right. Note: missing sounds.
- F-VEH-6: chase camera, hood camera (V), look behind (C). Expect sensible cameras for a big and a small vehicle. Note: camera inside the model.
- F-VEH-6b (render fix 1): take a scooter or bike, get off and walk round it; take a car, get out and look in through the window; watch a departure ghost. Expect no rider or driver on an empty vehicle, and the model otherwise complete (body not missing). Note: model name if a rider stays or the body disappears.
- F-VEH-7: stand by a row of parked cars on a busy street (debug overlay Ctrl+Shift+F10 on) until an owner walks to one and drives off; repeat on a quiet street. Expect the car never to vanish: it stays solid in place (overlay label GHOST) until CS1's car pulls out from that spot, or, if CS1 put the moving car elsewhere, until you look away from 60 m or more; pressing F on a ghost takes it and pulls the owner out. Log: one `ghost for vehicle ... ended:` line per ghost. Note: any car that blinked out, doubled, or a ghost that never ended.
- F-VEH-8: on a busy two-way road, stop a car across a lane and get out; watch from the pavement. Expect CS1 cars to queue behind it (no car drives through it), and after a minute or two routes to avoid that road (traffic info view shows it congested); only one car is drawn at the spot, same colour as before. Drive the car off, walk past the snapshot radius, or wait 3 minutes: the queue moves on. Save while it blocks: the save has no extra car. Log: one `effect BlockLane:` and one `effect UnblockLane:` line per block; note any `traffic pressure only` line.

**F-PED**
- F-PED-1: look at a citizen up close. Expect a card with name, age, job, home, happiness. Note: wrong or empty fields.
- F-PED-2: press E on several citizens. Expect short lines that fit their state. Note: lines that make no sense.
- F-PED-3: bump hard, threaten, punch a citizen. Expect stumble, flee, cower, hands up, some fight back, some call the police. Note: reaction seen.
- F-PED-4: injure and kill citizens. Expect an ambulance for the injured and a hearse for the dead; panicked citizens head home. Note: did the vehicle come (CS1 info panel).
- F-PED-5: leave an incident out of sight. Expect the citizens to go back to normal walking. Note: any stuck or frozen citizen.
- F-PED-6: take a Taxi or Paramedic job. Expect real waiting citizens and sick citizens as passengers. Note: citizen names match the city.
- F-PED-7: bump a walking citizen, then watch it calm down; carjack a car and watch the driver after it flees. Expect each to walk on to the building it was heading for and go in at the door (log: `effect ReturnCitizen: arrived at ...`); turn away mid-walk (beyond 60 m) and look back: the citizen continues the same way in CS1, never a second copy of it while we hold it (log: `continues to building ...`; `held citizen ... released` when CS1 tried to send it out). Note: teleports home, citizens stuck at doors, doubles.

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
- F-INT-2 (night lighting): visit an interior at night. Expect lamps, screens and signs lit (self-lit surfaces use an unlit fallback) and the room not dark. Note: unlit or black surfaces.
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
- F-QA-3: while playing, press Ctrl+Shift+F10 (Ctrl+Shift+F9 toggles the collision wireframe alone). Expect the collision wireframe; a label over every ped (kind, brain/anim, health) and car (owner, driver, km/h, damage) within 120 m; a line from each police unit to its target coloured by role and a cross at the target; the search zone circle and a yellow cross at the last known position while wanted; a panel with stars/evading/last known position/search radius/heat, every unit's role, and FPS, frame, session tick, streaming and render ms against the budget (OVER when exceeded). Press again: all of it gone. Note: missing layers, wrong roles.
- F-QA-3 building collision under construction: find a zoned building being built (scaffolding) and toggle the collision wireframe (Ctrl+Shift+F9). Expect the building's wireframe no higher than the drawn floors and rising with them (within about a second); a lot with only the foundation drawn has no building collision; walk against the drawn floors (solid) and through the air above them (no wall). Then wait for, or force, a level-up: during the upgrade the old building's collision shrinks from the top as CS1 takes it down and the new one rises with its drawn floors; once it completes, the new building's shape replaces the old within about a second without walking away. Player.log at play end: one `collision: N building shapes cut to their construction height, M left empty` line. Note: any invisible wall above a building site, any drawn floor you walk through, a stale shape after a level-up.
- Developer entry: holding Left Alt at the moment play mode starts opens a free-flying camera session (no game) for looking at the city and the collision streaming; release Alt before the drop for the real game. WASD flies, Space rises, Sneak descends, Sprint is faster; Esc or P opens a small box with Resume and Return to City Builder.

## 5. Sending evidence back

Right after a playtest, with the game closed:

```
tools/collect-evidence.sh
```

It copies `GtaSkylines.log`, `Player.log`, `launch.cfg`, the latest self-test report and screenshots into
`~/.cache/gta-skylines/evidence/collected-<UTC>/`. Tell the agent the folder name and paste your checklist notes
(feature ID, what you did, what happened). For a crash, also say what you were doing in the last minute and
whether the game closed or hung.
