# RWDUnlocker AI Instructions

## Scope
- This repository is for Rain World modding with BepInEx and MonoMod RuntimeDetour.
- Focus on writing gameplay tweaks, quality-of-life behavior patches, compatibility fixes, and optional debug tooling.
- Treat Devourment as a primary integration target, but keep guidance general and reusable for modding work.

## General Modding Guidance
- Prefer small, isolated hook classes grouped by feature.
- Use reflection to find target methods, then create hooks with `new Hook(target, delegate)`.
- Always call `orig(...)` unless a full override is explicitly intended.
- Keep `Apply()` and `Dispose()` symmetric for every hook class.
- Add strong null checks and state guards: runtime hooks execute in volatile live game state.
- Avoid hardcoding behavior that depends on a single run/state when a safe fallback can be used.

## Devourment Code Knowledge
- Devourment logic centers around `CritRef.CritRefDat`, `DevourmentMain`, and menu/status systems.
- Important fields and flows commonly used in patches:
- `Depth`, `pendingDepth`, `currentBellyStatus`, `isSwallowed`, `swallowerCrit`, `bellyCrits`.
- Common hook points include methods related to swallowing checks, in-belly updates, struggle handling, menu slot actions, and save/session transitions.
- When patching Devourment behavior, preserve vanilla/mod invariants where possible and minimize side effects.

## Logging Guidance
- Unity provides built-in logging via `UnityEngine.Debug.Log`.
- `Debug.Log` writes to `consoleLog.txt` in the Rain World installation directory.
- BepInEx provides `ManualLogSource`, typically accessed from `BaseUnityPlugin.Logger`.
- Using `ManualLogSource` gives richer structured logs and writes to `BepInEx/logOutput.log` by default.
- Prefer `ManualLogSource` for plugin-level diagnostics and lifecycle messages.
- Use `Debug.Log` mainly for quick runtime probes when needed.

## Build and Install Workflow
- `build.bat` runs `dotnet build RWDUnlock.csproj -c Release` and copies DLL/PDB into the Rain World mod folder.
- **CRITICAL**: Do NOT execute `build.bat` directly on the host system. Instead, run the build using `dotnet build RWDUnlock.csproj -c Release` in the terminal.
- The csproj references game and BepInEx assemblies via local paths; update `HintPath` if install paths differ.
- Plugin metadata is in the `BepInPlugin` attribute.

## Source References
- `Sources/Assembly-CSharp`: decompiled Rain World game code for reference.
- `Sources/rain-world-devourment-tweaked`: Devourment source code reference.
- **CRITICAL**: The `Sources/` directory is STRICTLY read-only. Never modify any code or files in this folder.

## Key Devourment Classes to Inspect
- `CritRef.CritRefDat`: State management, belly contents, digesting status, swallowing, struggle status.
- `DevourmentMain`: Mod entry point, hooks setup, swallow checks, helper functions.
- `MenuManager`: Radial menu construction, sub-menus, action handling, tab history.
- `RadialMenu`: Draws and handles visual radial menus, crosshairs, reticles.

## Logging Details
- **BepInEx Logger** (`Logger.LogInfo` / `Logger.LogError`): Writes to `Rain World/BepInEx/LogOutput.txt` (or `.log`).
- **Unity Debug Logger** (`Debug.Log`): Writes to `Rain World/consoleLog.txt`.
- **In-Game Console**: Enabled via Dev Tools, accessed by pressing the `K` key to view debug logs live.

## Radial Menu Sections (SubMenuTypes)
- `ViewCrit`: Default starting view. Offers belly viewing, managing self, opening the reticle, or debug menu.
- `Self`: Allows setting struggle modes (Fighting, Gentle) and setting default status overrides for prey, allies, and items.
- `ViewList`: Lists belly items/creatures or targets selected via the reticle.
- `ChangeStatus`: Changes the `CurrentBellyStatus` of the target (Held, Digesting, Energy Theft, Healing, Sedating, Regurgitating).
- `Interact`: Struggling actions in the same belly (Help Out, Pull Down, Try Swallowing, Cuddle).
- `Requests`: Prey requests to the predator (Request Out, Request Manage, Target Coord/Crit, Go to Den, Feed Self).
- `Debug`: Developer-only tools (force regurgitate, change part override, force successful influences, sound test, etc.).
- `SoundTest`: Play audio clips.
- `Reticle`: Crosshair targeting.
- `Confirm`: Choices confirmation.

## DAB (Devourment Action Button)
- **Concept**: Devourment Action Button (DAB) is a key modifier used to trigger almost all mod actions (swallowing, feeding, opening radial menu, cuddling, belly rubbing, pipe re-swallowing).
- **Default Key**: Configured per player, defaults to `KeyCode.V` (e.g. `DevourConfig.player1DAB`).
- **Input Checking**: Checked in code using `DevourmentMain.HoldingActionButton(playerNumber)` which supports BepInEx configurations or Improved Input.
- **Controls Reference**:
  - Open Radial Menu: Hold DAB + Tap Throw
  - Swallow: Hold DAB + Hold Grab
  - Force-Feed object: Hold DAB + Hold Throw
  - Force-Feed self: Hold DAB + Hold Map
  - Rub Belly: Hold DAB + Hold Down
  - Lockdown Mode: Hold DAB + Hold Down + Hold Grab
  - Re-swallow (at pipes): Hold DAB