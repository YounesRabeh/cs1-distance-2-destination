using System;
using ColossalFramework;
using ColossalFramework.UI;
using HarmonyLib;
using RouteDistance.Distance;
using RouteDistance.UI;
using UnityEngine;

namespace RouteDistance.Patches
{
    [HarmonyPatch(typeof(CitizenWorldInfoPanel), "UpdateBindings")]
    internal static class CitizenInfoPanelPatch
    {
        private const float RefreshInterval = 0.75f;

        private static readonly DistanceLabel Label = new DistanceLabel();
        private static uint lastCitizenId;
        private static float nextRefreshTime;

        [HarmonyPostfix]
        private static void Postfix(CitizenWorldInfoPanel __instance)
        {
            try
            {
                if (__instance == null || __instance.component == null ||
                    !__instance.component.isVisible)
                {
                    return;
                }

                UILabel status = __instance.Find<UILabel>("Status");
                if (!Label.Attach(status))
                {
                    return;
                }

                InstanceID selected = WorldInfoPanel.GetCurrentInstanceID();
                uint citizenId = selected.Type == InstanceType.Citizen ? selected.Citizen : 0u;
                if (citizenId != lastCitizenId)
                {
                    lastCitizenId = citizenId;
                    nextRefreshTime = 0f;
                    Label.SetUnavailable();
                }

                float now = Time.realtimeSinceStartup;
                if (now < nextRefreshTime)
                {
                    return;
                }

                nextRefreshTime = now + RefreshInterval;
                ushort instanceId = GetCitizenInstance(citizenId);

                float meters;
                if (instanceId != 0 &&
                    PathDistanceCalculator.TryGetCitizenRemainingDistance(instanceId, out meters))
                {
                    Label.SetDistance(meters);
                }
                else
                {
                    Label.SetUnavailable();
                }
            }
            catch (Exception exception)
            {
                Label.SetUnavailable();
                PathDistanceCalculator.LogUnexpected(exception);
            }
        }

        internal static void Cleanup()
        {
            Label.Remove();
            lastCitizenId = 0;
            nextRefreshTime = 0f;
        }

        private static ushort GetCitizenInstance(uint citizenId)
        {
            if (citizenId == 0 || !Singleton<CitizenManager>.exists)
            {
                return 0;
            }

            CitizenManager manager = Singleton<CitizenManager>.instance;
            if (manager == null || manager.m_citizens == null ||
                manager.m_citizens.m_buffer == null ||
                citizenId >= (uint)manager.m_citizens.m_buffer.Length)
            {
                return 0;
            }

            Citizen citizen = manager.m_citizens.m_buffer[(int)citizenId];
            return (citizen.m_flags & Citizen.Flags.Created) != 0 &&
                   (citizen.m_flags & Citizen.Flags.Dead) == 0 &&
                   citizen.m_vehicle == 0
                ? citizen.m_instance
                : (ushort)0;
        }
    }
}
