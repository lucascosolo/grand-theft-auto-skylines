# Decisions

Newest last. Each entry: what was chosen, what was rejected, why, and what would reopen it.

## 2026-10-10: one CS1 mod, no second game (owner)

Everything runs inside Cities: Skylines. The owner's earlier project (`minecraft-skylines`) ran
Minecraft hidden beside CS1 and streamed data over a socket; this project does not. Its CS1
integration layers (`Skylines.Core`, `Skylines.Host`) are lifted with the Minecraft parts removed.

## 2026-10-10: our own physics in pure C#, not Unity PhysX

Chosen: a kinematic capsule controller and an arcade rigid-body vehicle in `GtaSkylines.Game`,
colliding with triangle regions streamed from CS1 (the reference project's collision geometry,
verified in game for terrain, ramps, bridges and building meshes). Rejected: Unity PhysX
(`CharacterController`, `MeshCollider`, `WheelCollider`): nothing in CS1 is known to use it, so
its state in CS1's build is unknown; cooking mesh colliders for streamed regions risks frame
spikes; and none of it can be tested without the game. Reopen if our controller cannot be made to
feel right in the owner's playtest.

## 2026-10-10: the game simulation is headless; the Mod is an adapter

`GtaSkylines.Game` knows nothing of Unity or CS1. It reads the city through `ICity` and asks for
changes through `CityEffect` records. This keeps every rule testable on .NET 10 and keeps CS1
writes in one place (the Mod, on the simulation thread).

## 2026-10-10: generated interiors in an isolated interior space (owner: "like GTA")

Each building gets an interior generated on demand from its ID, service, level and footprint,
deterministic, one at a time, placed at the building's x, z and y = −1500 m behind a fade, with
lighting overridden while inside. Rejected: interiors inside the building's own shell (CS1's
building meshes have no interiors and arbitrary shapes, so walls would clash; hiding one
building's exterior needs Harmony). Reopen if the lighting override cannot be made to look right.

## 2026-10-10: police are witness-driven with lingering heat (owner: "GTA 6 style heat, smart cops")

Crimes are known only when a cop sees them or a witness's call completes; police work from a last
known position and a description; heat persists per player and per CS1 district after the stars
clear and shapes patrol density and recognition. GTA VI was not released at the time of writing;
this follows the owner's description, not a copy of a shipped system.

## 2026-10-10: first person by default, GTA PC controls and HUD (owner)

V cycles views as in GTA V; bindings and HUD layout follow GTA V on PC (`docs/DESIGN.md`).

## 2026-10-10: HUD in Unity IMGUI, not ColossalFramework UI

IMGUI allows free layout, rotation (`GUIUtility.RotateAroundPivot`) and per-frame text without a
component tree, and CS1's own UI is hidden while playing anyway. GL into render textures draws the
minimap. Rejected: ColossalFramework UI (component-heavy, no rotated sprites). Reopen if IMGUI's
per-frame cost shows up in the owner's profile.
