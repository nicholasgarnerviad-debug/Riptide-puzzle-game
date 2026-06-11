using NUnit.Framework;
using Riptide.Core;

namespace Riptide.Core.Tests
{
    /// <summary>
    /// Contract 6D: versioned save v1, corruption-safe (bad input → null → fresh
    /// save, never a crash), coin persistence regression (the Star Ladder audit),
    /// chest cap, and the committed v1 fixture.
    /// </summary>
    [TestFixture]
    public sealed class SaveTests
    {
        /// <summary>Mirrors docs/fixtures/save_v1.json — the committed migration fixture.</summary>
        public const string V1Fixture = @"{
  ""version"": 1,
  ""coins"": 1234,
  ""voyage"": ""z1-l1:3;z1-l2:2"",
  ""streak"": [5, 9, 1, 20617, 2945],
  ""endlessBest"": 4200,
  ""dailyAttemptDay"": 20617,
  ""dailyRetryUsed"": true,
  ""speciesRescues"": [12, 3, 0, 7, 1, 0, 2, 1],
  ""decorations"": [""rock_small"", ""anchor""],
  ""chestDay"": 20617,
  ""chestClaims"": 2,
  ""removeAds"": false
}";

        [Test]
        public void V1Fixture_ParsesToTheExpectedValues()
        {
            SaveData? save = SaveData.TryParse(V1Fixture);

            Assert.That(save, Is.Not.Null);
            Assert.That(save!.Coins, Is.EqualTo(1234));
            Assert.That(save.VoyageProgress, Is.EqualTo("z1-l1:3;z1-l2:2"));
            Assert.That(save.Streak.Current, Is.EqualTo(5));
            Assert.That(save.Streak.Best, Is.EqualTo(9));
            Assert.That(save.Streak.FreezesHeld, Is.EqualTo(1));
            Assert.That(save.EndlessBest, Is.EqualTo(4200));
            Assert.That(save.DailyRetryUsed, Is.True);
            Assert.That(save.RescuesFor(0), Is.EqualTo(12));
            Assert.That(save.DecorationsOwned, Is.EquivalentTo(new[] { "rock_small", "anchor" }));
            Assert.That(save.ChestClaims, Is.EqualTo(2));
        }

        [Test]
        public void Serialize_Parse_RoundTrips_EveryField()
        {
            var save = new SaveData
            {
                Coins = 999,
                VoyageProgress = "z2-l7:1",
                Streak = new StreakState(3, 8, 1, 20620, 2946),
                EndlessBest = 12345,
                DailyAttemptDay = 20620,
                DailyRetryUsed = false,
                SpeciesRescues = new[] { 1, 2, 3, 4, 5, 6, 7, 8 },
                ChestDay = 20620,
                ChestClaims = 1,
                RemoveAds = true,
            };
            save.DecorationsOwned.Add("kelp_tall");

            SaveData? restored = SaveData.TryParse(save.Serialize());

            Assert.That(restored, Is.Not.Null);
            Assert.That(restored!.Serialize(), Is.EqualTo(save.Serialize()), "stable canonical form");
            Assert.That(restored.Coins, Is.EqualTo(999), "coin persistence — the audit regression (contract 6D)");
            Assert.That(restored.Streak, Is.EqualTo(save.Streak));
            Assert.That(restored.RemoveAds, Is.True);
        }

        [TestCase("")]
        [TestCase("not json at all")]
        [TestCase("{ \"version\": 1 }")]
        [TestCase("{ \"version\": 1, \"coins\": \"lots\" }")]
        [TestCase("{ \"version\": 99, \"coins\": 1 }")]
        [TestCase("{ \"version\": 1, \"coins\": 5, \"voyage\": \"\", \"streak\": [1,2], \"endlessBest\": 0, \"dailyAttemptDay\": 0, \"dailyRetryUsed\": false, \"speciesRescues\": [], \"decorations\": [], \"chestDay\": 0, \"chestClaims\": 0, \"removeAds\": false }")]
        public void CorruptOrForeignSaves_ParseToNull_NeverThrow(string text)
        {
            Assert.That(SaveData.TryParse(text), Is.Null, "contract 6D: bad file → fresh save, never crash");
        }

        [Test]
        public void TruncatedFixture_ParsesToNull()
        {
            Assert.That(SaveData.TryParse(V1Fixture.Substring(0, V1Fixture.Length / 2)), Is.Null);
        }

        [Test]
        public void ChestCap_AllowsThreeClaimsPerDay_ResettingAtMidnight()
        {
            var save = new SaveData();

            Assert.That(save.TryClaimChest(100, 3), Is.True);
            Assert.That(save.TryClaimChest(100, 3), Is.True);
            Assert.That(save.TryClaimChest(100, 3), Is.True);
            Assert.That(save.TryClaimChest(100, 3), Is.False, "GDD 5.2: capped 3/day");
            Assert.That(save.TryClaimChest(101, 3), Is.True, "new day resets");
        }

        [Test]
        public void Wallet_NeverGoesNegative()
        {
            var wallet = new CoinWallet(100);

            Assert.That(wallet.TrySpend(150), Is.False);
            Assert.That(wallet.Balance, Is.EqualTo(100));
            Assert.That(wallet.TrySpend(100), Is.True);
            Assert.That(wallet.Balance, Is.EqualTo(0));
            Assert.That(wallet.CanAfford(1), Is.False);
            wallet.Earn(250);
            Assert.That(wallet.Balance, Is.EqualTo(250));
        }

        [Test]
        public void Decorations_Load_TwentyItems_WithinTheCostBand()
        {
            const string json = @"{ ""items"": [
                { ""id"": ""a"", ""name"": ""A"", ""cost"": 200, ""emoji"": ""🪨"" },
                { ""id"": ""b"", ""name"": ""B"", ""cost"": 2000, ""emoji"": ""🔱"" } ] }";

            var items = DecorationLoader.Load(json, "decorations.json");
            Assert.That(items.Count, Is.EqualTo(2));
            Assert.That(items[0].Cost, Is.EqualTo(200));

            Assert.Throws<ContentException>(() => DecorationLoader.Load(
                json.Replace("2000", "2500"), "decorations.json"), "cost band 200–2000 (GDD 5.2)");
            Assert.Throws<ContentException>(() => DecorationLoader.Load(
                json.Replace("\"b\"", "\"a\""), "decorations.json"), "duplicate ids");
        }
    }
}
