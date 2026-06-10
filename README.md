# Devourment: FOE (Feature Other Exit)

An add-on for the Rain World `Devourment` mod. It introduces the "Other Exit" mechanic—an alternative regurgitation pathway from the lower body.

## Features
- Alternative regurgitation from the lower body.
- When prey is ready to exit from the lower body, the status slot/arrow icon will appear brown.
- Active regurgitation: hold **Down + Grab** (`Down + PickUp`) to trigger the regurgitation sequence.
- Visually deforms the creature's tail and body to simulate the pressure and release.

## Requirements
- **Rain World**
- **BepInEx**
- **Devourment Mod** (hard dependency)

## Installation
1. Compile the mod (see Building below) or download the pre-compiled version.
2. Put the `DevourmentFOE` folder inside your Rain World mods folder: `RainWorld_Data\StreamingAssets\mods\`.
3. Enable the mod in the in-game Remix/Mods menu.

## Building
To compile the project, run:
```bash
dotnet build "DevourmentFOE.csproj" -c Release
```
Make sure you have copies of the referenced game and mod assemblies inside the `References/` directory when compiling.
