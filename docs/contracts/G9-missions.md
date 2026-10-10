# G9 contract: missions, side jobs, phone, Chirper, dialogue

Owns `src/GtaSkylines.Game/Missions/`, `src/GtaSkylines.Game/Phone/`,
`src/GtaSkylines.Game/Dialogue/` and their test folders. One `IGameSystem` (Missions, order 80)
on G6's world, writing `MissionStatus`, `HudMessages`, blips and the GPS target; reading
`GameEvents`, `WantedStatus`, actors and `ICity`.

## Framework (F-JOB-1)

- `MissionDefinition`: id, title, contact, unlock condition, reward (cash, profile flags),
  a list of `MissionStep`s; built by code from templates and the city (no data files).
- Steps (each with objective text using highlight spans, blips and a GPS target): GoTo
  (position, radius, on foot or in any/specific vehicle), EnterVehicle (a mission car or any car
  of a kind), StealVehicle (kind or specific spawned car), Deliver (bring the current car or a
  passenger to a position, optionally undamaged above a health), Chase (a spawned target car
  driven by a fleeing AI route; succeed when it is destroyed or stopped and the driver
  knocked down/arrested), Eliminate (spawned hostile peds), LoseWanted (stars 0), Collect (an
  interaction inside an interior), Timed (wraps any step with a limit shown on the HUD), Wait,
  Talk (subtitle lines), Respray (use a respray spot), BuyItem (a shop purchase).
- Fail conditions per mission: player wasted or busted, mission vehicle destroyed, target
  escaped (> 400 m), time out, passenger killed. MISSION PASSED (reward added via Wallet with
  a reason) / MISSION FAILED (reason) through `HudMessages` big screens; retry from the
  mission marker.
- Mission markers: start points at contact buildings shown as blips and as in-world marker
  requests (yellow cylinder) for the Mod; walking into one starts the mission (one active
  mission at a time; side jobs pause while a mission runs).
- Spawned mission actors are created through G6's registries and removed on end.

## Campaign (F-JOB-2): nine missions, three contacts, generated from the city

Places are chosen from `ICity.ServiceBuildings` and districts with fallbacks when a service is
missing (a Garage interior → any Industrial building; police station → the city's most central
building; hospital → any Health building, else a Commercial one; city hall → any Government or
Monument building, else the biggest Office building). Each mission text names the real
building, street and district.

1. **Vince** (a street hustler; contact building: an industrial garage)
   - V1 "Fresh Start": walk to the nearest convenience store, buy a snack; steal any parked car;
     drive it to Vince's garage. Tutorial texts for the controls.
   - V2 "Hot Wheels": steal a specific spawned sports-coloured car parked in another district;
     a witness is guaranteed (1★ after the call); bring it to the garage with health ≥ 60.
   - V3 "Clean Getaway": a 2★ wanted level is set; lose it, respray at a garage, return.
2. **Sergeant Reyes** (a police sergeant; contact: the police station)
   - R1 "Ride Along": take a police car from the station; drive to the hottest district; chase and
     stop a fleeing suspect car (Chase step).
   - R2 "Evidence Locker": inside the police station, collect evidence from the locker room
     (Collect at a Locker interaction the mission adds), deliver to a warehouse without
     getting a wanted level (sneak: the station's cops recognise the player at 2★ if seen
     taking it).
   - R3 "Takedown": three armed hostiles at an industrial building; eliminate them; escape.
3. **The Mayor's Office** (contact: city hall) — missions that help the city
   - C1 "Code Red": drive an ambulance with one of the city's really sick citizens (`ICity.
     Patients`) to the nearest hospital within a time limit (if nobody is sick, a fallback
     patient is spawned at a random residential door).
   - C2 "Five-Alarm": take a fire engine to a burning building (`ICity.BurningBuildings`; if
     none, the nearest building to a random point is flagged as a "reported fire" and the
     step is to inspect it) and hose it (stand within 15 m holding Attack in the fire engine
     for 12 s: DouseFire effects of 0.1/s).
   - C3 "Gridlock": collect the Mayor's aide (a spawned ped) at city hall, then a 4★ chase
     erupts; deliver the aide across the city to a destination 1.5-3 km away. Finale reward.
Unlocks: V1 first; R1 after V2; C1 after R1; each chain in order; finale C3 needs V3 and R3.

## Side jobs (F-JOB-3)

Started by pressing the bound key (Interact held 1 s) in a suitable vehicle, levels 1-10 with
growing pay and difficulty, ended by leaving the vehicle or failing:
- **Taxi** (in a taxi): fares are ambient CS1 citizens near the road (promoted, walk to the car,
  hidden while riding), destinations are real buildings 300-1500 m away; tip by time.
- **Paramedic** (in an ambulance): `ICity.Patients` first, else spawned; deliver to the nearest
  Health building; time limit by distance; patient count grows by level.
- **Firefighter** (in a fire engine): `ICity.BurningBuildings`, else a "reported fire" as in C2;
  hose for 12 s; DouseFire effects.
- **Vigilante** (in a police car): a criminal car flees from a point 300 m away (Chase); level
  adds a second car or an armed passenger.
- **Delivery** (in a van or truck): pick up at an industrial building, deliver to a commercial
  building; damage reduces pay.
- **Street race**: from a race marker (blip near the city's longest road); 6-12 checkpoints
  along road routes (`RoadGraph.FindRoute` chained through random targets), three rival
  `Car`s driven by `RouteFollower` at 1.1 × the limit; position on the HUD.

## Phone and Chirper (F-JOB-4)

- `Phone`: contacts (unlocked as met), texts (mission unlocked, payment received, job offers),
  call a contact to get the next mission's location, a "Jobs" list showing the side jobs and
  levels, and quick GPS to the nearest shop, garage, hospital, safehouse. A `PhoneModel` for
  the HUD.
- `Chirper`: posts (`CityEffect.Chirp` with an ambient citizen as sender) reacting to known
  crimes, explosions, police chases at 3★+, mission passes, side-job milestones; templates
  with street, district and city names; at most one post per 45 s; never during interiors.

## Dialogue (F-PED-1, F-PED-2)

- `PedCard` text from `CitizenProfile` (name, age, "Works at X" / "Student at X" / "Retired" /
  "Unemployed" / "Tourist", "Lives at X", happiness word).
- `DialogueLines`: greeting lines chosen deterministically from (citizen id, context) among
  ≥ 120 authored lines grouped by happiness band, wealth, education, age group, tourist,
  employment, health/sick, and the player's notoriety (heat ≥ 40: they recognise you), plus
  reaction lines (bumped, carjacked, robbed, witness calling, cop orders). Hooks used by G6
  (talk) and G7 (stumble, fight, call).

## Tests (test-author)

Every step type completes and fails in a `FakeCity` session; mission passed adds the reward
once; failure reasons; markers start missions; one mission at a time; campaign generation for a
full city and for a city with no police station, hospital, city hall or industry (all nine
missions still valid with fallbacks, texts contain the chosen names); unlock order; each side
job starts only in the right vehicle, completes one round and pays, level increases; taxi
fare promotion emits TakeCitizen; paramedic uses FakeCity patients first; firefighter emits
DouseFire; race checkpoints follow the road graph; Chirper rate limit; dialogue picks differ by
profile and are stable for the same citizen and context; phone texts on unlock.
