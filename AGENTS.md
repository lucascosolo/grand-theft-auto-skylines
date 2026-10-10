# GTA: Skylines — agent instructions

A Cities: Skylines (CS1) mod that turns the city into a GTA-style open world. Everything runs in
CS1. Read `docs/DESIGN.md` (the feature contract), `docs/ARCHITECTURE.md` (layers, ownership,
frame loop), `docs/PLAN.md` (chunks and their contracts), `docs/DECISIONS.md`, `docs/STATUS.md`.

`minecraft-skylines/` is a read-only reference copy of the owner's earlier CS1 project (its own
git repository, ignored here). Its `docs/CS1-API-NOTES.md` records CS1 APIs it verified; reuse
that knowledge, never edit that folder.

## Hard rules (owner)

- Never write a script, build step or test that deletes files; never run a build tool's clean
  task (`Directory.Build.targets` disables MSBuild's `IncrementalClean`).
- Never write into the game installation, the owner's real saves or settings. Tests use
  temporary directories only.
- No proprietary game files and no credentials in Git. CS1 assemblies are referenced from
  `~/.cache/gta-skylines/refs/cs1/Managed/`.
- No spending, publishing or releases without the owner's explicit yes.

## Layering

`Skylines.Core` (BCL) < `Skylines.Host` (CS1, no GTA knowledge) < `GtaSkylines.Game` (BCL only,
**no Unity, no CS1**) < `GtaSkylines.Mod` (CS1 + Unity). Nothing references upward. The Game sees
the city only through `ICity` and changes it only through `CityEffect` records.

## Code rules

- CS1-loaded assemblies target `net35` (Unity 5.6 Mono): no `Task`/`async`, no `ValueTuple`, no
  `Span`, no default interface methods, no APIs newer than .NET 3.5. Pure libraries also target
  `net10.0` for tests. `TreatWarningsAsErrors` is on.
- Per-frame code allocates nothing in steady state (no LINQ, no closures, no boxing in hot paths).
- Every CS1 API used is first read in the decompile (`~/.cache/minecraft-skylines/decompile/`,
  one `.cs` per type) and recorded in `docs/CS1-API-NOTES.md` with the file:line read. Never
  write a CS1 member from memory.
- Every mod callback (`OnLevelLoaded`, `Update`, `OnGUI`, serialization) is exception-guarded and
  logs through `Skylines.Host.HostLog`.
- Writes into CS1 state happen on the simulation thread via `SimulationManager.AddAction`.
- Follow the file you are editing; XML doc comments on public members.

## Verification

- `tools/check.sh` builds everything (0 warnings) and runs all unit tests; it is green before any
  commit. Test reports: `reports/`.
- Status per feature in `docs/STATUS.md` uses four separate columns: implemented / built /
  sandbox-tested / verified in game. A sandbox test is never evidence that CS1 integration works.

## Toolchain

- `tools/dotnet.sh` wraps .NET SDK 10.0.401 at `~/.cache/dotnet/sdk10/dotnet` with caches under
  `~/.cache`. Build: `tools/dotnet.sh build GtaSkylines.sln -c Release -m:1`.
- NuGet is offline (the sandbox blocks api.nuget.org); packages come from
  `~/.cache/minecraft-skylines/nuget`. Use only packages already there (xunit 2.9.3,
  Microsoft.NET.Test.Sdk 18.10.1, xunit.runner.visualstudio 4.0.0,
  Microsoft.NETFramework.ReferenceAssemblies 1.0.3).
- Python 3 standard library for tools.
