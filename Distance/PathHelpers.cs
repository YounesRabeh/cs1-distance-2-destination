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
        private const int LaneOffsetStep = 16;
        private const int ConnectorSamples = 16;
        private const int TangentOffsetStep = 4;
        private const float ConnectorHandleScale = 0.5f;
        private const float MaxConnectorHandleLength = 24f;
        private const float MinimumDirectionMagnitude = 0.0001f;

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

        /// <summary>
        /// Converts vanilla's encoded path-position phase into a PathUnit position index.
        /// </summary>
        private static int DecodePositionIndex(byte encodedPositionIndex)
        {
            // Vanilla PathVisualizer uses 0 for the initial 255 sentinel and index >> 1 otherwise.
            return encodedPositionIndex == byte.MaxValue ? 0 : encodedPositionIndex >> 1;
        }

        /// <summary>
        /// Checks that a path identifier can be safely dereferenced in the supplied buffer.
        /// </summary>
        private static bool IsPathIdInRange(uint pathId, PathUnit[] pathBuffer)
        {
            return pathId != 0 && pathId < (uint)pathBuffer.Length &&
                   pathId < PathManager.MAX_PATHUNIT_COUNT;
        }

        /// <summary>
        /// Checks that a PathUnit is created and still referenced by the simulation.
        /// </summary>
        private static bool IsAllocated(PathUnit unit)
        {
            return (unit.m_simulationFlags & PathUnit.SimulationFlags.FLAG_CREATED) != 0 &&
                   unit.m_referenceCount != 0;
        }

        /// <summary>
        /// Checks that pathfinding completed successfully for a PathUnit.
        /// </summary>
        private static bool IsReady(PathUnit unit)
        {
            byte flags = unit.m_pathFindFlags;
            return (flags & PathUnit.FLAG_READY) != 0 &&
                   (flags & (PathUnit.FLAG_QUEUED | PathUnit.FLAG_CALCULATING | PathUnit.FLAG_FAILED)) == 0;
        }

        /// <summary>
        /// Detects whether a PathUnit changed or was reused while its route was being read.
        /// </summary>
        private static bool IsSameUnitSnapshot(PathUnit expected, PathUnit current)
        {
            return IsAllocated(current) &&
                   expected.m_buildIndex == current.m_buildIndex &&
                   expected.m_nextPathUnit == current.m_nextPathUnit &&
                   expected.m_positionCount == current.m_positionCount &&
                   expected.m_pathFindFlags == current.m_pathFindFlags;
        }

        /// <summary>
        /// Checks that a path position resolves to a live lane in the current network buffers.
        /// </summary>
        private static bool IsValidNetworkPosition(PathUnit.Position position, NetManager netManager)
        {
            uint laneId;
            NetLane lane;
            return TryGetLane(position, netManager, out laneId, out lane);
        }

        /// <summary>
        /// Calculates the sampled distance between two consecutive route positions.
        /// </summary>
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

            // Path units do not retain the AI-specific connector curve. Approximate
            // it with a tangent-aligned cubic curve and sample that curve once.
            float connectorDistance = GetConnectorDistance(
                fromLane,
                from.m_offset,
                fromWorldPosition,
                toLane,
                toLaneEntryOffset,
                to.m_offset,
                toLaneEntryPosition);
            float toLaneDistance = GetLanePortionDistance(
                toLane,
                toLaneEntryOffset,
                to.m_offset);
            meters = connectorDistance + toLaneDistance;
            return IsFiniteNonNegative(meters);
        }

        /// <summary>
        /// Calculates only the untraversed part of the entity's current lane transition.
        /// </summary>
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

            meters = GetRemainingConnectorDistance(
                         currentWorldPosition,
                         fromLane,
                         from.m_offset,
                         fromWorldPosition,
                         toLane,
                         toLaneEntryOffset,
                         to.m_offset,
                         toLaneEntryPosition) +
                     GetLanePortionDistance(toLane, toLaneEntryOffset, to.m_offset);
            return IsFiniteNonNegative(meters);
        }

        /// <summary>
        /// Resolves and validates the network lane referenced by a path position.
        /// </summary>
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

        /// <summary>
        /// Checks that all network buffers required by distance calculation are available.
        /// </summary>
        private static bool HasNetworkBuffers(NetManager netManager)
        {
            return netManager != null && netManager.m_segments != null &&
                   netManager.m_segments.m_buffer != null && netManager.m_lanes != null &&
                   netManager.m_lanes.m_buffer != null;
        }

        /// <summary>
        /// Resolves a byte-normalized offset to a world position on a lane curve.
        /// </summary>
        private static Vector3 GetLanePosition(NetLane lane, byte offset)
        {
            return lane.CalculatePosition(offset * (1f / 255f));
        }

        /// <summary>
        /// Approximates a partial lane's arc length by sampling its vanilla Bezier curve.
        /// </summary>
        private static float GetLanePortionDistance(NetLane lane, byte fromOffset, byte toOffset)
        {
            int offsetDelta = Math.Abs((int)toOffset - fromOffset);
            if (offsetDelta == 0)
            {
                return 0f;
            }

            int sampleCount = Math.Max(1, (offsetDelta + LaneOffsetStep - 1) / LaneOffsetStep);
            float from = fromOffset * (1f / 255f);
            float to = toOffset * (1f / 255f);
            Vector3 previous = lane.CalculatePosition(from);
            float distance = 0f;

            for (int sample = 1; sample <= sampleCount; sample++)
            {
                float progress = sample / (float)sampleCount;
                Vector3 current = lane.CalculatePosition(from + ((to - from) * progress));
                distance += Vector3.Distance(previous, current);
                previous = current;
            }

            return distance;
        }

        /// <summary>
        /// Approximates the full arc length of a tangent-aligned lane connector.
        /// </summary>
        private static float GetConnectorDistance(
            NetLane fromLane,
            byte fromOffset,
            Vector3 fromPosition,
            NetLane toLane,
            byte toEntryOffset,
            byte toTargetOffset,
            Vector3 toPosition)
        {
            Vector3 control1;
            Vector3 control2;
            GetConnectorControls(
                fromLane,
                fromOffset,
                fromPosition,
                toLane,
                toEntryOffset,
                toTargetOffset,
                toPosition,
                out control1,
                out control2);

            Vector3 previous = fromPosition;
            float distance = 0f;
            for (int sample = 1; sample <= ConnectorSamples; sample++)
            {
                Vector3 current = GetCubicPosition(
                    fromPosition,
                    control1,
                    control2,
                    toPosition,
                    sample / (float)ConnectorSamples);
                distance += Vector3.Distance(previous, current);
                previous = current;
            }

            return distance;
        }

        /// <summary>
        /// Approximates the connector arc remaining after the entity's current position.
        /// </summary>
        private static float GetRemainingConnectorDistance(
            Vector3 currentWorldPosition,
            NetLane fromLane,
            byte fromOffset,
            Vector3 fromPosition,
            NetLane toLane,
            byte toEntryOffset,
            byte toTargetOffset,
            Vector3 toPosition)
        {
            Vector3 control1;
            Vector3 control2;
            GetConnectorControls(
                fromLane,
                fromOffset,
                fromPosition,
                toLane,
                toEntryOffset,
                toTargetOffset,
                toPosition,
                out control1,
                out control2);

            int nearestSample = 0;
            float nearestDistanceSquared = float.MaxValue;
            for (int sample = 0; sample <= ConnectorSamples; sample++)
            {
                Vector3 point = GetCubicPosition(
                    fromPosition,
                    control1,
                    control2,
                    toPosition,
                    sample / (float)ConnectorSamples);
                float distanceSquared = (point - currentWorldPosition).sqrMagnitude;
                if (distanceSquared < nearestDistanceSquared)
                {
                    nearestDistanceSquared = distanceSquared;
                    nearestSample = sample;
                }
            }

            Vector3 previous = currentWorldPosition;
            float distance = 0f;
            for (int sample = nearestSample + 1; sample <= ConnectorSamples; sample++)
            {
                Vector3 current = GetCubicPosition(
                    fromPosition,
                    control1,
                    control2,
                    toPosition,
                    sample / (float)ConnectorSamples);
                distance += Vector3.Distance(previous, current);
                previous = current;
            }

            if (nearestSample == ConnectorSamples)
            {
                distance = Vector3.Distance(currentWorldPosition, toPosition);
            }

            return distance;
        }

        /// <summary>
        /// Builds cubic control points from the connector chord and lane travel directions.
        /// </summary>
        private static void GetConnectorControls(
            NetLane fromLane,
            byte fromOffset,
            Vector3 fromPosition,
            NetLane toLane,
            byte toEntryOffset,
            byte toTargetOffset,
            Vector3 toPosition,
            out Vector3 control1,
            out Vector3 control2)
        {
            Vector3 chord = toPosition - fromPosition;
            float chordLength = chord.magnitude;
            if (chordLength <= MinimumDirectionMagnitude)
            {
                control1 = fromPosition;
                control2 = toPosition;
                return;
            }

            int fromDirection = fromOffset >= 128 ? 1 : -1;
            int toDirection = toTargetOffset >= toEntryOffset ? 1 : -1;
            Vector3 fromTangent = GetArrivalDirection(fromLane, fromOffset, fromDirection);
            Vector3 toTangent = GetDepartureDirection(toLane, toEntryOffset, toDirection);
            Vector3 chordDirection = chord / chordLength;

            if (!TryNormalize(ref fromTangent))
            {
                fromTangent = chordDirection;
            }
            if (!TryNormalize(ref toTangent))
            {
                toTangent = chordDirection;
            }

            float handleLength = Math.Min(
                chordLength * ConnectorHandleScale,
                MaxConnectorHandleLength);
            control1 = fromPosition + (fromTangent * handleLength);
            control2 = toPosition - (toTangent * handleLength);
        }

        /// <summary>
        /// Estimates the direction of travel as a route approaches a lane offset.
        /// </summary>
        private static Vector3 GetArrivalDirection(NetLane lane, byte offset, int direction)
        {
            int previousOffset = Math.Max(
                0,
                Math.Min(255, offset - (direction * TangentOffsetStep)));
            return GetLanePosition(lane, offset) -
                   GetLanePosition(lane, (byte)previousOffset);
        }

        /// <summary>
        /// Estimates the direction of travel as a route leaves a lane offset.
        /// </summary>
        private static Vector3 GetDepartureDirection(NetLane lane, byte offset, int direction)
        {
            int nextOffset = Math.Max(
                0,
                Math.Min(255, offset + (direction * TangentOffsetStep)));
            return GetLanePosition(lane, (byte)nextOffset) -
                   GetLanePosition(lane, offset);
        }

        /// <summary>
        /// Normalizes a finite, non-trivial direction vector in place.
        /// </summary>
        private static bool TryNormalize(ref Vector3 direction)
        {
            float magnitude = direction.magnitude;
            if (magnitude <= MinimumDirectionMagnitude || !IsFinite(magnitude))
            {
                return false;
            }

            direction /= magnitude;
            return IsFinite(direction);
        }

        /// <summary>
        /// Evaluates a cubic Bezier curve at normalized progress.
        /// </summary>
        private static Vector3 GetCubicPosition(
            Vector3 start,
            Vector3 control1,
            Vector3 control2,
            Vector3 end,
            float progress)
        {
            float inverse = 1f - progress;
            float inverseSquared = inverse * inverse;
            float progressSquared = progress * progress;
            return (start * (inverseSquared * inverse)) +
                   (control1 * (3f * inverseSquared * progress)) +
                   (control2 * (3f * inverse * progressSquared)) +
                   (end * (progressSquared * progress));
        }

        /// <summary>
        /// Adds a finite non-negative distance while guarding the accumulated total.
        /// </summary>
        private static bool TryAddDistance(ref float total, float distance)
        {
            if (!IsFiniteNonNegative(distance))
            {
                return false;
            }

            total += distance;
            return IsFiniteNonNegative(total);
        }

        /// <summary>
        /// Checks that every component of a world-space vector is finite.
        /// </summary>
        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        /// <summary>
        /// Checks that a scalar is finite and non-negative.
        /// </summary>
        private static bool IsFiniteNonNegative(float value)
        {
            return value >= 0f && IsFinite(value);
        }

        /// <summary>
        /// Checks that a scalar is neither NaN nor infinite.
        /// </summary>
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
