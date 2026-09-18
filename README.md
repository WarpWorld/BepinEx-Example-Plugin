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
	Reconnect status is shown on the mod's own overlay (see below). `CrowdControlMod.ShowGameUiMessage()`
	is where to also route messages into your game's own toast/HUD/dialog UI if it has one.

On-screen overlay:
	The `UI\` folder draws a small Crowd Control panel in the TOP RIGHT of the screen with IMGUI:
	a connection dot, a row per running timed effect with a draining progress bar and countdown, and
	short message lines. It hides itself completely when the Crowd Control app isn't running, so a
	player who never starts Crowd Control sees nothing. F8 toggles it; the streamer can also turn the
	pieces off in the mod's settings (`UI\ModSettings.cs`).

	- `UI\EffectNames.cs` - fill this in from your Crowd Control pack so rows show "Speed Up" rather
	  than `speedUp`. Unlisted codes fall back to a tidied-up version of the code.
	- `UI\Overlay.cs` - move the panel by changing the card rect in `Draw`, or restyle it via
	  `UI\CcTheme.cs` (the Crowd Control brand palette).
	- If your game pauses by setting `Time.timeScale = 0`, tick the mod from `Update` instead of
	  `FixedUpdate` (and use `Time.unscaledDeltaTime` for `DeltaTime`). Unity stops calling
	  FixedUpdate at timeScale 0, so effects would otherwise freeze mid-countdown without ever
	  registering as paused - here or in the Crowd Control app.

Custom effects (community-written effects loaded from disk) - see [CustomEffect.md](CustomEffect.md)
for the full guide, including how to write one:  
	The mod can load effects a creator drops into `%APPDATA%\CrowdControl-Apps\CustomEffects\<GameName>`,
	compile them at launch, and register them alongside the effects built into the mod. They are the
	same thing to everything downstream: they can be instant or timed, and they can declare conflicts
	against each other or against your own effects to stop both running at once.

	- Each subfolder is one pack, compiled as a single assembly so a multi-file effect can share
	  types. A loose `.cs` file at the top level is its own pack, so one syntax error does not take
	  down anyone else's effects. Prebuilt `.dll` files work too, and are the sensible way to hand a
	  finished effect to someone else.
	- Compiled packs are cached by content hash, so a streamer pays the compile cost once per change
	  rather than once per launch. A mod version bump invalidates the cache.
	- `[EffectMenu]` (next to the usual `[Effect]`) supplies the name, author, price, description,
	  category, and everything else the menu entry needs. These are registered over the existing RPC
	  channel (`CustomEffectsRpc.AddEffects`, relayed by the app to the Crowd Control API, which is
	  where the streamer's credentials and the game pack ID live). Your game pack needs
	  `allowCustomEffects` set or the call is rejected; nothing else is required of the pack.
	- Effect IDs are derived, not declared: `cc_custom_customEffect_mario_633185e18e42884` is the
	  readable name, the author, and a hash of the game, author, name, and whether it is timed. The
	  same effect therefore gets the same ID on every machine, which is what lets the app recognise
	  it and keep the streamer's settings. Renaming an effect or changing its author gives it a new
	  ID, and the old settings stay with the old name.
	- Every generated effect joins the `__cc_custom_effects` group. On connect the mod hides that
	  whole group and then shows only the effects it actually loaded, so a streamer playing on a
	  machine without the files does not offer viewers effects that cannot run. Nothing is ever
	  deleted - the records stay on the streamer's account with the prices they set, ready for the
	  next time the files are present.
	- A purchase reaches the connector with the handler ID in `request.arguments`, which
	  `EffectLoader.TryResolve` reads before falling back to the effect code, so it works whether the
	  client sends the custom effect's own ID or something else.
	- Crowd Control holds at most 75 custom effects per game; the mod logs an error and registers the
	  first 75 rather than failing the whole batch.
	- On by default, with `AllowCustomEffects` in the mod settings to turn it off. Code in that folder
	  runs as part of the game, and every pack that loads is logged with its content hash.
	- Adding a pack works while the game is running. Changing one needs a restart, because Mono
	  cannot unload an assembly - `DevReload` trades that correctness for iteration speed while
	  writing effects.
	- Set `IncludeCustomEffectCompiler=false` when building to leave the ~14MB C# compiler out of the
	  mod. Custom effects then have to be distributed as prebuilt DLLs.

`CrowdControlMod.Instance.Client` offers helper functions for hiding or disabling effects on the menu:  
	`ShowEffects(params string[] codes)` / `ShowAllEffects()`  
	`HideEffects(params string[] codes)` / `HideAllEffects()`  
	`EnableEffects(params string[] codes)` / `EnableAllEffects()`  
	`DisableEffects(params string[] codes)` / `DisableAllEffects()`  
	Async variants of all of the above are also available.
