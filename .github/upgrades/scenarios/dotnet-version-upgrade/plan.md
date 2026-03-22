# .NET Version Upgrade Plan

## Overview

**Target**: Upgrade SpireArena from net9.0 to net10.0 (LTS)
**Scope**: 1 project, ~1.5k LOC — straightforward upgrade

### Selected Strategy
**All-At-Once** — Single project upgraded in one operation.
**Rationale**: 1 project, already on net9.0, all packages compatible, only 3 minor code issues.

## Tasks

### 01-upgrade-spirearena: Upgrade SpireArena to .NET 10

Update the SpireArena project target framework from net9.0 to net10.0, address all API compatibility issues, and validate the build.

**Scope:**
- Update `TargetFramework` in SpireArena.csproj from `net9.0` to `net10.0`
- Fix `Path.Combine` source incompatibility in Services/CardDatabase.cs (line 25) — new `ReadOnlySpan<string>` overload may cause ambiguity
- Review `JsonDocument.Parse` behavioral change in Services/CardDatabase.cs (line 50)
- Build solution and fix any compilation errors

**Done when**: SpireArena.csproj targets net10.0, all code issues resolved, solution builds with 0 errors.
