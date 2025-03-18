using System.Reflection;

namespace CrowdControl.Delegates.Metadata;

/// <summary>
/// A metadata delegate container.
/// </summary>
public static class MetadataLoader
{
    public static readonly string[] CommonMetadata = ["levelTime"];

    private const BindingFlags BINDING_FLAGS = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    /*
     * this does not need to be explicitly filled out, it is done automatically
     * by reflection in the static constructor
     *
     * just make sure to add the [Metadata] attribute to your methods
     */
    public static readonly Dictionary<string, MetadataDelegate> Metadata = new();

    static MetadataLoader()
    {
        foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
        {
            try
            {
                foreach (MethodInfo methodInfo in type.GetMethods(BINDING_FLAGS))
                {
                    try
                    {
                        foreach (MetadataAttribute attribute in methodInfo.GetCustomAttributes<MetadataAttribute>())
                        {
                            foreach (string id in attribute.IDs)
                            {
                                try
                                {
                                    Metadata[id] = (MetadataDelegate)Delegate.CreateDelegate(typeof(MetadataDelegate), methodInfo);
                                }
                                catch (Exception e)
                                {
                                    CCMod.Instance.Logger.LogError(e);
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