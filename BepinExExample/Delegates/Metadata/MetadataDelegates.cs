//Everything in the Metadata namespace is free-form and just needs to have static methods with the Metadata attribute
//non-effect helper methods are allowed and encouraged - kat
namespace CrowdControl.Delegates.Metadata;

/// <summary>Contains the metadata delegates.</summary>
/// <remarks>This entire file is game-specific and everything here (including the class itself) can be changed or removed.</remarks>
public static class MetadataDelegates
{
    //everything in this list will be automatically included as metadata in every effect response
    //add the keys of your [Metadata] delegates here (e.g. "levelTime" in the commented example below)
    public static readonly string[] CommonMetadata = [];

    /* == EXAMPLE (Anger Foot) - a metadata delegate reporting the current level time ==
     * Metadata delegates are looked up by key. They are invoked to enrich every effect response
     * (if listed in CommonMetadata above) and to answer direct DataRequest queries from the client.
     * Uncomment and adapt this for your game.

    [Metadata("levelTime")]
    public static DataResponse LevelTime(CrowdControlMod mod)
    {
        const string KEY = "levelTime";
        try
        {
            float? levelTime = SingletonBehaviour<GameplayManager>.Instance?.CurrentLevelStats?.LevelTime;
            if (levelTime == null) return DataResponse.Failure(KEY, "Couldn't find the level timer.");

            return DataResponse.Success(KEY, levelTime);
        }
        catch (Exception e)
        {
            CrowdControlMod.Instance.Logger.LogError($"Crowd Control Error: {e}");
            return DataResponse.Failure(KEY, e, "The plugin encountered an internal error. Check the game logs for more information.");
        }
    }

    */
}
