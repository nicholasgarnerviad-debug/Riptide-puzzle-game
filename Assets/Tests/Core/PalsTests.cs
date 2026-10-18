using System.Collections.Generic;
using NUnit.Framework;

namespace Riptide.Core.Tests
{
    /// <summary>
    /// GDD §16 Tidelings: the loader; befriending Commons by rescue and eggs from rarer
    /// board creatures; bond and growing up; the Pod and tiered abilities; eggs from
    /// rows; the Call (tier boundaries, pity, ten-call guarantee, determinism, odds,
    /// Sparkle); egg sources; Second Wind; migration; the v5 save; board perks.
    /// </summary>
    [TestFixture]
    public sealed class PalsTests
    {
        private const string Json = @"{
  ""lines"": [
    { ""id"": 0, ""key"": ""crab"", ""name"": ""Crab"", ""rarity"": ""common"", ""boardSpecies"": 0, ""stages"": [""A"", ""B"", ""C""], ""perk"": ""coins"", ""perkValues"": [5, 8, 12] },
    { ""id"": 1, ""key"": ""star"", ""name"": ""Star"", ""rarity"": ""common"", ""boardSpecies"": 1, ""stages"": [""A"", ""B"", ""C""], ""perk"": ""starCoins"", ""perkValues"": [3, 5, 8] },
    { ""id"": 2, ""key"": ""horse"", ""name"": ""Horse"", ""rarity"": ""common"", ""boardSpecies"": 2, ""stages"": [""A"", ""B"", ""C""], ""perk"": ""questBoost"", ""perkValues"": [10, 15, 25] },
    { ""id"": 3, ""key"": ""octo"", ""name"": ""Octo"", ""rarity"": ""common"", ""boardSpecies"": 3, ""stages"": [""A"", ""B"", ""C""], ""perk"": ""boosterDiscount"", ""perkValues"": [5, 10, 15] },
    { ""id"": 4, ""key"": ""turtle"", ""name"": ""Turtle"", ""rarity"": ""common"", ""boardSpecies"": 4, ""stages"": [""A"", ""B"", ""C""], ""perk"": ""giftBoost"", ""perkValues"": [20, 35, 50] },
    { ""id"": 5, ""key"": ""puff"", ""name"": ""Puff"", ""rarity"": ""common"", ""boardSpecies"": 5, ""stages"": [""A"", ""B"", ""C""], ""perk"": ""rescueCoins"", ""perkValues"": [2, 3, 5] },
    { ""id"": 6, ""key"": ""jelly"", ""name"": ""Jelly"", ""rarity"": ""rare"", ""boardSpecies"": 6, ""stages"": [""A"", ""B"", ""C""], ""perk"": ""eggPace"", ""perkValues"": [25, 50, 75] },
    { ""id"": 7, ""key"": ""otter"", ""name"": ""Otter"", ""rarity"": ""rare"", ""boardSpecies"": -1, ""stages"": [""A"", ""B"", ""C""], ""perk"": ""freeRerolls"", ""perkValues"": [1, 1, 2] },
    { ""id"": 8, ""key"": ""axo"", ""name"": ""Axo"", ""rarity"": ""epic"", ""boardSpecies"": 7, ""stages"": [""A"", ""B"", ""C""], ""perk"": ""freeContinues"", ""perkValues"": [1, 1, 2] },
    { ""id"": 9, ""key"": ""angler"", ""name"": ""Angler"", ""rarity"": ""epic"", ""boardSpecies"": -1, ""stages"": [""A"", ""B"", ""C""], ""perk"": ""deepLuck"", ""perkValues"": [2, 3, 4], ""perkValues2"": [5, 10, 20] },
    { ""id"": 10, ""key"": ""dragon"", ""name"": ""Dragon"", ""rarity"": ""legendary"", ""boardSpecies"": -1, ""stages"": [""A"", ""B"", ""C""], ""perk"": ""bondBoost"", ""perkValues"": [150, 200, 300] },
    { ""id"": 11, ""key"": ""whale"", ""name"": ""Whale"", ""rarity"": ""legendary"", ""boardSpecies"": -1, ""stages"": [""A"", ""B"", ""C""], ""perk"": ""whaleSong"", ""perkValues"": [1, 2, 2], ""perkValues2"": [1, 1, 2] }
  ],
  ""growBond"": { ""common"": [15, 50], ""rare"": [10, 30], ""epic"": [8, 24], ""legendary"": [6, 18] },
  ""duplicateBond"": { ""common"": 3, ""rare"": 6, ""epic"": 10, ""legendary"": 15 },
  ""rescueBond"": 1, ""runBond"": 1, ""leaderRunBond"": 2, ""sparkleOneIn"": 64,
  ""podUnlocks"": [0, 4, 8], ""giftCoinsPerStage"": 3, ""giftCap"": 120, ""lineCompleteCoins"": 100,
  ""call"": { ""cost"": 5, ""tenCost"": 45, ""tierRatesPermille"": { ""common"": 700, ""rare"": 220, ""epic"": 65, ""legendary"": 15 },
    ""epicPity"": 10, ""legendaryPity"": 30, ""tenCallGuaranteesRare"": true },
  ""eggs"": { ""boardsPerEgg"": 5, ""welcome"": 10, ""voyageMilestones"": { ""5"": 2, ""20"": 5 }, ""deepZoneFrom"": 6,
    ""deepZoneLevel20"": 8, ""dailyWin"": 2, ""questsAllClaimed"": 3, ""eventWin"": 10, ""endlessEveryTides"": 20,
    ""endlessEggs"": 1, ""endlessMaxPerRun"": 3, ""rareRescue"": 1 }
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
        public void Loader_ParsesTiers_TheCall_AndEggs()
        {
            PalsConfig cfg = Cfg();
            Assert.That(cfg.Lines.Count, Is.EqualTo(12));
            Assert.That(cfg.LinesOfTier(PalRarity.Common).Count, Is.EqualTo(6));
            Assert.That(cfg.LinesOfTier(PalRarity.Legendary), Is.EqualTo(new[] { 10, 11 }));
            Assert.That(cfg.Call.TierRatesPermille, Is.EqualTo(new[] { 700, 220, 65, 15 }));
            Assert.That(cfg.Eggs.VoyageMilestones[20], Is.EqualTo(5));
            Assert.That(cfg.LineForSpecies(7), Is.EqualTo(8));
        }

        [TestCase(@"""key"": ""star""", @"""key"": ""crab""", TestName = "Loader_Rejects_DuplicateKey")]
        [TestCase(@"""boardSpecies"": 7", @"""boardSpecies"": 6", TestName = "Loader_Rejects_ReusedBoardSpecies")]
        [TestCase(@"""legendary"": 15 }", @"""legendary"": 16 }", TestName = "Loader_Rejects_RatesNotSumming1000")]
        [TestCase(@"""rarity"": ""rare"", ""boardSpecies"": -1", @"""rarity"": ""uncommon"", ""boardSpecies"": -1", TestName = "Loader_Rejects_UnknownTier")]
        [TestCase(@"""common"": [15, 50]", @"""common"": [50, 15]", TestName = "Loader_Rejects_DescendingGrowBond")]
        [TestCase(@"""podUnlocks"": [0, 4, 8]", @"""podUnlocks"": [1, 4, 8]", TestName = "Loader_Rejects_ClosedFirstPodSlot")]
        [TestCase(@"""tenCost"": 45", @"""tenCost"": 51", TestName = "Loader_Rejects_TenCallDearerThanTenSingles")]
        [TestCase(@"""legendaryPity"": 30", @"""legendaryPity"": 5", TestName = "Loader_Rejects_LegendaryPityBelowEpic")]
        public void Loader_Rejects(string from, string to)
        {
            Assert.Throws<ContentException>(() => PalsLoader.Load(Json.Replace(from, to), "bad", 8));
        }

        [Test]
        public void Loader_Rejects_ATierWithOddsButNoTidelings()
        {
            string noLegends = Json
                .Replace(@"""rarity"": ""legendary"", ""boardSpecies"": -1, ""stages"": [""A"", ""B"", ""C""], ""perk"": ""bondBoost""",
                    @"""rarity"": ""epic"", ""boardSpecies"": -1, ""stages"": [""A"", ""B"", ""C""], ""perk"": ""bondBoost""")
                .Replace(@"""rarity"": ""legendary"", ""boardSpecies"": -1, ""stages"": [""A"", ""B"", ""C""], ""perk"": ""whaleSong""",
                    @"""rarity"": ""epic"", ""boardSpecies"": -1, ""stages"": [""A"", ""B"", ""C""], ""perk"": ""whaleSong""");
            Assert.Throws<ContentException>(() => PalsLoader.Load(noLegends, "empty-tier", 8));
        }

        // ---------------------------------------------------------- befriending

        [Test]
        public void Rescue_BefriendsCommons_ButRareBoardCreaturesLeaveEggs()
        {
            PalsConfig cfg = Cfg();
            var pals = new PalSave();
            RescueOutcome crab = PalRules.RecordRescue(pals, cfg, 0, PalPerks.None);
            Assert.That(crab.Befriended, Is.True);
            Assert.That(pals.Leader, Is.EqualTo(0), "the first Tideling leads the Pod");
            Assert.That(pals.BondOf(0), Is.EqualTo(1));

            RescueOutcome jelly = PalRules.RecordRescue(pals, cfg, 6, PalPerks.None);
            Assert.That(jelly.Befriended, Is.False, "rare Tidelings arrive only by Call");
            Assert.That(jelly.Eggs, Is.EqualTo(1));
            Assert.That(pals.Owns(6), Is.False);
            Assert.That(pals.Eggs, Is.EqualTo(1));

            pals.Stage[6] = 1;
            PalRules.RecordRescue(pals, cfg, 6, PalPerks.None);
            Assert.That(pals.BondOf(6), Is.EqualTo(1), "owned rares still bond from rescues");
            Assert.That(pals.Eggs, Is.EqualTo(2));
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
            Assert.That(pals.BondOf(2), Is.EqualTo(0), "off-Pod Tidelings earn no run bond");

            PalRules.RecordRun(pals, cfg, new PalPerks { BondPercent = 150 });
            Assert.That(pals.BondOf(0), Is.EqualTo(5), "2 × 1.5 = 3");
            Assert.That(pals.BondOf(1), Is.EqualTo(3), "1 × 1.5 rounds up to 2");
        }

        // ------------------------------------------------------------ growing up

        [Test]
        public void Growing_FollowsTierThresholds_AndStopsAtGrand()
        {
            PalsConfig cfg = Cfg();
            PalSave pals = Owning(cfg, 0, 10);
            Assert.That(PalRules.NextThreshold(cfg, 0, 1), Is.EqualTo(15));
            Assert.That(PalRules.NextThreshold(cfg, 10, 2), Is.EqualTo(18));
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
        public void Pod_SlotsOpenByOwnedCount_AndOnlyOwnedTidelingsJoin()
        {
            PalsConfig cfg = Cfg();
            PalSave pals = Owning(cfg, 0, 1, 2);
            Assert.That(PalRules.PodSlotsOpen(cfg, 3), Is.EqualTo(1));
            Assert.That(PalRules.TrySetPod(pals, cfg, 1, 1), Is.False, "slot 2 opens at 4 Tidelings");
            pals.Stage[3] = 1;
            Assert.That(PalRules.TrySetPod(pals, cfg, 0, 0), Is.True);
            Assert.That(PalRules.TrySetPod(pals, cfg, 1, 1), Is.True);
            Assert.That(PalRules.TrySetPod(pals, cfg, 1, 5), Is.False, "unowned");
            Assert.That(PalRules.TrySetPod(pals, cfg, 1, 0), Is.True, "moving the leader swaps");
            Assert.That(pals.Pod, Is.EqualTo(new[] { 1, 0, -1 }));
            Assert.That(PalRules.TrySetPod(pals, cfg, 0, -1), Is.False, "the leader slot is never empty");
        }

        [Test]
        public void Perks_SumThePod_AtCurrentStages_AcrossTiers()
        {
            PalsConfig cfg = Cfg();
            PalSave pals = Owning(cfg, 0, 1, 2, 3, 4, 9, 11, 10);
            pals.Stage[0] = 3;
            pals.Stage[9] = 3;
            pals.Stage[11] = 3;
            pals.Pod = new[] { 0, 9, 11 };
            PalPerks perks = PalRules.Perks(pals, cfg);
            Assert.That(perks.CoinsPercent, Is.EqualTo(12), "a Grand Common");
            Assert.That(perks.SparkleMultiplier, Is.EqualTo(4), "a Grand Epic");
            Assert.That(perks.LegendaryPityCut, Is.EqualTo(20));
            Assert.That(perks.TideIntervalBonus, Is.EqualTo(2), "a Grand Legendary's song");
            Assert.That(perks.StartWaterReduction, Is.EqualTo(2));
            Assert.That(perks.BondPercent, Is.EqualTo(100), "the dragon isn't in the Pod");
            Assert.That(PalRules.LegendaryPity(cfg, perks), Is.EqualTo(10), "pity cut, floored at the Epic pity");

            PalPerks none = PalRules.Perks(new PalSave(), cfg);
            Assert.That(none.BondPercent, Is.EqualTo(100));
            Assert.That(none.SparkleMultiplier, Is.EqualTo(1));
        }

        [Test]
        public void Gift_IsStagesTimesCoins_Capped_ThenBoosted()
        {
            PalsConfig cfg = Cfg();
            PalSave pals = Owning(cfg, 0, 1);
            pals.Stage[1] = 3;
            Assert.That(PalRules.GiftCoins(pals, cfg, PalPerks.None), Is.EqualTo(12));
            Assert.That(PalRules.GiftCoins(pals, cfg, new PalPerks { GiftPercent = 50 }), Is.EqualTo(18));
            for (int i = 0; i < 12; i++)
            {
                pals.Stage[i] = 3;
            }

            Assert.That(PalRules.GiftCoins(pals, cfg, PalPerks.None), Is.EqualTo(108));
            pals.EnsureLines(16);
            for (int i = 12; i < 16; i++)
            {
                pals.Stage[i] = 3;
            }

            Assert.That(PalRules.GiftCoins(pals, cfg, PalPerks.None), Is.EqualTo(120), "capped");
        }

        // ------------------------------------------------------------------ eggs

        [Test]
        public void Boards_LayAnEggEveryFive_SoonerWithTheJellyfish()
        {
            PalsConfig cfg = Cfg();
            var pals = new PalSave();
            for (int board = 1; board <= 4; board++)
            {
                Assert.That(PalRules.AddBoard(pals, cfg, PalPerks.None), Is.Zero);
            }

            Assert.That(PalRules.BoardsTowardEgg(pals), Is.EqualTo(4));
            Assert.That(PalRules.EggFraction(pals, cfg), Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(PalRules.AddBoard(pals, cfg, PalPerks.None), Is.EqualTo(1), "the fifth board lays it");
            Assert.That(PalRules.BoardsTowardEgg(pals), Is.Zero);

            var glow = new PalPerks { EggPacePercent = 75 };
            int boards = 0;
            while (pals.Eggs < 2)
            {
                PalRules.AddBoard(pals, cfg, glow);
                boards++;
            }

            Assert.That(boards, Is.EqualTo(3), "1.75 boards per board → an egg on the third");
        }

        // -------------------------------------------------------------- the Call

        [Test]
        public void TierFor_SplitsTheRoll_ByTheDisclosedRates()
        {
            int[] rates = { 700, 220, 65, 15 };
            Assert.That(PalRules.TierFor(0, rates, PalRarity.Common), Is.EqualTo(PalRarity.Common));
            Assert.That(PalRules.TierFor(699, rates, PalRarity.Common), Is.EqualTo(PalRarity.Common));
            Assert.That(PalRules.TierFor(700, rates, PalRarity.Common), Is.EqualTo(PalRarity.Rare));
            Assert.That(PalRules.TierFor(919, rates, PalRarity.Common), Is.EqualTo(PalRarity.Rare));
            Assert.That(PalRules.TierFor(920, rates, PalRarity.Common), Is.EqualTo(PalRarity.Epic));
            Assert.That(PalRules.TierFor(985, rates, PalRarity.Common), Is.EqualTo(PalRarity.Legendary));
            Assert.That(PalRules.TierFor(64, rates, PalRarity.Epic), Is.EqualTo(PalRarity.Epic), "pity rerolls among Epic+");
            Assert.That(PalRules.TierFor(65, rates, PalRarity.Epic), Is.EqualTo(PalRarity.Legendary));
        }

        [Test]
        public void Call_CostsEggs_Befriends_AndDuplicatesBond()
        {
            PalsConfig cfg = Cfg();
            var pals = new PalSave { Seed = 9UL, Eggs = 4 };
            Assert.That(PalRules.Call(pals, cfg, PalPerks.None), Is.Null, "short of eggs");
            pals.Eggs = 500;
            CallResult first = PalRules.Call(pals, cfg, PalPerks.None)!.Value;
            Assert.That(pals.Eggs, Is.EqualTo(495));
            Assert.That(first.IsNew, Is.True);
            Assert.That(pals.Owns(first.Line), Is.True);
            Assert.That(pals.Leader, Is.EqualTo(first.Line));

            bool sawDuplicate = false;
            for (int i = 0; i < 80 && !sawDuplicate; i++)
            {
                CallResult r = PalRules.Call(pals, cfg, PalPerks.None)!.Value;
                if (!r.IsNew)
                {
                    sawDuplicate = true;
                    Assert.That(r.BondGained, Is.EqualTo(cfg.DuplicateBond[(int)r.Tier]));
                }
            }

            Assert.That(sawDuplicate, Is.True);
        }

        [Test]
        public void Calls_AreDeterministic_FromTheSeed()
        {
            PalsConfig cfg = Cfg();
            var a = new PalSave { Seed = 777UL, Eggs = 90 };
            var b = new PalSave { Seed = 777UL, Eggs = 90 };
            IReadOnlyList<CallResult> ra = PalRules.CallTen(a, cfg, PalPerks.None)!;
            IReadOnlyList<CallResult> rb = PalRules.CallTen(b, cfg, PalPerks.None)!;
            for (int i = 0; i < 10; i++)
            {
                Assert.That(ra[i].Line, Is.EqualTo(rb[i].Line));
                Assert.That(ra[i].Sparkle, Is.EqualTo(rb[i].Sparkle));
            }

            Assert.That(a.Serialize(), Is.EqualTo(b.Serialize()));
        }

        [Test]
        public void Pity_CapsTheDroughts_AndTheRatesHold()
        {
            PalsConfig cfg = Cfg();
            var pals = new PalSave { Seed = 31337UL, Eggs = 5 * 6000 };
            int sinceEpic = 0, sinceLegend = 0, legends = 0, epicsPlus = 0, sparkles = 0;
            const int n = 6000;
            for (int i = 0; i < n; i++)
            {
                CallResult r = PalRules.Call(pals, cfg, PalPerks.None)!.Value;
                sinceEpic = r.Tier >= PalRarity.Epic ? 0 : sinceEpic + 1;
                sinceLegend = r.Tier == PalRarity.Legendary ? 0 : sinceLegend + 1;
                Assert.That(sinceEpic, Is.LessThan(cfg.Call.EpicPity), "an Epic+ by the 10th call");
                Assert.That(sinceLegend, Is.LessThan(cfg.Call.LegendaryPity), "a Legendary by the 30th call");
                legends += r.Tier == PalRarity.Legendary ? 1 : 0;
                epicsPlus += r.Tier >= PalRarity.Epic ? 1 : 0;
                sparkles += r.Sparkle ? 1 : 0;
            }

            Assert.That(legends / (double)n, Is.GreaterThanOrEqualTo(0.015), "pity only ever adds luck");
            Assert.That(epicsPlus / (double)n, Is.GreaterThanOrEqualTo(0.08));
            Assert.That(sparkles / (double)n, Is.EqualTo(1 / 64.0).Within(0.006));
        }

        [Test]
        public void TenCall_AlwaysHoldsARareOrBetter()
        {
            PalsConfig cfg = Cfg();
            for (ulong seed = 1; seed <= 300; seed++)
            {
                var pals = new PalSave { Seed = seed, Eggs = 45 };
                IReadOnlyList<CallResult> ten = PalRules.CallTen(pals, cfg, PalPerks.None)!;
                Assert.That(ten.Count, Is.EqualTo(10));
                bool rare = false;
                foreach (CallResult r in ten)
                {
                    rare |= r.Tier >= PalRarity.Rare;
                }

                Assert.That(rare, Is.True, $"seed {seed}");
                Assert.That(pals.Eggs, Is.Zero);
            }
        }

        [Test]
        public void Odds_DiscloseEveryTideling_AndSumTo100()
        {
            PalsConfig cfg = Cfg();
            float total = 0f;
            foreach ((int line, float percent) in PalRules.Odds(cfg))
            {
                total += percent;
                if (line == 10)
                {
                    Assert.That(percent, Is.EqualTo(0.75f).Within(0.0001f), "1.5 % Legendary split between two");
                }
            }

            Assert.That(total, Is.EqualTo(100f).Within(0.01f));
        }

        // --------------------------------------------------------------- sources

        [Test]
        public void Sources_VoyageMilestones_DeepZones_Keys_AndSecondWind()
        {
            PalsConfig cfg = Cfg();
            Assert.That(PalRules.VoyageMilestoneEggs(cfg, 1, 5), Is.EqualTo(2));
            Assert.That(PalRules.VoyageMilestoneEggs(cfg, 1, 6), Is.EqualTo(0));
            Assert.That(PalRules.VoyageMilestoneEggs(cfg, 3, 20), Is.EqualTo(5));
            Assert.That(PalRules.VoyageMilestoneEggs(cfg, 7, 20), Is.EqualTo(8), "deep zones' finales lay more");

            var pals = new PalSave();
            Assert.That(PalRules.ClaimKey(pals, "voyage:z1-l5"), Is.True);
            Assert.That(PalRules.ClaimKey(pals, "voyage:z1-l5"), Is.False);

            var wind = new PalPerks { FreeContinuesPerDay = 2 };
            Assert.That(PalRules.FreeContinueLeft(pals, 100, PalPerks.None), Is.False);
            Assert.That(PalRules.FreeContinueLeft(pals, 100, wind), Is.True);
            PalRules.UseFreeContinue(pals, 100);
            PalRules.UseFreeContinue(pals, 100);
            Assert.That(PalRules.FreeContinueLeft(pals, 100, wind), Is.False);
            Assert.That(PalRules.FreeContinueLeft(pals, 101, wind), Is.True, "a new day refills");
        }

        // ------------------------------------------------------------- the save

        [Test]
        public void Migration_KeepsEveryMetSpecies_AndTheBuddyLeads()
        {
            PalsConfig cfg = Cfg();
            var pals = new PalSave();
            PalRules.MigrateLegacy(pals, cfg, new[] { 20, 0, 3, 0, 0, 0, 4, 1 }, 2);
            Assert.That(pals.StageOf(0), Is.EqualTo(1));
            Assert.That(pals.BondOf(0), Is.EqualTo(20));
            Assert.That(pals.Owns(1), Is.False);
            Assert.That(pals.Owns(6), Is.True, "a jellyfish met under the old rules is grandfathered");
            Assert.That(pals.Owns(8), Is.True, "so is an axolotl");
            Assert.That(pals.Leader, Is.EqualTo(2));
            Assert.That(PalRules.CanGrow(pals, cfg, 0), Is.True, "20 rescues already earn Grown");
        }

        [Test]
        public void SaveV5_RoundTrips_Tidelings()
        {
            var save = new SaveData();
            save.Pals.EnsureLines(4);
            save.Pals.Stage[1] = 2;
            save.Pals.Bond[1] = 33;
            save.Pals.Sparkle[1] = true;
            save.Pals.Eggs = 17;
            save.Pals.EggProgress = 250;
            save.Pals.Calls = 40;
            save.Pals.SinceEpic = 3;
            save.Pals.SinceLegendary = 22;
            save.Pals.Pod = new[] { 1, -1, -1 };
            save.Pals.EggKeys.Add("voyage:z1-l5");
            save.Pals.Seed = ulong.MaxValue - 3;
            save.Pals.WelcomeGiven = true;

            SaveData back = SaveData.TryParse(save.Serialize())!;
            Assert.That(back, Is.Not.Null);
            Assert.That(back.PalsNeedMigration, Is.False);
            Assert.That(back.Pals.Serialize(), Is.EqualTo(save.Pals.Serialize()));
            Assert.That(back.Pals.Seed, Is.EqualTo(ulong.MaxValue - 3));
        }

        [Test]
        public void SaveV4_FlagsMigration_OldV5PalsStillLoad_AndMalformedIsCorrupt()
        {
            string v5 = new SaveData().Serialize();
            string v4 = v5.Replace($"\"version\": {SaveData.CurrentVersion}", "\"version\": 4");
            int palsAt = v4.IndexOf(",\n  \"pals\"", System.StringComparison.Ordinal);
            v4 = v4.Substring(0, palsAt) + "\n}";
            Assert.That(SaveData.TryParse(v4)!.PalsNeedMigration, Is.True);

            // The first v5 layout (nest / incubator) predates the Call: it still loads.
            string early = v5.Replace("\"eggs\": 0, \"eggProgress\": 0, \"calls\": [0, 0, 0], ",
                "\"nest\": [1], \"incubating\": 0, \"warmth\": 300, \"counts\": [2, 3], ");
            Assert.That(early, Is.Not.EqualTo(v5));
            Assert.That(SaveData.TryParse(early), Is.Not.Null);

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
