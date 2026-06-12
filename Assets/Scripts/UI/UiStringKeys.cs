namespace Riptide.UI
{
    /// <summary>
    /// Every strings.json key the UI layer renders (5-UI-a ✅ coverage contract).
    /// Screens pull copy exclusively through Strings.Get with keys listed here;
    /// the coverage test asserts each resolves, so a typo or a missing entry
    /// fails in CI instead of rendering a KeyNotFound at runtime.
    /// </summary>
    public static class UiStringKeys
    {
        public static readonly string[] All =
        {
            "app.title",
            "home.voyage", "home.voyageContinue", "home.endless", "home.endlessBest", "home.daily",
            "home.tidepool", "home.settings", "home.chest",
            "zone.title",
            "hud.goal.rescue", "hud.goal.rows", "hud.goal.tides", "hud.goal.score",
            "hud.back", "hud.coins",
            "booster.drainPump", "booster.bubblePop", "booster.newTide", "booster.popHint",
            "booster.pieceSwap", "booster.swapHint",
            "continue.title", "continue.body", "continue.ad", "continue.coins", "continue.decline",
            "upsell.removeAds", "hud.milestone",
            "hud.combo", "hud.best", "praise.double", "praise.triple", "praise.quad",
            "resume.title", "resume.body.voyage", "resume.body.endless", "resume.body.daily",
            "resume.continue", "resume.abandon", "resume.expired",
            "stats.title", "stats.line1", "stats.line2",
            "home.dailyReady", "home.zoneProgress",
            "results.win", "results.lose.drown", "results.lose.stuck", "results.lose.creature",
            "results.next", "results.retry", "results.map", "results.doubleCoins",
            "endless.gameOver", "endless.best", "endless.newBest", "results.newBest",
            "stat.score", "stat.moves", "stat.tides", "stat.rescued",
            "daily.title", "daily.attempted", "daily.intro.body", "daily.intro.dive",
            "daily.tides", "daily.retry", "daily.retryCoins", "daily.buyFreeze",
            "daily.freezeOwned", "daily.share", "daily.shared", "daily.streak",
            "daily.result.win", "daily.result.lose",
            "settings.title", "settings.audio", "settings.music", "settings.haptics",
            "settings.reducedMotion", "settings.consent", "settings.restore",
            "settings.privacy", "settings.terms", "settings.version",
            "shop.title", "shop.removeAds", "shop.promise", "shop.thanks",
            "shop.pack.small", "shop.pack.medium", "shop.pack.large", "shop.comingSoon",
            "pause.title", "pause.resume", "pause.home",
            "consent.age.title", "consent.age.body", "consent.age.confirm",
            "errors.ad_unavailable", "errors.purchase_failed",
            "tidepool.title", "tidepool.rescued", "tidepool.never",
            "tidepool.decorations", "tidepool.owned", "tidepool.buy",
            "tidepool.edit", "tidepool.done", "tidepool.unknown", "tidepool.placeHint",
            "tutorial.l1.drag", "tutorial.l1.clear", "tutorial.l2.meter",
            "tutorial.l3.rescue", "tutorial.l4.pump", "tutorial.l5.go",
            "common.back", "common.play",
        };
    }
}
