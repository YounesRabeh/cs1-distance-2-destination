using System;
using HarmonyLib;
using RouteDistance.Patches;
using UnityEngine;

namespace RouteDistance
{
    internal static class Patcher
    {
        internal const string HarmonyId = "com.routedistance.cs1";

        private static bool patched;

        internal static void PatchAll()
        {
            if (patched)
            {
                return;
            }

            Harmony harmony = new Harmony(HarmonyId);
            try
            {
                harmony.PatchAll(typeof(Patcher).Assembly);
                patched = true;
                Debug.Log("[Route Distance] Harmony patches applied");
            }
            catch (Exception exception)
            {
                bool cleanupSucceeded = false;
                try
                {
                    harmony.UnpatchAll(HarmonyId);
                    cleanupSucceeded = true;
                }
                catch (Exception cleanupException)
                {
                    Debug.LogException(cleanupException);
                }

                patched = !cleanupSucceeded;
                CleanupLabels();
                Debug.LogError("[Route Distance] Failed to apply Harmony patches");
                Debug.LogException(exception);
            }
        }

        internal static void UnpatchAll()
        {
            if (!patched)
            {
                CleanupLabels();
                return;
            }

            try
            {
                new Harmony(HarmonyId).UnpatchAll(HarmonyId);
                patched = false;
                Debug.Log("[Route Distance] Harmony patches removed");
            }
            catch (Exception exception)
            {
                Debug.LogError("[Route Distance] Failed to remove Harmony patches");
                Debug.LogException(exception);
            }
            CleanupLabels();
        }

        private static void CleanupLabels()
        {
            try
            {
                VehicleInfoPanelPatch.Cleanup();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            try
            {
                CitizenInfoPanelPatch.Cleanup();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
