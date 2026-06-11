using System.Collections;
using NUnit.Framework;
using Riptide.Core;
using Riptide.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace Riptide.PlayMode.Tests
{
    /// <summary>
    /// Phase 4 automated acceptance: drive 20 scripted moves through the store and
    /// assert the view's cell states match the sim after every single one
    /// (master prompt 4 ✅ ACCEPT).
    /// </summary>
    public sealed class BoardAcceptanceTests
    {
        private static LevelConfig TestConfig()
        {
            var weights = new int[PieceCatalog.PieceCount];
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = 1;
            }

            var scoring = new ScoringConfig(1, 80, 2, 1, 5, 250, 250, 30, 5, true);
            return new LevelConfig(1, 1, 5, 3, 8, 6, weights, scoring, GoalSet.None);
        }

        [UnityTest]
        public IEnumerator TwentyScriptedMoves_ViewMatchesSim_AfterEachMove()
        {
            GameBootstrap game = GameBootstrap.CreateGame(TestConfig(), seed: 7, instantAnimations: true);
            yield return null;

            AssertViewMatchesSim(game, "initial state");

            var policy = new GreedyClearPolicy();
            DeterministicRng botRng = DeterministicRng.FromSeed(99);
            for (int move = 1; move <= 20; move++)
            {
                Assert.That(game.Store.State.Status, Is.EqualTo(GameStatus.InProgress),
                    $"game ended before move {move} — pick a friendlier seed");

                BotDecision decision = policy.Choose(game.Store.State, botRng);
                botRng = decision.Rng;
                Assert.That(decision.Move, Is.Not.Null, $"no legal move at step {move}");
                Assert.That(game.Store.TryDispatch(decision.Move!), Is.True);

                yield return null;
                AssertViewMatchesSim(game, $"after move {move}");
            }

            Object.Destroy(game.gameObject);
        }

        private static void AssertViewMatchesSim(GameBootstrap game, string context)
        {
            GameState state = game.Store.State;

            for (int row = 0; row < BoardSpec.Height; row++)
            {
                for (int col = 0; col < BoardSpec.Width; col++)
                {
                    Cell simCell = state.CellAt(col, row);
                    Assert.That(game.Board.KindAt(col, row), Is.EqualTo(simCell.Kind),
                        $"{context}: view kind mismatch at ({col},{row})");
                    if (simCell.Kind == CellKind.Block)
                    {
                        Assert.That(game.Board.ColorAt(col, row), Is.EqualTo(Palette.BlockColor(simCell.Id)),
                            $"{context}: block color mismatch at ({col},{row})");
                    }
                }
            }

            Assert.That(game.Water.CurrentLevel, Is.EqualTo((float)state.WaterLevel).Within(0.001f),
                $"{context}: water level mismatch");

            int expectedInterval = EscalationRules.EffectiveTideInterval(state.Config, state.Goals.TidesSurvived);
            Assert.That(game.Meter.TotalCount, Is.EqualTo(expectedInterval), $"{context}: ring segment count");
            Assert.That(game.Meter.FilledCount, Is.EqualTo(state.TideCounter), $"{context}: ring fill");

            // 4-UI-c chrome renders from the same state (spec §4.3/§6.1).
            Assert.That(game.Chrome.FloodLineLevel, Is.EqualTo((float)BoardSpec.DrownWaterLevel).Within(0.001f),
                $"{context}: flood line sits at the drown row");
            Assert.That(game.Chrome.NotchLevel, Is.EqualTo(state.WaterLevel), $"{context}: depth gauge notch");
            Assert.That(game.Chrome.DangerShown, Is.EqualTo(DangerRule.IsDanger(state.WaterLevel)),
                $"{context}: danger presentation");

            for (int slot = 0; slot < BoardSpec.TraySize; slot++)
            {
                Assert.That(game.Tray.ShownPieceAt(slot), Is.EqualTo(state.TrayAt(slot)?.Piece),
                    $"{context}: tray slot {slot}");
            }
        }
    }
}
