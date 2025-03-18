using ConnectorLib.JSON;

namespace CrowdControl.Delegates.Metadata;

/// <summary>
/// Represents a delegate that returns the metadata for an effect response.
/// </summary>
/// <param name="client">The control client.</param>
/// <returns>The effect response metadata.</returns>
public delegate DataResponse MetadataDelegate(ControlClient client);