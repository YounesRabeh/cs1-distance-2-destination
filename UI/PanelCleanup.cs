// Removes Distance 2 Destination UI from every patched information-panel type.
// Keeps layout cleanup available even when the Harmony implementation is unavailable.
using System;
using DistanceToDestination.Patches;
using UnityEngine;

namespace DistanceToDestination.UI
{
    /// <summary>
    /// Coordinates idempotent cleanup for all distance labels owned by the mod.
    /// </summary>
    internal static class PanelCleanup
    {
        /// <summary>
        /// Removes vehicle and citizen labels while isolating cleanup failures.
        /// </summary>
        internal static void CleanupAll()
        {
            try
            {
                VehicleInfoPanelPatch.Cleanup();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            try
            {
                CitizenInfoPanelPatch.Cleanup();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
