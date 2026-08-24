using ICities;

namespace RouteDistance
{
    public sealed class Mod : IUserMod
    {
        public string Name
        {
            get { return "Route Distance"; }
        }

        public string Description
        {
            get { return "Shows the remaining distance along a selected citizen or road vehicle's existing route."; }
        }
    }
}
