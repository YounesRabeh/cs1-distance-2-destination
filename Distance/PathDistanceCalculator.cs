// Provides safe entry points for reading remaining distances from live game entities.
// Validates mutable simulation data before delegating geometry work to PathHelpers.
using System;
using System.Collections.Generic;
using ColossalFramework;
using UnityEngine;

namespace DistanceToDestination.Distance
{
    /// <summary>
    /// Reads existing vehicle and pedestrian paths and calculates their remaining length.
    /// </summary>
    public static class PathDistanceCalculator
    {
        private const float UnexpectedLogInterval = 30f;
        private static float nextUnexpectedLogTime;

        /// <summary>
        /// Attempts to calculate the remaining route distance for a spawned road vehicle.
        /// </summary>
        public static bool TryGetVehicleRemainingDistance(ushort vehicleId, out float meters)
        {
            meters = 0f;

            List<PathUnit.Position> positions;
            byte pathPositionIndex;
            Vector3 worldPosition;
            if (!TryGetVehicleRemainingPath(
                    vehicleId,
                    out positions,
                    out pathPositionIndex,
                    out worldPosition))
            {
                return false;
            }

            return PathHelpers.TryCalculateRemainingDistance(
                positions,
                pathPositionIndex,
                worldPosition,
                out meters);
        }

        /// <summary>
        /// Attempts to calculate the remaining route distance for a walking citizen instance.
        /// </summary>
        public static bool TryGetCitizenRemainingDistance(ushort citizenInstanceId, out float meters)
        {
            meters = 0f;

            List<PathUnit.Position> positions;
            byte pathPositionIndex;
            Vector3 worldPosition;
            if (!TryGetCitizenRemainingPath(
                    citizenInstanceId,
                    out positions,
                    out pathPositionIndex,
                    out worldPosition))
            {
                return false;
            }

            return PathHelpers.TryCalculateRemainingDistance(
                positions,
                pathPositionIndex,
                worldPosition,
                out meters);
        }

        /// <summary>
        /// Determines whether the selected car or rider-owned bicycle has an active supported path.
        /// </summary>
        internal static bool SupportsVehicleWithActivePath(ushort vehicleId)
        {
            if (vehicleId == 0 || !Singleton<VehicleManager>.exists)
            {
                return false;
            }

            VehicleManager manager = Singleton<VehicleManager>.instance;
            if (manager == null || manager.m_vehicles == null ||
                manager.m_vehicles.m_buffer == null ||
                vehicleId >= manager.m_vehicles.m_buffer.Length)
            {
                return false;
            }

            Vehicle vehicle = manager.m_vehicles.m_buffer[vehicleId];
            if (!IsSupportedVehicle(vehicle))
            {
                return false;
            }

            if (!IsBicycle(vehicle))
            {
                return vehicle.m_path != 0;
            }

            ushort riderInstanceId;
            return TryGetBicycleRiderInstance(vehicleId, ref vehicle, out riderInstanceId) &&
                   SupportsCitizenInstanceWithActivePath(riderInstanceId, vehicleId);
        }

        /// <summary>
        /// Determines whether the selected citizen instance is walking on an active path.
        /// </summary>
        internal static bool SupportsCitizenWithActivePath(ushort citizenInstanceId)
        {
            if (citizenInstanceId == 0 || !Singleton<CitizenManager>.exists)
            {
                return false;
            }

            CitizenManager manager = Singleton<CitizenManager>.instance;
            return manager != null && manager.m_instances != null &&
                   manager.m_instances.m_buffer != null &&
                   citizenInstanceId < manager.m_instances.m_buffer.Length &&
                   IsSupportedCitizenInstance(
                       manager.m_instances.m_buffer[citizenInstanceId],
                       citizenInstanceId,
                       manager,
                       0) &&
                   manager.m_instances.m_buffer[citizenInstanceId].m_path != 0;
        }

        /// <summary>
        /// Captures a consistent vehicle-path snapshot and its current world position.
        /// </summary>
        internal static bool TryGetVehicleRemainingPath(
            ushort vehicleId,
            out List<PathUnit.Position> positions,
            out byte pathPositionIndex,
            out Vector3 worldPosition)
        {
            positions = null;
            pathPositionIndex = 0;
            worldPosition = Vector3.zero;

            try
            {
                if (vehicleId == 0 || !Singleton<VehicleManager>.exists)
                {
                    return false;
                }

                VehicleManager manager = Singleton<VehicleManager>.instance;
                if (manager == null || manager.m_vehicles == null || manager.m_vehicles.m_buffer == null ||
                    vehicleId >= manager.m_vehicles.m_buffer.Length)
                {
                    return false;
                }

                Vehicle vehicle = manager.m_vehicles.m_buffer[vehicleId];
                if (!IsSupportedVehicle(vehicle))
                {
                    return false;
                }

                if (IsBicycle(vehicle))
                {
                    return TryGetBicycleRemainingPath(
                        vehicleId,
                        ref vehicle,
                        out positions,
                        out pathPositionIndex,
                        out worldPosition);
                }

                uint pathId = vehicle.m_path;
                pathPositionIndex = vehicle.m_pathPositionIndex;
                if (pathId == 0 ||
                    !PathHelpers.TryGetRemainingPositions(pathId, pathPositionIndex, out positions))
                {
                    return false;
                }

                Vehicle current = manager.m_vehicles.m_buffer[vehicleId];
                if (!IsSupportedVehicle(current) || current.m_path != pathId ||
                    current.m_pathPositionIndex != pathPositionIndex)
                {
                    positions = null;
                    return false;
                }

                worldPosition = current.GetLastFramePosition();
                return true;
            }
            catch (Exception exception)
            {
                LogUnexpected(exception);
                positions = null;
                pathPositionIndex = 0;
                worldPosition = Vector3.zero;
                return false;
            }
        }

        /// <summary>
        /// Captures a bicycle route from the live citizen instance that drives its simulation.
        /// </summary>
        private static bool TryGetBicycleRemainingPath(
            ushort vehicleId,
            ref Vehicle vehicle,
            out List<PathUnit.Position> positions,
            out byte pathPositionIndex,
            out Vector3 worldPosition)
        {
            positions = null;
            pathPositionIndex = 0;
            worldPosition = Vector3.zero;

            ushort riderInstanceId;
            if (!TryGetBicycleRiderInstance(vehicleId, ref vehicle, out riderInstanceId))
            {
                return false;
            }

            return TryGetCitizenRemainingPath(
                riderInstanceId,
                vehicleId,
                out positions,
                out pathPositionIndex,
                out worldPosition);
        }

        /// <summary>
        /// Captures a consistent pedestrian-path snapshot and its current world position.
        /// </summary>
        internal static bool TryGetCitizenRemainingPath(
            ushort citizenInstanceId,
            out List<PathUnit.Position> positions,
            out byte pathPositionIndex,
            out Vector3 worldPosition)
        {
            positions = null;
            pathPositionIndex = 0;
            worldPosition = Vector3.zero;

            try
            {
                return TryGetCitizenRemainingPath(
                    citizenInstanceId,
                    0,
                    out positions,
                    out pathPositionIndex,
                    out worldPosition);
            }
            catch (Exception exception)
            {
                LogUnexpected(exception);
                positions = null;
                pathPositionIndex = 0;
                worldPosition = Vector3.zero;
                return false;
            }
        }

        /// <summary>
        /// Captures a citizen-owned path while enforcing its expected active vehicle relationship.
        /// </summary>
        private static bool TryGetCitizenRemainingPath(
            ushort citizenInstanceId,
            ushort expectedVehicleId,
            out List<PathUnit.Position> positions,
            out byte pathPositionIndex,
            out Vector3 worldPosition)
        {
            positions = null;
            pathPositionIndex = 0;
            worldPosition = Vector3.zero;

            if (citizenInstanceId == 0 || !Singleton<CitizenManager>.exists)
            {
                return false;
            }

            CitizenManager manager = Singleton<CitizenManager>.instance;
            if (manager == null || manager.m_instances == null ||
                manager.m_instances.m_buffer == null ||
                citizenInstanceId >= manager.m_instances.m_buffer.Length)
            {
                return false;
            }

            CitizenInstance instance = manager.m_instances.m_buffer[citizenInstanceId];
            if (!IsSupportedCitizenInstance(
                    instance,
                    citizenInstanceId,
                    manager,
                    expectedVehicleId))
            {
                return false;
            }

            uint pathId = instance.m_path;
            pathPositionIndex = instance.m_pathPositionIndex;
            if (pathId == 0 ||
                !PathHelpers.TryGetRemainingPositions(pathId, pathPositionIndex, out positions))
            {
                return false;
            }

            CitizenInstance current = manager.m_instances.m_buffer[citizenInstanceId];
            if (!IsSupportedCitizenInstance(
                    current,
                    citizenInstanceId,
                    manager,
                    expectedVehicleId) ||
                current.m_path != pathId || current.m_pathPositionIndex != pathPositionIndex)
            {
                positions = null;
                return false;
            }

            worldPosition = current.GetLastFramePosition();
            return true;
        }

        /// <summary>
        /// Logs an unexpected read or UI error at a bounded rate.
        /// </summary>
        internal static void LogUnexpected(Exception exception)
        {
            if (exception == null)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            if (now < nextUnexpectedLogTime)
            {
                return;
            }

            nextUnexpectedLogTime = now + UnexpectedLogInterval;
            Debug.LogError("[Distance 2 Destination] Unexpected calculator/UI exception (rate limited)");
            Debug.LogException(exception);
        }

        /// <summary>
        /// Determines whether a vehicle is a live, spawned car or bicycle supported by the calculator.
        /// </summary>
        private static bool IsSupportedVehicle(Vehicle vehicle)
        {
            Vehicle.Flags flags = vehicle.m_flags;
            if ((flags & Vehicle.Flags.Created) == 0 || (flags & Vehicle.Flags.Deleted) != 0 ||
                (flags & Vehicle.Flags.Spawned) == 0)
            {
                return false;
            }

            VehicleInfo info = vehicle.Info;
            VehicleInfo.VehicleType supportedTypes =
                VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Bicycle;
            return info != null && (info.m_vehicleType & supportedTypes) != 0;
        }

        /// <summary>
        /// Determines whether a supported vehicle is the citizen-driven bicycle proxy.
        /// </summary>
        private static bool IsBicycle(Vehicle vehicle)
        {
            VehicleInfo info = vehicle.Info;
            return info != null &&
                   (info.m_vehicleType & VehicleInfo.VehicleType.Bicycle) != 0;
        }

        /// <summary>
        /// Resolves the live citizen instance that owns and advances a bicycle's route.
        /// </summary>
        private static bool TryGetBicycleRiderInstance(
            ushort vehicleId,
            ref Vehicle vehicle,
            out ushort riderInstanceId)
        {
            riderInstanceId = 0;
            VehicleInfo info = vehicle.Info;
            if (info == null || info.m_vehicleAI == null || !IsBicycle(vehicle) ||
                !Singleton<CitizenManager>.exists)
            {
                return false;
            }

            InstanceID owner = info.m_vehicleAI.GetOwnerID(vehicleId, ref vehicle);
            if (owner.Type != InstanceType.Citizen || owner.Citizen == 0)
            {
                return false;
            }

            CitizenManager manager = Singleton<CitizenManager>.instance;
            if (manager == null || manager.m_citizens == null ||
                manager.m_citizens.m_buffer == null ||
                owner.Citizen >= (uint)manager.m_citizens.m_buffer.Length)
            {
                return false;
            }

            Citizen citizen = manager.m_citizens.m_buffer[(int)owner.Citizen];
            if ((citizen.m_flags & Citizen.Flags.Created) == 0 ||
                (citizen.m_flags & Citizen.Flags.Dead) != 0 ||
                citizen.m_vehicle != vehicleId || citizen.m_instance == 0)
            {
                return false;
            }

            riderInstanceId = citizen.m_instance;
            return true;
        }

        /// <summary>
        /// Determines whether a citizen-owned route is active for a particular bicycle.
        /// </summary>
        private static bool SupportsCitizenInstanceWithActivePath(
            ushort citizenInstanceId,
            ushort expectedVehicleId)
        {
            if (citizenInstanceId == 0 || !Singleton<CitizenManager>.exists)
            {
                return false;
            }

            CitizenManager manager = Singleton<CitizenManager>.instance;
            return manager != null && manager.m_instances != null &&
                   manager.m_instances.m_buffer != null &&
                   citizenInstanceId < manager.m_instances.m_buffer.Length &&
                   IsSupportedCitizenInstance(
                       manager.m_instances.m_buffer[citizenInstanceId],
                       citizenInstanceId,
                       manager,
                       expectedVehicleId) &&
                   manager.m_instances.m_buffer[citizenInstanceId].m_path != 0;
        }

        /// <summary>
        /// Determines whether a citizen instance is a live pedestrian with a stable path.
        /// </summary>
        private static bool IsSupportedCitizenInstance(
            CitizenInstance instance,
            ushort instanceId,
            CitizenManager manager,
            ushort expectedVehicleId)
        {
            CitizenInstance.Flags flags = instance.m_flags;
            if ((flags & CitizenInstance.Flags.Created) == 0 ||
                (flags & CitizenInstance.Flags.Deleted) != 0 ||
                (flags & CitizenInstance.Flags.OnPath) == 0 ||
                (flags & (CitizenInstance.Flags.WaitingPath | CitizenInstance.Flags.EnteringVehicle)) != 0)
            {
                return false;
            }

            if (instance.m_citizen == 0 || manager.m_citizens == null ||
                manager.m_citizens.m_buffer == null ||
                instance.m_citizen >= (uint)manager.m_citizens.m_buffer.Length)
            {
                return false;
            }

            Citizen citizen = manager.m_citizens.m_buffer[(int)instance.m_citizen];
            return (citizen.m_flags & Citizen.Flags.Created) != 0 &&
                   (citizen.m_flags & Citizen.Flags.Dead) == 0 &&
                   citizen.m_instance == instanceId &&
                   citizen.m_vehicle == expectedVehicleId;
        }
    }
}
