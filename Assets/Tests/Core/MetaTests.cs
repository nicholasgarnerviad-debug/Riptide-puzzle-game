using NUnit.Framework;
using Riptide.Core;

namespace Riptide.Core.Tests
{
    /// <summary>
    /// Phase 5 headless acceptance: stars, coin awards, civil-date math, streak
    /// with weekly freeze, voyage progression, and the share-card golden.
    /// </summary>
    [TestFixture]
    public sealed class MetaTests
    {
        // ---------------- stars (GDD 3.1) ----------------

        [Test]
        public void Stars_ThreeAtPar_TwoAtCeil14_OneBeyond()
        {
            Assert.That(StarRating.For(10, 10), Is.EqualTo(3), "at par");
            Assert.That(StarRating.For(9, 10), Is.EqualTo(3));
            Assert.That(StarRating.For(11, 10), Is.EqualTo(2));
            Assert.That(StarRating.For(14, 10), Is.EqualTo(2), "par x1.4 exactly");
            Assert.That(StarRating.For(15, 10), Is.EqualTo(1));
            Assert.That(StarRating.For(10, 7), Is.EqualTo(2), "ceil(7 x 1.4) = 10 (DECISIONS: ceil is generous)");
            Assert.That(StarRating.For(11, 7), Is.EqualTo(1));
        }

        // ---------------- coins (GDD 5.2) ----------------

        [Test]
        public void Coins_LevelAward_SpansTheGddRange()
        {
            CoinsConfig coins = TestKit.Economy().Coins;

            Assert.That(CoinRules.LevelCompleteAward(coins, 1, 1), Is.EqualTo(20), "GDD 5.2 floor");
            Assert.That(CoinRules.LevelCompleteAward(coins, 10, 3), Is.EqualTo(57), "near the 60 ceiling");
            Assert.That(CoinRules.LevelCompleteAward(coins, 5, 2), Is.EqualTo(20 + 12 + 5));
        }

        [Test]
        public void Coins_StreakMilestones_PayPerGdd()
        {
            CoinsConfig coins = TestKit.Economy().Coins;

            Assert.That(CoinRules.StreakMilestoneAward(coins, 7), Is.EqualTo(200));
            Assert.That(CoinRules.StreakMilestoneAward(coins, 30), Is.EqualTo(750));
            Assert.That(CoinRules.StreakMilestoneAward(coins, 100), Is.EqualTo(2000));
            Assert.That(CoinRules.StreakMilestoneAward(coins, 8), Is.EqualTo(0));
        }

        // ---------------- civil dates ----------------

        [Test]
        public void CivilDate_MatchesKnownAnchors()
        {
            Assert.That(CivilDate.ToEpochDays(1970, 1, 1), Is.EqualTo(0));
            Assert.That(CivilDate.ToEpochDays(1970, 1, 2), Is.EqualTo(1));
            Assert.That(CivilDate.ToEpochDays(2000, 1, 1), Is.EqualTo(10957));
            Assert.That(CivilDate.ToEpochDays(2024, 2, 29), Is.EqualTo(19782), "leap day");
            Assert.That(CivilDate.ToEpochDays(2026, 6, 11) - CivilDate.ToEpochDays(2026, 6, 1), Is.EqualTo(10));
        }

        [Test]
        public void CivilDate_WeekIndex_IsMondayAligned()
        {
            long sunday = CivilDate.ToEpochDays(1970, 1, 4);
            long monday = CivilDate.ToEpochDays(1970, 1, 5);

            Assert.That(CivilDate.WeekIndex(monday), Is.EqualTo(CivilDate.WeekIndex(sunday) + 1),
                "weeks roll over on Monday");
        }

        [Test]
        public void CivilDate_ParsesStrictIsoDates()
        {
            Assert.That(CivilDate.TryParseIsoDate("2026-06-11", out int y, out int m, out int d), Is.True);
            Assert.That((y, m, d), Is.EqualTo((2026, 6, 11)));
            Assert.That(CivilDate.TryParseIsoDate("2026-6-11", out _, out _, out _), Is.False);
            Assert.That(CivilDate.TryParseIsoDate("2026-13-01", out _, out _, out _), Is.False);
            Assert.That(CivilDate.TryParseIsoDate("garbage-da", out _, out _, out _), Is.False);
        }

        [Test]
        public void DailyNumber_CountsFromTheEpochDate()
        {
            DailyTuning daily = TestKit.Economy().Daily;
            long epoch = CivilDate.ToEpochDays(2026, 6, 11);

            Assert.That(daily.DailyNumber(epoch), Is.EqualTo(1), "launch day is #1");
            Assert.That(daily.DailyNumber(epoch + 141), Is.EqualTo(142), "the GDD example card");
        }

        // ---------------- streak (GDD 3.3) ----------------

        [Test]
        public void Streak_CountsConsecutiveDays_AndResetsOnGaps()
        {
            long day = CivilDate.ToEpochDays(2026, 6, 11);
            StreakState s = StreakLogic.CompleteDaily(StreakState.Empty, day);
            Assert.That(s.Current, Is.EqualTo(1));

            s = StreakLogic.CompleteDaily(s, day + 1);
            Assert.That(s.Current, Is.EqualTo(2));

            s = StreakLogic.CompleteDaily(s, day + 1);
            Assert.That(s.Current, Is.EqualTo(2), "same day never double-counts");

            s = StreakLogic.CompleteDaily(s, day + 4);
            Assert.That(s.Current, Is.EqualTo(1), "a 2-day hole without a freeze resets");
            Assert.That(s.Best, Is.EqualTo(2), "best survives the reset");
        }

        [Test]
        public void Streak_AFreezeBridges_ExactlyOneMissedDay()
        {
            long day = CivilDate.ToEpochDays(2026, 6, 11);
            StreakState s = StreakLogic.CompleteDaily(StreakState.Empty, day);
            s = StreakLogic.CompleteDaily(s, day + 1);
            s = StreakLogic.AcquireFreeze(s, day + 1);

            s = StreakLogic.CompleteDaily(s, day + 3);
            Assert.That(s.Current, Is.EqualTo(3), "freeze bridged the missed day (GDD 3.3 forgiveness)");
            Assert.That(s.FreezesHeld, Is.EqualTo(0), "consumed");

            StreakState withoutFreeze = StreakLogic.CompleteDaily(s, day + 6);
            Assert.That(withoutFreeze.Current, Is.EqualTo(1), "no freeze held — gap resets");
        }

        [Test]
        public void Streak_FreezeAcquisition_OncePerWeek_MaxOneHeld()
        {
            long monday = CivilDate.ToEpochDays(1970, 1, 5);
            StreakState s = StreakLogic.CompleteDaily(StreakState.Empty, monday);

            Assert.That(StreakLogic.CanAcquireFreeze(s, monday), Is.True);
            s = StreakLogic.AcquireFreeze(s, monday);
            Assert.That(StreakLogic.CanAcquireFreeze(s, monday + 1), Is.False, "one held = cap");

            StreakState consumed = StreakLogic.CompleteDaily(s, monday + 1);
            consumed = StreakLogic.CompleteDaily(consumed, monday + 3);
            Assert.That(consumed.FreezesHeld, Is.EqualTo(0));
            Assert.That(StreakLogic.CanAcquireFreeze(consumed, monday + 3), Is.False,
                "already bought this week");
            Assert.That(StreakLogic.CanAcquireFreeze(consumed, monday + 7), Is.True,
                "next Monday opens a new week");
        }

        // ---------------- share card (GDD 3.3 — golden, verbatim) ----------------

        [Test]
        public void ShareCard_MatchesTheGddExample_Verbatim()
        {
            string card = ShareCard.Compose(
                dailyNumber: 142,
                finalWaterLevel: 6,
                rescuedSpeciesEmoji: new[] { "🦀", "⭐", "🐢" },
                tidesSurvived: 20,
                tidesTarget: 20,
                score: 14250,
                streak: 9);

            const string expected =
                "Riptide #142 🌊\n" +
                "🟦🟦🟦🟦🟦🟦⬛⬛⬛⬛\n" +
                "🦀⭐🐢 rescued · 20/20 tides\n" +
                "Score 14,250 · 🔥 streak 9\n" +
                "riptide.game/d/142";

            Assert.That(card, Is.EqualTo(expected), "GDD 3.3's example card is the golden");
        }

        [Test]
        public void ShareCard_LosingRun_OmitsRescuePrefix_AndCapsSpecies()
        {
            string card = ShareCard.Compose(143, 9, new string[0], 13, 20, 980, 1);

            Assert.That(card, Does.Contain("🟦🟦🟦🟦🟦🟦🟦🟦🟦⬛\n13/20 tides"));
            Assert.That(card, Does.Contain("Score 980 · 🔥 streak 1"));

            string crowded = ShareCard.Compose(1, 1, new[] { "🦀", "⭐", "🐢", "🐙", "🐡" }, 20, 20, 100, 1);
            Assert.That(crowded, Does.Contain("🦀⭐🐢 rescued"), "max 3 species shown");
            // Ordinal: Mono's culture-sensitive IndexOf treats emoji as ignorable and
            // would call this a match (Unity-vs-dotnet divergence caught live).
            Assert.That(crowded.IndexOf("🐙", System.StringComparison.Ordinal), Is.EqualTo(-1),
                "the 4th species must not appear");
        }

        [Test]
        public void ShareCard_GroupsThousands_CultureIndependently()
        {
            Assert.That(ShareCard.GroupThousands(0), Is.EqualTo("0"));
            Assert.That(ShareCard.GroupThousands(999), Is.EqualTo("999"));
            Assert.That(ShareCard.GroupThousands(14250), Is.EqualTo("14,250"));
            Assert.That(ShareCard.GroupThousands(1234567), Is.EqualTo("1,234,567"));
            Assert.That(ShareCard.GroupThousands(-2500), Is.EqualTo("-2,500"));
        }

        // ---------------- voyage progress ----------------

        [Test]
        public void Voyage_UnlocksStrictlyInSequence_AcrossZones()
        {
            var progress = new VoyageProgress();

            Assert.That(progress.IsUnlocked(1, 1), Is.True, "1-1 always open");
            Assert.That(progress.IsUnlocked(1, 2), Is.False);

            progress.Record(VoyageProgress.LevelId(1, 1), 2);
            Assert.That(progress.IsUnlocked(1, 2), Is.True);

            for (int i = 1; i <= 20; i++)
            {
                progress.Record(VoyageProgress.LevelId(1, i), 1);
            }

            Assert.That(progress.IsUnlocked(2, 1), Is.True, "zone rollover");
            Assert.That(progress.NextLevel(), Is.EqualTo((2, 1)));
        }

        [Test]
        public void Voyage_KeepsBestStars_AndSerializesRoundTrip()
        {
            var progress = new VoyageProgress();
            progress.Record("z1-l1", 1);
            progress.Record("z1-l1", 3);
            progress.Record("z1-l1", 2);
            progress.Record("z2-l5", 2);

            Assert.That(progress.StarsFor("z1-l1"), Is.EqualTo(3), "best is kept");
            Assert.That(progress.TotalStars, Is.EqualTo(5));

            VoyageProgress restored = VoyageProgress.Deserialize(progress.Serialize());
            Assert.That(restored.StarsFor("z1-l1"), Is.EqualTo(3));
            Assert.That(restored.StarsFor("z2-l5"), Is.EqualTo(2));
            Assert.That(VoyageProgress.Deserialize(null).TotalStars, Is.EqualTo(0));
        }

        // ---------------- strings ----------------

        [Test]
        public void Strings_Load_AndMissingKeysThrow()
        {
            StringTable table = StringsLoader.Load(@"{ ""a.b"": ""Hello"" }", "strings.json");

            Assert.That(table.Get("a.b"), Is.EqualTo("Hello"));
            Assert.That(table.Has("nope"), Is.False);
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => table.Get("nope"));
            Assert.Throws<ContentException>(() => StringsLoader.Load(@"{ ""x"": """" }", "strings.json"));
        }
    }
}
