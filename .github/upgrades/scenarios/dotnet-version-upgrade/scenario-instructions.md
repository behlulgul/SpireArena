# Scenario Instructions — .NET Version Upgrade

## Scenario Parameters
- **Solution**: SpireArena.sln
- **Target Framework**: net10.0 (.NET 10.0 LTS)
- **Source Framework**: net9.0
- **Source Control**: None (no git repo)

## Preferences

### Flow Mode
**Automatic** — Run end-to-end, only pause when blocked or needing user input.

### Technical Preferences
*(none yet)*

### Execution Style
- User requested: upgrade + fix all possible errors

### Custom Instructions
*(none yet)*

## Strategy
**Selected**: All-at-Once
**Rationale**: Single project, already on net9.0, all packages compatible, low complexity

### Execution Constraints
- Single atomic upgrade — TFM + packages + code fixes in one pass
- Validate full solution build after upgrade
- No tier ordering needed (single project)

## Key Decisions Log
- All-at-Once strategy auto-selected (single project, low complexity)
