using System.Reflection;

namespace CrowdControl.Delegates.Effects;

/// <summary>
/// An effect delegate container.
/// </summary>
public static class EffectLoader
{
    private const BindingFlags BINDING_FLAGS = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    /*
     * this does not need to be explicitly filled out, it is done automatically
     * by reflection in the static constructor
     *
     * just make sure to add the [Effect] attribute to your methods
     */
    public static readonly Dictionary<string, EffectDelegate> Delegates = new();

    static EffectLoader()
    {
        foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
        {
            try
            {
                foreach (MethodInfo methodInfo in type.GetMethods(BINDING_FLAGS))
                {
                    try
                    {
                        foreach (EffectAttribute attribute in methodInfo.GetCustomAttributes<EffectAttribute>())
                        {
                            foreach (string id in attribute.IDs)
                            {
                                try
                                {
                                    Delegates[id] = (EffectDelegate)Delegate.CreateDelegate(typeof(EffectDelegate), methodInfo);
                                }
                                catch (Exception e)
                                {
                                    CCMod.mls.LogError(e);
                                }
                            }
                        }
                    }
                    catch {/**/}
                }
            }
            catch {/**/}
        }
    }
}