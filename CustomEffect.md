# Custom Effects

Custom effects let anyone write a Crowd Control effect for a game, drop it in a folder, and have it
show up in the streamer's menu — without touching the game's mod, waiting on a release, or getting
anything published by Warp World.

A custom effect is an ordinary effect. It can be instant or timed, it gets the same automatic
pause/resume handling when the game isn't ready, and it can declare conflicts against other custom
effects *or* against the effects built into the mod, so two things that shouldn't run at once don't.
Nothing about being loaded from disk makes it a second-class effect.

Requires **BepInEx 5 (Mono)**. See [Limitations](#limitations-and-known-gaps) for IL2CPP.

---

## Contents

- [Quick start](#quick-start)
- [Folder layout](#folder-layout)
- [Writing an effect](#writing-an-effect)
- [`[Effect]` reference](#effect-reference)
- [`[EffectMenu]` reference](#effectmenu-reference)
- [Who can turn this on](#who-can-turn-this-on)
- [For streamers](#for-streamers)
- [For mod developers: adding this to a game](#for-mod-developers-adding-this-to-a-game)
- [How it works](#how-it-works)
- [Effect IDs](#effect-ids)
- [Session visibility and the streamer's settings](#session-visibility-and-the-streamers-settings)
- [Limits and backend behaviour](#limits-and-backend-behaviour)
- [Limitations and known gaps](#limitations-and-known-gaps)
- [Troubleshooting](#troubleshooting)
- [What was built](#what-was-built)

---

## Quick start

1. Launch the game once. The mod creates the folder and drops a `README.txt` in it:

   ```
   %APPDATA%\CrowdControl-Apps\CustomEffects\<GameName>\
   ```

2. Put a `.cs` file in that folder:

   ```csharp
   [Effect(id: "chaos_gravity", defaultDuration: 30)]
   [EffectMenu("Chaos Gravity", 250,
       Author = "mario",
       Description = "Flips gravity upside down for half a minute.")]
   public class ChaosGravity : Effect
   {
       public ChaosGravity(CrowdControlMod mod, NetworkClient client) : base(mod, client) { }

       public override EffectResponse Start(EffectRequest request)
       {
           Physics.gravity = -Physics.gravity;
           return EffectResponse.Success(request.ID);
       }

       public override EffectResponse? Stop(EffectRequest request)
       {
           Physics.gravity = new Vector3(0f, -9.81f, 0f);
           return EffectResponse.Finished(request.ID);
       }
   }
   ```

3. Start the game. The effect appears in the Crowd Control menu, priced at 250 coins, and the
   streamer can change that price to whatever they like.

That's the whole loop. There's no registration step, no approval step, and nothing to enable — if
there's a `.cs` file in the folder, it gets compiled and loaded.

---

## Folder layout

Everything lives under one predictable path so the mod, the Crowd Control app, and a creator writing
effects all agree on where things are without anyone configuring anything:

```
%APPDATA%\CrowdControl-Apps\CustomEffects\<GameName>\
├── README.txt                  written on first run
├── .cache\                     compiled builds, safe to delete
├── ChaosGravity.cs             a single-file effect — its own pack
└── MyEffectPack\               a folder — one pack, compiled together
    ├── Helpers.cs
    ├── SpawnChaos.cs
    └── MyEffectPack.dll        a prebuilt DLL also works
```

`<GameName>` comes from the mod's own assembly name, so `CrowdControl.AngerFoot.dll` means
`...\CustomEffects\AngerFoot\`.

Each immediate subfolder is one **pack**, compiled as a single assembly, so a multi-file effect can
share types between its own files. A loose `.cs` file at the top level is its own pack — that way one
person's syntax error doesn't stop everyone else's effects from loading. Prebuilt `.dll` files work
too, and are the sensible way to hand a finished effect to someone who shouldn't have to compile it.

---

## Writing an effect

An effect is a public, non-abstract class inheriting `Effect`, carrying an `[Effect]` attribute, with
a constructor taking `(CrowdControlMod mod, NetworkClient client)`. Forgetting the constructor is the
most common mistake and the log calls it out by name with the signature you need.

Override what you need:

| Method | When it runs |
|---|---|
| `Start` | The effect fires. Required. |
| `Stop` | A timed effect's duration ran out, or it was stopped early. |
| `Tick` | Every frame while a timed effect is running. |
| `Pause` / `Resume` | The game stopped being ready mid-effect. Handled for you unless you override. |

### An instant effect

```csharp
[Effect(id: "give_coin")]
[EffectMenu("Give a Coin", 10, Author = "mario", Description = "One free coin.")]
public class GiveCoin : Effect
{
    public GiveCoin(CrowdControlMod mod, NetworkClient client) : base(mod, client) { }

    public override EffectResponse Start(EffectRequest request)
    {
        if (Player.Instance == null) return EffectResponse.Retry(request.ID);

        Player.Instance.Coins += 1;
        Mod.ShowGameUiMessage($"{request.GetViewerDisplayName()} gave you a coin!");
        return EffectResponse.Success(request.ID);
    }
}
```

Return `Retry` rather than `Failure` when the game simply isn't in a good moment — Crowd Control will
try again shortly, and the viewer isn't refunded for something that's about to work.

### A timed effect that conflicts with others

`defaultDuration` in seconds is what makes an effect timed. `conflicts` is a list of effect IDs that
must not be running at the same time — and it can name the game's *own* effects, not just custom
ones:

```csharp
[Effect(id: "super_speed", defaultDuration: 20, conflicts: ["super_speed", "slow_motion"])]
[EffectMenu("Super Speed", 200, Author = "luigi", Orderliness = -0.5f, Morality = 0f)]
public class SuperSpeed : Effect
{
    public SuperSpeed(CrowdControlMod mod, NetworkClient client) : base(mod, client) { }

    public override EffectResponse Start(EffectRequest request)
    {
        Player.Instance.SpeedMultiplier = 2f;
        return EffectResponse.Success(request.ID);
    }

    public override EffectResponse? Stop(EffectRequest request)
    {
        Player.Instance.SpeedMultiplier = 1f;
        return EffectResponse.Finished(request.ID);
    }
}
```

If a viewer buys Super Speed while Slow Motion is running, Crowd Control retries it automatically
until the conflict clears. Timed effects conflict with themselves by default, so you rarely need to
say so explicitly.

### What you can use

Your effect is compiled against everything the game already has loaded: the game's own
`Assembly-CSharp`, every Unity module, Harmony, `ConnectorLib.JSON`, and the mod itself. Anything the
mod can do, a custom effect can do — including Harmony patches.

These usings are injected for you, so a small effect doesn't have to open with a wall of directives:

```
System, System.Collections, System.Collections.Generic, System.Linq,
UnityEngine, ConnectorLib.JSON, CrowdControl, CrowdControl.Delegates.Effects
```

`CROWD_CONTROL` and `CROWD_CONTROL_CUSTOM_EFFECT` are defined, in case you want code that only
compiles in this context. The language version is whatever the bundled compiler supports (C# 12 as
shipped), with unsafe blocks and nullable reference types enabled.

---

## `[Effect]` reference

This is the existing attribute every effect in the mod already uses; nothing here is specific to
custom effects.

| Parameter | Meaning |
|---|---|
| `id` | The effect's internal ID. Used for conflicts and log messages. |
| `defaultDuration` | Seconds. Greater than zero makes the effect timed. |
| `conflicts` | Effect IDs that must not run at the same time as this one. |
| `selfConflict` | Whether a second copy may run alongside the first. Defaults to false for timed effects. |

---

## `[EffectMenu]` reference

`[EffectMenu]` is how a custom effect describes its own menu entry. Effects built into the mod don't
need it, because their entries come from the game pack. Two values are required and passed
positionally; everything else is a named property.

| Property | Type | Notes |
|---|---|---|
| *name* | `string` | Required, positional. Shown in the menu. Max 64 characters. |
| *price* | `long` | Required, positional. Default coin cost. Clamped to at least 1. |
| `Author` | `string` | Part of the effect's identity, not just credit. See [Effect IDs](#effect-ids). |
| `Description` | `string` | Max 512 characters. Worth writing — viewers read it before spending. |
| `Category` | `string[]` | Menu category path. Defaults to `["Custom Effects", "<pack name>"]`. |
| `Group` | `string[]` | Extra menu groups. The mod's own group is always added. |
| `Tags` | `string[]` | Freeform tags. `__cc_custom_effect` is always added. |
| `Image` | `string` | Name of the icon to use. |
| `Note` | `string` | Shown to the streamer, not to viewers. |
| `Hidden` | `bool` | Register the effect but keep it out of the menu. |
| `Inactive` | `bool` | Show it in the menu but leave it unpurchasable. |
| `DurationLocked` | `bool` | Stop the streamer changing a timed effect's duration. |
| `SessionCooldown` | `float` | **Minutes** of session-wide cooldown after firing. Capped at 120. |
| `UserCooldown` | `float` | **Minutes** of per-viewer cooldown after firing. Capped at 120. |
| `Orderliness` | `float` | −1 chaotic to +1 orderly. Set both axes or neither. |
| `Morality` | `float` | −1 evil to +1 good. Set both axes or neither. |
| `QuantityMin` / `QuantityMax` | `long` | Let viewers buy several at once. |

The duration deliberately isn't repeated here — it comes from `[Effect]`, so there's only ever one
place to change it.

`[EffectMenu]` is optional. Without it the mod guesses a name from the effect ID and uses a default
price, and logs a warning telling you to add one. Dropping a bare `.cs` file works; it just won't
look like you chose how it presents.

---

## Who can turn this on

Three separate things have to agree before a viewer can buy a custom effect, and they're owned by
three different people on purpose.

| Gate | Owned by | Where | Default |
|---|---|---|---|
| `CUSTOM_EFFECTS_SUPPORTED` | whoever builds the game's mod | compiled into the mod | off |
| `allowCustomEffects` | whoever maintains the game's pack | the pack's `base.json` | off |
| `AllowCustomEffects` | the streamer | the mod's config file | on |

The first is a compile-time constant rather than a setting, and that's the important part: **a
streamer cannot turn custom effects on for a game whose mod didn't ship with support.** Editing the
config file does nothing, because when the constant is false the mod never reads the folder, never
compiles anything, and never even creates those settings. Games adopt this one at a time, as their
authors decide the game is a good fit.

The pack gate is what makes effects *purchasable*: without it the app rejects the registration, so
effects would load and run locally but never reach anyone's menu.

The streamer's setting is the only one that defaults to on, and it exists so someone who doesn't want
community code in their game can say so without uninstalling anything.

---

## For streamers

**Settings.** `[CustomEffects]` in `BepInEx\config\WarpWorld.CrowdControl.cfg`. For a game that
supports custom effects they work out of the box; there's nothing to turn on.

| Setting | Default | What it does |
|---|---|---|
| `AllowCustomEffects` | `true` | Load effects from the folder. Set to `false` to ignore it entirely. |
| `DevReload` | `false` | Reload a changed effect immediately instead of at the next restart. |

If that section isn't in your config file, this game's mod doesn't support custom effects — see
[Who can turn this on](#who-can-turn-this-on).

**What's actually running.** Effects in this folder run as part of the game, so only put things there
that you trust — the same judgement you already apply to installing a mod. Every pack that loads is
named in the game's log with its content hash, so you can always check what's running and which
version of it.

**Prices are yours.** Whatever an effect author sets is only a starting point. Change the price,
duration, category, or cooldowns in the Crowd Control app and your changes stick — the mod will never
overwrite them, including on later launches or after a mod update.

**Playing on another machine.** Your custom effects live on your Crowd Control account, not in the
folder, so your settings follow you. But an effect whose files aren't on the machine you're playing on
is hidden for that session rather than deleted, so viewers are never offered something that can't
run. Copy the files over and it comes back, priced exactly as you left it.

**Restarts.** Adding a new effect works while the game is running. Changing an existing one needs a
restart, for reasons in [Limitations](#limitations-and-known-gaps).

---

## For mod developers: adding this to a game

Almost nothing is game-specific. In this template it's already wired up.

Custom effects are **off unless a game opts in**, and it takes two deliberate steps in two different
places to turn them on. See [Who can turn this on](#who-can-turn-this-on) for why.

**The mod needs to declare support.** Set the compile-time constant in `CrowdControlMod`:

```csharp
public const bool CUSTOM_EFFECTS_SUPPORTED = true;
```

While this is false, the mod never reads the folder, never compiles anything, and doesn't even create
the settings — so nothing a streamer or anything else puts on disk can change that. If effects are
sitting in the folder for a game that hasn't opted in, the mod says so in the log rather than failing
silently.

**The game pack needs `allowCustomEffects`.** Set `meta.allowCustomEffects: true` in the pack's
`base.json`. Without it the backend rejects effect registration, so effects would run locally but
never appear in anyone's menu. Nothing else is required of the pack — no dispatch effects, no
reserved IDs, no per-effect entries.

**Then the mod needs three lines.** In `CrowdControlMod.Awake`, after the scheduler exists:

```csharp
CustomEffects = new(this, Client);
CustomEffects.LoadAll();
```

and in `FixedUpdate`, so file changes and menu reports land on the game thread:

```csharp
CustomEffects?.Tick();
```

**Shipping the compiler is optional.** Build with `-p:IncludeCustomEffectCompiler=false` to leave
Roslyn (~14 MB) out. Custom effects then have to be distributed as prebuilt DLLs, and the mod says so
plainly in the log rather than failing in a way anyone has to guess at.

---

## How it works

```
%APPDATA%\...\CustomEffects\<Game>\
  │
  ├─ discover packs ─── compile (Roslyn) or load the cached .dll
  │                                              │
  │                                        cache: .cache\<pack>.<hash>.dll
  ↓
EffectLoader.RegisterAssembly(assembly, custom: true)
  │   derives an ID per effect, keyed into EffectLoader.Handlers
  ↓
CustomEffectManager builds a menu record per effect
  │
  ├─ addEffects RPC ────────────→ Crowd Control app ──→ PUT the streamer's account (S3)
  ├─ hide group __cc_custom_effects   (everything, including other machines' effects)
  └─ show the IDs loaded here        (so only what can actually run is purchasable)

viewer buys an effect
  ↓
EffectRequest arrives with the handler ID in `arguments`
  ↓
EffectLoader.TryResolve → the compiled Effect instance
  ↓
Scheduler: conflict check → timed countdown → pause/resume — identical to a built-in effect
```

**Discovery and hashing.** Each pack is hashed over its file contents plus the mod version, so a mod
update invalidates every cached build rather than running code compiled against an older effect API.

**Compilation.** Roslyn compiles each pack in memory, with references gathered from every assembly
the game currently has loaded (plus the `netstandard` facade, which the mod targets but the runtime
doesn't load). A portable PDB is emitted, so exceptions thrown by a custom effect have real file names
and line numbers in the log. Compiler errors and warnings are logged with the same
`file(line,col): error CS____` shape any C# developer already knows.

The compiled result is cached by content hash, so a streamer who isn't editing effects pays the
compile cost once. On a cold cache the compiler assemblies load; on a warm one they never do, because
nothing outside the compiler file holds a Roslyn type in a field or signature.

**Registration.** `EffectLoader` was already reflection-based; it now takes any assembly rather than
only the mod's own. Custom effects land in a separate `Handlers` dictionary rather than the pack
effect map, so menu messages like `ShowAllEffects` never send the Crowd Control app an ID its pack has
never heard of.

**Dispatch.** A purchase arrives with the handler ID in `EffectRequest.arguments`, which
`TryResolve` checks before falling back to the effect code. That means it works whether the app sends
the custom effect's own ID or something else entirely. From resolution onward, custom effects go
through exactly the same `Scheduler` path as built-in ones — the conflict check, the timed state
machine, focus-loss auto-pause, and the on-screen overlay all work without a single special case.

**Hot reload, honestly.** A `FileSystemWatcher` debounces changes for 1.5 seconds and then asks the
game thread to rescan (never the watcher's thread, since effect constructors may touch Unity). New
packs load immediately because registration is purely additive. Changed packs don't, because Mono
can't unload an assembly — `DevReload` will point the IDs at the new code anyway, leaking the old
assembly, which is a fine trade while writing an effect and the wrong one while streaming.

---

## Effect IDs

You don't assign effect IDs. They're derived, because the same effect has to end up with the same ID
on every machine the streamer plays on — that's what lets Crowd Control recognise an effect it has
seen before and keep the settings the streamer changed.

An ID is a readable slug, the author, and a fingerprint:

```
cc_custom_customEffect_mario_633185e18e42884
└───┬───┘ └────┬─────┘ └─┬──┘ └──────┬──────┘
 prefix    name slug   author   60-bit hash of
                                game + author + name + timed/instant
```

Real examples, generated by the shipped code:

| Effect | ID |
|---|---|
| "Custom Effect" by mario, instant | `cc_custom_customEffect_mario_633185e18e42884` |
| the same effect, but timed | `cc_custom_customEffect_mario_1a8a6967ed168a3` |
| "Custom Effect" by luigi | `cc_custom_customEffect_luigi_d5a17488528d843` |
| "Custom Effect 2" by mario | `cc_custom_customEffect2_mario_27305fbdb9f3d1d` |
| no `[EffectMenu]`, id `chaos_gravity` | `cc_custom_chaosGravity_a38eb84fef50054` |

Consequences worth knowing:

- **The pack folder isn't part of the ID.** Moving a file between folders, or renaming a folder you
  downloaded, doesn't orphan anyone's settings.
- **The internal `[Effect(id:)]` isn't part of it either.** You can rename it freely.
- **Renaming the display name or the author makes a new effect.** The old entry keeps the streamer's
  settings and sits hidden. That's the deliberate trade for nobody having to hand-assign IDs.
- **Two authors can use the same name.** Including the author is what keeps them apart. Two effects
  that do collide are reported in the log and the second one is skipped.
- IDs satisfy the API's `^[a-zA-Z0-9_]+$` and 150-character rules. Punctuation and non-ASCII names are
  sanitised (`Mário's ¡Wild! Ride™` becomes `mRioSWildRide`) and long names are truncated.

---

## Session visibility and the streamer's settings

Two rules drive this, and they pull in opposite directions:

- Effects the streamer doesn't have the files for **must not** be purchasable, or viewers buy
  something that silently does nothing.
- Effects the streamer configured **must not** lose that configuration just because they played a
  session on a laptop without the files.

So nothing is ever deleted, and visibility is handled per session instead. Every effect the mod
generates joins a `__cc_custom_effects` group. On connect the mod hides that whole group and then
shows only the IDs it actually loaded. Session visibility is transient state on the Crowd Control
side; the effect records and the streamer's prices are persistent and untouched.

The group matters: it's one the mod owns, so hiding it never touches a custom effect the streamer
built by hand in the app. Deleting a custom effect stays the streamer's decision, never the mod's.

Registration is sent with `preserveExisting`, so an effect the streamer already has is left exactly as
they configured it. The split is: **the mod is the authority on which effects exist, the streamer is
the authority on how they're configured.**

---

## Limits and backend behaviour

| Thing | Value |
|---|---|
| Custom effects per game | 75. The mod registers the first 75 and logs which were dropped. |
| Effect ID | `^[a-zA-Z0-9_]+$`, 150 characters |
| Name | 64 characters |
| Description | 512 characters |
| Minimum price | 1 coin |
| Cooldowns | 0–120 minutes |
| Where they're stored | The streamer's Crowd Control account, per user and per game pack |
| Requires | `allowCustomEffects` on the game pack |

Registration goes over the RPC channel the app already relays (`addEffects` / `removeEffect`), so no
new protocol surface exists for this feature. `clone` on an effect record is resolved when the effect
is saved and not stored afterwards, which means it's a convenience for inheriting price, duration, and
image from an existing effect — not a routing mechanism.

---

## Limitations and known gaps

**Changing an effect needs a restart.** Mono can't unload an assembly and collectible load contexts
don't exist on this runtime. `DevReload` works around it for authors at the cost of leaking the
replaced code and any static state it held.

**IL2CPP isn't supported yet.** BepInEx 6's IL2CPP support does run managed plugins with a live JIT,
so compiling at runtime isn't the blocker people expect. The problem is references: effects would have
to be written against per-build Il2CppInterop wrapper assemblies instead of a stable
`Assembly-CSharp`, so every game update would break every custom effect. Worth a spike, not a
promise.

**Multiplayer needs thought per game.** Custom effects break an assumption multiplayer relies on:
that every peer runs the same effect code. An effect that needs host authority can't simply be
forwarded, because the host has no code for it. The derived IDs and pack hashes already work as
capability tokens for peers to compare; what's missing is a per-game way to declare that an effect
needs host authority and to forward or refuse it. Nothing here blocks single-player or client-side
multiplayer effects.

**A `.cs` file here is arbitrary code execution inside the game process.** That is worth saying
plainly, but it does not put custom effects in a different trust category from mods generally:
installing a BepInEx plugin already means running someone else's code, and anything with write access
to `%APPDATA%` can run code on the machine without going anywhere near a game. The gate that matters
is the one a streamer can't undo — a game whose mod wasn't built with support will never run anything
from that folder.

An earlier design also gated each pack behind a content hash the streamer had to approve. It was
removed, because the approval file lived in the same user-writable folder as the effects it was
supposed to protect: anything that could drop a `.cs` there could append its own hash just as easily.
It cost creators a round trip through the log file and bought nothing. What remains is the honest
version — every pack that loads is logged by name and content hash, so anyone can see what ran.

---

## Troubleshooting

Everything below appears in the game's BepInEx log.

| What you see | What it means |
|---|---|
| "does not support them, so none of them were loaded" | This game's mod wasn't built with custom effect support. Nothing in the config can change that. |
| "Custom effects are turned off in the mod settings" | `AllowCustomEffects` is false in the mod config. |
| "did not compile and will not be loaded" | Compiler errors are logged above it with file, line, and column. |
| "The C# compiler could not be started" | The mod was built without the compiler. Ship the effect as a prebuilt DLL. |
| "loaded but contains no effects" | An effect must be a public non-abstract class inheriting `Effect` with an `[Effect]` attribute. |
| "has no matching constructor" | Add `public YourEffect(CrowdControlMod mod, NetworkClient client) : base(mod, client) { }`. |
| "resolves to the same ID as ..." | Two effects share a name and author. Change one. |
| "has changed. Restart the game" | Expected. Changing an effect needs a restart unless `DevReload` is on. |
| "no `[EffectMenu]` attribute, so its menu entry had to be guessed" | Add one to control the name, price, and description. |
| "Crowd Control only holds 75 per game" | Remove some effects so you choose which 75 are registered. |
| Effect exists but isn't in the menu | Check the mod is connected. Registration only happens during a session, and effects are hidden until the mod reports them. |

---

## What was built

Three repositories.

### `BepinEx-Example-Plugin` — the mod

New, under `BepinExExample/CustomEffects/`:

| File | Role |
|---|---|
| `CustomEffectPaths.cs` | The well-known folder locations, ID prefix, group, and handler argument name. |
| `CustomEffectPack.cs` | Pack discovery and content hashing. |
| `CustomEffectCompiler.cs` | The only file that touches Roslyn. Reference gathering, global usings, diagnostics. |
| `CustomEffectID.cs` | Derives the stable, readable effect IDs. |
| `CustomEffectManifest.cs` | Maps `[Effect]` + `[EffectMenu]` onto the API's effect record. |
| `CustomEffectManager.cs` | Orchestration: load, cache, watch, register, report. |

Also new: `Delegates/Effects/EffectMenuAttribute.cs`.

Modified: `EffectLoader.cs` (register any assembly, derived IDs, a `Handlers` map, `TryResolve`),
`Scheduler.cs` (resolve by handler argument, and overlay labels),
`CrowdControlMod.cs` (lifecycle, and the `CUSTOM_EFFECTS_SUPPORTED` opt-in),
`NetworkClient.cs` (re-report on reconnect), `NetworkClient.MessageHelpers.cs` (hide/show by group),
`UI/ModSettings.cs` (two settings, only created when the game supports the feature),
`BepinExExample.csproj` (Roslyn, made optional), `README.md`.

### `ConnectorLib.JSON` — the shared protocol library

Purely additive; no existing file changed. `CustomEffectDefinition.cs` (the effect record) and
`CustomEffectsRpc.cs` (helpers building the `addEffects` / `removeEffect` calls over the existing
`RpcRequest`).

An earlier draft added a dedicated `CustomEffectsUpdate` message on a new response byte. That was
removed once it became clear the app already relays an `addEffects` RPC end to end, so this feature
adds no new protocol surface.

### `crowd-control` — the backend

One change, 19 lines: a `preserveExisting` flag on `AddEffectsRpcCallDataSchema`, threaded through
`rpc.ts` into `processCreateEffect`, which now skips effect IDs the streamer already has. Checked
before the clone is resolved, so a stale clone target can't fail a call that would have changed
nothing, and it returns early when nothing new remains — so a game re-registering the same effects
every launch no longer rewrites storage or triggers a menu resync.

Without this, a mod reporting the same effects on each launch reset the streamer's prices every
session.

### Verified

The mod solution builds clean with and without the compiler bundled, and with
`CUSTOM_EFFECTS_SUPPORTED` both on and off, checked against Unity 2021.3 and BepInEx 5.4.21
reference assemblies. The ID scheme was exercised out of the compiled assembly to confirm stability,
collision behaviour, sanitisation, and length bounds. The backend change typechecks, is
prettier-clean, and introduces no new lint findings.

### Not yet verified

Roslyn's behaviour on a real game's Mono runtime, and that `args` reach the connector as
`EffectRequest.arguments` in practice. Both fail safe: a compiler that won't load logs a clear message
pointing at prebuilt DLLs, and handler resolution falls back to the effect code.