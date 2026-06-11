using NUnit.Framework;
using Riptide.Core;

namespace Riptide.Core.Tests
{
    /// <summary>
    /// Master prompt 2C — JSON parser, LevelDef schema + validator, economy and
    /// creature loaders. Loaders are tested against embedded JSON strings; the
    /// real disk fixtures are gated by Tools/ContentCheck (run_all_tests.sh gate 3).
    /// </summary>
    [TestFixture]
    public sealed class ContentTests
    {
        private static EconomyConfig Economy() => TestKit.Economy();

        // ---------------- JSON parser ----------------

        [Test]
        public void Parser_HandlesNestedDocuments()
        {
            JsonObject root = JsonParser.Parse(@"{ ""a"": [1, 2.5, true, null, ""x""], ""b"": { ""c"": -7 } }").AsObject();

            Assert.That(root.Require("a").AsArray().Count, Is.EqualTo(5));
            Assert.That(root.Require("a").AsArray().Items[0].AsInt(), Is.EqualTo(1));
            Assert.That(root.Require("a").AsArray().Items[1].AsDouble(), Is.EqualTo(2.5));
            Assert.That(root.Require("a").AsArray().Items[2].AsBool(), Is.True);
            Assert.That(root.Require("a").AsArray().Items[3].IsNull, Is.True);
            Assert.That(root.Require("b").AsObject().Require("c").AsInt(), Is.EqualTo(-7));
        }

        [Test]
        public void Parser_DecodesStringEscapes()
        {
            JsonObject root = JsonParser.Parse(@"{ ""s"": ""a\""b\\c\nA"" }").AsObject();

            Assert.That(root.Require("s").AsString(), Is.EqualTo("a\"b\\c\nA"));
        }

        [Test]
        public void Parser_ReportsLineAndColumn_OnSyntaxErrors()
        {
            var ex = Assert.Throws<JsonParseException>(() => JsonParser.Parse("{\n  \"a\": 1,\n  \"b\": oops\n}"));

            Assert.That(ex!.Line, Is.EqualTo(3), "the broken token sits on line 3");
            Assert.That(ex.Message, Does.Contain("line 3"));
        }

        [Test]
        public void Parser_RejectsDuplicateKeys()
        {
            var ex = Assert.Throws<JsonParseException>(() => JsonParser.Parse(@"{ ""a"": 1, ""a"": 2 }"));

            Assert.That(ex!.Message, Does.Contain("Duplicate member 'a'"));
        }

        [Test]
        public void Parser_RejectsTrailingContent()
        {
            Assert.Throws<JsonParseException>(() => JsonParser.Parse(@"{ ""a"": 1 } extra"));
        }

        [Test]
        public void Parser_RejectsMalformedNumbers()
        {
            Assert.Throws<JsonParseException>(() => JsonParser.Parse(@"{ ""a"": 1. }"));
            Assert.Throws<JsonParseException>(() => JsonParser.Parse(@"{ ""a"": - }"));
        }

        // ---------------- economy.json ----------------

        [Test]
        public void Economy_Loads_AndBuildsScoring()
        {
            EconomyConfig economy = Economy();

            Assert.That(economy.DealColorCount, Is.EqualTo(6));
            Assert.That(economy.PieceWeightBands.ContainsKey(1), Is.True);
            Assert.That(economy.Endless.StartTideInterval, Is.EqualTo(7));
            Assert.That(economy.Endless.IntervalFloor, Is.EqualTo(3));

            ScoringConfig endlessScoring = economy.BuildScoring(awardTideSurvival: true);
            Assert.That(endlessScoring.RowClearBase, Is.EqualTo(80));
            Assert.That(endlessScoring.AwardTideSurvival, Is.True);
            Assert.That(economy.BuildScoring(false).AwardTideSurvival, Is.False);
        }

        [Test]
        public void Economy_RejectsWrongWeightCount_CitingLine()
        {
            string bad = TestKit.CanonicalEconomyJson.Replace("[7,7,7,7,7,7,7,7,7,5,5,5,5,5,5,5,5,0,0,0]", "[1,2,3]");

            var ex = Assert.Throws<ContentException>(() => EconomyLoader.Load(bad, "economy.json"));

            Assert.That(ex!.Message, Does.Contain("economy.json"));
            Assert.That(ex.Message, Does.Contain("exactly 20 weights"));
            Assert.That(ex.Message, Does.Contain("line"));
        }

        // ---------------- creatures.json ----------------

        [Test]
        public void Creatures_RosterOfEight_Loads()
        {
            const string json = @"{ ""species"": [
                { ""id"": 0, ""name"": ""Crab"" }, { ""id"": 1, ""name"": ""Starfish"" },
                { ""id"": 2, ""name"": ""Seahorse"" }, { ""id"": 3, ""name"": ""Octopus"" },
                { ""id"": 4, ""name"": ""Turtle"" }, { ""id"": 5, ""name"": ""Pufferfish"" },
                { ""id"": 6, ""name"": ""Jellyfish"" }, { ""id"": 7, ""name"": ""Axolotl"", ""rare"": true } ] }";

            CreatureRoster roster = CreatureLoader.Load(json, "creatures.json");

            Assert.That(roster.Count, Is.EqualTo(8));
            Assert.That(roster.Species[7].Name, Is.EqualTo("Axolotl"));
            Assert.That(roster.Species[7].Rare, Is.True);
            Assert.That(roster.Species[0].Rare, Is.False);
        }

        [Test]
        public void Creatures_RejectDuplicateIds()
        {
            const string json = @"{ ""species"": [ { ""id"": 0, ""name"": ""Crab"" }, { ""id"": 0, ""name"": ""Starfish"" } ] }";

            var ex = Assert.Throws<ContentException>(() => CreatureLoader.Load(json, "creatures.json"));

            Assert.That(ex!.Message, Does.Contain("Duplicate species id 0"));
        }

        // ---------------- LevelDef schema ----------------

        private const string ValidZoneJson = @"[
  {
    ""id"": ""z1-l1"", ""zone"": 1, ""startWaterLevel"": 1, ""minWaterLevel"": 1,
    ""tideInterval"": 8, ""weightBand"": 1, ""parMoves"": 10,
    ""goals"": { ""clearRows"": 2 },
    ""preset"": []
  },
  {
    ""id"": ""z1-l2"", ""zone"": 1, ""startWaterLevel"": 1, ""minWaterLevel"": 1,
    ""tideInterval"": 8, ""weightBand"": 1,
    ""goals"": { ""rescueAll"": 1 },
    ""preset"": [
      { ""col"": 4, ""row"": 2, ""cell"": ""creature"", ""id"": 0 },
      { ""col"": 3, ""row"": 2, ""cell"": ""block"", ""id"": 1 },
      { ""col"": 0, ""row"": 1, ""cell"": ""coral"" }
    ]
  }
]";

        [Test]
        public void Levels_ValidZoneFile_Loads()
        {
            var levels = LevelDefLoader.LoadZone(ValidZoneJson, "zone1.json", Economy());

            Assert.That(levels.Count, Is.EqualTo(2));
            Assert.That(levels[0].Id, Is.EqualTo("z1-l1"));
            Assert.That(levels[0].Goals.ClearRowsTarget, Is.EqualTo(2));
            Assert.That(levels[0].ParMoves, Is.EqualTo(10));
            Assert.That(levels[0].PieceWeights[(int)PieceId.Mono1], Is.EqualTo(7), "band 1 resolved from economy");
            Assert.That(levels[0].PieceWeights[(int)PieceId.I5V], Is.EqualTo(0));
            Assert.That(levels[1].Goals.RescueAllTarget, Is.EqualTo(1));
            Assert.That(levels[1].ParMoves, Is.Null);
            Assert.That(levels[1].Preset.Count, Is.EqualTo(3));
            Assert.That(levels[1].Preset[0].Content.Kind, Is.EqualTo(CellKind.Creature));
        }

        [Test]
        public void Levels_LoadedDef_ProducesAPlayableGame()
        {
            var levels = LevelDefLoader.LoadZone(ValidZoneJson, "zone1.json", Economy());
            LevelConfig config = levels[1].ToLevelConfig(Economy(), creatureSpeciesCount: 8);

            GameState state = GameState.NewGame(config, seed: 4);

            Assert.That(state.Status, Is.EqualTo(GameStatus.InProgress));
            Assert.That(state.CellAt(4, 2).Kind, Is.EqualTo(CellKind.Creature), "preset creature landed");
            Assert.That(state.CellAt(0, 1).Kind, Is.EqualTo(CellKind.Coral));
            Assert.That(state.Config.Goals.HasRescueGoal, Is.True);
        }

        [Test]
        public void Levels_MissingRequiredField_Throws_WithPosition()
        {
            string bad = ValidZoneJson.Replace(@"""tideInterval"": 8, ""weightBand"": 1, ""parMoves"": 10,", "");

            var ex = Assert.Throws<ContentException>(() => LevelDefLoader.LoadZone(bad, "zone1.json", Economy()));

            Assert.That(ex!.Message, Does.Contain("zone1.json"));
            Assert.That(ex.Message, Does.Contain("'tideInterval'"));
            Assert.That(ex.Message, Does.Contain("line"));
        }

        [Test]
        public void Levels_MalformedJson_Throws_WithLineAndColumn()
        {
            var ex = Assert.Throws<ContentException>(() =>
                LevelDefLoader.LoadZone("[ { \"id\": \"x\" \n  \"zone\": 1 } ]", "zone1.json", Economy()));

            Assert.That(ex!.Message, Does.Contain("zone1.json"));
            Assert.That(ex.Message, Does.Contain("line 2"));
        }

        [Test]
        public void Levels_UnknownCellKind_Throws()
        {
            string bad = ValidZoneJson.Replace(@"""cell"": ""coral""", @"""cell"": ""lava""");

            var ex = Assert.Throws<ContentException>(() => LevelDefLoader.LoadZone(bad, "zone1.json", Economy()));

            Assert.That(ex!.Message, Does.Contain("Unknown cell kind 'lava'"));
        }

        [Test]
        public void Levels_LiveCellBelowStartWater_Throws()
        {
            string bad = ValidZoneJson.Replace(
                @"{ ""col"": 0, ""row"": 1, ""cell"": ""coral"" }",
                @"{ ""col"": 0, ""row"": 0, ""cell"": ""block"" }");

            var ex = Assert.Throws<ContentException>(() => LevelDefLoader.LoadZone(bad, "zone1.json", Economy()));

            Assert.That(ex!.Message, Does.Contain("below startWaterLevel"));
            Assert.That(ex.Message, Does.Contain("GDD 2.2"));
        }

        [Test]
        public void Levels_BandAndInlineWeightsTogether_Throw()
        {
            string bad = ValidZoneJson.Replace(@"""weightBand"": 1, ""parMoves"": 10,",
                @"""weightBand"": 1, ""pieceWeights"": [1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1], ""parMoves"": 10,");

            var ex = Assert.Throws<ContentException>(() => LevelDefLoader.LoadZone(bad, "zone1.json", Economy()));

            Assert.That(ex!.Message, Does.Contain("not both"));
        }

        [Test]
        public void Levels_UnknownWeightBand_Throws()
        {
            string bad = ValidZoneJson.Replace(@"""weightBand"": 1, ""parMoves"": 10,", @"""weightBand"": 9, ""parMoves"": 10,");

            var ex = Assert.Throws<ContentException>(() => LevelDefLoader.LoadZone(bad, "zone1.json", Economy()));

            Assert.That(ex!.Message, Does.Contain("weightBand 9 is not defined"));
        }

        [Test]
        public void Levels_DuplicateIds_Throw()
        {
            string bad = ValidZoneJson.Replace(@"""id"": ""z1-l2""", @"""id"": ""z1-l1""");

            var ex = Assert.Throws<ContentException>(() => LevelDefLoader.LoadZone(bad, "zone1.json", Economy()));

            Assert.That(ex!.Message, Does.Contain("Duplicate level id 'z1-l1'"));
        }

        [Test]
        public void Levels_WithoutAnyGoal_Throw()
        {
            string bad = ValidZoneJson.Replace(@"""goals"": { ""clearRows"": 2 }", @"""goals"": { }");

            var ex = Assert.Throws<ContentException>(() => LevelDefLoader.LoadZone(bad, "zone1.json", Economy()));

            Assert.That(ex!.Message, Does.Contain("at least one goal"));
        }

        [Test]
        public void Levels_InlineWeights_AreAccepted()
        {
            string inline = ValidZoneJson.Replace(@"""weightBand"": 1, ""parMoves"": 10,",
                @"""pieceWeights"": [1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0], ""parMoves"": 10,");

            var levels = LevelDefLoader.LoadZone(inline, "zone1.json", Economy());

            Assert.That(levels[0].PieceWeights[(int)PieceId.Mono1], Is.EqualTo(1));
            Assert.That(levels[0].PieceWeights[(int)PieceId.DominoH], Is.EqualTo(0));
        }
    }
}
