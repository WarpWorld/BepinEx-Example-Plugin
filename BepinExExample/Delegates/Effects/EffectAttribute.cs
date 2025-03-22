namespace CrowdControl.Delegates.Effects;

/// <summary>Attribute used to mark a method for starting an effect with the specified ID(s).</summary>
[AttributeUsage(AttributeTargets.Class)]
public class EffectAttribute(string[] ids, SITimeSpan defaultDuration, string[] conflicts) : Attribute
{
    /// <summary>
    /// Gets the IDs associated with the effect.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public IReadOnlyList<string> IDs { get; } = ids;

    /// <summary>
    /// The duration of the effect, if applicable.
    /// </summary>
    public SITimeSpan DefaultDuration { get; } = defaultDuration;

    /// <summary>
    /// All conflicting effect IDs, if applicable.
    /// </summary>
    public IReadOnlyList<string> Conflicts { get; } = conflicts;

    public EffectAttribute(params IEnumerable<string> ids) : this(ids.ToArray(), SITimeSpan.Zero, []) { }

    public EffectAttribute(params string[] ids) : this(ids.ToArray(), SITimeSpan.Zero, []) { }

    public EffectAttribute(string[] ids, float defaultDuration, string[] conflicts) : this(ids, (SITimeSpan)defaultDuration, conflicts) { }
    
    public EffectAttribute(string[] ids, float defaultDuration, string conflict) : this(ids, defaultDuration, [conflict]) { }

    public EffectAttribute(string id) : this([id], SITimeSpan.Zero, []) { }

    public EffectAttribute(string id, float defaultDuration) : this([id], defaultDuration, (SITimeSpan.Zero > 0) ? [id] : []) { }

    public EffectAttribute(string id, float defaultDuration, string conflict) : this([id], defaultDuration, [conflict]) { }

    public EffectAttribute(string id, float defaultDuration, string[] conflicts) : this([id], defaultDuration, conflicts) { }

    public EffectAttribute(string id, float defaultDuration, bool selfConflict) : this([id], defaultDuration, selfConflict ? [id] : []) { }

    public EffectAttribute(string[] ids, float defaultDuration, bool selfConflict) : this(ids, defaultDuration, selfConflict ? ids : []) { }

    public EffectAttribute(string id, bool selfConflict) : this([id], SITimeSpan.Zero, selfConflict ? [id] : []) { }

    public EffectAttribute(string[] ids, bool selfConflict) : this(ids, SITimeSpan.Zero, selfConflict ? ids : []) { }
}