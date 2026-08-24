using CitiesHarmony.API;
using ICities;
using UnityEngine;

namespace RouteDistance
{
    public sealed class LoadingExtension : LoadingExtensionBase
    {
        public override void OnCreated(ILoading loading)
        {
            Debug.Log("[Route Distance] Loaded");
            if (HarmonyHelper.IsHarmonyInstalled)
            {
                Patcher.PatchAll();
            }
            else
            {
                Debug.LogWarning("[Route Distance] Harmony 2.2.2 is unavailable; UI patches were not applied");
            }
        }

        public override void OnReleased()
        {
            if (HarmonyHelper.IsHarmonyInstalled)
            {
                Patcher.UnpatchAll();
            }
        }
    }
}
