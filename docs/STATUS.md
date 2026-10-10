# Status

Four separate columns, never merged: **implemented**, **built** (compiles against the owner's CS1
assemblies, Steam build 22724702), **sandbox-tested** (unit or headless tests, no game), **verified in
game** (owner run). Feature IDs from `docs/DESIGN.md`; chunks from `docs/PLAN.md`.

Updated as chunks land. Nothing is verified in game yet: the game cannot be launched from the
agent sandbox; the owner runs `tools/selftest.sh` and the checklist in `docs/TESTING.md`.

| Feature | What | Chunks | Implemented | Built | Sandbox-tested | Verified in game |
|---|---|---|---|---|---|---|
| F-MODE-1 | Enter play (toolbar drag, Ctrl+Shift+G) | M1 | | | | no |
| F-MODE-2 | CS1 UI/shortcuts suppressed, camera ours, sim rate | M1 | | | | no |
| F-MODE-3 | Pause menu (map, brief, stats, settings, exit) | M4, M7 | | | | no |
| F-MODE-4 | No city change before first entry; backup offer | M1 | | | | no |
| F-FOOT-1 | Capsule movement, sprint, jump, climb, fall damage | G1, G6 | | | | no |
| F-FOOT-2 | Collision with all city geometry and actors | G1, M2 | | | | no |
| F-FOOT-3 | Swimming and drowning | G1, G6 | | | | no |
| F-FOOT-4 | CS1 citizen model and animations, outfits | M3, G6 | | | | no |
| F-FOOT-5 | First/third person cameras, collision-aware | G6, M3 | | | | no |
| F-VEH-1 | Enter any road vehicle; carjack | G6, M2 | | | | no |
| F-VEH-2 | Taken vehicle drawn with CS1 model; arcade physics | G5, M3 | | | | no |
| F-VEH-3 | Vehicle collisions, damage, fire, explosion | G5, G7, M3 | | | | no |
| F-VEH-4 | Abandoned vehicles persist then despawn | G6 | | | | no |
| F-VEH-5 | Horn, siren, headlights, engine sound | G6, M3, M5 | | | | no |
| F-VEH-6 | Chase, hood and first-person car cameras | G6 | | | | no |
| F-PED-1 | Ped card from CS1 identity | G9, M2, M4 | | | | no |
| F-PED-2 | Talk lines from CS1 state | G9 | | | | no |
| F-PED-3 | Ped actors: stumble, flee, cower, hands up, fight, call police | G7 | | | | no |
| F-PED-4 | Injury/death into CS1 (ambulance, hearse), panic | G7, M2 | | | | no |
| F-PED-5 | Ped actors returned to CS1 | G6, M2 | | | | no |
| F-PED-6 | Citizens as taxi fares and patients | G9 | | | | no |
| F-CMB-1 | Weapons, wheel, ammo, reload | G7, M4 | | | | no |
| F-CMB-2 | Melee and hitscan, tracers, view model | G7, M3 | | | | no |
| F-CMB-3 | Health, armour, regeneration | G7 | | | | no |
| F-CMB-4 | Wasted and hospital respawn | G6, G7 | | | | no |
| F-HEAT-1 | Crime events | G8 | | | | no |
| F-HEAT-2 | Witness calls; unwitnessed crimes unknown | G7, G8 | | | | no |
| F-HEAT-3 | Wanted stars 1-5 | G8 | | | | no |
| F-HEAT-4 | Last known position, search zone, evasion | G8, M4 | | | | no |
| F-HEAT-5 | Recognition by description; disguise | G8 | | | | no |
| F-HEAT-6 | Lingering player and district heat | G8 | | | | no |
| F-HEAT-7 | Smart cops: roles, roadblocks, PIT, box-in, helicopter | G8 | | | | no |
| F-HEAT-8 | Arrest vs shooting rules | G8 | | | | no |
| F-HEAT-9 | Busted; hiding in interiors; cops enter at 4+ | G8, G6 | | | | no |
| F-INT-1 | Doors, markers, prompts | M2, M3, G6 | | | | no |
| F-INT-2 | Generated interiors, isolated space, lighting | G3, G6, M3 | | | | no |
| F-INT-3 | Archetypes and furnishing | G3 | | | | no |
| F-INT-4 | Occupants from the city; trespassing | G6, G7 | | | | no |
| F-INT-5 | Shops, safehouse, hospital, police desk, respray | G4, G6, M4 | | | | no |
| F-INT-6 | Exit back to the street | G6 | | | | no |
| F-JOB-1 | Mission framework | G9 | | | | no |
| F-JOB-2 | Campaign: 9 missions, 3 contacts, from city places | G9 | | | | no |
| F-JOB-3 | Side jobs: taxi, paramedic, firefighter, vigilante, delivery, race | G9 | | | | no |
| F-JOB-4 | Phone and Chirper | G9, M4 | | | | no |
| F-ECO-1 | Cash, purchases, fees | G4, G6 | | | | no |
| F-ECO-2 | Property and safehouses | G4, G6 | | | | no |
| F-ECO-3 | Stats | G4, G6, M4 | | | | no |
| F-SAVE-1 | Profile in the city save, versioned, CRC | G4, M1 | | | | no |
| F-SAVE-2 | Global settings and key bindings | G4, M1 | | | | no |
| F-SAVE-3 | City loads without the mod | M1 | | | | no |
| F-SET | Settings and rebinding | G4, M1, M4 | | | | no |
| F-AUD | Audio | M5 | | | | no |
| F-HUD | GTA HUD | G6, M4 | | | | no |
| F-QA-1 | Game layer unit-tested | all G | | | | no |
| F-QA-2 | In-game self-test | M6 | | | | no |
| F-QA-3 | Debug overlay | M2, M7 | | | | no |
