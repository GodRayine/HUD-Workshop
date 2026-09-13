# HUD Workshop

<img src="images/icon.png" alt="HUD Workshop layout icon" width="96" height="96">

**English** · [Русский](README.ru.md)

An in-game HUD editor for FFXIV / Dalamud, with snapping, group editing and properties available directly on selection. The plugin UI is currently in Russian; this README is available in both languages. Penumbra is not required.

## Status and compatibility

This branch prepares **1.1.0.0 for review**; it is not a published release or an accepted official Dalamud plugin. The public custom repository still serves [1.0.0](https://github.com/GodRayine/HUD-Workshop/releases/tag/v1.0.0).

Requires Windows, **Dalamud 15.0.3.4 (API 15)** and **FFXIV 2026.09.01.0000.0000**. Native structures are version-specific. The plugin refuses to load with another Dalamud version; removing this guard does not establish compatibility.

## Install the published version

In `/xlsettings` → Experimental → Custom Plugin Repositories, add:

```text
https://raw.githubusercontent.com/GodRayine/HUD-Workshop/main/repo.json
```

Open `/xlplugins`, refresh the list and install **HUD Workshop**. Use `/hudworkshop` to open the editor. If migrating from a development build, save the HUD, disable that build and remove its Dev Plugin Locations entry first. Keep the HudEditor configuration and run only one copy.

## Features

- Grid and edge snapping, alignment and equal spacing.
- Multi-selection, saved groups and group movement.
- Position, scale, standard hotbar shape and element visibility controls.
- Placeholders for supported inactive elements; main and detached chat windows.
- Undo/redo and cancellation of unsaved previews.
- Layout sharing through a `HUDW1` text code, with validation and preview before saving.

Select an element to edit its properties. **Shift + click** changes the selection; **Alt** temporarily disables snapping. **Сохранить HUD** saves the layout. Closing the inspector cancels unsaved edits. Escape is intended to cancel an active drag first, otherwise close the inspector; the drag-specific behavior still needs final in-game verification.

Layout codes transfer supported positions, scales, visibility and semantic shape settings. They do not include chat messages, character identifiers, action assignments, editor groups or combined/split target and status mode preferences. Unsupported elements are skipped. Optional resolution adaptation adjusts positions, not scale.

## Limitations and validation

Cross hotbars are unsupported. Other jobs and special modes have not received full coverage. When every status element is hidden and the mode has not been observed, split mode is assumed. Chat hiding is maintained while the plugin runs. Editing pauses during combat and transitions.

The 1.1 Release build and **72 automated checks** pass. These cover geometry, layout-code validation and managed save/recovery failure boundaries, not native game persistence. The owner confirmed opening, Escape closing, reopening and the corrected title-bar buttons. Final native save/restart, UI import, lifecycle interruption, partial recovery, and clean install/update checks remain pending. See the [test matrix](review/TEST-MATRIX.md) and [review readiness notes](review/READINESS.md).

## Build and package

Use **.NET SDK 10.0.100**, PowerShell 7 and the matching Dalamud libraries. Dependency versions and the NuGet lockfile are committed.

```powershell
./build.ps1 -DotnetPath 'C:/path/to/dotnet.exe' -DalamudPath 'C:/path/to/Hooks/15.0.3.4'
dotnet run --project tests/GeometryTests.csproj --configuration Release
./package-release.ps1 -DotnetPath 'C:/path/to/dotnet.exe' -DalamudPath 'C:/path/to/Hooks/15.0.3.4'
```

Packaging restores in locked mode, builds, runs tests, checks manifest/assembly compatibility and verifies ZIP contents and SHA-256 hashes. Output is `release/<version>/`, derived from the project version. The source archive includes both READMEs, licenses and review materials. Packaging does not upload files or change the published catalog. See [release instructions](docs/RELEASE.md).

## License and development

Plugin source: [GPL-3.0-only](LICENSE). Icon: **CC0**, [Layout Wireframe from SVG Repo](https://www.svgrepo.com/svg/408321/layout-wireframe); see [asset provenance](THIRD-PARTY-NOTICES.md).

An AI coding agent wrote the implementation, tests and most technical decisions. The owner specified requirements and iteratively tested the plugin in game. Independent human source review has not yet been completed; no invented contribution percentages are claimed. The [submission draft](review/SUBMISSION-DRAFT.md) records this disclosure and remaining review work.
