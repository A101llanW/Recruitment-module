using HR.Web.Models;

namespace HR.Web.Helpers
{
    public static class PositionViewTagHelper
    {
        public static bool ShouldShowRecentlyViewed(PositionView view, int currentLoginCount, bool positionIsOpen, bool hasApplied)
        {
            if (view == null || hasApplied)
            {
                return false;
            }

            // Keep the tag while the position is still in the same open/closed state the candidate saw.
            return view.IsOpenAtView == positionIsOpen;
        }
    }
}
