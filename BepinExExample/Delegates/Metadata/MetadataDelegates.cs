using ConnectorLib.JSON;
using Framework;

//Everything in the Metadata namespace is free-form and just needs to have static methods with the Metadata attribute
//non-effect helper methods are allowed and encouraged - kat
namespace CrowdControl.Delegates.Metadata;

/// <summary>Contains the metadata delegates.</summary>
/// <remarks>This entire file is game-specific and everything here (including the class itself) can be changed or removed.</remarks>
public static class MetadataDelegates
{
    //everything in this list will be automatically included as metadata in every effect response
    public static readonly string[] CommonMetadata = ["levelTime"];
    
    [Metadata("levelTime")]
    public static DataResponse LevelTime(CrowdControlMod mod)
    {
        const string KEY = "levelTime";
        try
        {
            float? levelTime = SingletonBehaviour<GameplayManager>.Instance?.CurrentLevelStats?.LevelTime;
            if (levelTime == null) return DataResponse.Failure(KEY, "Couldn't find health component.");

            return DataResponse.Success(KEY, levelTime);
        }
        catch (Exception e)
        {
            CrowdControlMod.Instance.Logger.LogError($"Crowd Control Error: {e}");
            return DataResponse.Failure(KEY, e, "The plugin encountered an internal error. Check the game logs for more information.");
        };
    }
}