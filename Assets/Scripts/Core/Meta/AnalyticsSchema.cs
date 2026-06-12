namespace Riptide.Core
{
    /// <summary>
    /// GDD 8.5 event schema, verbatim. The Game-layer analytics service emits
    /// ONLY these names; a pinning test compares them character-for-character
    /// against the GDD text (contract 7D acceptance).
    /// </summary>
    public static class AnalyticsSchema
    {
        public const string LevelStart = "level_start";
        public const string LevelEnd = "level_end";
        public const string EndlessEnd = "endless_end";
        public const string DailyAttempt = "daily_attempt";
        public const string BoosterUsed = "booster_used";
        public const string AdImpression = "ad_impression";
        public const string IapPurchase = "iap_purchase";
        public const string TidepoolPurchase = "tidepool_purchase";
        public const string TutorialStep = "tutorial_step";
        public const string ContinueOffered = "continue_offered";
        public const string ContinueUsed = "continue_used";
        public const string ContinueDeclined = "continue_declined";

        public static readonly string[] LevelEndParams = { "zone", "level", "result", "moves", "stars", "maxWater", "rescues" };
        public static readonly string[] EndlessEndParams = { "placements", "tides", "score", "deathType" };
        public static readonly string[] DailyAttemptParams = { "result", "score", "retryUsed" };
        public static readonly string[] BoosterUsedParams = { "type", "source" };
        public static readonly string[] AdImpressionParams = { "format", "placement" };
    }
}
