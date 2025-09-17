using UnityEngine;
using GlobalEnums;

namespace CrowdControl.Utilities
{
    /// <summary>
    /// Utility class for checking various game states that effects might need to consider
    /// </summary>
    public static class GameStateChecker
    {
        /// <summary>
        /// Checks if the player is currently transitioning between areas/rooms
        /// </summary>
        /// <returns>True if transitioning, false otherwise</returns>
        public static bool IsTransitioning()
        {
            var heroController = HeroController.instance;
            if (heroController == null) return false;
            
            return heroController.cState.transitioning;
        }

        /// <summary>
        /// Checks if the player is in a specific transition state
        /// </summary>
        /// <param name="state">The transition state to check for</param>
        /// <returns>True if in the specified state, false otherwise</returns>
        public static bool IsInTransitionState(HeroTransitionState state)
        {
            var heroController = HeroController.instance;
            if (heroController == null) return false;
            
            return heroController.transitionState == state;
        }

        /// <summary>
        /// Checks if the player is dead
        /// </summary>
        /// <returns>True if dead, false otherwise</returns>
        public static bool IsDead()
        {
            var heroController = HeroController.instance;
            if (heroController == null) return false;
            
            return heroController.cState.dead;
        }

        /// <summary>
        /// Checks if the player died from frost
        /// </summary>
        /// <returns>True if frost death, false otherwise</returns>
        public static bool IsFrostDeath()
        {
            var heroController = HeroController.instance;
            if (heroController == null) return false;
            
            return heroController.cState.isFrostDeath;
        }

        /// <summary>
        /// Checks if the player died from hazards
        /// </summary>
        /// <returns>True if hazard death, false otherwise</returns>
        public static bool IsHazardDeath()
        {
            var heroController = HeroController.instance;
            if (heroController == null) return false;
            
            return heroController.cState.hazardDeath;
        }

        /// <summary>
        /// Checks if the player is respawning from hazards
        /// </summary>
        /// <returns>True if hazard respawning, false otherwise</returns>
        public static bool IsHazardRespawning()
        {
            var heroController = HeroController.instance;
            if (heroController == null) return false;
            
            return heroController.cState.hazardRespawning;
        }

        /// <summary>
        /// Checks if the player is near a bench
        /// </summary>
        /// <returns>True if near bench, false otherwise</returns>
        public static bool IsNearBench()
        {
            var heroController = HeroController.instance;
            if (heroController == null) return false;
            
            return heroController.cState.nearBench;
        }

        /// <summary>
        /// Checks if the player is sitting at a bench
        /// </summary>
        /// <returns>True if at bench, false otherwise</returns>
        public static bool IsAtBench()
        {
            var playerData = PlayerData.instance;
            if (playerData == null) return false;
            
            return playerData.atBench;
        }

        /// <summary>
        /// Checks if the player is in a state where they can save (at bench or near bench)
        /// </summary>
        /// <returns>True if in saving state, false otherwise</returns>
        public static bool IsInSavingState()
        {
            return IsAtBench() || IsNearBench();
        }

        /// <summary>
        /// Checks if the game is currently in a saving process or state
        /// </summary>
        /// <returns>True if game is saving, false otherwise</returns>
        public static bool IsGameSaving()
        {
            // Check if saving is allowed by platform
            if (Platform.Current != null)
            {
                if (!Platform.Current.IsSavingAllowedByEngagement)
                {
                    return false; // Saving not allowed, so not saving
                }
                
                if (!Platform.Current.IsSaveStoreMounted)
                {
                    return false; // Save store not mounted, so not saving
                }
            }

            // Check if player is at a bench (primary save location)
            if (IsAtBench())
            {
                return true;
            }

            // Check if player is near a bench (approaching save location)
            if (IsNearBench())
            {
                return true;
            }

            // Check if game is paused (might be in menu where they can save)
            if (IsPaused())
            {
                return true;
            }

            // Check if player is in cutscene movement (might be in save menu)
            if (IsInCutsceneMovement())
            {
                return true;
            }

            // Try to detect saving through reflection (private fields)
            if (IsGameSavingThroughReflection())
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to detect saving through reflection of private fields
        /// </summary>
        /// <returns>True if saving is detected through reflection, false otherwise</returns>
        private static bool IsGameSavingThroughReflection()
        {
            try
            {
                var gameManager = GameManager.instance;
                if (gameManager == null) return false;

                var gameManagerType = gameManager.GetType();
                
                // Check isSaveGameQueued
                var isSaveGameQueuedField = gameManagerType.GetField("isSaveGameQueued", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (isSaveGameQueuedField != null)
                {
                    var isSaveGameQueued = (bool)isSaveGameQueuedField.GetValue(gameManager);
                    if (isSaveGameQueued)
                    {
                        CrowdControlMod.LogDebug("SAVING DETECTED: isSaveGameQueued = true");
                        return true;
                    }
                }

                // Check isAutoSaveQueued
                var isAutoSaveQueuedField = gameManagerType.GetField("isAutoSaveQueued", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (isAutoSaveQueuedField != null)
                {
                    var isAutoSaveQueued = (bool)isAutoSaveQueuedField.GetValue(gameManager);
                    if (isAutoSaveQueued)
                    {
                        CrowdControlMod.LogDebug("SAVING DETECTED: isAutoSaveQueued = true");
                        return true;
                    }
                }

                // Check if SavePersistentObjects event has subscribers (indicates saving might be happening)
                var savePersistentObjectsField = gameManagerType.GetField("SavePersistentObjects", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (savePersistentObjectsField != null)
                {
                    var savePersistentObjects = savePersistentObjectsField.GetValue(gameManager) as System.Action;
                    if (savePersistentObjects != null)
                    {
                        CrowdControlMod.LogDebug("SAVING DETECTED: SavePersistentObjects event has subscribers");
                        return true;
                    }
                }

                // Try to detect through other means - check if we're in a state where saving is likely
                // Check if player is in menu or UI state that might indicate saving
                if (IsInMenuOrUIState())
                {
                    CrowdControlMod.LogDebug("SAVING DETECTED: In menu/UI state");
                    return true;
                }
            }
            catch (System.Exception e)
            {
                CrowdControlMod.LogDebug($"Error detecting saving through reflection: {e.Message}");
            }

            return false;
        }

        /// <summary>
        /// Checks if the player is in a menu or UI state where they might be saving
        /// </summary>
        /// <returns>True if in menu/UI state, false otherwise</returns>
        private static bool IsInMenuOrUIState()
        {
            // Check if game is paused (indicates menu access)
            if (IsPaused())
            {
                return true;
            }

            // Check if player is in cutscene movement (might be in save menu)
            if (IsInCutsceneMovement())
            {
                return true;
            }

            // Check if player is at or near bench (can save)
            if (IsAtBench() || IsNearBench())
            {
                return true;
            }

            // Check if player is in walk zone (safe area where they might access menus)
            if (IsInWalkZone())
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if the player is in a walk zone
        /// </summary>
        /// <returns>True if in walk zone, false otherwise</returns>
        public static bool IsInWalkZone()
        {
            var heroController = HeroController.instance;
            if (heroController == null) return false;
            
            return heroController.cState.inWalkZone;
        }

        /// <summary>
        /// Checks if the game is paused
        /// </summary>
        /// <returns>True if paused, false otherwise</returns>
        public static bool IsPaused()
        {
            var gameManager = GameManager.instance;
            if (gameManager == null) return false;
            
            return gameManager.isPaused;
        }

        /// <summary>
        /// Checks if the player is in cutscene movement
        /// </summary>
        /// <returns>True if in cutscene movement, false otherwise</returns>
        public static bool IsInCutsceneMovement()
        {
            var heroController = HeroController.instance;
            if (heroController == null) return false;
            
            return heroController.cState.isInCutsceneMovement;
        }

        /// <summary>
        /// Checks if trigger events are paused
        /// </summary>
        /// <returns>True if trigger events paused, false otherwise</returns>
        public static bool IsTriggerEventsPaused()
        {
            var heroController = HeroController.instance;
            if (heroController == null) return false;
            
            return heroController.cState.isTriggerEventsPaused;
        }

        /// <summary>
        /// Checks if Needolin is playing memory
        /// </summary>
        /// <returns>True if Needolin playing memory, false otherwise</returns>
        public static bool IsNeedolinPlayingMemory()
        {
            var heroController = HeroController.instance;
            if (heroController == null) return false;
            
            return heroController.cState.needolinPlayingMemory;
        }

        /// <summary>
        /// Checks if the player is attacking
        /// </summary>
        /// <returns>True if attacking, false otherwise</returns>
        public static bool IsAttacking()
        {
            var heroController = HeroController.instance;
            if (heroController == null) return false;
            
            return heroController.cState.attacking;
        }

        /// <summary>
        /// Checks if the player is dashing
        /// </summary>
        /// <returns>True if dashing, false otherwise</returns>
        public static bool IsDashing()
        {
            var heroController = HeroController.instance;
            if (heroController == null) return false;
            
            return heroController.cState.dashing;
        }

        /// <summary>
        /// Checks if the player is back dashing
        /// </summary>
        /// <returns>True if back dashing, false otherwise</returns>
        public static bool IsBackDashing()
        {
            var heroController = HeroController.instance;
            if (heroController == null) return false;
            
            return heroController.cState.backDashing;
        }

        /// <summary>
        /// Checks if the player is wall sliding
        /// </summary>
        /// <returns>True if wall sliding, false otherwise</returns>
        public static bool IsWallSliding()
        {
            var heroController = HeroController.instance;
            if (heroController == null) return false;
            
            return heroController.cState.wallSliding;
        }

        /// <summary>
        /// Checks if the player is double jumping
        /// </summary>
        /// <returns>True if double jumping, false otherwise</returns>
        public static bool IsDoubleJumping()
        {
            var heroController = HeroController.instance;
            if (heroController == null) return false;
            
            return heroController.cState.doubleJumping;
        }

        /// <summary>
        /// Checks if the player is falling
        /// </summary>
        /// <returns>True if falling, false otherwise</returns>
        public static bool IsFalling()
        {
            var heroController = HeroController.instance;
            if (heroController == null) return false;
            
            return heroController.cState.falling;
        }

        /// <summary>
        /// Checks if the player is on the ground
        /// </summary>
        /// <returns>True if on ground, false otherwise</returns>
        public static bool IsOnGround()
        {
            var heroController = HeroController.instance;
            if (heroController == null) return false;
            
            return heroController.cState.onGround;
        }

        /// <summary>
        /// Checks if the player is recoiling
        /// </summary>
        /// <returns>True if recoiling, false otherwise</returns>
        public static bool IsRecoiling()
        {
            var heroController = HeroController.instance;
            if (heroController == null) return false;
            
            return heroController.cState.recoiling;
        }

        /// <summary>
        /// Checks if the player is sprinting
        /// </summary>
        /// <returns>True if sprinting, false otherwise</returns>
        public static bool IsSprinting()
        {
            var heroController = HeroController.instance;
            if (heroController == null) return false;
            
            return heroController.cState.isSprinting;
        }

        /// <summary>
        /// Checks if the player is back scuttling
        /// </summary>
        /// <returns>True if back scuttling, false otherwise</returns>
        public static bool IsBackScuttling()
        {
            var heroController = HeroController.instance;
            if (heroController == null) return false;
            
            return heroController.cState.isBackScuttling;
        }

        /// <summary>
        /// Checks if the player is in any state that should pause effects
        /// </summary>
        /// <returns>True if effects should be paused, false otherwise</returns>
        public static bool ShouldPauseEffects()
        {
            return IsTransitioning() || 
                   IsDead() || 
                   IsHazardDeath() || 
                   IsHazardRespawning() || 
                   IsPaused() || 
                   IsInCutsceneMovement() || 
                   IsTriggerEventsPaused() || 
                   IsNeedolinPlayingMemory() ||
                   IsGameSaving() ||
                   !Application.isFocused;
        }

        /// <summary>
        /// Checks if the player is in any state that should disable effects entirely
        /// </summary>
        /// <returns>True if effects should be disabled, false otherwise</returns>
        public static bool ShouldDisableEffects()
        {
            return IsDead() || 
                   IsHazardDeath() || 
                   IsHazardRespawning();
        }

        /// <summary>
        /// Gets a comprehensive status string for debugging
        /// </summary>
        /// <returns>String describing current game state</returns>
        public static string GetGameStateDebugInfo()
        {
            var heroController = HeroController.instance;
            if (heroController == null) return "HeroController not found";

            var states = new System.Collections.Generic.List<string>();
            
            // Critical states
            if (IsTransitioning()) states.Add("Transitioning");
            if (IsDead()) states.Add("Dead");
            if (IsFrostDeath()) states.Add("FrostDeath");
            if (IsHazardDeath()) states.Add("HazardDeath");
            if (IsHazardRespawning()) states.Add("HazardRespawning");
            if (IsPaused()) states.Add("Paused");
            if (IsInCutsceneMovement()) states.Add("CutsceneMovement");
            if (IsTriggerEventsPaused()) states.Add("TriggerEventsPaused");
            if (IsNeedolinPlayingMemory()) states.Add("NeedolinPlayingMemory");
            
            // Bench and saving states
            if (IsAtBench()) states.Add("AtBench");
            if (IsNearBench()) states.Add("NearBench");
            if (IsInSavingState()) states.Add("InSavingState");
            if (IsGameSaving()) states.Add("GameSaving");
            
            // Movement states
            if (IsInWalkZone()) states.Add("InWalkZone");
            if (IsAttacking()) states.Add("Attacking");
            if (IsDashing()) states.Add("Dashing");
            if (IsBackDashing()) states.Add("BackDashing");
            if (IsWallSliding()) states.Add("WallSliding");
            if (IsDoubleJumping()) states.Add("DoubleJumping");
            if (IsFalling()) states.Add("Falling");
            if (IsOnGround()) states.Add("OnGround");
            if (IsRecoiling()) states.Add("Recoiling");
            if (IsSprinting()) states.Add("Sprinting");
            if (IsBackScuttling()) states.Add("BackScuttling");

            return states.Count > 0 ? string.Join(", ", states) : "Normal";
        }
    }
}
