using System;
using System.Collections.Generic;
using ColossalFramework;

namespace RouteDistance.Distance
{
    internal static class PathHelpers
    {
        internal const int MaxPathUnits = 4096;

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
            catch (Exception)
            {
                // Simulation buffers are mutable. A transient invalidation is an unavailable result.
                return false;
            }

            return false;
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

            uint laneId = PathManager.GetLaneID(position);
            NetLane[] laneBuffer = netManager.m_lanes.m_buffer;
            if (laneId == 0 || laneId >= (uint)laneBuffer.Length)
            {
                return false;
            }

            return laneBuffer[(int)laneId].m_segment == position.m_segment;
        }
    }
}
