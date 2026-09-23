using HR.Web.Models;

namespace HR.Web.Helpers
{
    public static class PositionViewTagHelper
    {
        public const int LoginsUntilTagHides = 5;

        public static bool ShouldShowRecentlyViewed(PositionView view, int currentLoginCount, bool positionIsOpen, bool hasApplied)
        {
            if (view == null || hasApplied)
            {
                return false;
            }

            if (view.IsOpenAtView != positionIsOpen)
            {
                return false;
            }

            var loginsSinceView = currentLoginCount - view.LoginCountAtView;
            if (loginsSinceView < 0)
            {
                loginsSinceView = 0;
            }

            return loginsSinceView < LoginsUntilTagHides;
        }
    }
}
