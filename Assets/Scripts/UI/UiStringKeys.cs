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
            "home.endlessHint", "home.zoneNamed",
            "zones.1", "zones.2", "zones.3", "zones.4", "zones.5",
            "zones.6", "zones.7", "zones.8", "zones.9", "zones.10",
            "home.tidepool", "home.settings", "home.chest",
            "zone.title", "zone.stars",
            "hud.goal.rescue", "hud.goal.rows", "hud.goal.tides", "hud.goal.score",
            "hud.back", "hud.coins",
            "booster.drainPump", "booster.bubblePop", "booster.newTide", "booster.popHint",
            "booster.pieceSwap", "booster.swapHint",
            "continue.title", "continue.body", "continue.ad", "continue.coins", "continue.decline",
            "upsell.removeAds", "hud.milestone",
            "hud.combo", "hud.best", "praise.double", "praise.triple", "praise.quad",
            "resume.title", "resume.body.voyage", "resume.body.endless", "resume.body.daily",
            "resume.continue", "resume.abandon", "resume.expired",
            "stats.title",
            "stats.rescued", "stats.stars", "stats.best", "stats.streak", "stats.decor",
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
            "shop.benefit1", "shop.benefit2", "shop.price.removeAds", "shop.owned", "shop.bestValue",
            "shop.pack.smallAmount", "shop.pack.mediumAmount", "shop.pack.largeAmount",
            "shop.price.small", "shop.price.medium", "shop.price.large",
            "pause.title", "pause.resume", "pause.home", "pause.map", "home.map",
            "consent.age.title", "consent.age.body", "consent.age.confirm",
            "errors.ad_unavailable", "errors.purchase_failed",
            "tidepool.title", "tidepool.rescued", "tidepool.never",
            "tidepool.decorations", "tidepool.owned", "tidepool.buy",
            "tidepool.edit", "tidepool.done", "tidepool.unknown", "tidepool.placeHint",
            "tidepool.tapHint",
            "tutorial.l1.drag", "tutorial.l1.clear", "tutorial.l2.meter",
            "tutorial.l3.rescue", "tutorial.l4.pump", "tutorial.l5.go",
            "common.back", "common.play",

            // Tidal Pop overhaul (UI spec §13).
            "tab.home", "home.play", "home.level", "home.zoneRibbon", "home.zoneStars",
            "home.levelProgress", "home.dailyShort", "home.dailyBoard", "home.dailyDone",
            "home.dailyNew", "home.endlessShort", "zone.locked",
            "results.zoneLevel", "results.scoreLabel", "stat.movesShort", "stat.tidesShort",
            "stat.rescuedShort", "results.home", "daily.intro.streak",
            "booster.drainName", "booster.popName", "booster.rerollName", "booster.swapName",
            "booster.free", "hud.chip",

            // GDD §15 Pets & Tides. Badge / event / quest / power keys are built from
            // content ids at runtime — every one is listed so the coverage test sees it.
            "tab.quests", "quests.title", "quests.daily", "quests.resets", "quests.claim", "quests.done",
            "quests.badges", "quests.badgesEarned",
            "quest.rescue", "quest.rescueSpecies", "quest.rows", "quest.deepClear", "quest.tides",
            "quest.score", "quest.levels", "quest.daily", "quest.powers", "quest.endless",
            "badge.first_rescue", "badge.rescuer_50", "badge.rescuer_250", "badge.species_4", "badge.species_8",
            "badge.rows_100", "badge.rows_1000", "badge.clear_3", "badge.clear_4", "badge.tides_100",
            "badge.stars_30", "badge.stars_150", "badge.levels_20", "badge.endless_5000", "badge.streak_7",
            "badge.decor_5", "badge.powers_25", "badge.quests_10", "badge.events_1", "badge.pet_max",
            "badgeStat.rescues", "badgeStat.species", "badgeStat.rows", "badgeStat.bestClear", "badgeStat.tides",
            "badgeStat.stars", "badgeStat.levels", "badgeStat.endlessBest", "badgeStat.streakBest",
            "badgeStat.decor", "badgeStat.powers", "badgeStat.quests", "badgeStat.events", "badgeStat.bestStage",
            "pet.level", "pet.next", "pet.maxed", "pet.makeBuddy", "pet.isBuddy",
            "pet.gift", "pet.giftClaimed", "pet.giftTomorrow",
            "power.Pinch", "power.Wish", "power.Steady", "power.Ink", "power.Calm", "power.Puff", "power.Glow",
            "power.Regrow", "power.Pinch.desc", "power.Wish.desc", "power.Steady.desc", "power.Ink.desc",
            "power.Calm.desc", "power.Puff.desc", "power.Glow.desc", "power.Regrow.desc", "power.popup",
            "home.eventShort", "home.eventDays", "home.eventLastDay", "home.eventWon",
            "event.title", "event.goal.rescue", "event.goal.rows", "event.goal.tides", "event.goal.score",
            "event.reward", "event.best", "event.won",
            "event.crab_carnival", "event.starfish_shower", "event.seahorse_stampede", "event.octopus_ink",
            "event.turtle_trek", "event.jelly_glow",
            "event.crab_carnival.desc", "event.starfish_shower.desc", "event.seahorse_stampede.desc",
            "event.octopus_ink.desc", "event.turtle_trek.desc", "event.jelly_glow.desc",
            "results.eventWin", "results.eventReward", "results.podBonus", "results.egg", "results.eggs", "results.powers",
            "tidepool.eventReward",
            "continue.secondWind", "pet.ready", "pet.growUp", "pet.tierLine", "pet.callOnly",
            "tier.common", "tier.rare", "tier.epic", "tier.legendary",

            // Home "Buddy Stage" (2026-10-18).
            "home.petGift", "buddy.next", "buddy.gift", "buddy.daily", "buddy.cheer1", "buddy.cheer2",
            "buddy.cheer3", "buddy.power", "buddy.pick",
        };
    }
}
