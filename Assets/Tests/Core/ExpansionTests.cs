using System.Collections.Generic;
using NUnit.Framework;

namespace Riptide.Core.Tests
{
    /// <summary>
    /// GDD §15 Pets &amp; Tides: creature powers (each rule, off = byte-identical,
    /// determinism), featured species, pets, quests, badges, events, the loader,
    /// and the v4 save.
    /// </summary>
    [TestFixture]
    public sealed class ExpansionTests
    {
        private static CreaturePowersConfig Powers() => new CreaturePowersConfig(1, 200, 2, 3, 1, 2);

        /// <summary>Row 3 full except col 8; a species-<paramref name="species"/> creature at (4,3).</summary>
        private static Cell[] RescueBoard(byte species)
        {
            Cell[] cells = TestKit.EmptyBoard();
            TestKit.FillRow(cells, 3, 8);
            cells[BoardSpec.IndexOf(4, 3)] = Cell.Creature(species);
            return cells;
        }

        private static MoveResult Rescue(LevelConfig cfg, Cell[] cells, int water = 1, int tide = 0, int combo = 0)
        {
            GameState state = TestKit.Build(cfg, cells, water: water, tide: tide, comboChain: combo);
            return TestKit.Place(state, 0, 8, 3);
        }

        [Test]
        public void PowersOff_IsByteIdentical_AndFiresNothing()
        {
            LevelConfig plain = TestKit.Config();
            LevelConfig off = plain.WithCreaturePowers(null);
            MoveResult a = Rescue(plain, RescueBoard(1));
            MoveResult b = Rescue(off, RescueBoard(1));
            Assert.That(a.Events.Powers, Is.Empty);
            Assert.That(StateHash.Compute(a.Next), Is.EqualTo(StateHash.Compute(b.Next)));
            Assert.That(a.Events.RescuedCreatures.Count, Is.EqualTo(1));
        }

        [Test]
        public void Crab_Pinch_RemovesTheNearestCoral()
        {
            Cell[] cells = RescueBoard(0);
            cells[BoardSpec.IndexOf(4, 5)] = Cell.Coral; // distance 2
            cells[BoardSpec.IndexOf(0, 9)] = Cell.Coral; // distance 10
            MoveResult r = Rescue(TestKit.Config().WithCreaturePowers(Powers()), cells);

            Assert.That(r.Events.Powers.Count, Is.EqualTo(1));
            Assert.That(r.Events.Powers[0].Power, Is.EqualTo(CreaturePower.Pinch));
            Assert.That(r.Events.Powers[0].ClearedCells, Is.EqualTo(new[] { new GridPos(4, 5) }));
            Assert.That(r.Next.CellAt(4, 5).Kind, Is.EqualTo(CellKind.Empty));
            Assert.That(r.Next.CellAt(0, 9).Kind, Is.EqualTo(CellKind.Coral));
        }

        [Test]
        public void Starfish_Wish_AddsItsScore_IntoRescuePoints()
        {
            MoveResult off = Rescue(TestKit.Config(), RescueBoard(1));
            MoveResult on = Rescue(TestKit.Config().WithCreaturePowers(Powers()), RescueBoard(1));
            Assert.That(on.Next.Score - off.Next.Score, Is.EqualTo(200));
            Assert.That(on.Events.Scoring.RescuePoints - off.Events.Scoring.RescuePoints, Is.EqualTo(200));
            Assert.That(on.Events.Powers[0].ScoreBonus, Is.EqualTo(200));
        }

        [Test]
        public void Seahorse_Steady_RelievesTheTideTick()
        {
            LevelConfig cfg = TestKit.Config(tideInterval: 3);
            MoveResult off = Rescue(cfg, RescueBoard(2), tide: 2);
            Assert.That(off.Events.TideRose, Is.True, "control: the third placement rises");

            MoveResult on = Rescue(cfg.WithCreaturePowers(Powers()), RescueBoard(2), tide: 2);
            Assert.That(on.Events.TideRose, Is.False, "Steady holds the tide back");
            Assert.That(on.Next.TideCounter, Is.EqualTo(1), "2 + 1 tick − 2 relief");
        }

        [Test]
        public void Octopus_Ink_ClearsTheLowestCoral_LeftToRight()
        {
            Cell[] cells = RescueBoard(3);
            cells[BoardSpec.IndexOf(0, 5)] = Cell.Coral;
            cells[BoardSpec.IndexOf(1, 5)] = Cell.Coral;
            cells[BoardSpec.IndexOf(2, 7)] = Cell.Coral;
            cells[BoardSpec.IndexOf(3, 7)] = Cell.Coral;
            MoveResult r = Rescue(TestKit.Config().WithCreaturePowers(Powers()), cells);

            Assert.That(r.Events.Powers[0].ClearedCells,
                Is.EqualTo(new[] { new GridPos(0, 5), new GridPos(1, 5), new GridPos(2, 7) }));
            Assert.That(r.Next.CellAt(3, 7).Kind, Is.EqualTo(CellKind.Coral), "only three");
        }

        [Test]
        public void Turtle_Calm_DrainsOneMore_FlooredAtMin()
        {
            LevelConfig cfg = TestKit.Config(startWater: 3, minWater: 1).WithCreaturePowers(Powers());
            MoveResult r = Rescue(cfg, RescueBoard(4), water: 3);
            Assert.That(r.Next.WaterLevel, Is.EqualTo(1), "3 − 1 row − 1 Calm");
            Assert.That(r.Events.DrainAmount, Is.EqualTo(2));
            Assert.That(r.Events.Powers[0].WaterDelta, Is.EqualTo(-1));

            MoveResult floored = Rescue(TestKit.Config(startWater: 2, minWater: 1).WithCreaturePowers(Powers()),
                RescueBoard(4), water: 2);
            Assert.That(floored.Next.WaterLevel, Is.EqualTo(1), "never below minWaterLevel");
            Assert.That(floored.Events.Powers[0].WaterDelta, Is.EqualTo(0));
        }

        [Test]
        public void Pufferfish_Puff_PopsAdjacentBlocksAndCoral_NeverCreatures()
        {
            Cell[] cells = RescueBoard(5);
            cells[BoardSpec.IndexOf(4, 2)] = Cell.Coral;
            cells[BoardSpec.IndexOf(4, 4)] = Cell.Block(2);
            MoveResult r = Rescue(TestKit.Config().WithCreaturePowers(Powers()), cells);
            Assert.That(r.Events.Powers[0].ClearedCells, Is.EqualTo(new[] { new GridPos(4, 2), new GridPos(4, 4) }));

            Cell[] guarded = RescueBoard(5);
            guarded[BoardSpec.IndexOf(4, 4)] = Cell.Creature(1);
            MoveResult g = Rescue(TestKit.Config().WithCreaturePowers(Powers()), guarded);
            Assert.That(g.Next.CellAt(4, 4).Kind, Is.EqualTo(CellKind.Creature), "creatures are never popped");
        }

        [Test]
        public void Jellyfish_Glow_ExtendsTheComboChain()
        {
            MoveResult off = Rescue(TestKit.Config(), RescueBoard(6));
            MoveResult on = Rescue(TestKit.Config().WithCreaturePowers(Powers()), RescueBoard(6));
            Assert.That(on.Next.ComboChain, Is.EqualTo(off.Next.ComboChain + 1));
        }

        [Test]
        public void Axolotl_Regrow_DrainsTwo_AndResetsTheTide()
        {
            LevelConfig cfg = TestKit.Config(startWater: 3, minWater: 0, tideInterval: 3).WithCreaturePowers(Powers());
            MoveResult r = Rescue(cfg, RescueBoard(7), water: 3, tide: 2);
            Assert.That(r.Next.WaterLevel, Is.EqualTo(0), "3 − 1 row − 2 Regrow");
            Assert.That(r.Events.Powers[0].WaterDelta, Is.EqualTo(-2));
            Assert.That(r.Events.TideRose, Is.False, "the counter reset — no rise this move");
            Assert.That(r.Next.TideCounter, Is.EqualTo(0));
        }

        [Test]
        public void PoweredEndless_ReplaysIdentically_AndPowersActuallyFire()
        {
            LevelConfig cfg = ModeFactory.Endless(TestKit.Economy(), 8).WithCreaturePowers(Powers());
            int fired = 0;
            for (ulong seed = 1; seed <= 12; seed++)
            {
                List<ulong> first = PlayOut(cfg, seed, ref fired);
                int ignored = 0;
                List<ulong> second = PlayOut(cfg, seed, ref ignored);
                Assert.That(second, Is.EqualTo(first), $"seed {seed}: powered games must replay hash-identically");
            }

            Assert.That(fired, Is.GreaterThan(0), "the bots rescued something — powers were exercised");
        }

        private static List<ulong> PlayOut(LevelConfig cfg, ulong seed, ref int fired)
        {
            var hashes = new List<ulong>();
            GameState state = GameState.NewGame(cfg, seed);
            var policy = new GreedyClearPolicy();
            DeterministicRng botRng = DeterministicRng.FromSeed(seed ^ 0xABCDUL);
            for (int i = 0; i < 400 && !state.Status.IsTerminal(); i++)
            {
                BotDecision decision = policy.Choose(state, botRng);
                botRng = decision.Rng;
                if (decision.Move == null)
                {
                    break;
                }

                MoveResult result = SimEngine.ApplyMove(state, decision.Move);
                fired += result.Events.Powers.Count;
                state = result.Next;
                hashes.Add(StateHash.Compute(state));
            }

            return hashes;
        }

        [Test]
        public void FeaturedSpecies_ForcesTheId_WithoutChangingTheRngStream()
        {
            LevelConfig plain = TestKit.Config(spawnEveryTrays: 1);
            var featured = new LevelConfig(plain.StartWaterLevel, plain.MinWaterLevel, plain.TideInterval,
                plain.CreatureSpawnIntervalTrays, plain.CreatureSpeciesCount, plain.DealColorCount, plain.PieceWeights,
                plain.Scoring, plain.Goals, featuredSpecies: 3);

            // Empty the tray with the last piece so step 5 refills and spawns.
            TrayPiece?[] tray = TestKit.Tray(PieceId.Mono1);
            MoveResult a = TestKit.Place(TestKit.Build(plain, tray: tray), 0, 0, 6);
            MoveResult b = TestKit.Place(TestKit.Build(featured, tray: tray), 0, 0, 6);

            Assert.That(b.Events.SpawnedCreatures.Count, Is.EqualTo(1));
            Assert.That(b.Events.SpawnedCreatures[0].CreatureId, Is.EqualTo(3));
            Assert.That(b.Events.SpawnedCreatures[0].Pos, Is.EqualTo(a.Events.SpawnedCreatures[0].Pos));
            Assert.That(b.Next.Rng, Is.EqualTo(a.Next.Rng), "the species draw still happened");
        }

        // ---------------------------------------------------------------- meta

        private static PetsConfig Pets() => new PetsConfig(new[] { 1, 5, 15, 35, 75 }, 4, 120, 2);

        [Test]
        public void Pets_LevelGiftAndBuddy()
        {
            PetsConfig pets = Pets();
            Assert.That(PetRules.LevelFor(0, pets), Is.EqualTo(0));
            Assert.That(PetRules.LevelFor(1, pets), Is.EqualTo(1));
            Assert.That(PetRules.LevelFor(14, pets), Is.EqualTo(2));
            Assert.That(PetRules.LevelFor(500, pets), Is.EqualTo(5));
            Assert.That(PetRules.NextThreshold(14, pets), Is.EqualTo(15));
            Assert.That(PetRules.NextThreshold(500, pets), Is.Null);

            Assert.That(PetRules.GiftCoins(new[] { 1, 5, 0 }, pets), Is.EqualTo((1 + 2) * 4));
            Assert.That(PetRules.GiftCoins(new[] { 999, 999, 999, 999, 999, 999, 999, 999 }, pets), Is.EqualTo(120),
                "capped");
            Assert.That(PetRules.GiftAvailable(10, 9, new[] { 1 }, pets), Is.True);
            Assert.That(PetRules.GiftAvailable(9, 9, new[] { 1 }, pets), Is.False, "once per day");
            Assert.That(PetRules.GiftAvailable(8, 9, new[] { 1 }, pets), Is.False, "clock rollback can't re-arm");
            Assert.That(PetRules.GiftAvailable(10, 9, new[] { 0 }, pets), Is.False, "no pets, no gift");

            Assert.That(PetRules.BuddyBonus(100, 3, pets), Is.EqualTo(6));
            Assert.That(PetRules.BuddyBonus(100, 0, pets), Is.EqualTo(0));
        }

        [Test]
        public void QuestBoard_IsDeterministic_Distinct_AndVariesByDay()
        {
            int[] a = QuestBoard.ForDay(20000, 19, 3);
            Assert.That(QuestBoard.ForDay(20000, 19, 3), Is.EqualTo(a), "same day, same quests — for everyone");
            Assert.That(new HashSet<int>(a).Count, Is.EqualTo(3), "distinct");
            bool differs = false;
            for (long day = 20001; day < 20010 && !differs; day++)
            {
                differs = !ArraysEqual(QuestBoard.ForDay(day, 19, 3), a);
            }

            Assert.That(differs, Is.True, "the board rotates");
        }

        private static bool ArraysEqual(int[] x, int[] y)
        {
            for (int i = 0; i < x.Length; i++)
            {
                if (x[i] != y[i])
                {
                    return false;
                }
            }

            return true;
        }

        [Test]
        public void QuestRules_Advance_ClampsAndTakesBestScore()
        {
            var rows = new QuestDef("rows_15", QuestKind.Rows, 15, -1, 40);
            Assert.That(QuestRules.Advance(rows, 10, new QuestSignal { Rows = 9 }), Is.EqualTo(15), "clamped");
            var crab = new QuestDef("crab_1", QuestKind.RescueSpecies, 1, 0, 40);
            Assert.That(QuestRules.Advance(crab, 0, new QuestSignal { SpeciesRescues = new[] { 1, 0 } }), Is.EqualTo(1));
            Assert.That(QuestRules.Advance(crab, 0, new QuestSignal { SpeciesRescues = new[] { 0, 3 } }), Is.EqualTo(0));
            var score = new QuestDef("score", QuestKind.Score, 1500, -1, 60);
            Assert.That(QuestRules.Advance(score, 900, new QuestSignal { RunScore = 400 }), Is.EqualTo(900),
                "score quests track the best single run, not a sum");
            Assert.That(QuestRules.Complete(rows, 15), Is.True);
        }

        [Test]
        public void Badges_ReadTheirStat()
        {
            var badge = new BadgeDef("rows_100", BadgeStat.Rows, 100, 80, "rows");
            var stats = new PlayerStats(0, 0, 50, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            Assert.That(BadgeRules.Earned(badge, stats), Is.False);
            Assert.That(BadgeRules.Progress(badge, stats), Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(BadgeRules.Earned(badge, new PlayerStats(0, 0, 100, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)), Is.True);
        }

        [Test]
        public void EventSchedule_WeeksRotate_DaysLeftCountdown_AndConfigMapsTheGoal()
        {
            long monday = CivilDate.ToEpochDays(2026, 10, 12);
            Assert.That(EventSchedule.DaysLeft(monday), Is.EqualTo(7), "a week opens on Monday");
            Assert.That(EventSchedule.DaysLeft(monday + 6), Is.EqualTo(1), "Sunday is the last day");
            Assert.That(EventSchedule.WeekOf(monday + 6), Is.EqualTo(EventSchedule.WeekOf(monday)));
            Assert.That(EventSchedule.WeekOf(monday + 7), Is.EqualTo(EventSchedule.WeekOf(monday) + 1));

            var rotation = new[]
            {
                new EventDef("a", 0, 1, 6, 3, EventGoalKind.Rescue, 5, 150, "x"),
                new EventDef("b", 4, 2, 6, 3, EventGoalKind.Tides, 20, 175, "y"),
            };
            Assert.That(EventSchedule.ForWeek(10, rotation).Id, Is.EqualTo("a"));
            Assert.That(EventSchedule.ForWeek(11, rotation).Id, Is.EqualTo("b"));

            LevelConfig cfg = EventSchedule.BuildConfig(rotation[1], TestKit.Economy(), 8, Powers());
            Assert.That(cfg.Goals.SurviveTidesTarget, Is.EqualTo(20));
            Assert.That(cfg.FeaturedSpecies, Is.EqualTo(4));
            Assert.That(cfg.StartWaterLevel, Is.EqualTo(2));
            Assert.That(cfg.Powers, Is.Not.Null);
            Assert.That(cfg.BoostersAllowed, Is.True);
            Assert.That(EventSchedule.SeedForWeek(5), Is.Not.EqualTo(EventSchedule.SeedForWeek(6)));
        }

        // ------------------------------------------------------------- content

        private const string ExpansionJson = @"{
  ""creaturePowers"": { ""modes"": { ""voyage"": true, ""endless"": true, ""event"": true },
    ""crabCoral"": 1, ""starfishScore"": 200, ""seahorseTideRelief"": 2, ""octopusCoral"": 3, ""turtleDrain"": 1, ""axolotlDrain"": 2 },
  ""pets"": { ""levelThresholds"": [1, 5, 15, 35, 75], ""giftCoinsPerLevel"": 4, ""giftCap"": 120, ""buddyBonusPercentPerLevel"": 2 },
  ""quests"": { ""perDay"": 2, ""pool"": [
    { ""id"": ""rescue_3"", ""kind"": ""rescue"", ""target"": 3, ""reward"": 40 },
    { ""id"": ""crab_1"", ""kind"": ""rescueSpecies"", ""species"": 0, ""target"": 1, ""reward"": 40 } ] },
  ""badges"": [ { ""id"": ""first"", ""stat"": ""bestClear"", ""target"": 3, ""reward"": 25, ""icon"": ""fish"" } ],
  ""events"": [ { ""id"": ""crab"", ""featuredSpecies"": 0, ""startWaterLevel"": 1, ""startTideInterval"": 6,
    ""creatureSpawnIntervalTrays"": 3, ""goal"": ""rescue"", ""goalTarget"": 5, ""rewardCoins"": 150, ""rewardDecoration"": ""golden_crab"" } ]
}";

        [Test]
        public void ExpansionLoader_ParsesTheSchema()
        {
            ExpansionConfig cfg = ExpansionLoader.Load(ExpansionJson, "fixture", 8);
            Assert.That(cfg.Powers.StarfishScore, Is.EqualTo(200));
            Assert.That(cfg.PowerModes.Event, Is.True);
            Assert.That(cfg.Pets.MaxLevel, Is.EqualTo(5));
            Assert.That(cfg.QuestPool[1].Kind, Is.EqualTo(QuestKind.RescueSpecies));
            Assert.That(cfg.Badges[0].Stat, Is.EqualTo(BadgeStat.BestClear));
            Assert.That(cfg.Events[0].GoalKind, Is.EqualTo(EventGoalKind.Rescue));
        }

        [Test]
        public void ExpansionLoader_RejectsBadContent()
        {
            Assert.Throws<ContentException>(() =>
                ExpansionLoader.Load(ExpansionJson.Replace(@"""species"": 0", @"""species"": 9"), "bad", 8),
                "species out of the roster");
            Assert.Throws<ContentException>(() =>
                ExpansionLoader.Load(ExpansionJson.Replace(@"""id"": ""crab_1""", @"""id"": ""rescue_3"""), "dup", 8),
                "duplicate quest id");
            Assert.Throws<ContentException>(() =>
                ExpansionLoader.Load(ExpansionJson.Replace(@"""perDay"": 2", @"""perDay"": 3"), "small", 8),
                "pool smaller than perDay");
            Assert.Throws<ContentException>(() =>
                ExpansionLoader.Load(ExpansionJson.Replace(@"""bestClear""", @"""nonsense"""), "stat", 8),
                "unknown badge stat");
        }

        [Test]
        public void SaveV4_RoundTrips_TheExpansionState()
        {
            var save = new SaveData
            {
                BuddySpecies = 3,
                PetGiftDay = 20010,
                QuestDay = 20011,
                QuestProgress = new[] { 2, 15, 0 },
                QuestClaimed = new[] { false, true, false },
                TotalRowsCleared = 412,
                BestClearAtOnce = 3,
                TotalTides = 77,
                PowersTriggered = 9,
                QuestsCompleted = 4,
                EventsCompleted = 1,
                EventRewardWeek = 2915,
                EventProgressWeek = 2916,
                EventBestProgress = 12,
            };
            save.BadgesClaimed.Add("first_rescue");

            SaveData? back = SaveData.TryParse(save.Serialize());
            Assert.That(back, Is.Not.Null);
            Assert.That(back!.BuddySpecies, Is.EqualTo(3));
            Assert.That(back.QuestProgress, Is.EqualTo(new[] { 2, 15, 0 }));
            Assert.That(back.QuestClaimed, Is.EqualTo(new[] { false, true, false }));
            Assert.That(back.BadgesClaimed, Is.EqualTo(new[] { "first_rescue" }));
            Assert.That(back.TotalRowsCleared, Is.EqualTo(412));
            Assert.That(back.EventBestProgress, Is.EqualTo(12));
            Assert.That(back.EventRewardWeek, Is.EqualTo(2915));
        }

        [Test]
        public void SaveV3_MigratesToV4_WithDefaults()
        {
            string v3 = new SaveData { Coins = 77 }.Serialize();
            int cut = v3.IndexOf(",\n  \"buddy\"", System.StringComparison.Ordinal);
            v3 = v3.Substring(0, cut) + "\n}";
            v3 = v3.Replace($"\"version\": {SaveData.CurrentVersion}", "\"version\": 3");

            SaveData? save = SaveData.TryParse(v3);
            Assert.That(save, Is.Not.Null, "a v3 save still loads");
            Assert.That(save!.Coins, Is.EqualTo(77));
            Assert.That(save.BuddySpecies, Is.EqualTo(-1));
            Assert.That(save.QuestDay, Is.EqualTo(-1));
            Assert.That(save.QuestProgress, Is.Empty);
            Assert.That(save.EventRewardWeek, Is.EqualTo(-1));
        }
    }
}
