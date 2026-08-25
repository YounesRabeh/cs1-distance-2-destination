// Stores Distance 2 Destination's persistent visibility and unit preferences.
// Exposes a small typed API shared by the settings menu and panel patches.
using ColossalFramework;

namespace DistanceToDestination
{
    /// <summary>
    /// Provides persistent user preferences backed by the game's settings system.
    /// </summary>
    internal static class ModSettings
    {
        internal const string SettingsFileName = "DistanceToDestination";

        private static readonly SavedBool ServiceVehiclesSetting =
            new SavedBool("ShowServiceVehicles", SettingsFileName, true, true);
        private static readonly SavedBool OtherVehiclesSetting =
            new SavedBool("ShowOtherVehicles", SettingsFileName, true, true);
        private static readonly SavedBool PedestriansSetting =
            new SavedBool("ShowPedestrians", SettingsFileName, true, true);
        private static readonly SavedInt UnitSetting =
            new SavedInt("DistanceUnit", SettingsFileName, 0, true);

        /// <summary>
        /// Gets or sets whether city-service vehicle panels show route distance.
        /// </summary>
        internal static bool ShowServiceVehicles
        {
            get { return ServiceVehiclesSetting.value; }
            set { ServiceVehiclesSetting.value = value; }
        }

        /// <summary>
        /// Gets or sets whether non-service car and bicycle panels show route distance.
        /// </summary>
        internal static bool ShowOtherVehicles
        {
            get { return OtherVehiclesSetting.value; }
            set { OtherVehiclesSetting.value = value; }
        }

        /// <summary>
        /// Gets or sets whether moving pedestrian panels show route distance.
        /// </summary>
        internal static bool ShowPedestrians
        {
            get { return PedestriansSetting.value; }
            set { PedestriansSetting.value = value; }
        }

        /// <summary>
        /// Gets or sets whether distances use imperial instead of metric units.
        /// </summary>
        internal static bool UseImperial
        {
            get { return UnitSetting.value == 1; }
            set { UnitSetting.value = value ? 1 : 0; }
        }

        /// <summary>
        /// Gets the dropdown index corresponding to the persisted unit preference.
        /// </summary>
        internal static int UnitSelection
        {
            get { return UseImperial ? 1 : 0; }
        }

        /// <summary>
        /// Ensures the game knows the settings file used by the saved-value wrappers.
        /// </summary>
        internal static void EnsureSettingsFile()
        {
            if (GameSettings.FindSettingsFileByName(SettingsFileName) == null)
            {
                GameSettings.AddSettingsFile(new SettingsFile
                {
                    fileName = SettingsFileName
                });
            }
        }
    }
}
