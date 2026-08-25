// Adds a route-distance row to active supported-vehicle information panels.
// Removes that row for stationary parked vehicles that have no active trip to show.
using System;
using System.Collections.Generic;
using System.Reflection;
using ColossalFramework.UI;
using HarmonyLib;
using DistanceToDestination.Distance;
using DistanceToDestination.UI;
using UnityEngine;

namespace DistanceToDestination.Patches
{
    /// <summary>
    /// Refreshes the vehicle distance row after vanilla binds its information panel.
    /// </summary>
    [HarmonyPatch]
    internal static class VehicleInfoPanelPatch
    {
        private const float RefreshInterval = 0.75f;
        private const float StationaryVelocitySquared = 0.0001f;

        private static readonly FieldInfo InstanceIdField =
            AccessTools.Field(typeof(WorldInfoPanel), "m_InstanceID");
        private static readonly DistanceLabel Label = new DistanceLabel();
        private static int bindingDepth;
        private static ushort lastVehicleId;
        private static float nextRefreshTime;

        /// <summary>
        /// Selects the base binding method and every concrete vanilla vehicle-panel override.
        /// </summary>
        private static IEnumerable<MethodBase> TargetMethods()
        {
            Type[] panelTypes =
            {
                typeof(VehicleWorldInfoPanel),
                typeof(CitizenVehicleWorldInfoPanel),
                typeof(CityServiceVehicleWorldInfoPanel),
                typeof(PublicTransportVehicleWorldInfoPanel),
                typeof(RaceVehicleWorldInfoPanel),
                typeof(TouristVehicleWorldInfoPanel)
            };

            HashSet<MethodBase> methods = new HashSet<MethodBase>();
            for (int index = 0; index < panelTypes.Length; index++)
            {
                MethodInfo method = AccessTools.DeclaredMethod(panelTypes[index], "UpdateBindings");
                if (method != null && methods.Add(method))
                {
                    yield return method;
                }
            }
        }

        /// <summary>
        /// Restores layout once before the outermost vehicle panel refresh begins.
        /// </summary>
        [HarmonyPrefix]
        private static void Prefix(VehicleWorldInfoPanel __instance, out bool __state)
        {
            __state = bindingDepth == 0;
            bindingDepth++;
            if (!__state)
            {
                return;
            }

            try
            {
                if (__instance != null && __instance.component != null)
                {
                    Label.PrepareForVanillaRefresh(__instance.component);
                }
            }
            catch (Exception exception)
            {
                PathDistanceCalculator.LogUnexpected(exception);
            }
        }

        /// <summary>
        /// Updates the selected vehicle's distance row after vanilla refreshes the panel.
        /// </summary>
        [HarmonyPostfix]
        private static void Postfix(VehicleWorldInfoPanel __instance, bool __state)
        {
            bool activePathConfirmed = false;
            try
            {
                if (!__state)
                {
                    return;
                }

                if (__instance == null || __instance.component == null ||
                    !__instance.component.isVisible)
                {
                    return;
                }

                if (!ShouldShowForPanel(__instance))
                {
                    Label.Remove();
                    lastVehicleId = 0;
                    nextRefreshTime = 0f;
                    return;
                }

                ushort vehicleId = GetPanelVehicleId(__instance);
                vehicleId = GetFirstVehicle(vehicleId);
                UILabel status = __instance.Find<UILabel>("Status");

                if (vehicleId == 0 ||
                    !PathDistanceCalculator.SupportsVehicleWithActivePath(vehicleId) ||
                    IsParkedVehicle(vehicleId))
                {
                    Label.Remove();
                    lastVehicleId = vehicleId;
                    nextRefreshTime = 0f;
                    return;
                }

                activePathConfirmed = true;
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
                if (activePathConfirmed)
                {
                    Label.SetUnavailable();
                }
                else
                {
                    Label.Remove();
                }

                PathDistanceCalculator.LogUnexpected(exception);
            }
            finally
            {
                if (bindingDepth > 0)
                {
                    bindingDepth--;
                }
            }
        }

        /// <summary>
        /// Removes the vehicle row and resets its refresh state.
        /// </summary>
        internal static void Cleanup()
        {
            Label.Remove();
            bindingDepth = 0;
            lastVehicleId = 0;
            nextRefreshTime = 0f;
        }

        /// <summary>
        /// Applies the service or other-vehicle preference for the active panel type.
        /// </summary>
        private static bool ShouldShowForPanel(VehicleWorldInfoPanel panel)
        {
            return panel is CityServiceVehicleWorldInfoPanel
                ? ModSettings.ShowServiceVehicles
                : ModSettings.ShowOtherVehicles;
        }

        /// <summary>
        /// Resolves the live vehicle bound by vanilla, including citizen-owned bicycles.
        /// </summary>
        private static ushort GetPanelVehicleId(VehicleWorldInfoPanel panel)
        {
            if (panel != null && InstanceIdField != null)
            {
                object panelValue = InstanceIdField.GetValue(panel);
                if (panelValue is InstanceID)
                {
                    InstanceID panelInstance = (InstanceID)panelValue;
                    if (panelInstance.Type == InstanceType.Vehicle)
                    {
                        return panelInstance.Vehicle;
                    }

                    return 0;
                }
            }

            InstanceID selected = WorldInfoPanel.GetCurrentInstanceID();
            return selected.Type == InstanceType.Vehicle ? selected.Vehicle : (ushort)0;
        }

        /// <summary>
        /// Resolves a trailer or consist member to the vehicle that owns the route path.
        /// </summary>
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

        /// <summary>
        /// Detects a stationary vehicle whose simulation flags indicate parking.
        /// </summary>
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

            Vehicle vehicle = manager.m_vehicles.m_buffer[vehicleId];
            Vector3 velocity = vehicle.GetLastFrameVelocity();
            bool isStationary = velocity.sqrMagnitude <= StationaryVelocitySquared;
            bool isParking = (vehicle.m_flags & Vehicle.Flags.Parking) != 0;
            return isStationary && isParking;
        }
    }
}
