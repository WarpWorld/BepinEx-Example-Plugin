
using DunGen;
using GameNetcodeStuff;
using LethalCompanyTestMod;
using Newtonsoft.Json.Linq;
using Steamworks.Data;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Xml.Linq;
using Unity.Netcode;
using UnityEngine;
using static System.Net.Mime.MediaTypeNames;
using static UnityEngine.EventSystems.EventTrigger;
using static UnityEngine.GraphicsBuffer;


namespace BepinControl
{
    public delegate CrowdResponse CrowdDelegate(ControlClient client, CrowdRequest req);

    public class CrowdDelegates
    {


        public static CrowdResponse Example(ControlClient client, CrowdRequest req)
        {
            //example of a non-timed effect
            //implement the game code to carry out the effect here
            //some code may need to run within the Unity loop, for which you can use the ActionQueue
            //the function should check if it is valid and return a status message depending on what was able to be done
                //STATUS_SUCCESS - effect ran successfully
                //STATUS_RETRY   - effect cannot be run now, try again soon
                //STATUS_FAILURE   - effect could not be run, don't try again

            //some reflection helper methods like setProperty are included at the bottom to help with accessing private variables/functions

            CrowdResponse.Status status = CrowdResponse.Status.STATUS_SUCCESS;
            string message = "";

            try
            {
                var playerRef = StartOfRound.Instance.localPlayerController;

                if (playerRef.health <= 0 || StartOfRound.Instance.timeSinceRoundStarted < 2f || !playerRef.playersManager.shipDoorsEnabled) status = CrowdResponse.Status.STATUS_RETRY;
                else
                {
                    TestMod.ActionQueue.Enqueue(() =>
                    {
                        playerRef.KillPlayer(playerRef.transform.up * 100.0f);
                    });
                }
            }
            catch (Exception e)
            {
                status = CrowdResponse.Status.STATUS_RETRY;
                TestMod.mls.LogInfo($"Crowd Control Error: {e.ToString()}");
            }

            return new CrowdResponse(req.GetReqID(), status, message);
        }
        

        public static CrowdResponse ExampleTimed(ControlClient client, CrowdRequest req)
        {
            //timed effects are slightly different
            int dur = 30;
            if (req.duration > 0) dur = req.duration / 1000;

            if (TimedThread.isRunning(TimedType.EXAMPLE)) return new CrowdResponse(req.GetReqID(), CrowdResponse.Status.STATUS_RETRY, "");
                    
            new Thread(new TimedThread(req.GetReqID(), TimedType.EXAMPLE, dur * 1000).Run).Start();
            return new TimedResponse(req.GetReqID(), dur * 1000, CrowdResponse.Status.STATUS_SUCCESS);
        }
        

        public static void setProperty(System.Object a, string prop, System.Object val)
        {
            var f = a.GetType().GetField(prop, BindingFlags.Instance | BindingFlags.NonPublic);
            f.SetValue(a, val);
        }

        public static System.Object getProperty(System.Object a, string prop)
        {
            var f = a.GetType().GetField(prop, BindingFlags.Instance | BindingFlags.NonPublic);
            return f.GetValue(a);
        }

        public static void setSubProperty(System.Object a, string prop, string prop2, System.Object val)
        {
            var f = a.GetType().GetField(prop, BindingFlags.Instance | BindingFlags.NonPublic);
            var f2 = f.GetType().GetField(prop, BindingFlags.Instance | BindingFlags.NonPublic);
            f2.SetValue(f, val);
        }

        public static void callSubFunc(System.Object a, string prop, string func, System.Object val)
        {
            callSubFunc(a, prop, func, new object[] { val });
        }

        public static void callSubFunc(System.Object a, string prop, string func, System.Object[] vals)
        {
            var f = a.GetType().GetField(prop, BindingFlags.Instance | BindingFlags.NonPublic);


            var p = f.GetType().GetMethod(func, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            p.Invoke(f, vals);

        }

        public static void callFunc(System.Object a, string func, System.Object val)
        {
            callFunc(a, func, new object[] { val });
        }

        public static void callFunc(System.Object a, string func, System.Object[] vals)
        {
            var p = a.GetType().GetMethod(func, BindingFlags.Instance | BindingFlags.NonPublic);
            p.Invoke(a, vals);

        }

        public static System.Object callAndReturnFunc(System.Object a, string func, System.Object val)
        {
            return callAndReturnFunc(a, func, new object[] { val });
        }

        public static System.Object callAndReturnFunc(System.Object a, string func, System.Object[] vals)
        {
            var p = a.GetType().GetMethod(func, BindingFlags.Instance | BindingFlags.NonPublic);
            return p.Invoke(a, vals);

        }

    }
}
