// Adds a route-distance row to active road-vehicle information panels.
// Removes that row for stationary parked vehicles that have no active trip to show.
using System;
using ColossalFramework.UI;
using HarmonyLib;
using RouteDistance.Distance;
using RouteDistance.UI;
using UnityEngine;

namespace RouteDistance.Patches
{
    /// <summary>
    /// Refreshes the vehicle distance row after vanilla binds its information panel.
    /// </summary>
    [HarmonyPatch(typeof(VehicleWorldInfoPanel), "UpdateBindings")]
    internal static class VehicleInfoPanelPatch
    {
        private const float RefreshInterval = 0.75f;
        private const float StationaryVelocitySquared = 0.0001f;

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
                UILabel status = __instance.Find<UILabel>("Status");

                if (vehicleId == 0 || IsParkedVehicle(
                    vehicleId,
                    status == null ? null : status.text))
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

        /// <summary>
        /// Removes the vehicle row and resets its refresh state.
        /// </summary>
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

        private static bool IsParkedVehicle(ushort vehicleId, string statusText)
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

            Vehicle vehicle = manager.m_vehicles.m_buffer[vehicleId];
            Vector3 velocity = vehicle.GetLastFrameVelocity();
            bool isStationary = velocity.sqrMagnitude <= StationaryVelocitySquared;
            bool isParking = (vehicle.m_flags & Vehicle.Flags.Parking) != 0;
            bool statusSaysParked = !string.IsNullOrEmpty(statusText) &&
                                    statusText.IndexOf("Parked", StringComparison.OrdinalIgnoreCase) >= 0;
            return isStationary && (isParking || statusSaysParked);
        }
    }
}
