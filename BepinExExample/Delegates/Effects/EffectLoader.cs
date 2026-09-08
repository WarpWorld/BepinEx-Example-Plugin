using System.Reflection;

namespace CrowdControl.Delegates.Effects;

/// <summary>An effect delegate container.</summary>
public class EffectLoader
{
    /// <summary>Provides a mapping of effect IDs to their respective delegates.</summary>
    /// <remarks>
    /// This should not need to be explicitly filled out, it is done automatically via reflection in the constructor.
    /// Just make sure to add the [Effect] attribute to your classes.
    /// <para>
    /// This holds effects declared by the game pack only. Custom effects loaded from disk live in
    /// <see cref="Handlers"/> instead, so that menu messages like
    /// <see cref="NetworkClient.ShowAllEffects"/> never send the client an ID its pack has never heard of.
    /// </para>
    /// </remarks>
    public readonly Dictionary<string, Effect> Effects = new();

    /// <summary>Provides a mapping of custom effect handler IDs to their respective delegates.</summary>
    /// <remarks>
    /// A custom effect is purchased through a cloned menu entry, so the request arrives carrying the
    /// clone target's effect code. The handler ID travels in
    /// <see cref="ConnectorLib.JSON.EffectRequest.arguments"/> and is what actually identifies which
    /// effect to run.
    /// </remarks>
    public readonly Dictionary<string, Effect> Handlers = new(StringComparer.Ordinal);

    private readonly CrowdControlMod m_mod;
    private readonly NetworkClient m_client;

    /// <summary>An effect that was successfully registered, along with the metadata it declared.</summary>
    /// <param name="id">The ID the effect was registered under.</param>
    /// <param name="declaredID">The ID the effect declared on its <see cref="EffectAttribute"/>.</param>
    /// <param name="effect">The effect instance.</param>
    public readonly struct Registration(string id, string declaredID, Effect effect)
    {
        /// <summary>The ID the effect was registered under.</summary>
        /// <remarks>For a custom effect this is derived from the effect's identity, so it differs from <see cref="DeclaredID"/>.</remarks>
        public string ID { get; } = id;

        /// <summary>The ID the effect declared on its <see cref="EffectAttribute"/>.</summary>
        public string DeclaredID { get; } = declaredID;

        /// <summary>The effect instance. Shared between all IDs the effect class declares.</summary>
        public Effect Effect { get; } = effect;

        /// <summary>The effect's behavioural metadata (duration and conflicts).</summary>
        public EffectAttribute Attribute => Effect.EffectAttribute;

        /// <summary>The effect's menu metadata, if it declared any.</summary>
        public EffectMenuAttribute? Menu { get; } = effect.GetType().GetCustomAttribute<EffectMenuAttribute>(false);
    }

    /// <summary>
    /// Automatically loads all effect delegates from the mod assembly.
    /// </summary>
    public EffectLoader(CrowdControlMod mod, NetworkClient client)
    {
        m_mod = mod;
        m_client = client;
        RegisterAssembly(Assembly.GetExecutingAssembly());
    }

    /// <summary>Instantiates and registers every effect class in the supplied assembly.</summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <param name="custom">
    /// True to register the effects as custom effects under <see cref="Handlers"/>, keyed by an ID
    /// derived from each effect's identity, rather than as pack effects under <see cref="Effects"/>.
    /// </param>
    /// <returns>The effects that were registered.</returns>
    /// <remarks>Effects that fail to load are logged and skipped - one bad effect must not cost us the rest.</remarks>
    public List<Registration> RegisterAssembly(Assembly assembly, bool custom = false)
    {
        List<Registration> registered = new();

        //abstract classes are skipped so intermediate base classes can be used to share code between effects
        foreach (Type type in SafeGetTypes(assembly).Where(type => type.IsSubclassOf(typeof(Effect)) && !type.IsAbstract))
        {
            try
            {
                foreach (EffectAttribute attribute in type.GetCustomAttributes<EffectAttribute>())
                {
                    Effect? effect = null;
                    foreach (string id in attribute.IDs)
                    {
                        try
                        {
                            //one instance per class, shared by every ID it declares
                            effect ??= (Effect)Activator.CreateInstance(type, m_mod, m_client);

                            if (!custom)
                            {
                                Effects[id] = effect;
                                registered.Add(new(id, id, effect));
                                continue;
                            }

                            //derived rather than declared, so the same effect gets the same ID on
                            //every machine and the streamer's settings follow it around
                            EffectMenuAttribute? menu = type.GetCustomAttribute<EffectMenuAttribute>(false);
                            string handlerID = CustomEffects.CustomEffectID.Build(id, menu, attribute.DefaultDuration > 0);

                            if (!IsValidEffectID(handlerID))
                            {
                                m_mod.Logger.LogError($"Custom effect \"{type.FullName}\" produced an unusable ID \"{handlerID}\". Effect IDs may only contain letters, numbers, and underscores, and must stay under 150 characters.");
                                continue;
                            }
                            if (Handlers.TryGetValue(handlerID, out Effect existing) && !ReferenceEquals(existing, effect))
                            {
                                m_mod.Logger.LogError($"Custom effect \"{type.FullName}\" resolves to the same ID as \"{existing.GetType().FullName}\". Give one of them a different name or author. Skipping it.");
                                continue;
                            }

                            Handlers[handlerID] = effect;
                            registered.Add(new(handlerID, id, effect));
                        }
                        catch (MissingMethodException)
                        {
                            //by far the most common mistake in a hand-written effect, and the raw
                            //exception does not say what the constructor should look like
                            m_mod.Logger.LogError($"Effect \"{type.FullName}\" could not be created because it has no matching constructor. Add: public {type.Name}(CrowdControlMod mod, NetworkClient client) : base(mod, client) {{ }}");
                        }
                        catch (Exception e) { m_mod.Logger.LogError(e); }
                    }
                }
            }
            catch (Exception e) { m_mod.Logger.LogError(e); }
        }

        return registered;
    }

    /// <summary>Removes the supplied custom effects.</summary>
    /// <param name="handlerIDs">The IDs the effects were registered under.</param>
    public void UnregisterHandlers(IEnumerable<string> handlerIDs)
    {
        foreach (string id in handlerIDs) Handlers.Remove(id);
    }

    /// <summary>Resolves an incoming request to an effect, by handler ID if one was supplied and by effect code otherwise.</summary>
    /// <param name="request">The incoming request.</param>
    /// <param name="effect">The effect that should service the request.</param>
    /// <returns>True if an effect was found, false otherwise.</returns>
    /// <remarks>
    /// The handler ID is checked first so that a custom effect always wins over the dispatch template
    /// it was cloned from. A request with no handler is an ordinary pack effect.
    /// </remarks>
    public bool TryResolve(ConnectorLib.JSON.EffectRequest request, out Effect effect)
    {
        string? handler = CustomEffects.CustomEffectManager.ReadHandlerID(request);
        if ((handler != null) && Handlers.TryGetValue(handler, out effect))
            return true;

        return Effects.TryGetValue(request.code ?? string.Empty, out effect);
    }

    /// <summary>Checks an effect ID against the rules the Crowd Control API enforces on custom effect IDs.</summary>
    /// <param name="id">The fully-prefixed effect ID.</param>
    /// <returns>True if the ID is usable, false otherwise.</returns>
    public static bool IsValidEffectID(string? id)
    {
        if (string.IsNullOrEmpty(id) || (id!.Length > 150)) return false;
        foreach (char c in id)
        {
            if (c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_') continue;
            return false;
        }
        return true;
    }

    /// <summary>Gets the types in an assembly, tolerating the ones that fail to load.</summary>
    /// <remarks>
    /// A custom effect compiled against a since-updated game will throw here for the types it can no
    /// longer resolve. The ones that still load are worth keeping.
    /// </remarks>
    private IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException e)
        {
            m_mod.Logger.LogWarning($"Some types in \"{assembly.GetName().Name}\" could not be loaded, and the effects in them will be unavailable. This usually means the code was built against a different version of the game.");
            foreach (Exception? inner in e.LoaderExceptions.Take(5))
                if (inner != null) m_mod.Logger.LogWarning($"  {inner.Message}");
            return e.Types.Where(t => t != null)!;
        }
        catch (Exception e)
        {
            m_mod.Logger.LogError(e);
            return Array.Empty<Type>();
        }
    }
}
