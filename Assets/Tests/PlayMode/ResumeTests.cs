using System.Collections;
using NUnit.Framework;
using Riptide.Core;
using Riptide.Game;
using Riptide.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace Riptide.PlayMode.Tests
{
    /// <summary>
    /// Mid-run save &amp; resume, app half (SAVE_RESUME_DESIGN.md): a process death
    /// mid-run leaves a resumable record; resume reproduces the exact state hash;
    /// finishing or quitting clears it; a Daily resume keeps the attempt lock;
    /// corruption discards gracefully.
    /// </summary>
    public sealed class ResumeTests
    {
        private static string RunPath =>
            System.IO.Path.Combine(Application.persistentDataPath, "riptide_run.json");

        private static void Wipe()
        {
            foreach (string key in new[]
            {
                "riptide.voyage", "riptide.streak", "riptide.endless.best",
                "riptide.daily.attemptDay", "riptide.daily.retryUsed",
            })
            {
                PlayerPrefs.DeleteKey(key);
            }

            string savePath = System.IO.Path.Combine(Application.persistentDataPath, "riptide_save.json");
            if (System.IO.File.Exists(savePath))
            {
                System.IO.File.Delete(savePath);
            }

            if (System.IO.File.Exists(RunPath))
            {
                System.IO.File.Delete(RunPath);
            }
        }

        private static void KillApp(ScreenManager screens)
        {
            // Process death stand-in: the app object tree vanishes without any
            // FinishRun/abandon path running.
            Object.DestroyImmediate(screens.transform.parent != null
                ? screens.transform.parent.gameObject
                : screens.gameObject);
        }

        private static IEnumerator PlayMoves(GameFlow flow, int count)
        {
            var policy = new GreedyClearPolicy();
            DeterministicRng rng = DeterministicRng.FromSeed(11);
            for (int i = 0; i < count && !flow.Store!.State.Status.IsTerminal(); i++)
            {
                BotDecision decision = policy.Choose(flow.Store.State, rng);
                rng = decision.Rng;
                Assert.That(decision.Move, Is.Not.Null);
                flow.Store.TryDispatch(decision.Move!);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator KilledVoyageRun_Resumes_ToTheExactState()
        {
            Wipe();
            (GameFlow flow, ScreenManager screens) = GameBootstrap.CreateApp(instantAnimations: true);
            yield return null;

            flow.StartVoyageLevel(1, 2);
            yield return PlayMoves(flow, 6);
            Assert.That(flow.Store!.State.Status.IsTerminal(), Is.False, "fixture: still mid-run");
            ulong hashAtKill = StateHash.Compute(flow.Store.State);
            long scoreAtKill = flow.Store.State.Score;

            KillApp(screens);
            yield return null;

            (GameFlow revived, ScreenManager screens2) = GameBootstrap.CreateApp(instantAnimations: true);
            yield return null;

            Assert.That(revived.PendingRun, Is.Not.Null, "the record survived the kill");
            Assert.That(revived.PendingRun!.Mode, Is.EqualTo("Voyage"));
            Assert.That(revived.ResumeRun(), Is.True);
            Assert.That(revived.Screen, Is.EqualTo(FlowScreen.Playing));
            Assert.That(StateHash.Compute(revived.Store!.State), Is.EqualTo(hashAtKill),
                "resume reproduces the exact state");
            Assert.That(revived.Store.State.Score, Is.EqualTo(scoreAtKill));
            Assert.That(revived.Mode, Is.EqualTo(GameMode.Voyage));

            KillApp(screens2);
        }

        [UnityTest]
        public IEnumerator ResumedRun_KeepsRecording_FurtherMoves()
        {
            Wipe();
            (GameFlow flow, ScreenManager screens) = GameBootstrap.CreateApp(instantAnimations: true);
            yield return null;
            flow.StartEndless();
            yield return PlayMoves(flow, 4);
            KillApp(screens);
            yield return null;

            (GameFlow revived, ScreenManager screens2) = GameBootstrap.CreateApp(instantAnimations: true);
            yield return null;
            Assert.That(revived.ResumeRun(), Is.True);
            yield return PlayMoves(revived, 3);
            ulong hashAfterMore = StateHash.Compute(revived.Store!.State);
            KillApp(screens2);
            yield return null;

            (GameFlow third, ScreenManager screens3) = GameBootstrap.CreateApp(instantAnimations: true);
            yield return null;
            Assert.That(third.PendingRun, Is.Not.Null, "second kill is also resumable");
            Assert.That(third.ResumeRun(), Is.True);
            Assert.That(StateHash.Compute(third.Store!.State), Is.EqualTo(hashAfterMore),
                "the resumed run kept appending to the record");

            KillApp(screens3);
        }

        [UnityTest]
        public IEnumerator FinishedRun_LeavesNoPendingRecord()
        {
            Wipe();
            (GameFlow flow, ScreenManager screens) = GameBootstrap.CreateApp(instantAnimations: true);
            yield return null;
            flow.StartVoyageLevel(1, 1);

            var policy = new GreedyClearPolicy();
            DeterministicRng rng = DeterministicRng.FromSeed(5);
            int guard = 0;
            while (!flow.Store!.State.Status.IsTerminal() && guard++ < 400)
            {
                BotDecision decision = policy.Choose(flow.Store.State, rng);
                rng = decision.Rng;
                flow.Store.TryDispatch(decision.Move!);
            }

            int settle = 0;
            while (flow.Screen == FlowScreen.Playing && settle++ < 60)
            {
                yield return null;
            }

            Assert.That(System.IO.File.Exists(RunPath), Is.False,
                "a concluded run never offers a resume");

            KillApp(screens);
        }

        [UnityTest]
        public IEnumerator QuitToHome_AbandonsTheRecord()
        {
            Wipe();
            (GameFlow flow, ScreenManager screens) = GameBootstrap.CreateApp(instantAnimations: true);
            yield return null;
            flow.StartEndless();
            yield return PlayMoves(flow, 3);
            Assert.That(System.IO.File.Exists(RunPath), Is.True, "mid-run record exists");

            flow.GoTo(FlowScreen.Home);
            Assert.That(System.IO.File.Exists(RunPath), Is.False,
                "quit-to-home keeps abandon semantics (design §4)");

            KillApp(screens);
        }

        [UnityTest]
        public IEnumerator KilledDailyRun_Resumes_WithAttemptStillLocked()
        {
            Wipe();
            (GameFlow flow, ScreenManager screens) = GameBootstrap.CreateApp(instantAnimations: true);
            // Real clock: the revived app's boot-time DetectPendingRun compares
            // against the real today, so the record must carry it.
            yield return null;

            Assert.That(flow.StartDaily(), Is.True);
            ulong dailySeed = flow.CurrentSeed;
            yield return PlayMoves(flow, 4);
            KillApp(screens);
            yield return null;

            (GameFlow revived, ScreenManager screens2) = GameBootstrap.CreateApp(instantAnimations: true);
            yield return null;

            Assert.That(revived.PendingRun, Is.Not.Null);
            Assert.That(revived.ResumeRun(), Is.True);
            Assert.That(revived.CurrentSeed, Is.EqualTo(dailySeed), "same date-derived board");
            Assert.That(revived.Meta.CanAttemptDailyToday(), Is.False,
                "resume never grants a second attempt");

            KillApp(screens2);
        }

        [UnityTest]
        public IEnumerator StaleDailyRecord_IsDiscarded()
        {
            Wipe();
            (GameFlow flow, ScreenManager screens) = GameBootstrap.CreateApp(instantAnimations: true);
            long yesterday = flow.Meta.TodayEpochDay() - 1;
            flow.Meta.TodayEpochDay = () => yesterday;
            yield return null;
            Assert.That(flow.StartDaily(), Is.True);
            yield return PlayMoves(flow, 3);
            KillApp(screens);
            yield return null;

            // The revived app boots on the REAL clock — the record is a day old.
            (GameFlow revived, ScreenManager screens2) = GameBootstrap.CreateApp(instantAnimations: true);
            yield return null;

            Assert.That(revived.PendingRun, Is.Null, "yesterday's daily board no longer exists");
            Assert.That(System.IO.File.Exists(RunPath), Is.False, "stale record deleted");

            KillApp(screens2);
        }

        [UnityTest]
        public IEnumerator KilledWithPendingContinueOffer_Resume_ReRaisesTheOffer()
        {
            Wipe();
            // Craft a real endless run that ends drowned with the continue unspent
            // (design §7.1: the recorded state is terminal LostDrowned, offer pending).
            Riptide.Core.EconomyConfig economy = Riptide.Game.RuntimeContent.LoadEconomy();
            Riptide.Core.CreatureRoster roster = Riptide.Game.RuntimeContent.LoadCreatures();
            Riptide.Core.LevelConfig config = Riptide.Core.ModeFactory.Endless(economy, roster.Count);

            ulong seed = 0;
            System.Collections.Generic.List<Riptide.Core.Move>? moves = null;
            Riptide.Core.GameState? final = null;
            for (ulong candidate = 1; candidate <= 60 && moves == null; candidate++)
            {
                (System.Collections.Generic.List<Riptide.Core.Move> played, Riptide.Core.GameState end)
                    = PlayToTerminalHeadless(config, candidate);
                if (end.Status == Riptide.Core.GameStatus.LostDrowned && !end.ContinueUsed)
                {
                    seed = candidate;
                    moves = played;
                    final = end;
                }
            }

            Assert.That(moves, Is.Not.Null, "fixture: some seed under 60 drowns");
            var record = new Riptide.Core.RunRecord("Endless", 0, 0, 0, seed, moves!,
                Riptide.Core.StateHash.Compute(final!));
            System.IO.File.WriteAllText(RunPath, record.Serialize());

            (GameFlow revived, ScreenManager screens) = GameBootstrap.CreateApp(instantAnimations: true);
            yield return null;

            Assert.That(revived.PendingRun, Is.Not.Null, "drowned-with-offer record is resumable");
            Assert.That(revived.ResumeRun(), Is.True);
            Assert.That(revived.Screen, Is.EqualTo(FlowScreen.Playing));
            Assert.That(revived.ContinueOfferPending, Is.True,
                "the unspent continue offer re-raises on resume (design §7.1)");

            KillApp(screens);
        }

        private static (System.Collections.Generic.List<Riptide.Core.Move>, Riptide.Core.GameState)
            PlayToTerminalHeadless(Riptide.Core.LevelConfig config, ulong seed)
        {
            var moves = new System.Collections.Generic.List<Riptide.Core.Move>();
            Riptide.Core.GameState state = Riptide.Core.GameState.NewGame(config, seed);
            Riptide.Core.DeterministicRng rng = Riptide.Core.DeterministicRng.FromSeed(seed * 7919UL);
            for (int step = 0; step < 300 && !state.Status.IsTerminal(); step++)
            {
                Riptide.Core.Move? move = PickAnyLegal(state, ref rng);
                if (move == null)
                {
                    break;
                }

                state = Riptide.Core.SimEngine.ApplyMove(state, move).Next;
                moves.Add(move);
            }

            return (moves, state);
        }

        private static Riptide.Core.Move? PickAnyLegal(Riptide.Core.GameState state,
            ref Riptide.Core.DeterministicRng rng)
        {
            for (int attempt = 0; attempt < 64; attempt++)
            {
                Riptide.Core.RngIntDraw slotDraw = rng.NextInt(Riptide.Core.BoardSpec.TraySize);
                rng = slotDraw.Rng;
                Riptide.Core.RngIntDraw colDraw = rng.NextInt(Riptide.Core.BoardSpec.Width);
                rng = colDraw.Rng;
                Riptide.Core.RngIntDraw rowDraw = rng.NextInt(Riptide.Core.BoardSpec.Height);
                rng = rowDraw.Rng;
                Riptide.Core.TrayPiece? piece = state.TrayAt(slotDraw.Value);
                if (!piece.HasValue || rowDraw.Value < state.WaterLevel)
                {
                    continue;
                }

                var pos = new Riptide.Core.GridPos(colDraw.Value, rowDraw.Value);
                if (Riptide.Core.PlacementValidator.CanPlace(state, piece.Value.Piece, pos))
                {
                    return new Riptide.Core.PlaceMove(slotDraw.Value, pos);
                }
            }

            for (int slot = 0; slot < Riptide.Core.BoardSpec.TraySize; slot++)
            {
                Riptide.Core.TrayPiece? piece = state.TrayAt(slot);
                if (!piece.HasValue)
                {
                    continue;
                }

                for (int col = 0; col < Riptide.Core.BoardSpec.Width; col++)
                {
                    for (int row = state.WaterLevel; row < Riptide.Core.BoardSpec.Height; row++)
                    {
                        var pos = new Riptide.Core.GridPos(col, row);
                        if (Riptide.Core.PlacementValidator.CanPlace(state, piece.Value.Piece, pos))
                        {
                            return new Riptide.Core.PlaceMove(slot, pos);
                        }
                    }
                }
            }

            return null;
        }

        [UnityTest]
        public IEnumerator CorruptRecord_DiscardsGracefully()
        {
            Wipe();
            System.IO.File.WriteAllText(RunPath, "{ this is not a run record");

            (GameFlow flow, ScreenManager screens) = GameBootstrap.CreateApp(instantAnimations: true);
            yield return null;

            Assert.That(flow.PendingRun, Is.Null, "corruption reports no pending run");
            Assert.That(System.IO.File.Exists(RunPath), Is.False, "corrupt file deleted");
            Assert.That(flow.Screen, Is.EqualTo(FlowScreen.Home), "boot lands on Home, never crashes");

            KillApp(screens);
        }
    }
}
