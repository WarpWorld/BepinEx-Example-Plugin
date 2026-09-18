using System.IO;
using System.Reflection;
using ConnectorLib.JSON;
using CrowdControl.Delegates.Effects;
using Newtonsoft.Json.Linq;

namespace CrowdControl.CustomEffects;

/// <summary>Loads custom effects from disk and keeps the client's menu in step with them.</summary>
/// <remarks>
/// Custom effects are ordinary <see cref="Effect"/> subclasses that happen to have been compiled at
/// runtime, so everything downstream of registration - conflicts, timed countdowns, pausing when the
/// game is not ready - treats them identically to the effects built into the mod.
/// <para>
/// Loading has to happen on the game thread, because an effect's constructor is free to touch Unity.
/// The file watcher therefore only sets a flag, and the actual work happens in <see cref="Tick"/>.
/// </para>
/// </remarks>
public class CustomEffectManager : IDisposable
{
    private readonly CrowdControlMod m_mod;
    private readonly NetworkClient m_client;

    private readonly Dictionary<string, LoadedPack> m_loaded = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CustomEffectDefinition> m_manifest = new(StringComparer.Ordinal);
    private readonly HashSet<string> m_reportedChanges = new(StringComparer.Ordinal);

    private FileSystemWatcher? m_watcher;
    private volatile bool m_changeSeen;
    private volatile bool m_manifestDirty;
    private bool m_rescanPending;
    private float m_rescanAt;
    private bool m_disposed;

    /// <summary>How long to wait after a file change before acting on it, so a save-in-progress is not read half-written.</summary>
    private const float RESCAN_DELAY_SECONDS = 1.5f;

    /// <summary>A pack that has been compiled, loaded, and registered.</summary>
    private class LoadedPack(CustomEffectPack pack, List<EffectLoader.Registration> registrations)
    {
        public CustomEffectPack Pack { get; set; } = pack;
        public List<EffectLoader.Registration> Registrations { get; set; } = registrations;
    }

    /// <summary>Creates the custom effect manager.</summary>
    /// <param name="mod">The Crowd Control game mod object.</param>
    /// <param name="client">The client connection.</param>
    public CustomEffectManager(CrowdControlMod mod, NetworkClient client)
    {
        m_mod = mod;
        m_client = client;
    }

    /// <summary>The number of custom effects currently loaded.</summary>
    public int Count => m_manifest.Count;

    /// <summary>Discovers, compiles, and registers every pack in the custom effects folder. Must be called on the game thread.</summary>
    public void LoadAll()
    {
        //checked before the setting, and before the folder is created, so that a game which has not
        //adopted custom effects neither advertises the folder nor runs anything found in one
#pragma warning disable CS0162 // Unreachable code detected
        if (!CrowdControlMod.CUSTOM_EFFECTS_SUPPORTED)
        {
            WarnIfEffectsArePresentButUnsupported();
            return;
        }

        if (!UI.ModSettings.AllowCustomEffects)
        {
            m_mod.Logger.LogInfo("Custom effects are turned off in the mod settings, so the custom effects folder was ignored.");
            return;
        }

        string folder = CustomEffectPaths.GameFolder;

        try { EnsureFolder(folder); }
        catch (Exception e)
        {
            m_mod.Logger.LogError($"Could not prepare the custom effects folder \"{folder}\": {e}");
            return;
        }

        m_mod.Logger.LogInfo($"Loading custom effects from \"{folder}\".");

        foreach (CustomEffectPack pack in CustomEffectPack.Discover(folder, m_mod.Logger))
            LoadPack(pack);

        RebuildManifest();
        StartWatching(folder);
#pragma warning restore CS0162 // Unreachable code detected
    }

    /// <summary>Applies pending file changes and pushes the manifest when the client is listening. Must be called on the game thread.</summary>
    public void Tick()
    {
        if (m_disposed) return;

        //the deadline is computed here rather than in the watcher callback, because Unity's clock may
        //only be read from the game thread. Each new change pushes it back, so a folder being written
        //to is left alone until it settles.
        if (m_changeSeen)
        {
            m_changeSeen = false;
            m_rescanAt = UnityEngine.Time.realtimeSinceStartup + RESCAN_DELAY_SECONDS;
            m_rescanPending = true;
        }

        if (m_rescanPending && (UnityEngine.Time.realtimeSinceStartup >= m_rescanAt))
        {
            m_rescanPending = false;
            try { Rescan(); }
            catch (Exception e) { m_mod.Logger.LogError(e); }
        }

        if (m_manifestDirty && m_client.Connected)
        {
            m_manifestDirty = false;
            SendManifest();
        }
    }

    /// <summary>Asks for the manifest to be sent again, after a reconnect.</summary>
    /// <remarks>Safe to call from any thread - this only sets a flag.</remarks>
    public void RequestManifestResend() => m_manifestDirty = true;

    /// <summary>Reads the custom effect handler ID out of an incoming request, if it has one.</summary>
    /// <param name="request">The incoming request.</param>
    /// <returns>The handler ID, or null if this is not a custom effect request.</returns>
    public static string? ReadHandlerID(EffectRequest? request)
    {
        if (request == null) return null;

        if ((request.arguments != null) &&
            request.arguments.TryGetValue(CustomEffectPaths.HANDLER_ARG, out object? value) &&
            (value != null))
        {
            string? handler = (value as string) ?? value.ToString();
            if (!string.IsNullOrWhiteSpace(handler)) return handler;
        }

        //some request paths carry effect arguments as parameters instead
        if (request.parameters is JObject parameters &&
            parameters.TryGetValue(CustomEffectPaths.HANDLER_ARG, out JToken? token))
        {
            string? handler = token?.ToString();
            if (!string.IsNullOrWhiteSpace(handler)) return handler;
        }

        return null;
    }

    /// <summary>Gets the menu name of a custom effect, for the on-screen overlay.</summary>
    /// <param name="request">The request that started the effect.</param>
    /// <param name="name">The name shown in the streamer's menu.</param>
    /// <returns>True if the request was for a custom effect, false otherwise.</returns>
    /// <remarks>
    /// Without this the overlay would label every custom effect with the name of the dispatch
    /// template they were cloned from, which is the same name for all of them.
    /// </remarks>
    public bool TryGetDisplayName(EffectRequest? request, out string name)
    {
        name = string.Empty;
        string? handler = ReadHandlerID(request);
        if (handler == null) return false;
        if (!m_manifest.TryGetValue(handler, out CustomEffectDefinition definition)) return false;

        name = definition.name;
        return true;
    }

    /// <summary>Says something when there are effects on disk that this mod will never load.</summary>
    /// <remarks>
    /// Silence here would be indistinguishable from a broken effect, and someone who went to the
    /// trouble of writing one deserves to know the game rather than their code is the reason.
    /// </remarks>
    private void WarnIfEffectsArePresentButUnsupported()
    {
        try
        {
            string folder = CustomEffectPaths.GameFolder;
            if (!Directory.Exists(folder)) return;
            if (CustomEffectPack.Discover(folder, m_mod.Logger).Count == 0) return;

            m_mod.Logger.LogWarning(
                $"There are custom effects in \"{folder}\", but {CrowdControlMod.MOD_NAME} does not support them, " +
                "so none of them were loaded. Custom effect support is enabled per game by the mod's author.");
        }
        catch {/* a folder we cannot read is exactly as unsupported as one that is not there */}
    }

    private void LoadPack(CustomEffectPack pack)
    {
        List<EffectLoader.Registration> registrations = new();

        foreach (Assembly assembly in LoadAssemblies(pack))
        {
            try { registrations.AddRange(m_mod.EffectLoader.RegisterAssembly(assembly, custom: true)); }
            catch (Exception e) { m_mod.Logger.LogError($"Custom effect pack \"{pack.DisplayName}\" failed to register: {e}"); }
        }

        if (registrations.Count == 0)
        {
            m_mod.Logger.LogWarning($"Custom effect pack \"{pack.DisplayName}\" loaded but contains no effects. An effect needs to be a public non-abstract class inheriting Effect, with an [Effect] attribute.");
            return;
        }

        m_loaded[pack.ID] = new(pack, registrations);
        m_mod.Logger.LogMessage($"Loaded custom effect pack \"{pack.DisplayName}\" ({registrations.Count} effect(s), hash {pack.Hash.Substring(0, 12)}).");
    }

    /// <summary>Gets every assembly belonging to a pack, compiling its sources if the cache is cold.</summary>
    private List<Assembly> LoadAssemblies(CustomEffectPack pack)
    {
        List<Assembly> assemblies = new();

        //prebuilt assemblies are read into memory rather than loaded in place, so that replacing the
        //file later does not require the streamer to work out why it is locked
        foreach (string file in pack.AssemblyFiles)
        {
            //a pack that ships a copy of the mod or of Newtonsoft alongside its own DLL would
            //otherwise load a second copy of it, and the resulting type mismatches are miserable to
            //diagnose from a support log
            string name = Path.GetFileNameWithoutExtension(file);
            if (AppDomain.CurrentDomain.GetAssemblies().Any(a =>
                    string.Equals(a.GetName().Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                m_mod.Logger.LogInfo($"Ignoring \"{Path.GetFileName(file)}\" in \"{pack.DisplayName}\" because the game already has that assembly loaded.");
                continue;
            }

            try { assemblies.Add(Assembly.Load(File.ReadAllBytes(file))); }
            catch (Exception e) { m_mod.Logger.LogError($"Could not load \"{Path.GetFileName(file)}\": {e.Message}"); }
        }

        if (pack.SourceFiles.Count == 0) return assemblies;

        Assembly? compiled = LoadCompiled(pack);
        if (compiled != null) assemblies.Add(compiled);

        return assemblies;
    }

    private Assembly? LoadCompiled(CustomEffectPack pack)
    {
        string cacheDirectory = CustomEffectPaths.CacheFolder;
        string cachedAssembly = Path.Combine(cacheDirectory, $"{pack.ID}.{pack.Hash}.dll");
        string cachedSymbols = Path.ChangeExtension(cachedAssembly, ".pdb");

        try
        {
            if (File.Exists(cachedAssembly))
                return Load(File.ReadAllBytes(cachedAssembly), File.Exists(cachedSymbols) ? File.ReadAllBytes(cachedSymbols) : null);
        }
        catch (Exception e)
        {
            m_mod.Logger.LogWarning($"Cached build of \"{pack.DisplayName}\" could not be used, rebuilding it: {e.Message}");
        }

        m_mod.Logger.LogInfo($"Compiling custom effect pack \"{pack.DisplayName}\" ({pack.SourceFiles.Count} file(s))...");

        CustomEffectCompiler.Result result;
        try { result = CustomEffectCompiler.Compile(pack); }
        catch (Exception e)
        {
            //the most likely cause is the compiler assemblies missing from the mod folder
            m_mod.Logger.LogError($"The C# compiler could not be started, so \"{pack.DisplayName}\" cannot be built from source. Ship the pack as a prebuilt DLL instead, or reinstall the mod. ({e.Message})");
            return null;
        }

        foreach (string diagnostic in result.Diagnostics.Take(50))
        {
            if (result.Succeeded) m_mod.Logger.LogWarning($"[{pack.DisplayName}] {diagnostic}");
            else m_mod.Logger.LogError($"[{pack.DisplayName}] {diagnostic}");
        }

        if (!result.Succeeded)
        {
            m_mod.Logger.LogError($"Custom effect pack \"{pack.DisplayName}\" did not compile and will not be loaded.");
            return null;
        }

        try
        {
            Directory.CreateDirectory(cacheDirectory);
            File.WriteAllBytes(cachedAssembly, result.Assembly!);
            if (result.Symbols != null) File.WriteAllBytes(cachedSymbols, result.Symbols);
            PruneCache(cacheDirectory, pack);
        }
        catch (Exception e)
        {
            //a cache we cannot write just means compiling again next launch
            m_mod.Logger.LogWarning($"Could not cache the compiled build of \"{pack.DisplayName}\": {e.Message}");
        }

        return Load(result.Assembly!, result.Symbols);
    }

    private Assembly? Load(byte[] assembly, byte[]? symbols)
    {
        try { return (symbols != null) ? Assembly.Load(assembly, symbols) : Assembly.Load(assembly); }
        catch (Exception e)
        {
            m_mod.Logger.LogError($"A custom effect assembly could not be loaded: {e}");
            return null;
        }
    }

    /// <summary>Deletes cached builds of previous versions of a pack.</summary>
    private static void PruneCache(string cacheDirectory, CustomEffectPack pack)
    {
        foreach (string stale in Directory.GetFiles(cacheDirectory, $"{pack.ID}.*"))
        {
            if (Path.GetFileName(stale).IndexOf(pack.Hash, StringComparison.OrdinalIgnoreCase) >= 0) continue;
            try { File.Delete(stale); }
            catch {/* a locked cache file is not worth reporting */}
        }
    }

    private void StartWatching(string folder)
    {
        try
        {
            m_watcher = new(folder)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName
            };

            m_watcher.Changed += OnFolderChanged;
            m_watcher.Created += OnFolderChanged;
            m_watcher.Deleted += OnFolderChanged;
            m_watcher.Renamed += OnFolderChanged;
            m_watcher.EnableRaisingEvents = true;
        }
        catch (Exception e)
        {
            //without a watcher the folder is simply read once at startup, which is not fatal
            m_mod.Logger.LogWarning($"Could not watch the custom effects folder for changes: {e.Message}");
        }
    }

    private void OnFolderChanged(object sender, FileSystemEventArgs e)
    {
        //this fires on a thread pool thread, so all it may safely do is ask Tick to look again
        string? extension = Path.GetExtension(e.FullPath);
        bool interesting =
            string.IsNullOrEmpty(extension) ||
            string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase);

        if (!interesting) return;
        if (e.FullPath.IndexOf($"{Path.DirectorySeparatorChar}.cache{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) >= 0) return;

        m_changeSeen = true;
    }

    /// <summary>Reconciles the loaded packs with what is currently on disk.</summary>
    /// <remarks>
    /// Adding a pack works at runtime because registration is purely additive. Changing one does not:
    /// Mono cannot unload an assembly, so the old code stays resident. Replacing a pack in place is
    /// therefore opt-in, for the person writing the effect rather than the person streaming it.
    /// </remarks>
    private void Rescan()
    {
        List<CustomEffectPack> onDisk = CustomEffectPack.Discover(CustomEffectPaths.GameFolder, m_mod.Logger);
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        bool changed = false;

        foreach (CustomEffectPack pack in onDisk)
        {
            seen.Add(pack.ID);

            if (!m_loaded.TryGetValue(pack.ID, out LoadedPack loaded))
            {
                LoadPack(pack);
                changed |= m_loaded.ContainsKey(pack.ID);
                continue;
            }

            if (string.Equals(loaded.Pack.Hash, pack.Hash, StringComparison.OrdinalIgnoreCase)) continue;

            if (!UI.ModSettings.CustomEffectDevReload)
            {
                //log once per version, or an editor that saves on every keystroke would flood the log
                if (m_reportedChanges.Add(pack.Hash))
                    m_mod.Logger.LogMessage($"Custom effect pack \"{pack.DisplayName}\" has changed. Restart the game to load the new version.");
                continue;
            }

            m_mod.Logger.LogMessage($"Reloading custom effect pack \"{pack.DisplayName}\". The previous version stays in memory until the game restarts.");
            m_mod.EffectLoader.UnregisterHandlers(loaded.Registrations.Select(r => r.ID));
            m_loaded.Remove(pack.ID);
            LoadPack(pack);
            changed = true;
        }

        foreach (string id in m_loaded.Keys.Where(id => !seen.Contains(id)).ToList())
        {
            LoadedPack removed = m_loaded[id];
            m_mod.Logger.LogMessage($"Custom effect pack \"{removed.Pack.DisplayName}\" was removed. Its menu entries will be hidden, and the streamer's settings for them are kept in case it comes back.");
            m_mod.EffectLoader.UnregisterHandlers(removed.Registrations.Select(r => r.ID));
            m_loaded.Remove(id);
            changed = true;
        }

        if (changed) RebuildManifest();
    }

    private void RebuildManifest()
    {
        m_manifest.Clear();

        foreach (LoadedPack loaded in m_loaded.Values)
        {
            foreach (EffectLoader.Registration registration in loaded.Registrations)
            {
                if (registration.Menu == null)
                {
                    m_mod.Logger.LogWarning($"Custom effect \"{registration.ID}\" has no [EffectMenu] attribute, so its menu entry had to be guessed. Add one to control its name, price, and description.");
                }

                try { m_manifest[registration.ID] = CustomEffectManifest.Build(registration, loaded.Pack); }
                catch (Exception e) { m_mod.Logger.LogError($"Could not describe custom effect \"{registration.ID}\": {e}"); }
            }
        }

        m_manifestDirty = true;
    }

    /// <summary>The most custom effects the Crowd Control API will hold for one game pack.</summary>
    private const int MAX_CUSTOM_EFFECTS = 75;

    /// <summary>Registers the current custom effects with the client and makes them visible for this session.</summary>
    /// <remarks>
    /// Whatever this mod has is not necessarily all the streamer owns: they may have played on
    /// another machine with a different set of files, and those effects still exist on their account
    /// with whatever prices they set. So the group is hidden first and only the effects actually
    /// loaded here are shown, which leaves the rest configured but unavailable rather than offering
    /// viewers effects that cannot run.
    /// <para>
    /// Nothing is ever deleted. Deleting would discard the streamer's settings, and is only ever
    /// their decision to make in the app.
    /// </para>
    /// </remarks>
    private void SendManifest()
    {
        CustomEffectDefinition[] definitions = [.. m_manifest.Values];
        string[] ids = [.. m_manifest.Keys];

        if (definitions.Length > MAX_CUSTOM_EFFECTS)
        {
            m_mod.Logger.LogError($"There are {definitions.Length} custom effects, but Crowd Control only holds {MAX_CUSTOM_EFFECTS} per game. Only the first {MAX_CUSTOM_EFFECTS} will be registered - remove some to choose which.");
            definitions = [.. definitions.Take(MAX_CUSTOM_EFFECTS)];
            ids = [.. definitions.Select(d => d.effectID)];
        }

        //one task rather than three, because these are only correct in order: an effect has to exist
        //before it can be shown, and the group has to be hidden before the survivors are shown or
        //the hide would immediately undo the show
        Task.Run(() =>
        {
            if (definitions.Length > 0) m_client.Send(CustomEffectsRpc.AddEffects(definitions));

            m_client.HideEffectGroups(CustomEffectPaths.GROUP);
            if (ids.Length > 0) m_client.ShowEffects(ids);
        }).Forget();

        m_mod.Logger.LogInfo($"Reported {ids.Length} custom effect(s) to the Crowd Control client.");
    }

    private void EnsureFolder(string folder)
    {
        if (Directory.Exists(folder)) return;

        Directory.CreateDirectory(folder);

        //a first-run reader is the only documentation some people will ever see
        File.WriteAllText(Path.Combine(folder, "README.txt"),
            $$"""
             Custom Crowd Control effects for {{CustomEffectPaths.GameName}}
             =============================================================

             Drop a .cs file in this folder, or a folder of .cs files if your effect needs more than
             one. Each folder is compiled on its own, so a mistake in one effect does not stop the
             others from loading. A prebuilt .dll works here too.

             Effects you write here are the same as the ones built into the mod: they can be instant
             or timed, and they can declare conflicts against each other or against the game's own
             effects to stop both running at once.

                 [Effect(id: "chaos_gravity", defaultDuration: 30, conflicts: ["low_gravity"])]
                 [EffectMenu("Chaos Gravity", 250, Description = "Gravity flips at random.")]
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
                         Physics.gravity = new Vector3(0, -9.81f, 0);
                         return EffectResponse.Finished(request.ID);
                     }
                 }

             Anything in this folder runs as part of the game, so only put things here that you
             trust. Every effect that loads is named in the game's log if you need to check what is
             running.

             Changing an effect needs a game restart to take hold. Adding a new one does not.
             """);
    }

    /// <summary>Stops watching the custom effects folder.</summary>
    public void Dispose()
    {
        if (m_disposed) return;
        m_disposed = true;

        try
        {
            if (m_watcher != null)
            {
                m_watcher.EnableRaisingEvents = false;
                m_watcher.Changed -= OnFolderChanged;
                m_watcher.Created -= OnFolderChanged;
                m_watcher.Deleted -= OnFolderChanged;
                m_watcher.Renamed -= OnFolderChanged;
                m_watcher.Dispose();
                m_watcher = null;
            }
        }
        catch {/**/}
    }
}
