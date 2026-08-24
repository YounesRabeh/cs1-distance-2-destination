using CitiesHarmony.API;
using ICities;

namespace RouteDistance
{
    public sealed class Mod : IUserMod
    {
        private const string Version = "1.0.0";

        public string Name
        {
            get { return "Route Distance v" + Version; }
        }

        public string Description
        {
            get { return "Shows the remaining distance along a selected citizen or road vehicle's existing route."; }
        }

        public void OnEnabled()
        {
            HarmonyHelper.EnsureHarmonyInstalled();
        }

        public void OnDisabled()
        {
            if (HarmonyHelper.IsHarmonyInstalled)
            {
                Patcher.UnpatchAll();
            }
        }
    }
}
