using System;
using ColossalFramework.UI;
using HarmonyLib;
using RouteDistance.Distance;
using RouteDistance.UI;
using UnityEngine;

namespace RouteDistance.Patches
{
    [HarmonyPatch(typeof(VehicleWorldInfoPanel), "UpdateBindings")]
    internal static class VehicleInfoPanelPatch
    {
        private const float RefreshInterval = 0.75f;

        private static readonly DistanceLabel Label = new DistanceLabel();
        private static ushort lastVehicleId;
        private static float nextRefreshTime;

        [HarmonyPostfix]
        private static void Postfix(VehicleWorldInfoPanel __instance)
        {
            try
            {
                if (__instance == null || __instance.component == null ||
                    !__instance.component.isVisible)
                {
                    return;
                }

                InstanceID selected = WorldInfoPanel.GetCurrentInstanceID();
                ushort vehicleId = selected.Type == InstanceType.Vehicle ? selected.Vehicle : (ushort)0;
                vehicleId = GetFirstVehicle(vehicleId);

                if (vehicleId == 0 || IsParkedVehicle(vehicleId))
                {
                    Label.Remove();
                    lastVehicleId = vehicleId;
                    nextRefreshTime = 0f;
                    return;
                }

                if (vehicleId != lastVehicleId)
                {
                    lastVehicleId = vehicleId;
                    nextRefreshTime = 0f;
                    Label.SetUnavailable();
                }

                UILabel status = __instance.Find<UILabel>("Status");
                if (!Label.Attach(status))
                {
                    return;
                }

                float now = Time.realtimeSinceStartup;
                if (now < nextRefreshTime)
                {
                    return;
                }

                nextRefreshTime = now + RefreshInterval;

                float meters;
                if (vehicleId != 0 &&
                    PathDistanceCalculator.TryGetVehicleRemainingDistance(vehicleId, out meters))
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
            lastVehicleId = 0;
            nextRefreshTime = 0f;
        }

        private static ushort GetFirstVehicle(ushort vehicleId)
        {
            if (vehicleId == 0 || !ColossalFramework.Singleton<VehicleManager>.exists)
            {
                return 0;
            }

            VehicleManager manager = ColossalFramework.Singleton<VehicleManager>.instance;
            if (manager == null || manager.m_vehicles == null ||
                manager.m_vehicles.m_buffer == null ||
                vehicleId >= manager.m_vehicles.m_buffer.Length)
            {
                return 0;
            }

            return manager.m_vehicles.m_buffer[vehicleId].GetFirstVehicle(vehicleId);
        }

        private static bool IsParkedVehicle(ushort vehicleId)
        {
            if (vehicleId == 0 || !ColossalFramework.Singleton<VehicleManager>.exists)
            {
                return false;
            }

            VehicleManager manager = ColossalFramework.Singleton<VehicleManager>.instance;
            if (manager == null || manager.m_vehicles == null ||
                manager.m_vehicles.m_buffer == null ||
                vehicleId >= manager.m_vehicles.m_buffer.Length)
            {
                return false;
            }

            return (manager.m_vehicles.m_buffer[vehicleId].m_flags & Vehicle.Flags.Parking) != 0;
        }
    }
}
