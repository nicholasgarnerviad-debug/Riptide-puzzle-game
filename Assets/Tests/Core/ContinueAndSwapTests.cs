using System.Collections.Generic;
using NUnit.Framework;
using Riptide.Core;

namespace Riptide.Core.Tests
{
    /// <summary>
    /// ROADMAP rulings 2026-06-11: the once-per-run drowned-board continue and the
    /// single-slot Piece Swap booster — legality, effects, determinism, and the
    /// documented StateHash exclusion of the continue flag.
    /// </summary>
    [TestFixture]
    public sealed class ContinueAndSwapTests
    {
        private static LevelConfig Config(bool boostersAllowed = true)
        {
            var weights = new int[PieceCatalog.PieceCount];
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = 1;
            }

            var scoring = new ScoringConfig(1, 80, 2, 1, 5, 250, 250, 30, 5, true);
            return new LevelConfig(5, 1, 5, 0, 8, 6, weights, scoring, GoalSet.None,
                boostersAllowed: boostersAllowed);
        }

        private static GameState Drowned(LevelConfig config, bool continueUsed = false)
        {
            GameState fresh = GameState.NewGame(config, 99);
            var cells = new Cell[BoardSpec.CellCount];
            var tray = new TrayPiece?[BoardSpec.TraySize];
            for (int i = 0; i < BoardSpec.TraySize; i++)
            {
                tray[i] = fresh.TrayAt(i);
            }

            return GameState.Restore(config, cells, tray, BoardSpec.DrownWaterLevel, 0, 500L, 0, 0,
                12, 4, fresh.Rng, new GoalState(config.Goals, 0, 0, 3), GameStatus.LostDrowned,
                continueUsed);
        }

        [Test]
        public void Continue_RevivesTheBoard_PerTheRuling()
        {
            GameState drowned = Drowned(Config());

            MoveResult result = SimEngine.ApplyMove(drowned, new ContinueMove());
            GameState next = result.Next;

            Assert.That(next.WaterLevel, Is.EqualTo(BoardSpec.DrownWaterLevel - 3), "water −3");
            Assert.That(next.TideCounter, Is.EqualTo(0), "tide counter reset");
            Assert.That(next.Status, Is.EqualTo(GameStatus.InProgress), "back in the game");
            Assert.That(next.ContinueUsed, Is.True, "the single continue is spent");
            Assert.That(next.TrayPieceCount, Is.EqualTo(3), "fresh full tray");
            Assert.That(next.MoveCount, Is.EqualTo(drowned.MoveCount), "not a placement");
            Assert.That(next.Score, Is.EqualTo(drowned.Score), "no score change");
            Assert.That(next.ComboChain, Is.EqualTo(0), "death broke the combo");
            Assert.That(next.TraysDealt, Is.EqualTo(drowned.TraysDealt + 1), "counts as a dealt tray");
            Assert.That(result.Events.DrainAmount, Is.EqualTo(3), "the relief drain animates");
        }

        [Test]
        public void Continue_FloorsAtMinWaterLevel()
        {
            // minWater 1, drown at 10: −3 → 7, comfortably above the floor; craft a
            // high floor via config minWater == startWater == 8 to prove the clamp.
            var weights = new int[PieceCatalog.PieceCount];
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = 1;
            }

            var scoring = new ScoringConfig(1, 80, 2, 1, 5, 250, 250, 30, 5, true);
            var config = new LevelConfig(8, 8, 5, 0, 8, 6, weights, scoring, GoalSet.None);
            GameState drowned = Drowned(config);

            GameState next = SimEngine.ApplyMove(drowned, new ContinueMove()).Next;
            Assert.That(next.WaterLevel, Is.EqualTo(8), "floored at minWaterLevel");
        }

        [Test]
        public void Continue_RefusedWhenNotDrowned_OrAlreadyUsed_OrBoostersOff()
        {
            GameState alive = GameState.NewGame(Config(), 7);
            Assert.Throws<InvalidMoveException>(
                () => SimEngine.ApplyMove(alive, new ContinueMove()), "in-progress refuses");

            GameState spent = Drowned(Config(), continueUsed: true);
            Assert.Throws<InvalidMoveException>(
                () => SimEngine.ApplyMove(spent, new ContinueMove()), "once per run");

            GameState daily = Drowned(Config(boostersAllowed: false));
            Assert.Throws<InvalidMoveException>(
                () => SimEngine.ApplyMove(daily, new ContinueMove()), "never in the Daily (GDD 5.3 gate)");
        }

        [Test]
        public void Continue_IsDeterministic()
        {
            GameState drowned = Drowned(Config());

            GameState a = SimEngine.ApplyMove(drowned, new ContinueMove()).Next;
            GameState b = SimEngine.ApplyMove(drowned, new ContinueMove()).Next;

            Assert.That(a.ComputeHash(), Is.EqualTo(b.ComputeHash()), "same state, same continue, same result");
            for (int i = 0; i < BoardSpec.TraySize; i++)
            {
                Assert.That(a.TrayAt(i)!.Value.Piece, Is.EqualTo(b.TrayAt(i)!.Value.Piece));
            }
        }

        [Test]
        public void Continue_OtherMovesStayRefused_InTerminalStates()
        {
            GameState drowned = Drowned(Config());
            Assert.Throws<InvalidMoveException>(
                () => SimEngine.ApplyMove(drowned, new DrainPumpMove()), "only the continue enters a dead board");
        }

        [Test]
        public void ContinueUsed_PersistsThroughSubsequentMoves()
        {
            GameState drowned = Drowned(Config());
            GameState revived = SimEngine.ApplyMove(drowned, new ContinueMove()).Next;

            GameState afterBooster = SimEngine.ApplyMove(revived, new DrainPumpMove()).Next;
            Assert.That(afterBooster.ContinueUsed, Is.True, "the flag survives every BuildResult path");
        }

        [Test]
        public void ContinueUsed_IsExcludedFromStateHash_AsDocumented()
        {
            // DECISIONS 2026-06-11: deliberate exclusion preserves the golden pins.
            GameState a = Drowned(Config(), continueUsed: false);
            GameState b = Drowned(Config(), continueUsed: true);

            Assert.That(a.ComputeHash(), Is.EqualTo(b.ComputeHash()),
                "states differing only in ContinueUsed hash identically (documented, deliberate)");
        }

        [Test]
        public void PieceSwap_ReplacesExactlyOneSlot_Deterministically()
        {
            GameState state = GameState.NewGame(Config(), 7);
            TrayPiece before0 = state.TrayAt(0)!.Value;
            TrayPiece before2 = state.TrayAt(2)!.Value;

            MoveResult a = SimEngine.ApplyMove(state, new PieceSwapMove(1));
            MoveResult b = SimEngine.ApplyMove(state, new PieceSwapMove(1));

            Assert.That(a.Next.TrayAt(0)!.Value.Piece, Is.EqualTo(before0.Piece), "slot 0 untouched");
            Assert.That(a.Next.TrayAt(2)!.Value.Piece, Is.EqualTo(before2.Piece), "slot 2 untouched");
            Assert.That(a.Next.TrayAt(1).HasValue, Is.True, "slot 1 holds a fresh piece");
            Assert.That(a.Next.TrayAt(1)!.Value.Piece, Is.EqualTo(b.Next.TrayAt(1)!.Value.Piece), "deterministic draw");
            Assert.That(a.Next.MoveCount, Is.EqualTo(state.MoveCount), "not a placement");
            Assert.That(a.Next.Score, Is.EqualTo(state.Score), "free of score effects");
            Assert.That(a.Next.Rng, Is.Not.EqualTo(state.Rng), "consumed rng draws");
        }

        [Test]
        public void PieceSwap_RefusesEmptySlots_AndDailyMode()
        {
            GameState state = GameState.NewGame(Config(), 7);
            GameState oneGone = SimEngine.ApplyMove(state,
                new PlaceMove(0, FirstLegalAnchor(state, 0))).Next;
            Assert.Throws<InvalidMoveException>(
                () => SimEngine.ApplyMove(oneGone, new PieceSwapMove(0)), "emptied slot refuses");

            GameState daily = GameState.NewGame(Config(boostersAllowed: false), 7);
            Assert.Throws<InvalidMoveException>(
                () => SimEngine.ApplyMove(daily, new PieceSwapMove(1)), "GDD 5.3: Daily refuses boosters");
        }

        [Test]
        public void EndlessMilestoneAward_BanksEveryFiveTides()
        {
            CoinsConfig coins = TestKit.Economy().Coins;

            Assert.That(CoinRules.EndlessMilestoneAward(coins, 0), Is.EqualTo(0));
            Assert.That(CoinRules.EndlessMilestoneAward(coins, 4), Is.EqualTo(0));
            Assert.That(CoinRules.EndlessMilestoneAward(coins, 5), Is.EqualTo(15));
            Assert.That(CoinRules.EndlessMilestoneAward(coins, 13), Is.EqualTo(30), "two milestones banked");
        }

        private static GridPos FirstLegalAnchor(GameState state, int slot)
        {
            PieceId piece = state.TrayAt(slot)!.Value.Piece;
            for (int row = 0; row < BoardSpec.Height; row++)
            {
                for (int col = 0; col < BoardSpec.Width; col++)
                {
                    var pos = new GridPos(col, row);
                    if (PlacementValidator.CanPlace(state, piece, pos))
                    {
                        return pos;
                    }
                }
            }

            throw new AssertionException("no legal anchor found");
        }
    }
}
