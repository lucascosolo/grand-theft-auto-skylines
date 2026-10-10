# Status

Four separate columns, never merged: **implemented**, **built** (compiles against the owner's CS1
assemblies, Steam build 22724702, 0 warnings), **sandbox-tested** (unit or headless tests, no game),
**verified in game** (owner run). Feature IDs from `docs/DESIGN.md`; chunks from `docs/PLAN.md`.

**Nothing is verified in game yet.** The game cannot be launched from the development sandbox. The
owner's first run is `tools/selftest.sh` (unattended, writes a report) followed by the manual
checklist in `docs/TESTING.md`.

| Feature | What | Chunks | Implemented | Built | Sandbox-tested | Verified in game |
|---|---|---|---|---|---|---|
| F-MODE-1 | Enter play (toolbar drag, Ctrl+Shift+G) | M1, M7 | yes | yes | helpers (M1) | no |
| F-MODE-2 | CS1 UI/shortcuts suppressed, camera ours, sim rate | M1, M7 | yes | yes | helpers (M1) | no |
| F-MODE-3 | Pause menu (map, brief, stats, settings, controls, game) | M4, M7 | yes | yes | menu logic (M4), routing (M7) | no |
| F-MODE-4 | No city change before first entry; backup offer | M1 | yes | yes | helpers (M1) | no |
| F-FOOT-1 | Capsule movement, sprint/stamina, jump, ledge climb, fall damage | G1, G6 | yes | yes | yes (physics 132, session) | no |
| F-FOOT-2 | Collision with terrain, roads, buildings, props, trees, vehicles, citizens | G1, M2 | yes | yes | collision world yes; streaming geometry from the reference project | no |
| F-FOOT-3 | Swimming, breath, drowning | G1, G6 | yes | yes | yes | no |
| F-FOOT-4 | CS1 citizen model and animations; outfits | M3, G6, polish | yes | yes | anim mapping (M3) | no |
| F-FOOT-5 | First/third-person cameras, collision-aware spring arm, aim view | G6 | yes | yes | yes (camera tests) | no |
| F-VEH-1 | Enter any road vehicle; carjack moving traffic | G6, M2 | yes | yes | yes (session tests) | no |
| F-VEH-2 | Taken vehicle drawn with its CS1 model; arcade physics | G5, M3 | yes | yes | yes (vehicles 114) | no |
| F-VEH-3 | Vehicle collisions, damage, smoke, fire, explosion | G5, G6, G7 | yes | yes | yes | no |
| F-VEH-4 | Abandoned vehicles persist, despawn far away | G6 | yes | yes | yes | no |
| F-VEH-5 | Horn, siren, headlights, engine and siren sound | G6, M3, M5, M7 | yes | yes | audio synth tests; cue mapping (M7) | no |
| F-VEH-6 | Chase, hood and first-person car cameras, look behind | G6 | yes | yes | yes | no |
| F-PED-1 | Ped card from CS1 identity | G9, M2, M4 | yes | yes | yes | no |
| F-PED-2 | Talk lines from CS1 state (147 lines) | G9 | yes | yes | yes | no |
| F-PED-3 | Ped actors: stumble, flee, cower, hands up, fight, call police | G7 | yes | yes | yes (G7 218) | no |
| F-PED-4 | Injury/death into CS1 (ambulance, hearse), panic | G7, M2 | yes | yes | effects asserted; CS1 paths read in the decompile | no |
| F-PED-5 | Ped actors returned to CS1 | G6, M2 | yes | yes | yes | no |
| F-PED-6 | Citizens as taxi fares and patients | G9 | yes | yes | yes | no |
| F-CMB-1 | Weapons, wheel, ammo, reload | G7, M4 | yes | yes | yes | no |
| F-CMB-2 | Melee and hitscan, tracers, muzzle flash, view model | G7, M3 | yes | yes | combat yes; drawing pure parts | no |
| F-CMB-3 | Health, armour, regeneration | G7 | yes | yes | yes | no |
| F-CMB-4 | Wasted and hospital respawn | G6, G7 | yes | yes | yes | no |
| F-HEAT-1 | Crime events | G8 | yes | yes | yes (G8 196) | no |
| F-HEAT-2 | Witness calls; unwitnessed crimes unknown | G7, G8 | yes | yes | yes | no |
| F-HEAT-3 | Wanted stars 1-5 with escalation | G8 | yes | yes | yes | no |
| F-HEAT-4 | Last known position, search zone, evasion | G8, M4 | yes | yes | yes | no |
| F-HEAT-5 | Recognition by description; disguise by car/outfit/respray | G8 | yes | yes | yes | no |
| F-HEAT-6 | Lingering player and district heat | G8 | yes | yes | yes | no |
| F-HEAT-7 | Smart cops: pursue, intercept, search, roadblock, box-in, PIT, foot, helicopter | G8 | yes | yes | roles and dispatch yes; driving pursuit in the integration test | no |
| F-HEAT-8 | Arrest at low stars, shooting at 3+ or when armed | G8 | yes | yes | yes | no |
| F-HEAT-9 | Busted; hiding in interiors; cops enter at 4+ | G8, G6 | yes | yes | yes | no |
| F-INT-1 | Doors, markers, prompts | M2, M3, G6 | yes | yes | prompts yes; door positions from the decompile | no |
| F-INT-2 | Generated interiors in an isolated space with indoor lighting | G3, G6, M3 | yes | yes | yes (interiors 502) | no |
| F-INT-3 | Archetypes and furnishing | G3 | yes | yes | yes | no |
| F-INT-4 | Occupants from the city; trespassing | G6, G7 | yes | yes | yes | no |
| F-INT-5 | Shops, safehouse save/sleep, hospital, police desk, respray | G4, G6, M4 | yes | yes | yes | no |
| F-INT-6 | Exit back to the street | G6 | yes | yes | yes | no |
| F-JOB-1 | Mission framework (17 step types) | G9 | yes | yes | yes (G9 256) | no |
| F-JOB-2 | Campaign: 9 missions, 3 contacts, from the city's places | G9 | yes | yes | yes, incl. a city with no services | no |
| F-JOB-3 | Side jobs: taxi, paramedic, firefighter, vigilante, delivery, race | G9 | yes | yes | yes | no |
| F-JOB-4 | Phone and Chirper | G9, M4 | yes | yes | yes | no |
| F-ECO-1 | Cash, purchases, fees | G4, G6 | yes | yes | yes | no |
| F-ECO-2 | Property and safehouses | G4, G6 | yes | yes | yes | no |
| F-ECO-3 | Stats | G4, G6, M4 | yes | yes | yes | no |
| F-SAVE-1 | Profile in the city save, versioned, CRC, snapshot at save | G4, M1, M7 | yes | yes | codec yes | no |
| F-SAVE-2 | Global settings and key bindings | G4, M1 | yes | yes | yes | no |
| F-SAVE-3 | City loads without the mod | M1 | yes | yes | by design (mod data key ignored by CS1) | no |
| F-SET | Settings and rebinding (options page and pause menu) | G4, M1, M4 | yes | yes | yes | no |
| F-AUD | Procedural audio, CS1 engine sounds, indoor ducking | M5, M7 | yes | yes | synthesis yes | no |
| F-HUD | GTA V-style HUD, minimap, big screens | G6, M4 | yes | yes | HUD model and layout maths | no |
| F-QA-1 | Game layer unit-tested | all G | yes | yes | 3 875 tests green | no |
| F-QA-2 | In-game self-test and unattended runner | M6 | in progress |  |  | no |
| F-QA-3 | Debug overlay (collision viewer, perf log) | M2, M7 | yes | yes | n/a | no |

## Known limitations (by design or deferred)

- Cops on foot look like ordinary citizens: CS1 has no police pedestrian model.
- The police helicopter appears only when the game has a police helicopter prefab (otherwise two extra cars).
- Cops who follow the player into an interior search with seeded walking, not room by room.
- Interiors are generated per building and are not the building's real floor plan (GTA-style interior cells).
- Interior self-lit surfaces use an unlit fallback; CS1's prop illumination channel appears night-gated (unverified).
- No custom art: characters and vehicles are CS1's own models; weapons and furniture are procedural.
- Out of scope for v1 (DESIGN.md): multiplayer, aircraft, boats, trains, bicycles as player vehicles, voiced dialogue.

## First in-game risks (from the chunk reports)

See `.agent/ledger.md` entries for M2, M3, M4, M5, M7: camera ordering against CS1's culling, a possible one-frame
gap when a CS1 vehicle is taken, RenderTexture orientation on some GPUs, CS1 speed-unit assumptions, and citizens
taken from the street possibly respawning from home while the game holds them.
