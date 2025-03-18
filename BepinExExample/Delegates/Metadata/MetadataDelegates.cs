using ConnectorLib.JSON;
using Framework;

//Everything in the Metadata namespace is free-form and just needs to have static methods with the Metadata attribute
//non-effect helper methods are allowed and encouraged - kat
namespace CrowdControl.Delegates.Metadata;

/// <summary>
/// Contains the metadata delegates.
/// </summary>
/// <remarks>This entire file is game-specific and everything here (including the class itself) can be renamed or removed.</remarks>
public static class MetadataDelegates
{
    [Metadata("levelTime")]
    public static DataResponse LevelTime(ControlClient client)
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
            CCMod.Instance.Logger.LogError($"Crowd Control Error: {e}");
            return DataResponse.Failure(KEY, e, "The plugin encountered an internal error. Check the game logs for more information.");
        };
    }
}