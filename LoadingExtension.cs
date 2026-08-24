// Owns the game-loading lifecycle for Distance 2 Destination.
// Applies patches after Harmony is available and removes them when the level is released.
using CitiesHarmony.API;
using ICities;
using UnityEngine;

namespace DistanceToDestination
{
    /// <summary>
    /// Connects Distance 2 Destination's Harmony lifecycle to Cities: Skylines level loading.
    /// </summary>
    public sealed class LoadingExtension : LoadingExtensionBase
    {
        /// <summary>
        /// Applies the mod patches when a level loading extension is created.
        /// </summary>
        public override void OnCreated(ILoading loading)
        {
            Debug.Log("[Distance 2 Destination] Loaded");
            if (HarmonyHelper.IsHarmonyInstalled)
            {
                Patcher.PatchAll();
            }
            else
            {
                Debug.LogWarning("[Distance 2 Destination] Harmony 2.2.2 is unavailable; UI patches were not applied");
            }
        }

        /// <summary>
        /// Removes the mod patches when the level loading extension is released.
        /// </summary>
        public override void OnReleased()
        {
            if (HarmonyHelper.IsHarmonyInstalled)
            {
                Patcher.UnpatchAll();
            }
        }
    }
}
