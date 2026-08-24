// Declares the mod metadata shown by Cities: Skylines.
// Ensures the shared Harmony dependency is initialized before gameplay loading begins.
using CitiesHarmony.API;
using ICities;

namespace RouteDistance
{
    /// <summary>
    /// Supplies Route Distance metadata and dependency lifecycle hooks to the game.
    /// </summary>
    public sealed class Mod : IUserMod
    {
        private const string Version = "1.0.0";

        /// <summary>
        /// Gets the versioned name displayed by Content Manager.
        /// </summary>
        public string Name
        {
            get { return "Route Distance v" + Version; }
        }

        /// <summary>
        /// Gets the concise Content Manager description.
        /// </summary>
        public string Description
        {
            get { return "Shows the remaining distance along a selected citizen or road vehicle's existing route."; }
        }

        /// <summary>
        /// Requests the supported CitiesHarmony installation when the mod is enabled.
        /// </summary>
        public void OnEnabled()
        {
            HarmonyHelper.EnsureHarmonyInstalled();
        }

        /// <summary>
        /// Removes Route Distance patches when the mod is disabled.
        /// </summary>
        public void OnDisabled()
        {
            if (HarmonyHelper.IsHarmonyInstalled)
            {
                Patcher.UnpatchAll();
            }
        }
    }
}
