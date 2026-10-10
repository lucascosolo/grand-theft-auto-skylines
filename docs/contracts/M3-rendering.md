# M3 contract: rendering

Owns `src/GtaSkylines.Mod/Rendering/` (net35, CS1 + Unity). Draws everything the Game owns, using
CS1's own models and shaders wherever possible. Every CS1/Unity member is read in the decompile
first and logged in `docs/CS1-API-NOTES.md`. No per-frame allocation in steady state.

## Actors

- Player and ped actors as CS1 citizens: the citizen's `CitizenInfo` prefab instance from CS1's
  pool (`PrefabInfo.ObtainPrefabInstance<CitizenInfo>(InstanceID, 255)`; research the pool
  (`PrefabInstancePool`) to pick `InstanceID`s that never collide with live CS1 citizens and to
  release ours on despawn), posed each frame with `CitizenInfo.SetRenderParameters(position,
  rotation, velocity, colour, AnimationState, underground: false)` mapping the Game's `PedAnim`
  to CS1's animation states (Default with speed for idle/walk/run/sprint; Panic; Sitting; others
  approximated: lying = the model rotated onto its back, hands-up/phone = Default at speed 0).
  The player is hidden in first person (but drawn in mirrors-free CS1, so simply not drawn) and
  while in a vehicle. Interior occupants are drawn only while the player is inside.
- Interpolation between fixed steps using the session's alpha.

## Vehicles

- Game cars through the static `Vehicle.RenderInstance(cameraInfo, VehicleInfo, position,
  rotation, swayPosition, lightState, tyrePosition, velocity, acceleration, colour, flags,
  flags2, variationMask, InstanceID, underground: false, overground: true)` called from a
  render hook inside CS1's camera rendering (research how CS1 calls its managers' render
  methods, e.g. `RenderManager` and `IRenderableManager`/`EndRenderingImpl`, and the
  reference's `UndergroundVehicles`/`TunnelInteriorRenderer`), with: position converted from the
  body's bounds centre to CS1's vehicle origin; sway from suspension; light state (headlights
  at night or when toggled, brake lights, siren light bar for emergency vehicles via the flags
  CS1 uses for an emergency); tyre position from the wheel spin/steer; the body's colour.
  Damage: smoke and fire effects (CS1 effect infos if usable, else our own billboards),
  exploded cars drawn blackened (colour) and a fire/smoke column for 20 s.
- Ambient CS1 vehicles and citizens keep being drawn by CS1. Taken ones disappear from CS1
  when the effect applier releases them, so the frame they are taken must also be the first
  frame we draw our copy (no flicker): coordinate with the session's promotion timing.

## Interiors

- Build Unity `Mesh`es from the layout's `MeshPart`s (16-bit indices) and one material per
  `TextureRecipe`, with a 256 × 256 texture painted procedurally per pattern (plain, planks,
  tiles, carpet, bricks, concrete, stripes, checker, brushed, window sky, light panel), mipmaps
  and point-free bilinear filtering, using the game's `Custom/Props/Prop/Default` shader
  (`_MainTex`, `_Color`, plus `_XYSMap`/`_ACIMap` neutral textures as the reference's block
  renderer does); emissive recipes use the shader's illumination channel if it works, else an
  unlit fallback (`Hidden/Internal-Colored` with the texture's average colour).
- Drawn with `Graphics.DrawMesh` at the interior's world transform (building x, z, y = −1500,
  yaw) on the layer CS1 uses for props, no shadows cast or received. Destroyed on exit.
- While inside: override CS1's sun/ambient/fog so the interior reads as lit indoors: neutral
  light from above, ambient raised, fog and other outdoor post effects (`FogEffect` and
  friends) disabled; everything restored exactly on exit or mode end (save the original values
  once, as the reference's `FrameOverride` pattern does). Research `DayNightProperties`,
  `RenderManager`, the main light and CS1's fog component in the decompile.
- Doors outside: a subtle in-world marker at building doors within 20 m of the player when
  enterable (a small arrow quad), and the mission/job markers (yellow translucent cylinders,
  blue for side jobs, red for hostile targets) and checkpoint rings (races) with
  `Hidden/Internal-Colored` (alpha blended, unlit).

## Effects

Tracers (thin bright quads from `Shot` events, 60 ms), muzzle flashes (a billboard at the gun
for one frame plus a short CS1 point light if `LightSystem.DrawLight` is usable), bullet
impacts (small dust puffs), blood-free hit reactions (the game stays non-gory), explosion
flash and smoke billboards, the first-person weapon view model (procedural low-poly meshes for
bat, pistol, SMG, shotgun, carbine with simple recoil and reload animation) and a fists pose
(no hands drawn). Siren lights on emergency cars come from CS1's own vehicle rendering.

## Camera

Apply the Game's `CameraRig` output to CS1's main camera through the Host `CameraTakeover`
(position, rotation, FOV, near clip from the reference's clip preset 1), every frame after the
session's LateTick.

## Tests

Pure parts (texture painters returning colour arrays, mesh conversion from MeshPart, the
PedAnim → AnimationState table, body-centre → CS1 origin conversion, tyre/sway maths) get
net10 tests in `tests/GtaSkylines.Mod.Tests`. The rest is compile-verified and covered by the
in-game self-test.
