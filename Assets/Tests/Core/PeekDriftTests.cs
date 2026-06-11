using NUnit.Framework;
using Riptide.Core;

namespace Riptide.Core.Tests
{
    /// <summary>
    /// THE drift guard: PeekEvaluator re-implements GDD 2.6 steps 1–4 for speed,
    /// so every prediction is pinned against the real SimEngine across thousands
    /// of sampled candidate moves. If the engine's rules move, this fails first.
    /// </summary>
    [TestFixture]
    public sealed class PeekDriftTests
    {
        [Test]
        public void Peek_MatchesEngine_AcrossRandomEndlessGames()
        {
            LevelConfig config = TestKit.Config(
                startWater: 1, minWater: 1, tideInterval: 5, spawnEveryTrays: 3, awardTideSurvival: true,
                escalation: new EscalationConfig(4, 3, 25, 2, 8));
            RunDriftSweep(config, seeds: 30, label: "endless", minComparisons: 300);
        }

        [Test]
        public void Peek_MatchesEngine_WithRescueGoals()
        {
            var preset = new System.Collections.Generic.List<PresetCell>
            {
                new PresetCell(new GridPos(2, 3), Cell.Creature(0)),
                new PresetCell(new GridPos(6, 4), Cell.Creature(1)),
                new PresetCell(new GridPos(4, 2), Cell.Creature(2)),
            };
            LevelConfig config = TestKit.Config(
                startWater: 1, minWater: 1, tideInterval: 4,
                goals: new GoalSet(rescueAllTarget: 3, clearRowsTarget: null, surviveTidesTarget: null, scoreTarget: null),
                preset: preset);
            RunDriftSweep(config, seeds: 20, label: "rescue", minComparisons: 100);
        }

        [Test]
        public void Peek_MatchesEngine_WithScoreAndSurviveGoals()
        {
            LevelConfig config = TestKit.Config(
                startWater: 1, minWater: 1, tideInterval: 3, awardTideSurvival: true,
                goals: new GoalSet(null, null, surviveTidesTarget: 6, scoreTarget: 900));
            RunDriftSweep(config, seeds: 20, label: "score+survive", minComparisons: 100);
        }

        private static void RunDriftSweep(LevelConfig config, int seeds, string label, int minComparisons)
        {
            var peek = new PeekEvaluator();
            int comparisons = 0;
            for (ulong seed = 0; seed < (ulong)seeds; seed++)
            {
                GameState state = GameState.NewGame(config, seed);
                DeterministicRng botRng = DeterministicRng.FromSeed(seed ^ 0x5EEDUL);
                int moves = 0;
                while (state.Status == GameStatus.InProgress && moves < 200)
                {
                    // Compare peek vs engine on up to 5 random legal candidates.
                    for (int candidate = 0; candidate < 5; candidate++)
                    {
                        RngIntDraw slotDraw = botRng.NextInt(BoardSpec.TraySize);
                        botRng = slotDraw.Rng;
                        RngIntDraw colDraw = botRng.NextInt(BoardSpec.Width);
                        botRng = colDraw.Rng;
                        RngIntDraw rowDraw = botRng.NextInt(BoardSpec.Height);
                        botRng = rowDraw.Rng;

                        TrayPiece? piece = state.TrayAt(slotDraw.Value);
                        if (!piece.HasValue || rowDraw.Value < state.WaterLevel)
                        {
                            continue;
                        }

                        var pos = new GridPos(colDraw.Value, rowDraw.Value);
                        if (!PlacementValidator.CanPlace(state, piece.Value.Piece, pos))
                        {
                            continue;
                        }

                        MovePeek predicted = peek.Evaluate(state, piece.Value.Piece, pos);
                        MoveResult actual = SimEngine.ApplyMove(state, new PlaceMove(slotDraw.Value, pos));
                        string ctx = $"{label} seed {seed} move {moves} @ {pos}";

                        Assert.That(predicted.RowsCleared, Is.EqualTo(actual.Events.RowsCleared.Count), $"{ctx}: rowsCleared");
                        Assert.That(predicted.Rescues, Is.EqualTo(actual.Events.RescuedCreatures.Count), $"{ctx}: rescues");
                        Assert.That(predicted.WaterAfter, Is.EqualTo(actual.Next.WaterLevel), $"{ctx}: waterAfter");
                        Assert.That(predicted.TideRose, Is.EqualTo(actual.Events.TideRose), $"{ctx}: tideRose");
                        bool actuallyFatal = actual.Next.Status == GameStatus.LostDrowned
                            || actual.Next.Status == GameStatus.LostCreature;
                        Assert.That(predicted.Fatal, Is.EqualTo(actuallyFatal), $"{ctx}: fatal");
                        Assert.That(predicted.Won, Is.EqualTo(actual.Next.Status == GameStatus.Won), $"{ctx}: won");
                        comparisons++;
                    }

                    // Advance the game with a random legal move.
                    BotDecision step = new RandomLegalPolicy().Choose(state, botRng);
                    botRng = step.Rng;
                    if (step.Move == null)
                    {
                        Assert.Fail($"{label} seed {seed}: InProgress with no legal move");
                    }

                    state = SimEngine.ApplyMove(state, step.Move!).Next;
                    moves++;
                }
            }

            Assert.That(comparisons, Is.GreaterThan(minComparisons), $"{label}: drift sweep must actually sample candidates");
        }
    }
}
