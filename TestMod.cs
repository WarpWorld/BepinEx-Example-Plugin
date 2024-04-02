using System;
using System.Collections.Generic;
using System.Threading;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace CrowdControl
{
    [BepInPlugin(modGUID, modName, modVersion)]
    public class TestMod : BaseUnityPlugin
    {
        // Mod Details
        private const string modGUID = "WarpWorld.CrowdControl";
        private const string modName = "Crowd Control";
        private const string modVersion = "1.0.0.0";

        private readonly Harmony harmony = new Harmony(modGUID);

        public static ManualLogSource mls;

        internal static TestMod Instance = null;
        private ControlClient client = null;

        void Awake()
        {
            

            Instance = this;
            mls = BepInEx.Logging.Logger.CreateLogSource("Crowd Control");

            mls.LogInfo($"Loaded {modGUID}. Patching.");
            harmony.PatchAll(typeof(TestMod));

            mls.LogInfo($"Initializing Crowd Control");

            try
            {
                client = new ControlClient();
                new Thread(new ThreadStart(client.NetworkLoop)).Start();
                new Thread(new ThreadStart(client.RequestLoop)).Start();
            }
            catch (Exception e)
            {
                mls.LogInfo($"CC Init Error: {e.ToString()}");
            }

            mls.LogInfo($"Crowd Control Initialized");


            mls = Logger;
        }
        

        public static Queue<Action> ActionQueue = new Queue<Action>();

        //attach this to some game class with a function that runs every frame like the player's Update()
        [HarmonyPatch(typeof(RoundManager), "Update")]
        [HarmonyPrefix]
        static void RunEffects()
        {
            if (ActionQueue.Count > 0)
            {
                Action action = ActionQueue.Dequeue();
                action.Invoke();
            }

            lock(TimedThread.threads)
            {
                foreach (var thread in TimedThread.threads)
                {
                    if (!thread.paused)
                        thread.effect.tick();
                }
            }

        }
        
    }
        
}
