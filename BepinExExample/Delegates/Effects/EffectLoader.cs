using System.Reflection;

namespace CrowdControl.Delegates.Effects;

/// <summary>An effect delegate container.</summary>
public class EffectLoader
{
    private const BindingFlags BINDING_FLAGS = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    
    /// <summary>Provides a mapping of effect IDs to their respective delegates.</summary>
    /// <remarks>
    /// This should not need to be explicitly filled out, it is done automatically via reflection in the static constructor.
    /// Just make sure to add the [Effect] attribute to your methods.
    /// </remarks>
    public readonly Dictionary<string, Effect> Effects = new();

    /// <summary>
    /// Automatically loads all effect delegates from the assembly.
    /// </summary>
    public EffectLoader(CrowdControlMod mod, NetworkClient client)
    {
        foreach (Type type in Assembly.GetExecutingAssembly().GetTypes().Where(type => type.IsSubclassOf(typeof(Effect))))
        {
            try
            {
                foreach (EffectAttribute attribute in type.GetCustomAttributes<EffectAttribute>())
                {
                    foreach (string id in attribute.IDs)
                    {
                        try { Effects[id] = (Effect)Activator.CreateInstance(type, mod, client); }
                        catch (Exception e) { CrowdControlMod.Instance.Logger.LogError(e); }
                    }
                }
            }
            catch (Exception e) { CrowdControlMod.Instance.Logger.LogError(e); }
        }
    }
}