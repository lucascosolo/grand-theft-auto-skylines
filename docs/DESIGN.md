# GTA: Skylines — product design

A Cities: Skylines (CS1) mod that lets you step out of the city builder into your own city as a
GTA-style character: walk, sprint, fight, steal any car, drive, enter every building, run jobs,
and get chased by smart, witness-driven police. **Everything runs inside CS1**: one mod, no second
game, no external process. The city keeps simulating around you, and what you do feeds back into
it (ambulances for the injured, hearses for the dead, police from the city's own stations).

This file is the contract for "feature-complete". Every feature has an ID; `docs/STATUS.md`
reports each one as implemented / built / sandbox-tested / verified in game.

## Feel: GTA V/VI on PC

First-person view by default; **V** cycles first person → third person near → mid → far, on foot
and in vehicles separately (as GTA V does). Controls and HUD copy GTA's PC conventions so a GTA
player needs no tutorial.

### Controls (defaults; all rebindable, F-SET)

| Action | On foot | In a vehicle |
|---|---|---|
| Move / drive | W A S D | W throttle, S brake/reverse, A D steer |
| Look | mouse | mouse (camera recentres after 1.5 s idle) |
| Sprint | Shift (hold; tap-sprint also works) | — |
| Jump / climb | Space (climbs ledges up to 2 m) | handbrake |
| Sneak | Left Ctrl (toggle) | — |
| Enter / exit vehicle | F (carjacks occupied cars) | F |
| Interact | E (talk, doors, shops, pick up) | E horn; emergency vehicles: E toggles siren |
| Attack / fire | left mouse | left mouse while aiming: drive-by |
| Aim | right mouse | right mouse |
| Reload | R | — |
| Weapon wheel | Tab (hold); mouse wheel cycles | — |
| Hands up (surrender) | X | — |
| Camera view | V | V |
| Look behind | C | C |
| Headlights | — | H |
| Phone | Up arrow | Up arrow |
| Interaction menu | M | M |
| Pause menu (map, brief, stats, settings, exit) | Esc or P | Esc or P |
| Big map toggle on the minimap | Z | Z |

### HUD (GTA V layout, scaled to resolution, F-HUD)

- **Minimap**, bottom-left, rotating with the camera: roads, buildings, water and parks drawn from
  the city's own data; blips for the player, waypoint, missions, shops, safehouses, cops (only
  while wanted), mission targets; GPS route line along the roads to the waypoint or objective;
  zooms out with speed. Police search area drawn as a flashing circle (heat search zone).
- **Health** (green) and **armour** (blue) bars under the minimap.
- Top-right: **wanted stars** (filled white; flashing grey while police have lost sight),
  **cash** with animated +/− deltas, **weapon** name and ammo.
- Top-left: **help box** (black translucent, "Press ~F~ to enter vehicle").
- Bottom-centre: **objective** text with highlighted keywords; **subtitles** for dialogue.
- Bottom-right: **street and district name** when they change (CS1 road and district names);
  **vehicle name** when you get in.
- Notification feed above the minimap: phone texts, mission contacts, and **Chirper** posts in
  which citizens react to what you do.
- Full-screen states: **WASTED**, **BUSTED**, **MISSION PASSED** (with reward), **MISSION FAILED**
  (with reason). A brief fade on interior transitions.
- Crosshair: a dot when unarmed or aiming; a ped info card when looking at a citizen up close
  (name, age, job, home from CS1's data).

## Features

### F-MODE: entering and leaving play

- F-MODE-1 Enter from the city view by toolbar button (drag the figure onto the map, Street View
  style) or Ctrl+Shift+G (aim at a point). Spawn at the drop point on the nearest walkable
  surface, or at the last safehouse for a returning profile.
- F-MODE-2 While playing, CS1's UI and its shortcuts are suppressed, the camera is ours, the cursor
  is locked, and the simulation runs at a configurable rate (default 0.5x) so traffic and
  pedestrians move at believable speeds.
- F-MODE-3 Pause menu (Esc/P) pauses the game and the simulation and offers Resume, Map (pan,
  zoom, set waypoint), Brief (current objective, mission log), Stats, Settings, Controls, and
  Return to City Builder (camera and UI restored exactly).
- F-MODE-4 Nothing changes in a city until the player first enters play; the first entry per city
  offers a backup save (the reference project's verified backup routine).

### F-FOOT: the player on foot

- F-FOOT-1 Kinematic capsule (radius 0.35 m, height 1.8 m): walk, jog (default), sprint with
  stamina, jump, auto step-up 0.45 m, slopes up to 50°, slide on steeper, fall damage, ledge
  climb up to 2 m with Space.
- F-FOOT-2 Collides with terrain, roads and bridges, buildings, props, trees, parked and moving
  vehicles, and citizens (the reference project's verified collision geometry).
- F-FOOT-3 Swims in CS1 water (sampled water surface), with breath; drowns after it runs out.
- F-FOOT-4 Drawn as a CS1 citizen model with the game's own idle/walk/run/sprint animations;
  outfit = which citizen model; changed in clothes shops and safehouses.
- F-FOOT-5 Cameras: first-person (head height, gentle bob), third-person orbit at three
  distances, collision-aware (never through walls), aim mode over the shoulder.

### F-VEH: vehicles

- F-VEH-1 Enter any road vehicle within 3 m: parked cars, moving traffic (carjack: the driver is
  pulled out and flees or fights), and service vehicles (police, ambulance, fire, garbage,
  hearse, taxi, bus, trucks). Trains, trams, metro, aircraft, ships and bicycles are excluded.
- F-VEH-2 The taken vehicle leaves CS1's simulation and becomes ours: drawn with its own CS1
  model, colour and lights (`Vehicle.RenderInstance`), driven by an arcade rigid-body model with
  raycast suspension, grip, drift under handbrake, reverse, mass from the model's size.
- F-VEH-3 Collides with the world and with other vehicles and peds (traffic gets shoved or stops;
  peds are knocked down). Damage: smoke at 40 %, fire at 15 %, explodes 6 s after catching fire.
- F-VEH-4 Exiting leaves it where it stopped; abandoned vehicles persist until 300 m away and
  unseen, then return to the city as nothing (no CS1 vehicle is recreated).
- F-VEH-5 Horn, siren and light bar on emergency vehicles, headlights, engine and siren sound.
- F-VEH-6 Chase camera, hood/first-person camera from the model's bounds, look-behind.

### F-PED: citizens at GTA level

- F-PED-1 Ambient pedestrians are CS1's own walking citizens; looking at one up close shows a card
  with their CS1 identity (name, age, job, home, happiness).
- F-PED-2 E talks: a short line chosen from their CS1 state (happiness, wealth, health,
  education, whether they have a job, what they are doing) and the player's notoriety.
- F-PED-3 Any interaction beyond talking (bumping hard, threatening, attacking, carjacking,
  near-misses) promotes the citizen to a **ped actor** that we simulate and draw with the same
  model: it can stumble, flee, cower, put its hands up, fight back (some), call the police, be
  knocked down, injured or killed.
- F-PED-4 Consequences go back into the city: injured citizens are made sick so CS1 sends an
  ambulance; killed citizens die through CS1's own death path so a hearse comes; panicked
  citizens head home.
- F-PED-5 Ped actors return to CS1 control when the incident ends and they are out of sight.
- F-PED-6 Citizens are passengers and targets in jobs (taxi fares from CS1 citizens waiting at
  stops, paramedic patients who really are sick in the city).

### F-CMB: combat

- F-CMB-1 Weapons: fists, bat, pistol, SMG, shotgun, carbine. Weapon wheel, ammo, reloads.
- F-CMB-2 Melee with hit reactions; hitscan firearms against world geometry, vehicles and peds;
  recoil and spread; muzzle flashes and tracers; first-person weapon view model.
- F-CMB-3 Health and armour; health regenerates to 50 %; buyable armour and health.
- F-CMB-4 WASTED: respawn at the nearest hospital, lose a cash fee and the current weapon ammo.

### F-HEAT: wanted level and heat (GTA VI style)

- F-HEAT-1 Crimes are events with a severity and a location: theft (parked, occupied), assault,
  hit-and-run, murder, firing a weapon, threatening, trespassing in private interiors, robbery,
  reckless driving, resisting arrest, attacking police, killing police.
- F-HEAT-2 **The police only know what someone reports.** A crime seen by a cop is known at once;
  a crime seen only by citizens becomes known when a witness finishes a call (4-10 s, the
  witness visibly on the phone). Stopping or scaring off a witness before the call lands keeps
  it unreported. Unseen crimes add no wanted level.
- F-HEAT-3 Wanted level 1-5 stars from what is known; it escalates with severity and with
  repeated crimes while wanted.
- F-HEAT-4 Police work from a **last known position** and a **description** (on foot: outfit;
  in a vehicle: model and colour). In sight: pursuit. Out of sight: stars flash, units search a
  zone around the last known position that widens with time and is shown on the minimap. Unseen
  for the search time (longer at more stars) clears the wanted level.
- F-HEAT-5 A cop recognises you only by line of sight plus the description: change car unseen,
  respray it, or change clothes and they must re-identify you; sneaking and staying in cover
  lower detection range.
- F-HEAT-6 **Heat** is the lingering notoriety after the stars go: per player (decays over game
  hours) and per CS1 district (where you did it). Heat raises patrol density nearby, shortens
  response times and makes cops recognise you faster. Shown in the pause menu and as a meter.
- F-HEAT-7 **Smart cops**: units come from the city's police stations (CS1 buildings) along the
  road network; a dispatcher assigns roles: pursue, intercept ahead on your predicted route,
  search a sector of the zone, set a roadblock at junctions ahead (3+ stars), box in a stopped
  car, PIT a fleeing car (3+), go on foot when you are on foot or stopped, and call for more
  units as stars rise. Helicopter with spotlight at 4+ when the city has one (else extra units).
- F-HEAT-8 Arrest: at 1-2 stars cops try to arrest instead of shooting; hands up (X) or being
  grabbed while stopped → BUSTED. At 3+ or when you are armed and hostile, cops shoot.
- F-HEAT-9 BUSTED: respawn outside the police station, lose weapons and a fine. Hiding inside
  an interior unseen breaks line of sight; at 4+ stars cops may come in after you.

### F-INT: enterable buildings

- F-INT-1 Every building with a door is enterable: the door comes from the building's own door
  markers (`BuildingInfo.m_enterDoors`), or the front centre at ground level. A marker and the
  prompt "Press E to enter <building name>" appear within 2 m.
- F-INT-2 Interiors are generated, GTA style: built on demand from the building's ID, service,
  level and footprint, deterministic (same building, same interior), one loaded at a time, in an
  isolated interior space with its own lighting, entered and left through a short fade.
- F-INT-3 Archetypes: house, apartment lobby + one apartment, shop (convenience, clothes, gun
  store, restaurant), office, warehouse/factory, police station (lobby, desk, cells, locker
  room), hospital (reception, ward), fire station (garage, bunks), school, generic service, and
  a fallback. Rooms by subdivision, connected by doors, furnished from a procedural kit (beds,
  sofas, tables, chairs, desks, counters, shelves, racks, lockers, cells, beds, plants, lamps).
- F-INT-4 Occupants come from the city: residents whose CS1 location is home appear inside their
  home, workers at work; shop clerks at counters. Entering a private home is trespassing.
- F-INT-5 Interiors hold the game's services: shops (buy), safehouse (save, outfit, sleep to
  skip time), hospital (heal), police station (pay off heat), respray garage (industrial
  buildings with a garage: clears the vehicle description).
- F-INT-6 Exiting returns you to the street at the door.

### F-JOB: missions and activities

- F-JOB-1 Mission framework: start markers, briefing subtitles, steps (go to, enter vehicle of a
  kind, deliver, chase, lose the cops, defend, collect, timed checkpoint), fail conditions,
  rewards, MISSION PASSED/FAILED.
- F-JOB-2 A short contact campaign (at least 8 missions over three contacts) generated from the
  city's real places (its police station, hospital, industry, commercial district, landmarks),
  so it plays in any city.
- F-JOB-3 Repeatable vehicle side jobs, GTA style, wired into the city: Taxi (CS1 citizens as
  fares), Paramedic (the city's actually sick citizens to the nearest hospital), Firefighter
  (the city's actual burning buildings), Vigilante (in a police car, take down a fleeing
  criminal), Delivery (industry to shops), Street race (checkpoints along roads).
- F-JOB-4 Phone: contacts, job offers, texts; Chirper feed reacts to the player.

### F-ECO: money, property and progression

- F-ECO-1 Cash: earned from jobs, robbery, missions; spent on weapons, ammo, armour, food
  (health), clothes, resprays, fines, hospital fees, property.
- F-ECO-2 Property: buy residential buildings as safehouses (save point, spawn point).
- F-ECO-3 Stats: distance, vehicles stolen, crimes, arrests, deaths, jobs done, money earned.

### F-SAVE: persistence

- F-SAVE-1 The player profile (cash, health, armour, weapons, outfit, properties, mission
  progress, stats, heat, last safehouse) lives in the city's save as versioned mod data with a
  CRC, written whenever CS1 saves; saving from the pause menu triggers the game's own save.
- F-SAVE-2 Settings and key bindings are global (a settings file), not per city.
- F-SAVE-3 Without the mod the city loads normally; the mod data is ignored by CS1.

### F-SET: settings

Mouse sensitivity, invert Y, field of view, default camera views, HUD scale, minimap on/off,
subtitles, simulation rate while playing, police difficulty, audio volumes, key rebinding.
Reachable from CS1's mod options and from the pause menu.

### F-AUD: audio

Procedural or CS1-sourced sounds: footsteps, engine (CS1 vehicle effects where available),
horn, siren, gunshots, impacts, UI blips, phone, mission jingles.

### F-QA: testability

- F-QA-1 Everything that does not need Unity is in `GtaSkylines.Game` and unit-tested on .NET 10.
- F-QA-2 An in-game self-test (debug key and unattended launch) exercises each feature against a
  new city on a built-in map and writes a report, for the owner's playtest runs.
- F-QA-3 Debug overlay: collision, actor states, police roles and search zone, FPS budget.

## Out of scope for v1 (recorded so a reviewer does not count them missing)

Multiplayer; flying aircraft; boats; trains; bicycles as player vehicles; custom voiced
dialogue; custom 3D art assets (we use CS1's models and procedural meshes); CS1 DLC-only
content as a requirement (used when present).
