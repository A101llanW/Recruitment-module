namespace HR.Web.Services
{
    /// <summary>
    /// Temporary toggles for admin UI surfaces that are not ready for production navigation.
    /// </summary>
    public static class AdminFeatureFlags
    {
        /// <summary>
        /// Set to true when the Email Templates admin screen is ready to ship.
        /// Does not affect candidate send modals (Applications / Interviews): those keep
        /// Use Template vs Compose from Scratch regardless of this flag.
        /// </summary>
        public const bool EmailTemplatesAdminUiEnabled = false;
    }
}
