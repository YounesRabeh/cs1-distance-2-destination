// Traverses vanilla PathUnit chains and converts their lane positions into physical distance.
// All helpers treat mutable or malformed simulation data as an unavailable route.
using System;
using System.Collections.Generic;
using ColossalFramework;
using UnityEngine;

namespace RouteDistance.Distance
{
    /// <summary>
    /// Contains bounded, defensive path traversal and lane-distance calculations.
    /// </summary>
    internal static class PathHelpers
    {
        internal const int MaxPathUnits = 4096;

        /// <summary>
        /// Collects the remaining positions in a ready PathUnit chain without mutating it.
        /// </summary>
        internal static bool TryGetRemainingPositions(
            uint initialPathId,
            byte encodedPositionIndex,
            out List<PathUnit.Position> positions)
        {
            positions = null;

            try
            {
                if (!Singleton<PathManager>.exists || !Singleton<NetManager>.exists)
                {
                    return false;
                }

                PathManager pathManager = Singleton<PathManager>.instance;
                NetManager netManager = Singleton<NetManager>.instance;
                if (pathManager == null || pathManager.m_pathUnits == null ||
                    pathManager.m_pathUnits.m_buffer == null ||
                    netManager == null || netManager.m_segments == null ||
                    netManager.m_segments.m_buffer == null ||
                    netManager.m_lanes == null || netManager.m_lanes.m_buffer == null)
                {
                    return false;
                }

                PathUnit[] pathBuffer = pathManager.m_pathUnits.m_buffer;
                if (!IsPathIdInRange(initialPathId, pathBuffer))
                {
                    return false;
                }

                PathUnit root = pathBuffer[(int)initialPathId];
                if (!IsAllocated(root) || !IsReady(root))
                {
                    return false;
                }

                int firstPositionIndex = DecodePositionIndex(encodedPositionIndex);
                List<PathUnit.Position> collected = new List<PathUnit.Position>();
                HashSet<uint> visited = new HashSet<uint>();
                uint pathId = initialPathId;

                for (int traversed = 0; traversed < MaxPathUnits; traversed++)
                {
                    if (!IsPathIdInRange(pathId, pathBuffer) || !visited.Add(pathId))
                    {
                        return false;
                    }

                    PathUnit unit = pathBuffer[(int)pathId];
                    if (!IsAllocated(unit) || unit.m_positionCount == 0 ||
                        unit.m_positionCount > PathUnit.MAX_POSITIONS)
                    {
                        return false;
                    }

                    int startIndex = pathId == initialPathId ? firstPositionIndex : 0;
                    if (startIndex < 0 || startIndex >= unit.m_positionCount)
                    {
                        return false;
                    }

                    for (int index = startIndex; index < unit.m_positionCount; index++)
                    {
                        PathUnit.Position position = unit.GetPosition(index);
                        if (!IsValidNetworkPosition(position, netManager))
                        {
                            return false;
                        }

                        collected.Add(position);
                    }

                    // A released or reused unit must invalidate this snapshot.
                    PathUnit current = pathBuffer[(int)pathId];
                    if (!IsSameUnitSnapshot(unit, current))
                    {
                        return false;
                    }

                    if (unit.m_nextPathUnit == 0)
                    {
                        PathUnit currentRoot = pathBuffer[(int)initialPathId];
                        if (!IsSameUnitSnapshot(root, currentRoot) || !IsReady(currentRoot))
                        {
                            return false;
                        }

                        positions = collected;
                        return collected.Count != 0;
                    }

                    pathId = unit.m_nextPathUnit;
                }
            }
            catch (Exception exception)
            {
                // Simulation buffers are mutable. A transient invalidation is an unavailable result.
                PathDistanceCalculator.LogUnexpected(exception);
                return false;
            }

            return false;
        }

        /// <summary>
        /// Calculates distance from the current position through the supplied remaining path positions.
        /// </summary>
        internal static bool TryCalculateRemainingDistance(
            IList<PathUnit.Position> positions,
            byte encodedPositionIndex,
            Vector3 currentWorldPosition,
            out float meters)
        {
            meters = 0f;

            try
            {
                if (positions == null || positions.Count == 0 ||
                    !IsFinite(currentWorldPosition) || !Singleton<NetManager>.exists)
                {
                    return false;
                }

                NetManager netManager = Singleton<NetManager>.instance;
                if (!HasNetworkBuffers(netManager))
                {
                    return false;
                }

                float total = 0f;
                bool isTransitionPhase = encodedPositionIndex != byte.MaxValue &&
                                         (encodedPositionIndex & 1) != 0;

                if (isTransitionPhase)
                {
                    // The even phase has reached the current PathUnit position. During
                    // the odd phase vanilla moves toward the next lane/position.
                    if (positions.Count > 1)
                    {
                        float transitionDistance;
                        if (!TryGetCurrentTransitionDistance(
                                currentWorldPosition,
                                positions[0],
                                positions[1],
                                netManager,
                                out transitionDistance) ||
                            !TryAddDistance(ref total, transitionDistance))
                        {
                            return false;
                        }
                    }
                }
                else
                {
                    uint currentLaneId;
                    NetLane currentLane;
                    if (!TryGetLane(positions[0], netManager, out currentLaneId, out currentLane))
                    {
                        return false;
                    }

                    byte currentOffset;
                    PathUnit.CalculatePathPositionOffset(
                        currentLaneId,
                        currentWorldPosition,
                        out currentOffset);

                    float currentLaneDistance = GetLanePortionDistance(
                        currentLane,
                        currentOffset,
                        positions[0].m_offset);
                    if (!TryAddDistance(ref total, currentLaneDistance))
                    {
                        return false;
                    }
                }

                int firstPair = isTransitionPhase ? 1 : 0;
                for (int index = firstPair; index + 1 < positions.Count; index++)
                {
                    float pairDistance;
                    if (!TryGetDistanceBetweenPositions(
                            positions[index],
                            positions[index + 1],
                            netManager,
                            out pairDistance) ||
                        !TryAddDistance(ref total, pairDistance))
                    {
                        return false;
                    }
                }

                meters = total;
                return true;
            }
            catch (Exception exception)
            {
                PathDistanceCalculator.LogUnexpected(exception);
                meters = 0f;
                return false;
            }
        }

        private static int DecodePositionIndex(byte encodedPositionIndex)
        {
            // Vanilla PathVisualizer uses 0 for the initial 255 sentinel and index >> 1 otherwise.
            return encodedPositionIndex == byte.MaxValue ? 0 : encodedPositionIndex >> 1;
        }

        private static bool IsPathIdInRange(uint pathId, PathUnit[] pathBuffer)
        {
            return pathId != 0 && pathId < (uint)pathBuffer.Length &&
                   pathId < PathManager.MAX_PATHUNIT_COUNT;
        }

        private static bool IsAllocated(PathUnit unit)
        {
            return (unit.m_simulationFlags & PathUnit.SimulationFlags.FLAG_CREATED) != 0 &&
                   unit.m_referenceCount != 0;
        }

        private static bool IsReady(PathUnit unit)
        {
            byte flags = unit.m_pathFindFlags;
            return (flags & PathUnit.FLAG_READY) != 0 &&
                   (flags & (PathUnit.FLAG_QUEUED | PathUnit.FLAG_CALCULATING | PathUnit.FLAG_FAILED)) == 0;
        }

        private static bool IsSameUnitSnapshot(PathUnit expected, PathUnit current)
        {
            return IsAllocated(current) &&
                   expected.m_buildIndex == current.m_buildIndex &&
                   expected.m_nextPathUnit == current.m_nextPathUnit &&
                   expected.m_positionCount == current.m_positionCount &&
                   expected.m_pathFindFlags == current.m_pathFindFlags;
        }

        private static bool IsValidNetworkPosition(PathUnit.Position position, NetManager netManager)
        {
            uint laneId;
            NetLane lane;
            return TryGetLane(position, netManager, out laneId, out lane);
        }

        private static bool TryGetDistanceBetweenPositions(
            PathUnit.Position from,
            PathUnit.Position to,
            NetManager netManager,
            out float meters)
        {
            meters = 0f;

            uint fromLaneId;
            NetLane fromLane;
            uint toLaneId;
            NetLane toLane;
            if (!TryGetLane(from, netManager, out fromLaneId, out fromLane) ||
                !TryGetLane(to, netManager, out toLaneId, out toLane))
            {
                return false;
            }

            if (fromLaneId == toLaneId)
            {
                meters = GetLanePortionDistance(fromLane, from.m_offset, to.m_offset);
                return IsFiniteNonNegative(meters);
            }

            Vector3 fromWorldPosition = GetLanePosition(fromLane, from.m_offset);
            byte toLaneEntryOffset;
            PathUnit.CalculatePathPositionOffset(
                toLaneId,
                fromWorldPosition,
                out toLaneEntryOffset);
            Vector3 toLaneEntryPosition = GetLanePosition(toLane, toLaneEntryOffset);

            // Vanilla constructs a short transition curve between lanes. Its control
            // points are mode-specific, so use the endpoint chord exactly once, then
            // measure only the untraversed portion of the destination lane.
            float connectorDistance = Vector3.Distance(fromWorldPosition, toLaneEntryPosition);
            float toLaneDistance = GetLanePortionDistance(
                toLane,
                toLaneEntryOffset,
                to.m_offset);
            meters = connectorDistance + toLaneDistance;
            return IsFiniteNonNegative(meters);
        }

        private static bool TryGetCurrentTransitionDistance(
            Vector3 currentWorldPosition,
            PathUnit.Position from,
            PathUnit.Position to,
            NetManager netManager,
            out float meters)
        {
            meters = 0f;

            uint fromLaneId;
            NetLane fromLane;
            uint toLaneId;
            NetLane toLane;
            if (!TryGetLane(from, netManager, out fromLaneId, out fromLane) ||
                !TryGetLane(to, netManager, out toLaneId, out toLane))
            {
                return false;
            }

            if (fromLaneId == toLaneId)
            {
                byte currentOffset;
                PathUnit.CalculatePathPositionOffset(
                    toLaneId,
                    currentWorldPosition,
                    out currentOffset);
                meters = GetLanePortionDistance(toLane, currentOffset, to.m_offset);
                return IsFiniteNonNegative(meters);
            }

            Vector3 fromWorldPosition = GetLanePosition(fromLane, from.m_offset);
            byte toLaneEntryOffset;
            PathUnit.CalculatePathPositionOffset(
                toLaneId,
                fromWorldPosition,
                out toLaneEntryOffset);
            Vector3 toLaneEntryPosition = GetLanePosition(toLane, toLaneEntryOffset);

            meters = Vector3.Distance(currentWorldPosition, toLaneEntryPosition) +
                     GetLanePortionDistance(toLane, toLaneEntryOffset, to.m_offset);
            return IsFiniteNonNegative(meters);
        }

        private static bool TryGetLane(
            PathUnit.Position position,
            NetManager netManager,
            out uint laneId,
            out NetLane lane)
        {
            laneId = 0;
            lane = default(NetLane);
            if (!HasNetworkBuffers(netManager))
            {
                return false;
            }

            NetSegment[] segmentBuffer = netManager.m_segments.m_buffer;
            if (position.m_segment == 0 || position.m_segment >= segmentBuffer.Length)
            {
                return false;
            }

            NetSegment segment = segmentBuffer[position.m_segment];
            if ((segment.m_flags & NetSegment.Flags.Created) == 0 ||
                (segment.m_flags & NetSegment.Flags.Deleted) != 0)
            {
                return false;
            }

            NetInfo info = segment.Info;
            if (info == null || info.m_lanes == null || position.m_lane >= info.m_lanes.Length)
            {
                return false;
            }

            laneId = PathManager.GetLaneID(position);
            NetLane[] laneBuffer = netManager.m_lanes.m_buffer;
            if (laneId == 0 || laneId >= (uint)laneBuffer.Length)
            {
                return false;
            }

            lane = laneBuffer[(int)laneId];
            return lane.m_segment == position.m_segment && IsFiniteNonNegative(lane.m_length);
        }

        private static bool HasNetworkBuffers(NetManager netManager)
        {
            return netManager != null && netManager.m_segments != null &&
                   netManager.m_segments.m_buffer != null && netManager.m_lanes != null &&
                   netManager.m_lanes.m_buffer != null;
        }

        private static Vector3 GetLanePosition(NetLane lane, byte offset)
        {
            return lane.CalculatePosition(offset * (1f / 255f));
        }

        private static float GetLanePortionDistance(NetLane lane, byte fromOffset, byte toOffset)
        {
            return lane.m_length * Math.Abs((int)toOffset - fromOffset) / 255f;
        }

        private static bool TryAddDistance(ref float total, float distance)
        {
            if (!IsFiniteNonNegative(distance))
            {
                return false;
            }

            total += distance;
            return IsFiniteNonNegative(total);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return value >= 0f && IsFinite(value);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
