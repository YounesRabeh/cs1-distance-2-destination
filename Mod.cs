// Declares the mod metadata shown by Cities: Skylines.
// Ensures the shared Harmony dependency is initialized before gameplay loading begins.
using CitiesHarmony.API;
using ICities;

namespace DistanceToDestination
{
    /// <summary>
    /// Supplies Distance 2 Destination metadata and dependency lifecycle hooks to the game.
    /// </summary>
    public sealed class Mod : IUserMod
    {
        private const string Version = "1.1.0";

        /// <summary>
        /// Gets the versioned name displayed by Content Manager.
        /// </summary>
        public string Name
        {
            get { return "Distance 2 Destination v" + Version; }
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
            ModSettings.EnsureSettingsFile();
            HarmonyHelper.EnsureHarmonyInstalled();
        }

        /// <summary>
        /// Removes Distance 2 Destination patches when the mod is disabled.
        /// </summary>
        public void OnDisabled()
        {
            if (HarmonyHelper.IsHarmonyInstalled)
            {
                Patcher.UnpatchAll();
            }
        }

        /// <summary>
        /// Builds the Distance 2 Destination options shown in the game's Mods Settings menu.
        /// </summary>
        public void OnSettingsUI(UIHelperBase helper)
        {
            ModSettings.EnsureSettingsFile();

            UIHelperBase visibilityGroup = helper.AddGroup("Show distance for");
            visibilityGroup.AddCheckbox(
                "Service vehicles",
                ModSettings.ShowServiceVehicles,
                delegate(bool value) { ModSettings.ShowServiceVehicles = value; });
            visibilityGroup.AddCheckbox(
                "All other vehicles",
                ModSettings.ShowOtherVehicles,
                delegate(bool value) { ModSettings.ShowOtherVehicles = value; });
            visibilityGroup.AddCheckbox(
                "Pedestrians",
                ModSettings.ShowPedestrians,
                delegate(bool value) { ModSettings.ShowPedestrians = value; });

            helper.AddSpace(12);

            UIHelperBase unitGroup = helper.AddGroup("Units");
            unitGroup.AddDropdown(
                "Distance units",
                new[] { "Metric (m, km)", "Imperial (ft, mi)" },
                ModSettings.UnitSelection,
                delegate(int selection) { ModSettings.UseImperial = selection == 1; });
        }
    }
}
