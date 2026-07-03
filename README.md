Example project for setting up a BepinEx 5 (mono) plugin to connect a game to Crowd Control

All game-specific code (effects, Harmony patches, metadata, and game state checks) is included
as commented-out examples marked with `== EXAMPLE (Anger Foot) ==`. The project builds and runs
as-is without any game-specific code; uncomment and adapt the examples for your game instead of
deleting them.

Instructions:

1) Name the Project for Your Game  
	Both settings are in the `ALWAYS SET THESE FOR A NEW GAME!` blocks:  
	- `GameName` in `BepinExExample\BepinExExample.csproj` - names the output DLL (e.g. `CrowdControl.AngerFoot.dll`)  
	- `MOD_NAME` (and `MOD_VERSION`) in `BepinExExample\CrowdControlMod.cs` - the display name shown in the BepinEx log  
	(`MOD_NAME` can't be moved into the csproj because the `[BepInPlugin]` attribute requires compile-time constants.)  
	`MOD_GUID` can stay as-is - only one Crowd Control mod is installed per game.

2) Update References  
	Set `GameBaseDir` in `BepinExExample\BepinExExample.csproj` to your game's install folder.  
	Update the BepinEx references to point to the BepinEx.dll from the downloaded version of BepinEx.  
	Add a reference to Assembly-CSharp from the game's data folders.

3) Create Effect Functions  
	`Delegates\Effects\Implementations\` contains the classes implementing effects.  
	Each file there is a commented example demonstrating a pattern:  
	- `CompleteLevel.cs` / `RestartLevel.cs` - instant (non-timed) effects  
	- `GodMode.cs` / `InfiniteAmmo.cs` - timed effects toggled on start/stop  
	- `ForceKick.cs` - a timed effect that acts every tick  
	- `PassiveEnemies.cs` / `StaticEnemies.cs` - timed effects with cross-effect conflicts

4) Create Timed Effects  
	Timed effects are any effects with a `defaultDuration` on their `[Effect]` attribute.  
	Pausing while the game is busy, resuming, and reporting the remaining time to the
	Crowd Control client are all handled automatically by `TimedEffectState`.

5) Setup IsReady & GetGameState Functions  
	`GameStateManager.cs` contains functions called `IsReady` and `GetGameState`.  
	`IsReady` returns a boolean indicating whether the game is in a state ready to execute effects.  
	`GetGameState` returns the current game state (Ready, Paused, NotFocused, Menu, Loading, ...).  
	State changes are automatically reported to the Crowd Control client as they happen;
	add your game-specific checks where marked with TODO.

6) Define Metadata (Optional)  
	`Delegates\Metadata\MetadataDelegates.cs` contains the metadata delegates.  
	Static methods tagged `[Metadata("key")]` answer `DataRequest` queries from the client,
	and any keys listed in `CommonMetadata` are attached to every effect response.

7) Attach Action Queue (Uncommon)  
	In rare cases, the FixedUpdate() method of the plugin is not called automatically as part of the standard game loop.  
	In `CrowdControlMod.cs` there is an example harmony patch to attach to the FixedUpdate() function of some universal object.  
	This should be used if and only if the FixedUpdate() method is not called automatically.

Displaying viewer names:  
	Viewer names come from external services and may contain characters your game can't render
	(emoji, control characters, rich-text markup, etc). Use `request.GetViewerDisplayName()`
	(from `EffectRequestEx.cs`) instead of reading `request.viewer` directly - it returns a
	sanitized name and falls back to "the crowd" when no usable name is present.

Manual reconnect hotkey:  
	Press F9 in-game to request a Crowd Control reconnect. The plugin only attempts this when the
	Crowd Control client process/semaphore is found, and the hotkey has a 5 second cooldown to avoid spam.
	`CrowdControlMod.ShowGameUiMessage()` is the game-specific hook for displaying reconnect status in
	your game's toast/HUD/dialog UI; it is intentionally a no-op in the example pack.

`CrowdControlMod.Instance.Client` offers helper functions for hiding or disabling effects on the menu:  
	`ShowEffects(params string[] codes)` / `ShowAllEffects()`  
	`HideEffects(params string[] codes)` / `HideAllEffects()`  
	`EnableEffects(params string[] codes)` / `EnableAllEffects()`  
	`DisableEffects(params string[] codes)` / `DisableAllEffects()`  
	Async variants of all of the above are also available.
