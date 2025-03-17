namespace CrowdControl.Delegates.Effects;

/// <summary>
/// Attribute used to mark a method as an effect with the specified IDs.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class EffectAttribute(params string[] ids) : Attribute
{
    /// <summary>
    /// Gets the IDs associated with the effect.
    /// </summary>
    public string[] IDs { get; } = ids;
}
