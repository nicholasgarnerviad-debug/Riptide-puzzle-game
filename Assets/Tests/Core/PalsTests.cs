using System.Collections.Generic;
using NUnit.Framework;

namespace Riptide.Core.Tests
{
    /// <summary>
    /// GDD §16 Tidelings: the loader, owning and bond, growing up, the Pod and perks,
    /// eggs (grant / nest / warmth / deterministic hatch / odds / Deep Luck), egg
    /// sources, Second Wind, the v4 → v5 migration, the v5 save, and the
    /// board-touching perks on LevelConfig.
    /// </summary>
    [TestFixture]
    public sealed class PalsTests
    {
        private const string Json = @"{
  ""lines"": [
    { ""id"": 0, ""key"": ""crab"", ""name"": ""Crab"", ""rarity"": ""common"", ""boardSpecies"": 0, ""stages"": [""A"", ""B"", ""C""], ""perk"": ""coins"", ""perkValues"": [5, 10, 15] },
    { ""id"": 1, ""key"": ""star"", ""name"": ""Star"", ""rarity"": ""common"", ""boardSpecies"": 1, ""stages"": [""A"", ""B"", ""C""], ""perk"": ""starCoins"", ""perkValues"": [5, 10, 15] },
    { ""id"": 2, ""key"": ""horse"", ""name"": ""Horse"", ""rarity"": ""common"", ""boardSpecies"": 2, ""stages"": [""A"", ""B"", ""C""], ""perk"": ""tideInterval"", ""perkValues"": [1, 1, 2] },
    { ""id"": 3, ""key"": ""octo"", ""name"": ""Octo"", ""rarity"": ""uncommon"", ""boardSpecies"": 3, ""stages"": [""A"", ""B"", ""C""], ""perk"": ""boosterDiscount"", ""perkValues"": [10, 20, 30] },
    { ""id"": 4, ""key"": ""turtle"", ""name"": ""Turtle"", ""rarity"": ""uncommon"", ""boardSpecies"": 4, ""stages"": [""A"", ""B"", ""C""], ""perk"": ""startWater"", ""perkValues"": [1, 1, 2] },
    { ""id"": 5, ""key"": ""puff"", ""name"": ""Puff"", ""rarity"": ""uncommon"", ""boardSpecies"": 5, ""stages"": [""A"", ""B"", ""C""], ""perk"": ""freePops"", ""perkValues"": [1, 1, 2] },
    { ""id"": 6, ""key"": ""jelly"", ""name"": ""Jelly"", ""rarity"": ""rare"", ""boardSpecies"": 6, ""stages"": [""A"", ""B"", ""C""], ""perk"": ""incubation"", ""perkValues"": [25, 50, 75] },
    { ""id"": 7, ""key"": ""axo"", ""name"": ""Axo"", ""rarity"": ""epic"", ""boardSpecies"": 7, ""stages"": [""A"", ""B"", ""C""], ""perk"": ""freeContinues"", ""perkValues"": [1, 1, 2] },
    { ""id"": 8, ""key"": ""angler"", ""name"": ""Angler"", ""rarity"": ""epic"", ""boardSpecies"": -1, ""stages"": [""A"", ""B"", ""C""], ""perk"": ""deepLuck"", ""perkValues"": [2, 3, 4], ""perkValues2"": [5, 10, 100] },
    { ""id"": 9, ""key"": ""dragon"", ""name"": ""Dragon"", ""rarity"": ""legendary"", ""boardSpecies"": -1, ""stages"": [""A"", ""B"", ""C""], ""perk"": ""bondBoost"", ""perkValues"": [150, 200, 300] }
  ],
  ""growBond"": { ""common"": [15, 50], ""uncommon"": [12, 40], ""rare"": [10, 30], ""epic"": [8, 24], ""legendary"": [6, 18] },
  ""duplicateBond"": { ""common"": 3, ""uncommon"": 4, ""rare"": 6, ""epic"": 8, ""legendary"": 10 },
  ""rescueBond"": 1, ""runBond"": 1, ""leaderRunBond"": 2, ""sparkleOneIn"": 64, ""nestCapacity"": 2,
  ""podUnlocks"": [0, 4, 8], ""giftCoinsPerStage"": 3, ""giftCap"": 120, ""lineCompleteCoins"": 100,
  ""eggs"": [
    { ""id"": ""tide"", ""rowsToHatch"": 15, ""overflowCoins"": 30, ""weights"": { ""crab"": 50, ""star"": 30, ""horse"": 20 } },
    { ""id"": ""abyss"", ""rowsToHatch"": 80, ""overflowCoins"": 240, ""weights"": { ""angler"": 1, ""dragon"": 1 } }
  ],
  ""eggSources"": { ""welcome"": ""tide"", ""voyageMilestones"": { ""5"": ""tide"", ""20"": ""tide"" }, ""deepZoneFrom"": 6,
    ""deepZoneLevel20"": ""abyss"", ""dailyWin"": ""tide"", ""questsAllClaimed"": ""tide"", ""eventWin"": ""abyss"",
    ""endlessEveryTides"": 20, ""endlessEgg"": ""tide"", ""endlessMaxPerRun"": 2 }
}";

        private static PalsConfig Cfg() => PalsLoader.Load(Json, "fixture", 8);

        private static PalSave Owning(PalsConfig cfg, params int[] lines)
        {
            var pals = new PalSave { Seed = 12345UL };
            pals.EnsureLines(cfg.Lines.Count);
            foreach (int line in lines)
            {
                pals.Stage[line] = 1;
            }

            return pals;
        }

        // ---------------------------------------------------------------- loader

        [Test]
        public void Loader_ParsesLines_Eggs_AndSources()
        {
            PalsConfig cfg = Cfg();
            Assert.That(cfg.Lines.Count, Is.EqualTo(10));
            Assert.That(cfg.Lines[9].Rarity, Is.EqualTo(PalRarity.Legendary));
            Assert.That(cfg.Lines[8].BoardSpecies, Is.EqualTo(-1));
            Assert.That(cfg.Lines[8].Perk2At(3), Is.EqualTo(100));
            Assert.That(cfg.LineForSpecies(4), Is.EqualTo(4));
            Assert.That(cfg.Eggs[0].TotalWeight, Is.EqualTo(100));
            Assert.That(cfg.Eggs[1].Weights[9], Is.EqualTo(1));
            Assert.That(cfg.Sources.EventWin, Is.EqualTo(1));
            Assert.That(cfg.PodSize, Is.EqualTo(3));
        }

        [TestCase(@"""key"": ""star""", @"""key"": ""crab""", TestName = "Loader_Rejects_DuplicateKey")]
        [TestCase(@"""boardSpecies"": 7", @"""boardSpecies"": 6", TestName = "Loader_Rejects_ReusedBoardSpecies")]
        [TestCase(@"""horse"": 20", @"""ghost"": 20", TestName = "Loader_Rejects_UnknownWeightKey")]
        [TestCase(@"""common"": [15, 50]", @"""common"": [50, 15]", TestName = "Loader_Rejects_DescendingGrowBond")]
        [TestCase(@"""podUnlocks"": [0, 4, 8]", @"""podUnlocks"": [1, 4, 8]", TestName = "Loader_Rejects_ClosedFirstPodSlot")]
        [TestCase(@"""welcome"": ""tide""", @"""welcome"": ""golden""", TestName = "Loader_Rejects_UnknownEggSource")]
        [TestCase(@"""rarity"": ""epic"", ""boardSpecies"": 7", @"""rarity"": ""mega"", ""boardSpecies"": 7", TestName = "Loader_Rejects_UnknownRarity")]
        public void Loader_Rejects(string from, string to)
        {
            Assert.Throws<ContentException>(() => PalsLoader.Load(Json.Replace(from, to), "bad", 8));
        }

        [Test]
        public void Loader_Rejects_AMissingBoardSpeciesLine()
        {
            Assert.Throws<ContentException>(() => PalsLoader.Load(Json, "short", 9), "species 8 has no line");
        }

        // ------------------------------------------------------- owning & bond

        [Test]
        public void Rescue_OwnsTheLine_LeadsAnEmptyPod_AndBuildsBond()
        {
            PalsConfig cfg = Cfg();
            var pals = new PalSave();
            Assert.That(PalRules.RecordRescue(pals, cfg, 2, PalPerks.None), Is.True, "first rescue meets the pal");
            Assert.That(PalRules.RecordRescue(pals, cfg, 2, PalPerks.None), Is.False);
            Assert.That(pals.StageOf(2), Is.EqualTo(1));
            Assert.That(pals.BondOf(2), Is.EqualTo(2));
            Assert.That(pals.Leader, Is.EqualTo(2), "the first pal leads the Pod");
        }

        [Test]
        public void RunBond_GoesToThePod_LeaderDouble_DragonBoosted()
        {
            PalsConfig cfg = Cfg();
            PalSave pals = Owning(cfg, 0, 1, 2, 3);
            pals.Pod = new[] { 0, 1, -1 };
            PalRules.RecordRun(pals, cfg, PalPerks.None);
            Assert.That(pals.BondOf(0), Is.EqualTo(2));
            Assert.That(pals.BondOf(1), Is.EqualTo(1));
            Assert.That(pals.BondOf(2), Is.EqualTo(0), "off-Pod pals earn no run bond");

            var boosted = new PalPerks(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 150);
            PalRules.RecordRun(pals, cfg, boosted);
            Assert.That(pals.BondOf(0), Is.EqualTo(5), "2 × 1.5 = 3");
            Assert.That(pals.BondOf(1), Is.EqualTo(3), "1 × 1.5 rounds up to 2");
        }

        // --------------------------------------------------------- growing up

        [Test]
        public void Growing_FollowsRarityThresholds_AndStopsAtGrand()
        {
            PalsConfig cfg = Cfg();
            PalSave pals = Owning(cfg, 0, 9);
            Assert.That(PalRules.NextThreshold(cfg, 0, 1), Is.EqualTo(15));
            Assert.That(PalRules.NextThreshold(cfg, 9, 2), Is.EqualTo(18));
            pals.Bond[0] = 14;
            Assert.That(PalRules.TryGrow(pals, cfg, 0), Is.False);
            pals.Bond[0] = 50;
            Assert.That(PalRules.TryGrow(pals, cfg, 0), Is.True);
            Assert.That(PalRules.TryGrow(pals, cfg, 0), Is.True);
            Assert.That(pals.StageOf(0), Is.EqualTo(3));
            Assert.That(PalRules.NextThreshold(cfg, 0, 3), Is.Null);
            Assert.That(PalRules.TryGrow(pals, cfg, 0), Is.False);
            Assert.That(PalRules.LineRewardDue(pals, 0), Is.True);
            Assert.That(PalRules.TryGrow(pals, cfg, 5), Is.False, "unowned Tidelings never grow");
        }

        // ---------------------------------------------------------- pod & perks

        [Test]
        public void Pod_SlotsOpenByOwnedCount_AndOnlyOwnedPalsJoin()
        {
            PalsConfig cfg = Cfg();
            PalSave pals = Owning(cfg, 0, 1, 2);
            Assert.That(PalRules.PodSlotsOpen(cfg, 3), Is.EqualTo(1));
            Assert.That(PalRules.TrySetPod(pals, cfg, 1, 1), Is.False, "slot 2 opens at 4 pals");
            pals.Stage[3] = 1;
            Assert.That(PalRules.TrySetPod(pals, cfg, 0, 0), Is.True);
            Assert.That(PalRules.TrySetPod(pals, cfg, 1, 1), Is.True);
            Assert.That(PalRules.TrySetPod(pals, cfg, 1, 5), Is.False, "unowned");
            Assert.That(PalRules.TrySetPod(pals, cfg, 1, 0), Is.True, "moving the leader swaps");
            Assert.That(pals.Pod, Is.EqualTo(new[] { 1, 0, -1 }));
            Assert.That(PalRules.TrySetPod(pals, cfg, 0, -1), Is.False, "the leader slot is never empty");
        }

        [Test]
        public void Perks_SumThePod_AtCurrentStages()
        {
            PalsConfig cfg = Cfg();
            PalSave pals = Owning(cfg, 0, 2, 4, 6, 8, 9, 1, 3);
            pals.Stage[0] = 3;
            pals.Stage[8] = 3;
            pals.Pod = new[] { 0, 8, 9 };
            PalPerks perks = PalRules.Perks(pals, cfg);
            Assert.That(perks.CoinsPercent, Is.EqualTo(15));
            Assert.That(perks.SparkleMultiplier, Is.EqualTo(4));
            Assert.That(perks.EggUpgradePercent, Is.EqualTo(100));
            Assert.That(perks.BondPercent, Is.EqualTo(150));
            Assert.That(perks.TideIntervalBonus, Is.EqualTo(0), "the seahorse isn't in the Pod");

            Assert.That(PalRules.Perks(new PalSave(), cfg).BondPercent, Is.EqualTo(100), "no Pod is neutral");
        }

        [Test]
        public void Gift_IsStagesTimesCoins_Capped_ThenBoosted()
        {
            PalsConfig cfg = Cfg();
            PalSave pals = Owning(cfg, 0, 1);
            pals.Stage[1] = 3;
            Assert.That(PalRules.GiftCoins(pals, cfg, PalPerks.None), Is.EqualTo(12));
            var boost = new PalPerks(0, 0, 0, 0, 0, 0, 0, 0, 50, 0, 1, 0, 100);
            Assert.That(PalRules.GiftCoins(pals, cfg, boost), Is.EqualTo(18));
            for (int i = 0; i < 10; i++)
            {
                pals.Stage[i] = 3;
            }

            Assert.That(PalRules.GiftCoins(pals, cfg, PalPerks.None), Is.EqualTo(90));
        }

        // ------------------------------------------------------------------ eggs

        [Test]
        public void Eggs_Incubate_Queue_Overflow_Warm_AndHatch()
        {
            PalsConfig cfg = Cfg();
            var pals = new PalSave { Seed = 7UL };
            pals.EnsureLines(cfg.Lines.Count);
            Assert.That(PalRules.GrantEgg(pals, cfg, 0, PalPerks.None, out _), Is.EqualTo(0));
            Assert.That(pals.Incubating, Is.EqualTo(0), "the first egg goes straight in");
            PalRules.GrantEgg(pals, cfg, 1, PalPerks.None, out _);
            PalRules.GrantEgg(pals, cfg, 0, PalPerks.None, out _);
            Assert.That(pals.Nest, Is.EqualTo(new[] { 1, 0 }));
            Assert.That(PalRules.GrantEgg(pals, cfg, 1, PalPerks.None, out _), Is.EqualTo(240), "a full nest pays out");

            PalRules.Warm(pals, 10, PalPerks.None);
            Assert.That(PalRules.HatchReady(pals, cfg), Is.False);
            Assert.That(PalRules.Hatch(pals, cfg, PalPerks.None), Is.Null);
            var glow = new PalPerks(0, 0, 0, 0, 0, 0, 50, 0, 0, 0, 1, 0, 100);
            PalRules.Warm(pals, 4, glow); // 10 + 6 = 16 rows ≥ 15
            Assert.That(PalRules.HatchReady(pals, cfg), Is.True);

            HatchResult first = PalRules.Hatch(pals, cfg, PalPerks.None)!.Value;
            Assert.That(first.IsNew, Is.True);
            Assert.That(first.Line, Is.LessThanOrEqualTo(2), "tide eggs hold crab, star or horse");
            Assert.That(pals.Owns(first.Line), Is.True);
            Assert.That(pals.Incubating, Is.EqualTo(1), "the next nest egg starts warming");
            Assert.That(pals.Nest, Is.EqualTo(new[] { 0 }));
            Assert.That(pals.EggsHatched, Is.EqualTo(1));
        }

        [Test]
        public void Duplicates_AddBondByRarity()
        {
            PalsConfig cfg = Cfg();
            PalSave pals = Owning(cfg, 0, 1, 2);
            pals.Seed = 99UL;
            PalRules.GrantEgg(pals, cfg, 0, PalPerks.None, out _);
            PalRules.Warm(pals, 15, PalPerks.None);
            HatchResult r = PalRules.Hatch(pals, cfg, PalPerks.None)!.Value;
            Assert.That(r.IsNew, Is.False);
            Assert.That(r.BondGained, Is.EqualTo(3));
            Assert.That(pals.BondOf(r.Line), Is.EqualTo(3));
        }

        [Test]
        public void Roll_IsDeterministic_AndFollowsTheWeights()
        {
            PalsConfig cfg = Cfg();
            Assert.That(PalRules.Roll(cfg, 0, 42UL, 5, 1), Is.EqualTo(PalRules.Roll(cfg, 0, 42UL, 5, 1)));
            var counts = new int[cfg.Lines.Count];
            int sparkles = 0;
            const int n = 20000;
            for (int i = 1; i <= n; i++)
            {
                (int line, bool sparkle) = PalRules.Roll(cfg, 0, 42UL, i, 1);
                counts[line]++;
                sparkles += sparkle ? 1 : 0;
            }

            Assert.That(counts[0] / (double)n, Is.EqualTo(0.50).Within(0.02));
            Assert.That(counts[1] / (double)n, Is.EqualTo(0.30).Within(0.02));
            Assert.That(counts[2] / (double)n, Is.EqualTo(0.20).Within(0.02));
            Assert.That(counts[5], Is.Zero, "weight-0 lines never hatch");
            Assert.That(sparkles / (double)n, Is.EqualTo(1 / 64.0).Within(0.005));

            IReadOnlyList<(int line, float percent)> odds = PalRules.Odds(cfg, 0);
            Assert.That(odds.Count, Is.EqualTo(3));
            Assert.That(odds[0].percent, Is.EqualTo(50f).Within(0.001f));
        }

        [Test]
        public void DeepLuck_MultipliesSparkles_AndLiftsEggs()
        {
            PalsConfig cfg = Cfg();
            int sparkles = 0;
            for (int i = 1; i <= 20000; i++)
            {
                sparkles += PalRules.Roll(cfg, 0, 3UL, i, 4).sparkle ? 1 : 0;
            }

            Assert.That(sparkles / 20000.0, Is.EqualTo(4 / 64.0).Within(0.008));
            Assert.That(PalRules.UpgradeRoll(cfg, 0, 3UL, 1, 0), Is.EqualTo(0));
            Assert.That(PalRules.UpgradeRoll(cfg, 0, 3UL, 1, 100), Is.EqualTo(1));
            Assert.That(PalRules.UpgradeRoll(cfg, 1, 3UL, 1, 100), Is.EqualTo(1), "the top tier can't lift");
        }

        // --------------------------------------------------------------- sources

        [Test]
        public void Sources_VoyageMilestones_DeepZones_Keys_AndSecondWind()
        {
            PalsConfig cfg = Cfg();
            Assert.That(PalRules.VoyageMilestoneEgg(cfg, 1, 5), Is.EqualTo(0));
            Assert.That(PalRules.VoyageMilestoneEgg(cfg, 1, 6), Is.EqualTo(-1));
            Assert.That(PalRules.VoyageMilestoneEgg(cfg, 3, 20), Is.EqualTo(0));
            Assert.That(PalRules.VoyageMilestoneEgg(cfg, 7, 20), Is.EqualTo(1), "deep zones' finales lay abyss eggs");

            var pals = new PalSave();
            Assert.That(PalRules.ClaimKey(pals, "voyage:z1-l5"), Is.True);
            Assert.That(PalRules.ClaimKey(pals, "voyage:z1-l5"), Is.False);

            var wind = new PalPerks(0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 1, 0, 100);
            Assert.That(PalRules.FreeContinueLeft(pals, 100, PalPerks.None), Is.False);
            Assert.That(PalRules.FreeContinueLeft(pals, 100, wind), Is.True);
            PalRules.UseFreeContinue(pals, 100);
            PalRules.UseFreeContinue(pals, 100);
            Assert.That(PalRules.FreeContinueLeft(pals, 100, wind), Is.False);
            Assert.That(PalRules.FreeContinueLeft(pals, 101, wind), Is.True, "a new day refills");
        }

        // ------------------------------------------------------------- the save

        [Test]
        public void Migration_SeedsPalsFromRescues_AndTheBuddyLeads()
        {
            PalsConfig cfg = Cfg();
            var pals = new PalSave();
            PalRules.MigrateLegacy(pals, cfg, new[] { 20, 0, 3, 0, 0, 0, 0, 1 }, 2);
            Assert.That(pals.StageOf(0), Is.EqualTo(1));
            Assert.That(pals.BondOf(0), Is.EqualTo(20));
            Assert.That(pals.Owns(1), Is.False);
            Assert.That(pals.Leader, Is.EqualTo(2));
            Assert.That(PalRules.CanGrow(pals, cfg, 0), Is.True, "20 rescues already earn stage II");
        }

        [Test]
        public void SaveV5_RoundTrips_Pals()
        {
            var save = new SaveData();
            save.Pals.EnsureLines(4);
            save.Pals.Stage[1] = 2;
            save.Pals.Bond[1] = 33;
            save.Pals.Sparkle[1] = true;
            save.Pals.Nest.Add(2);
            save.Pals.Incubating = 1;
            save.Pals.Warmth = 1250;
            save.Pals.Pod = new[] { 1, -1, -1 };
            save.Pals.EggKeys.Add("voyage:z1-l5");
            save.Pals.Seed = ulong.MaxValue - 3;
            save.Pals.WelcomeGiven = true;
            save.Pals.EggsHatched = 4;

            SaveData back = SaveData.TryParse(save.Serialize())!;
            Assert.That(back, Is.Not.Null);
            Assert.That(back.PalsNeedMigration, Is.False);
            Assert.That(back.Pals.Serialize(), Is.EqualTo(save.Pals.Serialize()));
            Assert.That(back.Pals.Seed, Is.EqualTo(ulong.MaxValue - 3));
        }

        [Test]
        public void SaveV4_FlagsMigration_AndMalformedPalsAreCorrupt()
        {
            string v5 = new SaveData().Serialize();
            string v4 = v5.Replace("\"version\": 5", "\"version\": 4");
            int palsAt = v4.IndexOf(",\n  \"pals\"", System.StringComparison.Ordinal);
            v4 = v4.Substring(0, palsAt) + "\n}";
            SaveData old = SaveData.TryParse(v4)!;
            Assert.That(old, Is.Not.Null);
            Assert.That(old.PalsNeedMigration, Is.True);

            Assert.That(SaveData.TryParse(v5.Replace("\"stage\": []", "\"stage\": [7]")), Is.Null);
            Assert.That(SaveData.TryParse(v5.Replace("\"pod\": [-1, -1, -1]", "\"pod\": [-1]")), Is.Null);
        }

        // -------------------------------------------------- board-touching perks

        [Test]
        public void WithPalPerks_LengthensTheTide_AndLowersTheWaterFloored()
        {
            LevelConfig plain = TestKit.Config();
            Assert.That(plain.WithPalPerks(0, 0), Is.SameAs(plain));
            LevelConfig perked = plain.WithPalPerks(2, 5);
            Assert.That(perked.TideInterval, Is.EqualTo(plain.TideInterval + 2));
            Assert.That(perked.StartWaterLevel, Is.EqualTo(0));
            Assert.That(perked.MinWaterLevel, Is.LessThanOrEqualTo(perked.StartWaterLevel));
        }
    }
}
