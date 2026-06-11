using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Riptide.Core;
using Riptide.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace Riptide.PlayMode.Tests
{
    /// <summary>
    /// UI spec 4-UI-c ✅: the §6.1 event queue plays clear → drain → rise as an
    /// ordered beat sequence within t.resolveBudget, and input is locked during
    /// board resolution only.
    /// </summary>
    public sealed class EventQueueTests
    {
        private static LevelConfig TestConfig()
        {
            var weights = new int[PieceCatalog.PieceCount];
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = 1;
            }

            var scoring = new ScoringConfig(1, 80, 2, 1, 5, 250, 250, 30, 5, true);
            return new LevelConfig(5, 1, 5, 3, 8, 6, weights, scoring, GoalSet.None);
        }

        private static MoveResult ClearDrainRiseResult(GameState next)
        {
            var events = new MoveEvents(
                placedCells: new[] { new GridPos(0, 0) },
                rowsCleared: new[] { 0, 1 },
                petrifiedCells: new[] { new GridPos(0, 0), new GridPos(1, 1) },
                removedCells: Array.Empty<GridPos>(),
                rescuedCreatures: Array.Empty<CreatureEvent>(),
                lostCreatures: Array.Empty<CreatureEvent>(),
                spawnedCreatures: Array.Empty<CreatureEvent>(),
                drainAmount: 2,
                tideRose: true,
                waterDelta: -1,
                dealtPieces: Array.Empty<TrayPiece>(),
                scoring: new ScoreBreakdown(1, 160, 0, 0, 0, 2),
                statusAfter: GameStatus.InProgress);
            return new MoveResult(next, events);
        }

        [UnityTest]
        public IEnumerator ClearDrainRise_PlaysOrdered_WithinBudget_AndLocksInputOnlyDuringResolution()
        {
            GameBootstrap game = GameBootstrap.CreateGame(TestConfig(), seed: 7, instantAnimations: false);
            yield return null;
            Assert.That(game.Driver.IsAnimating, Is.False, "no lock before any move (§6.1)");

            var observed = new List<string>();
            game.Driver.BeatStarted += (name, _) => observed.Add(name);

            MoveResult result = ClearDrainRiseResult(game.Store.State);
            game.Driver.DriveForTest(new PlaceMove(0, new GridPos(0, 0)), result);

            Assert.That(game.Driver.IsAnimating, Is.True, "input locked during board resolution (§6.1)");
            Assert.That(game.Driver.LastPlannedSeconds,
                Is.LessThanOrEqualTo(ThemeRuntime.Seconds("t.resolveBudget") + 0.001f),
                "clear+drain+rise fits t.resolveBudget");

            IReadOnlyList<string> planned = game.Driver.LastBeats;
            AssertOrdered(planned, "place", "clear");
            AssertOrdered(planned, "clear", "drain");
            AssertOrdered(planned, "drain", "rise");

            float deadline = Time.realtimeSinceStartup + game.Driver.LastPlannedSeconds + 3f;
            while (game.Driver.IsAnimating && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(game.Driver.IsAnimating, Is.False, "input unlocks when the queue empties");
            AssertOrdered(observed, "place", "clear");
            AssertOrdered(observed, "clear", "drain");
            AssertOrdered(observed, "drain", "rise");

            UnityEngine.Object.Destroy(game.gameObject);
        }

        [Test]
        public void ClearStagger_HonorsTheNominal30ms_AndCompressesToFitTheBudget()
        {
            // Plenty of room: one cleared row, nothing else → the nominal 30ms stands.
            Assert.That(UiEventQueue.ClearStagger(BoardSpec.Width, false, false, false),
                Is.EqualTo(0.03f).Within(0.0001f));

            // Worst case (multi-drain + rise): the stagger collapses; budget wins.
            float worst = UiEventQueue.ClearStagger(2 * BoardSpec.Width, true, true, true);
            Assert.That(worst, Is.EqualTo(0f).Within(0.0001f), "stagger compresses to zero");

            float budget = ThemeRuntime.Seconds("t.resolveBudget");
            float clearBlock = Mathf.Min(2 * BoardSpec.Width * worst + 0.12f,
                UiEventQueue.ClearBudgetSeconds(true, true, true));
            float planned = clearBlock + UiEventQueue.DrainSeconds(true) + UiEventQueue.RiseSeconds();
            Assert.That(planned, Is.LessThanOrEqualTo(budget + 0.001f), "worst-case plan ≤ t.resolveBudget");
        }

        private static void AssertOrdered(IReadOnlyList<string> beats, string first, string second)
        {
            int a = IndexOf(beats, first);
            int b = IndexOf(beats, second);
            Assert.That(a, Is.GreaterThanOrEqualTo(0), $"beat '{first}' fired");
            Assert.That(b, Is.GreaterThanOrEqualTo(0), $"beat '{second}' fired");
            Assert.That(a, Is.LessThan(b), $"'{first}' precedes '{second}' (§2.6/§6.1)");
        }

        private static int IndexOf(IReadOnlyList<string> beats, string name)
        {
            for (int i = 0; i < beats.Count; i++)
            {
                if (beats[i] == name)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
