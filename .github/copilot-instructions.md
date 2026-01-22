# DorsalFin AI Coding Guidelines

## Project Overview
DorsalFin is a BepInEx plugin for Unity-based games (Aicomi, SamabakeScramble) that provides advanced color modification tools in character creation mode. It integrates with CoastalSmell for extended functionality and uses Reactive Extensions for UI management.

## Architecture
- **Shared Core**: `Definitions.cs` defines color properties and UI sprites; `DorsalFin.cs` contains palette logic, HSV calculations, and UI components
- **Game-Specific Entry Points**: `AC/AC_DorsalFin.cs` and `SVS/SVS_DorsalFin.cs` define partial Plugin classes with game-specific constants
- **Build System**: MSBuild-based with `Tasks.xml` handling deployment; uses conditional compilation (`#if Aicomi`) for game differences
- **Data Storage**: Colors and mappings saved to `UserData/plugins/DorsalFin/{Mapping,Palette}/`

## Key Patterns
- **Color Properties**: Use `ColorProperty` tuple `(Available: Func<bool>, Get: Func<Color>, Set: Action<Color>)` for all color manipulations
- **HSV Palette Generation**: `Palette` struct calculates color variations using trigonometric functions for hue/saturation/brightness gaps
- **Reactive UI**: Initialize UI components with `UGUI.OnCommonSpaceInitialize.Subscribe()` and `CompositeDisposable` for cleanup
- **Conditional Access**: Chain null checks with `?.` and conditional compilation for game-specific APIs (e.g., `human?.body?._customTexCtrlBody` vs `human?.body?.customTexCtrlBody`)

## Development Workflow
- **Build**: Run `dotnet build --configuration Debug` to deploy directly to game directory (requires registry install path)
- **Release**: `dotnet build --configuration Release` creates zipped distribution in project root
- **Dependencies**: Clone `SVS-Fishbone` repo automatically; reference CoastalSmell project for shared mod functionality
- **Testing**: Activate in-game with Ctrl+D during character creation; verify color changes persist across sessions

## Code Conventions
- **Tuples**: Prefer C# 10 tuples for multi-return values (e.g., `PaletteHSV`, `PaletteRGB`)
- **Extension Methods**: Use fluent extensions like `.With()` for object configuration
- **LINQ**: Leverage `SelectMany`, `Aggregate`, and `Where` for sprite generation and color calculations
- **Math**: Use `Mathf.Repeat()` for cyclic HSV values; trigonometric functions for palette variations
- **UI Sprites**: Generate procedural sprites from HSV color spaces using `Texture2D.SetPixels()`

## Dependencies & Integration
- **BepInEx.Unity.IL2CPP**: Base plugin framework with IL2CPP support
- **IllusionLibs**: Game-specific assemblies (Aicomi.AllPackages)
- **System.Reactive**: Observable patterns for UI lifecycle management
- **CoastalSmell**: Referenced mod providing extended character manipulation APIs

## Common Pitfalls
- **Game API Differences**: Always use `#if Aicomi` for API variations (e.g., custom texture controls)
- **Null Safety**: Extensive null checking required due to Unity's nullable object hierarchies
- **Registry Deployment**: Debug builds depend on Windows registry install paths; test on actual game installations
- **Color Persistence**: Ensure `SetNewCreateTexture()` called after material color changes</content>
<parameter name="filePath">f:\Repositories\SVS-DorsalFin\.github\copilot-instructions.md