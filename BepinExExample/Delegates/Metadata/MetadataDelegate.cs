using ConnectorLib.JSON;

namespace CrowdControl.Delegates.Metadata;

/// <summary>Represents a delegate that returns the metadata for an effect response or direct metadata request.</summary>
/// <param name="mod">The Crowd Control mod object.</param>
/// <returns>The requested metadata.</returns>
public delegate DataResponse MetadataDelegate(CrowdControlMod mod);