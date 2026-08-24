using System;
using System.Reflection;
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

        private static readonly FieldInfo IsEmbeddedField =
            AccessTools.Field(typeof(WorldInfoPanel), "m_IsEmbbeded");
        private static readonly FieldInfo InstanceIdField =
            AccessTools.Field(typeof(WorldInfoPanel), "m_InstanceID");
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

                InstanceID selected = WorldInfoPanel.GetCurrentInstanceID();
                if (!RepresentsCurrentTopLevelCitizen(__instance, selected))
                {
                    return;
                }

                UILabel status = __instance.Find<UILabel>("Status");
                if (status == null || string.IsNullOrEmpty(status.text) ||
                    !status.text.StartsWith("Going", StringComparison.OrdinalIgnoreCase))
                {
                    Label.Remove();
                    lastCitizenId = 0;
                    nextRefreshTime = 0f;
                    return;
                }

                uint citizenId = selected.Citizen;
                if (citizenId != lastCitizenId)
                {
                    lastCitizenId = citizenId;
                    nextRefreshTime = 0f;
                    Label.SetUnavailable();
                }

                if (!Label.Attach(status, __instance.component))
                {
                    return;
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

        private static bool RepresentsCurrentTopLevelCitizen(
            CitizenWorldInfoPanel panel,
            InstanceID selected)
        {
            if (selected.Type != InstanceType.Citizen || selected.Citizen == 0 ||
                IsEmbeddedField == null || InstanceIdField == null)
            {
                return false;
            }

            object embeddedValue = IsEmbeddedField.GetValue(panel);
            if (embeddedValue is bool && (bool)embeddedValue)
            {
                return false;
            }

            object panelValue = InstanceIdField.GetValue(panel);
            if (!(panelValue is InstanceID))
            {
                return false;
            }

            InstanceID panelInstance = (InstanceID)panelValue;
            return panelInstance.Type == InstanceType.Citizen &&
                   panelInstance.Citizen == selected.Citizen;
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
