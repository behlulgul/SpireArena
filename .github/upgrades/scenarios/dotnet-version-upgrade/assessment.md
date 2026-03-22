# Projects and dependencies analysis

This document provides a comprehensive overview of the projects and their dependencies in the context of upgrading to .NETCoreApp,Version=v10.0.

## Table of Contents

- [Executive Summary](#executive-Summary)
  - [Highlevel Metrics](#highlevel-metrics)
  - [Projects Compatibility](#projects-compatibility)
  - [Package Compatibility](#package-compatibility)
  - [API Compatibility](#api-compatibility)
- [Aggregate NuGet packages details](#aggregate-nuget-packages-details)
- [Top API Migration Challenges](#top-api-migration-challenges)
  - [Technologies and Features](#technologies-and-features)
  - [Most Frequent API Issues](#most-frequent-api-issues)
- [Projects Relationship Graph](#projects-relationship-graph)
- [Project Details](#project-details)

  - [SpireArena.csproj](#spirearenacsproj)


## Executive Summary

### Highlevel Metrics

| Metric | Count | Status |
| :--- | :---: | :--- |
| Total Projects | 1 | All require upgrade |
| Total NuGet Packages | 6 | All compatible |
| Total Code Files | 12 |  |
| Total Code Files with Incidents | 2 |  |
| Total Lines of Code | 1478 |  |
| Total Number of Issues | 4 |  |
| Estimated LOC to modify | 3+ | at least 0.2% of codebase |

### Projects Compatibility

| Project | Target Framework | Difficulty | Package Issues | API Issues | Est. LOC Impact | Description |
| :--- | :---: | :---: | :---: | :---: | :---: | :--- |
| [SpireArena.csproj](#spirearenacsproj) | net9.0 | 🟢 Low | 0 | 3 | 3+ | ClassLibrary, Sdk Style = True |

### Package Compatibility

| Status | Count | Percentage |
| :--- | :---: | :---: |
| ✅ Compatible | 6 | 100.0% |
| ⚠️ Incompatible | 0 | 0.0% |
| 🔄 Upgrade Recommended | 0 | 0.0% |
| ***Total NuGet Packages*** | ***6*** | ***100%*** |

### API Compatibility

| Category | Count | Impact |
| :--- | :---: | :--- |
| 🔴 Binary Incompatible | 0 | High - Require code changes |
| 🟡 Source Incompatible | 1 | Medium - Needs re-compilation and potential conflicting API error fixing |
| 🔵 Behavioral change | 2 | Low - Behavioral changes that may require testing at runtime |
| ✅ Compatible | 1780 |  |
| ***Total APIs Analyzed*** | ***1783*** |  |

## Aggregate NuGet packages details

| Package | Current Version | Suggested Version | Projects | Description |
| :--- | :---: | :---: | :--- | :--- |
| Alchyr.Sts2.BaseLib | * |  | [SpireArena.csproj](#spirearenacsproj) | ✅Compatible |
| Alchyr.Sts2.ModAnalyzers | * |  | [SpireArena.csproj](#spirearenacsproj) | ✅Compatible |
| BepInEx.AssemblyPublicizer.MSBuild | 0.4.3 |  | [SpireArena.csproj](#spirearenacsproj) | ✅Compatible |
| Godot.SourceGenerators | 4.5.1 |  | [SpireArena.csproj](#spirearenacsproj) | ✅Compatible |
| GodotSharp | 4.5.1 |  | [SpireArena.csproj](#spirearenacsproj) | ✅Compatible |
| GodotSharpEditor | 4.5.1 |  | [SpireArena.csproj](#spirearenacsproj) | ✅Compatible |

## Top API Migration Challenges

### Technologies and Features

| Technology | Issues | Percentage | Migration Path |
| :--- | :---: | :---: | :--- |

### Most Frequent API Issues

| API | Count | Percentage | Category |
| :--- | :---: | :---: | :--- |
| T:System.Text.Json.JsonDocument | 2 | 66.7% | Behavioral Change |
| M:System.IO.Path.Combine(System.ReadOnlySpan{System.String}) | 1 | 33.3% | Source Incompatible |

## Projects Relationship Graph

Legend:
📦 SDK-style project
⚙️ Classic project

```mermaid
flowchart LR
    P1["<b>📦&nbsp;SpireArena.csproj</b><br/><small>net9.0</small>"]
    click P1 "#spirearenacsproj"

```

## Project Details

<a id="spirearenacsproj"></a>
### SpireArena.csproj

#### Project Info

- **Current Target Framework:** net9.0
- **Proposed Target Framework:** net10.0
- **SDK-style**: True
- **Project Kind:** ClassLibrary
- **Dependencies**: 0
- **Dependants**: 0
- **Number of Files**: 12
- **Number of Files with Incidents**: 2
- **Lines of Code**: 1478
- **Estimated LOC to modify**: 3+ (at least 0.2% of the project)

#### Dependency Graph

Legend:
📦 SDK-style project
⚙️ Classic project

```mermaid
flowchart TB
    subgraph current["SpireArena.csproj"]
        MAIN["<b>📦&nbsp;SpireArena.csproj</b><br/><small>net9.0</small>"]
        click MAIN "#spirearenacsproj"
    end

```

### API Compatibility

| Category | Count | Impact |
| :--- | :---: | :--- |
| 🔴 Binary Incompatible | 0 | High - Require code changes |
| 🟡 Source Incompatible | 1 | Medium - Needs re-compilation and potential conflicting API error fixing |
| 🔵 Behavioral change | 2 | Low - Behavioral changes that may require testing at runtime |
| ✅ Compatible | 1780 |  |
| ***Total APIs Analyzed*** | ***1783*** |  |

