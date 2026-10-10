# G8 contract: crime, wanted level, heat and smart police

Owns `src/GtaSkylines.Game/Crime/`, `src/GtaSkylines.Game/Police/` and their test folders.
Implements DESIGN.md F-HEAT-1..9 as two `IGameSystem`s (Crime, order 60; Police, order 70) on
the world from G6 (`GameWorld`, `WantedStatus`, `GameEvents`, `Ped`, `Car`, `IDriver`), using G7's
perception and ped brains (`Perception.WitnessesOf`, `CallPolice` brain, `Combat.Fire`), G5's
`VehicleBody`, and G2's `RoadGraph` (`FindRoute`, `Predict`, `FindJunctionsAhead`, `RouteFollower`).
Heat scale is the project convention: 0-100, 20 per star-equivalent (`PlayerProfile.HeatPerStar`).

## Crimes and knowledge

- A `CrimeClassifier` turns `GameEvent`s caused by the player into `Crime`s {Type, Severity
  1-5, Position, Time, Victim, Vehicle}: CarStolen (parked) 1, CarJacked 2, Punched/Shove of a
  civilian 1, PedKnockedDown by the player's car 1 (hit-and-run 2 if injured), PedInjured 2,
  PedKilled 3, Gunshot (firing in public) 1, Shot at a ped 2, Explosion 3, Trespass 1 (private
  interiors), Robbery 3 (aiming at a shop clerk), RecklessDriving 0.5 (accumulates: three within
  20 s = 1), assault on a cop 3, killing a cop 4, resisting arrest 2 (fleeing a cop's arrest
  attempt), CarStolen of a police vehicle 3.
- **Only witnessed crimes count.** A crime seen by a cop (cop ped or police car occupant with
  line of sight within 60 m day / 35 m night, or within 20 m regardless if heard: gunshots,
  explosions) is known at once. Otherwise G7's perception lists civilian witnesses (sight
  within 40 m day / 25 m night, sneaking halves the range, interiors use the room's walls);
  up to two witnesses start a `CallPolice` (4-10 s, seeded). When a call completes the crime
  becomes known with the witness's position, the player's position at the moment the witness
  last saw them, and the description at that moment. Interrupting the call (threatening,
  hitting, killing the caller, or the caller losing the player and being > 60 m away) loses it.
  Crimes nobody witnessed add nothing.
- Known crimes raise `Wanted.Stars`: star = max(current, ceil(severity)), plus one star when
  a further known crime happens while already wanted and the accumulated severity since the
  last star exceeds 3 (cap 5). Killing a cop jumps to at least 3; 2 cop kills to 4.
- Every known crime adds heat: player heat += 6 × severity (cap 100) and the CS1 district of
  the crime += 10 × severity (cap 100). Player heat decays 1 per real minute while not wanted
  (faster, 3/min, when inside an owned safehouse); district heat decays 2 per real minute.
  Paying off at a police desk (G6 menu) sets player heat to 0 and the price comes from `Shop`.

## Pursuit, search and evasion

- `Wanted.LastKnownPosition` updates whenever any cop sees the player. `Evading` is true when
  no cop has seen the player for 2 s; the HUD flashes the stars. The search zone (centre =
  last known position, radius 40 m + 6 m/s × time since last seen, cap 400 m) is published for
  the minimap. Unseen for the search time (1★ 20 s, 2★ 30 s, 3★ 45 s, 4★ 60 s, 5★ 90 s; ×1.5
  when district heat > 60) clears the stars; heat stays.
- **Recognition** (`Recognition.Sees(cop, player)`): line of sight (CollisionWorld raycast from
  eye height), within view range (cops: 120° cone, 60 m day,
  35 m night, ×0.6 when the player sneaks on foot, ×1.3 for a helicopter's spotlight cone),
  AND either (a) the player matches the description (same outfit on foot; same car model and
  colour in a car), or (b) the player is within 12 m, or (c) the player commits a crime in
  sight, or (d) the player is in the search zone and the cop's suspicion meter fills (2 s of
  continuous sight at < 25 m, 4 s at < 45 m; faster with high player heat). Changing the car
  unseen, resprays (G6 menu → `Description` updated) and changing outfit therefore force the
  police to re-identify the player.
- While wanted and the player is inside an interior: the search zone centres on the building
  door if the player was last seen entering; at 4★+ cops enter interiors (they spawn at the
  entrance spawn of the interior after 20-40 s and search room by room on the nav grid).

## Dispatcher and smart cops

- Unit budget by stars: 1★ 1 car (2 cops), 2★ 3 cars, 3★ 4 cars + 1 roadblock, 4★ 6 cars + 2
  roadblocks + helicopter if `ICity.FindVehicleModel(VehicleKind.PoliceHelicopter, seed)` finds a model
  (else +2 cars; the helicopter is a simple kinematic flyer, not a G5 body), 5★ 8 cars + 3 roadblocks, aggressive. Not wanted: patrol cars proportional to the
  district heat at the player's position (0 at < 20 heat, up to 3 at 100) that wander
  (`FindWanderRoute`) and can recognise a player with high heat. Police difficulty setting
  (easy/normal/hard) scales budgets 0.6/1/1.4 and reaction times.
- Spawning: cars appear at the nearest CS1 police station door (`ICity.ServiceBuildings
  (Police)`) when it is within 1.5 km, otherwise at a road point 250-400 m from the player out
  of the player's view (`RoadGraph.TryNearestPoint` on random bearings, rejected if the camera
  could see it). Response delay 6-20 s scaled down by heat. Cars use the city's police car
  model (`FindVehicleModel(Police)`), siren on.
- Roles assigned every second by a `Dispatcher` (deterministic for a seed): **Pursue** (the
  nearest unit that can see the player: drive straight at them; on foot, get out and chase),
  **Intercept** (drive to `RoadGraph.Predict(player, 6-10 s)` ahead of the player's car),
  **Search** (one sector of the search zone each, wander routes inside it, slower, sirens off
  at 1-2★), **Roadblock** (3★+: two cars parked across a junction from `FindJunctionsAhead`
  200-500 m ahead on the player's predicted route, with cops beside them; static obstacles
  the player must ram or avoid), **BoxIn** (when the player's car is slower than 3 m/s for 2 s:
  up to 3 cars park against it front, back and side), **PIT** (3★+ and the player's car is fast
  and within 15 m behind-left/right: the cop steers into the rear quarter), **OnFoot** (when
  the player is on foot or stopped: cops leave the car within 25 m and run using motors and
  G7 steering), **Helicopter** (4★+: hovers 40 m up, follows the last known position, its
  spotlight extends recognition). Roles never all go to Pursue: at least one Intercept or
  Search per 3 units when available.
- Cop behaviour on foot (a G8 `CopBrain` using G7 actions): 1-2★ and the player is unarmed or
  not hostile → try to arrest: approach, order hands up (subtitle "Police! Hands up!"); the
  player holding HandsUp (X) or standing still within 2 m for 2 s while a cop is adjacent →
  BUSTED; fleeing during an arrest → resisting (crime). 3★+, or the player armed and aiming or
  firing, or a cop was attacked → cops shoot (`Combat.Fire` with pistol, accuracy falling with
  distance and the player's speed) and cars ram. Cops take cover behind their cars (stand on
  the far side) when shot at.
- BUSTED request → G6 flow; WASTED clears pursuit; despawn units > 400 m away and unseen;
  units that lose the player return to Search, then leave (drive away) when the stars clear.
- Police radio chatter as subtitle lines on the HUD ("Suspect last seen heading north on
  <street> in a red <car>") using `ICity.StreetNameNear` and the description — GTA flavour
  that also tells the player what the police know.

## Tests (test-author)

Scenario tests in a `FakeCity` with a small road graph and a police station building:
unwitnessed crime → no stars, no heat; witnessed crime → stars only after the call completes
(timing within the 4-10 s window); interrupting the call (threaten/kill the witness) → none;
cop sees crime → instant stars; stars escalate by the rules and cap at 5; heat added to player
and district and decays at the stated rates; pay-off resets heat; losing line of sight →
Evading after 2 s, search zone growing at 6 m/s, stars cleared after the per-star time;
outfit change unseen → a cop at 30 m does not recognise (outside 12 m), recognises at 10 m;
car change unseen likewise; dispatcher budgets per star and difficulty; units spawn at the
police station when within 1.5 km else 250-400 m away on a road and out of view; roles: with
4 units at 3★ at least one Intercept/Search and one Roadblock placed at a junction ahead on the
predicted route; BoxIn when the player's car is stopped; arrest at 1★ when the player holds
hands up next to a cop → Busted request; cops shoot at 3★ (Combat.Fire called) and not at 1★
when the player is unarmed; patrols appear only with district heat ≥ 20; determinism for a
seed.
