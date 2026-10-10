# Architecture reviews, 2026-10-10 (main at 1c965bb)

Two read-only reviews (architecture-reviewer agents) of the Game and Mod layers, before the M7 wiring
merged. Findings are fixed in the review-fix pass after M7; each line keeps the reviewer's wording.

## Game layer — verdict: FIX BEFORE MERGE (layering holds)

1. FIX — Session/InteriorSystem.cs:328 — property purchase mutates `BuildingDescriptor.OwnedByPlayer` (city data) and ownership has two owners; CrimeSystem.cs:184 checks only the flag, so a safehouse stops speeding heat decay after reload — make `Profile.OwnsProperty` the single source, remove the field from BuildingDescriptor.
2. FIX — Session/VehicleSystem.cs:145-157 — exploding-car occupants killed by writing Health/Dead directly, bypassing Damage (the only KillCitizen emitter) — kill occupants through Damage's death path.
3. FIX — Session/PlayerControlSystem.cs:576 (bail) and :151 (fall) — player damage outside Damage: no LastDamageTime stamp, stale attacker — one Damage entry point for non-combat player damage.
4. FIX — Combat/Weapons.cs:241-247 duplicates GameWorld.DamageCar (GameWorld.cs:584) — call world.DamageCar.
5. FIX — Session/InteriorSystem.cs:333-348 + Profile/Shop.cs:214-217 — health/armour stored twice (profile int, ped float) and synced by hand — Shop acts on the live Ped; copy to the profile only on save.
6. FOLLOW-UP — Crime/CrimeSystem.cs:78,169 vs Police/PoliceSystem.cs:188,261,318 and Police/CopBrain.cs:183 — wanted episode has two owners — move the episode onto WantedStatus.
7. FOLLOW-UP — Missions/MissionContext.cs:336-350 — DirectWantedControl bypasses CrimeSystem (episode, heat, description) — CrimeSystem-backed IWantedControl.
8. FOLLOW-UP — Session/RespawnSystem.cs:74-86, Police/PoliceSpawner.cs:95, Missions/CampaignPlaces.cs:153-173,346 — three "nearest service door" searches with different filters — one helper over ICity.
9. FOLLOW-UP — FNV-1a implemented three times (InteriorGenerator.cs:194, InteriorLayout.cs:136, GameSession.cs:379) — one Mathx hash.
10. DONE (f638103) — Interiors sort ties could differ between Mono and .NET.
11. FOLLOW-UP — Missions/SideJobs.cs:157,169 — PromoteFare allocates a list and closure per call and sorts by distance without a tie-break; duplicates Chirper.NearestCitizen — reuse a member list, a non-capturing comparer, tie-break by id.

## Mod layer — verdict: FIX BEFORE MERGE (structure and layering sound)

1. FIX — Lifecycle/SimRate.cs:56-62 — writes SimulationManager m_dayTimeOffsetFrames / m_timeOffsetTicks (64-bit, non-atomic on Mono) from the main thread every frame — queue via SimulationManager.AddAction.
2. FIX — Lifecycle/PlayController.cs:497-506 — ForcedSimulationPaused set on the main thread (as CS1's own PauseMenu does) — route via AddAction or record the exception in CS1-API-NOTES and AGENTS.md.
3. FIX — Rendering/InteriorRenderer.cs:93-101 — Texture2D created with mipmaps then LoadRawTextureData with level 0 only (Unity 5.6 expects the full chain) — SetPixels32 + Apply(true, true), or no mips.
4. FIX — Rendering/InteriorRenderer.cs:103-105 — null material from MakeMaterial reaches Graphics.DrawMesh every frame — skip the part or fall back to the unlit material.
5. FIX — Audio/AmbienceDucker.cs:286 — Set(false) returns early without clearing _active; mute only released in AudioPlayer.OnDestroy — clear unconditionally; Dispose called by every exit path.
6. FIX — Audio/AudioPlayer.cs:292-296 + ClipBank.cs:72 — AudioClips never destroyed: the bank leaks per city load — destroy in OnDestroy.
7. FIX — Rendering/Renderer.cs:124-128 — after 5 faults rendering is silently skipped; play continues invisible — after N faults signal the session to exit and restore.
8. FIX — City/SnapshotBuilder.cs:61-170, Rendering/VehicleMath.cs:35 — ~13 CS1 members missing from docs/CS1-API-NOTES.md — add per-member rows with file:line.
9. FOLLOW-UP — SnapshotBuilder duplicates Skylines.Host MovingObjects' grid walk — reuse one walker or mark Host's reference-only.
10. FOLLOW-UP — three owners build Hidden/Internal-Colored materials (CollisionViewer.cs:96, HudStyle.cs:103, MaterialKit.cs:149) — share MaterialKit's.
11. FOLLOW-UP — Rendering/RenderHook.cs:19 static Target outlives level loads — null it in Loading.OnLevelUnloading.
12. FOLLOW-UP — Rendering/IndoorLighting.cs:181 relies on script order to capture originals — document the assumption.
13. FOLLOW-UP — Hud/MinimapRenderer.cs:27-31 recreates the RenderTexture on every size change (animated enlarge) — size to the max, draw a sub-viewport.
14. FOLLOW-UP — Lifecycle/PlayController.cs:352-356 serialises the profile every 5 s — snapshot at CS1 save time (M7).
