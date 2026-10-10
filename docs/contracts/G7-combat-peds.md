# G7 contract: combat and pedestrian AI

Owns `src/GtaSkylines.Game/Combat/`, `src/GtaSkylines.Game/Peds/` and their test folders.
Implements DESIGN.md F-CMB and the citizen side of F-PED as `IGameSystem`s (Peds, order 30;
Combat, order 50) on G6's world. Weapons are the `WeaponId`s of `Profile` (Fists, Bat, Pistol,
Smg, Shotgun, Carbine); ammo lives in `PlayerProfile`.

## Weapons and firing

- `WeaponTable` (real values): Fists melee 8 dmg, reach 1.2 m, 2.2 swings/s; Bat melee 26,
  reach 1.7, 1.4/s; Pistol 26 dmg, range 70 m, 3.5 shots/s, magazine 12, reload 1.4 s, spread
  1.2°; Smg 14, 45 m, 12/s, 30, 2.0 s, 3.0°; Shotgun 8 pellets × 11, 22 m, 1.1/s, 8, 2.8 s,
  6°; Carbine 30, 100 m, 8/s, 30, 2.2 s, 1.0°. Spread doubles while moving, halves while
  aiming still. Headshots (hit above 1.5 m on a standing ped) × 2.5.
- Player: Attack fires/swings the current weapon; Aim (hold) enters aim mode (G6 camera) with
  soft lock-on to the ped nearest the crosshair within 6° and 40 m that is hostile or the
  current target; NextWeapon/PrevWeapon and the weapon wheel (`WeaponWheelModel` for the HUD:
  owned weapons with ammo, selection by pointer direction) switch; Reload reloads; empty
  magazine auto-reloads; weapons only in owned slots (fists always).
- `Combat.Fire(Ped shooter, Vec3 origin, Vec3 direction)` (also used by G8 cops): hitscan with
  `CollisionWorld.Raycast` ignoring the shooter's tag; resolves what was hit from the tag
  encoding G6 defines: Game ped → damage; Game car → car damage (tyres are not modelled);
  ambient CS1 citizen capsule → promote (G6 `PromoteCitizen`) then damage; ambient CS1 vehicle
  box → promote car then damage; static → impact. Emits `Shot` (origin, end point) for
  tracers and muzzle flashes and `Gunshot` noise events.
- Melee: arc 70° in front within reach against peds and ambient citizens (promoted on hit);
  knock-down chance by weapon (fists 25 %, bat 70 %); emits `Punched`.
- Damage: armour absorbs 66 % of damage until it runs out; the player regenerates health to
  50 after 5 s without damage at 2/s; fall damage from G6 events; vehicle impacts on peds from
  G5 impact events (tag is a ped): relative speed > 4 m/s knocks down, damage (v − 4) × 7, > 14
  m/s kills; explosions (car Exploded) damage within 8 m (falloff) and knock down within 12 m.
- Consequences for CS1 citizens (promoted peds with `Citizen` ≠ 0): death → `KillCitizen`
  effect (building 0 = their home) once; injury (health < 35 after a hit, not dead) →
  `InjureCitizen` effect once; events PedInjured/PedKilled/PedKnockedDown.
- Wasted: player health 0 → G6 death flow; current weapon's ammo is lost (profile).

## Pedestrian brains (`IPedBrain` implementations)

Steering outdoors: the ped's CharacterMotor with a desired velocity from a simple local
planner (seek/flee plus obstacle avoidance by short capsule casts against the CollisionWorld,
picking among 8 directions); indoors: `InteriorNavGrid.FindPath`.

- `IdleBrain` (interior occupants and calm promoted peds): stand, small turns, sit on their
  seated NpcSpot.
- `StumbleBrain` (bumped while walking): stagger 0.6 s, a complaint subtitle from G9's dialogue
  hook ("Watch it!"), then `ReturnBrain`.
- `FleeBrain`: run away from the threat position at 5-6 m/s for 6-15 s, panicking animation;
  from vehicles approaching fast, sidestep.
- `CowerBrain`: crouch in place (adults near walls, seniors) for 6-12 s, then flee.
- `HandsUpBrain`: when the player aims at them within 15 m: hands up while aimed at, flee
  when the aim leaves; shop clerks open the till (G9/G6 robbery hook: event Robbery).
- `FightBrain`: some adults (seeded 15 %, more for young males models) answer a punch with
  fists; stop when hurt below 40 or the player draws a gun.
- `CallPolice` (requested by G8 for witnesses): stop, phone out (OnPhone flag, Phone anim)
  4-10 s, then event `CallCompleted` with the crime id; threatened (aimed at, punched, shot
  at within 20 m, car driven at them) → `CallInterrupted` and flee.
- `KnockedDownBrain`: lie 2-4 s (6 s if injured), then get up and flee; dead peds stay down.
- `ReturnBrain`: walk away from the player calmly; when out of sight G6 demotes them.
- Carjacked drivers: 70 % flee, 30 % fight (seeded); drivers of police cars are cops (G8).
- Perception (`Perception`): `CanSee(ped, point)` (120° cone, range by day/night, line of
  sight raycast), `Hears(ped, event)` (gunshot 60 m, explosion 100 m), and
  `WitnessesOf(GameEvent e, List<WitnessCandidate>)` over Game peds and ambient snapshot
  citizens (ambient ones are only promoted when chosen as callers).
- Reactions to the player: aiming a weapon at a ped within 25 m → HandsUp or Flee; gunshot
  heard → nearby ambient citizens (within 30 m) are made to panic via `PanicCitizen` effects
  (CS1 handles their running) and promoted peds flee; driving on the pavement fast → nearby
  peds sidestep or flee.

## Tests (test-author)

Damage maths (armour split, headshot multiplier, regeneration to 50 after 5 s), each weapon's
rate, magazine, reload time and spread bounds; hitscan blocked by a wall; ped hit by a ray dies
at the right number of shots; an ambient citizen capsule hit promotes it and emits KillCitizen
when killed (once); melee arc hits in front and misses behind; vehicle impact speeds map to
knock-down, injury (InjureCitizen once) and death; explosion radius; each brain's transitions
from its stimulus (bump → stumble → return; aim → hands up → flee when aim leaves; punch → fight
or flee by seed; call police completes after 4-10 s and is interrupted by a threat);
perception cone, range day vs night, occlusion; WitnessesOf returns only peds that could see
the event; flee moves away and avoids a wall; determinism for a seed; no allocation per step
with 30 idle peds.
