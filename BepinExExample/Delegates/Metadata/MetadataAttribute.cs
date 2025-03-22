namespace CrowdControl.Delegates.Metadata;

/// <summary> Attribute used to mark a method as metadata with the specified IDs.</summary>
[AttributeUsage(AttributeTargets.Method)]
public class MetadataAttribute(params string[] ids) : Attribute
{
    /// <summary>
    /// Gets the IDs associated with the metadata.
    /// </summary>
    public string[] IDs { get; } = ids;
    
    public MetadataAttribute(string ids) : this([ids]) { }
    
    public MetadataAttribute(params IEnumerable<string> ids) : this(ids.ToArray()) { }
}