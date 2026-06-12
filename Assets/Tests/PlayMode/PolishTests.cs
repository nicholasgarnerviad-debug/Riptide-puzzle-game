using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Riptide.Core;
using Riptide.UI;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;

namespace Riptide.PlayMode.Tests
{
    /// <summary>
    /// 8-UI ✅: reduced-motion semantics (§1.4), the §6.4 share card render, the
    /// §9 JuiceDirector allocation budget, and an editor draw-call capture
    /// written to docs/perf (the 80-call device budget is Nick's device check).
    /// </summary>
    public sealed class PolishTests
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

        private static MoveResult SyntheticResult(GameState next)
        {
            var events = new MoveEvents(
                placedCells: new[] { new GridPos(0, 0) },
                rowsCleared: new[] { 0 },
                petrifiedCells: Array.Empty<GridPos>(),
                removedCells: Array.Empty<GridPos>(),
                rescuedCreatures: Array.Empty<CreatureEvent>(),
                lostCreatures: Array.Empty<CreatureEvent>(),
                spawnedCreatures: Array.Empty<CreatureEvent>(),
                drainAmount: 1,
                tideRose: true,
                waterDelta: 0,
                dealtPieces: Array.Empty<TrayPiece>(),
                scoring: new ScoreBreakdown(1, 80, 0, 0, 0, 2),
                statusAfter: GameStatus.InProgress);
            return new MoveResult(next, events);
        }

        [Test]
        public void NotificationPlanner_ProtectsStreaks_AndAlwaysAnnouncesTheDaily()
        {
            var scheduler = new Riptide.Game.FakeNotificationScheduler();

            scheduler.CancelAll();
            foreach (var n in Riptide.Game.NotificationPlanner.Plan(attemptedToday: false, streak: 3))
            {
                scheduler.Schedule(n);
            }

            Assert.That(scheduler.Scheduled.Count, Is.EqualTo(2), "daily ping + streak risk");
            Assert.That(scheduler.Scheduled[1].Kind, Is.EqualTo(Riptide.Game.NotificationKind.StreakRisk));
            Assert.That(scheduler.Scheduled[1].HourLocal, Is.EqualTo(Riptide.Game.NotificationPlanner.StreakRiskHour));

            var done = Riptide.Game.NotificationPlanner.Plan(attemptedToday: true, streak: 3);
            Assert.That(done.Count, Is.EqualTo(1), "no nag once today's daily is played");
            Assert.That(done[0].Kind, Is.EqualTo(Riptide.Game.NotificationKind.NewDaily));

            var noStreak = Riptide.Game.NotificationPlanner.Plan(attemptedToday: false, streak: 0);
            Assert.That(noStreak.Count, Is.EqualTo(1), "nothing to protect without a streak");
        }

        [Test]
        public void ReducedMotion_HalvesDurations_AndKillsStaggers()
        {
            int before = PlayerPrefs.GetInt("settings.reducedMotion.on", 0);
            try
            {
                PlayerPrefs.SetInt("settings.reducedMotion.on", 1);

                Assert.That(UiEventQueue.ClearStagger(BoardSpec.Width, false, false, false),
                    Is.EqualTo(0f), "§1.4: staggers off");
                float expected = ThemeRuntime.Seconds("t.drain") * ThemeRuntime.Theme.ReducedMotionScale;
                Assert.That(UiEventQueue.DrainSeconds(false), Is.EqualTo(expected).Within(0.0001f),
                    "§1.4: durations ×0.5 (accessibility override of the GDD-locked value)");

                PlayerPrefs.SetInt("settings.reducedMotion.on", 0);
                Assert.That(UiEventQueue.ClearStagger(BoardSpec.Width, false, false, false),
                    Is.EqualTo(0.03f).Within(0.0001f), "nominal stagger returns");
            }
            finally
            {
                PlayerPrefs.SetInt("settings.reducedMotion.on", before);
            }
        }

        [Test]
        public void ShareCard_Renders_TheSpecCanvas()
        {
            Texture2D tex = ShareCardRenderer.Render("Riptide #2 🌊\nrow\nriptide.game/d/2");

            Assert.That(tex.width, Is.EqualTo(ShareCardRenderer.Width), "§6.4: 1080 wide");
            Assert.That(tex.height, Is.EqualTo(ShareCardRenderer.Height), "§6.4: 1350 tall");
            Color corner = tex.GetPixel(8, 8);
            Assert.That(corner.a, Is.GreaterThan(0.9f), "opaque composition");
            Assert.That(corner.r + corner.g + corner.b, Is.GreaterThan(0.01f).And.LessThan(1.2f),
                "abyss-dark background, not a blank render");
            UnityEngine.Object.Destroy(tex);
        }

        [UnityTest]
        public IEnumerator JuiceDirector_BeatRouting_DoesNotAllocate()
        {
            int soundBefore = PlayerPrefs.GetInt("settings.audio.on", 1);
            GameBootstrap game = GameBootstrap.CreateGame(TestConfig(), seed: 11, instantAnimations: true);
            yield return null;

            try
            {
                JuiceDirector juice = UnityEngine.Object.FindFirstObjectByType<JuiceDirector>();
                Assert.That(juice, Is.Not.Null);
                MoveResult result = SyntheticResult(game.Store.State);

                // Warm every path once (clip cache, dictionary), then measure.
                PlayerPrefs.SetInt("settings.audio.on", 0);
                juice.OnBeat("clear", result);
                juice.OnBeat("rise", result);

                long before = GC.GetTotalMemory(false);
                for (int i = 0; i < 64; i++)
                {
                    juice.OnBeat("clear", result);
                    juice.OnBeat("drain", result);
                    juice.OnBeat("rise", result);
                }

                long delta = GC.GetTotalMemory(false) - before;
                Assert.That(delta, Is.LessThan(1024),
                    $"§9: beat routing must not allocate (saw {delta}B over 192 beats)");
            }
            finally
            {
                PlayerPrefs.SetInt("settings.audio.on", soundBefore);
                UnityEngine.Object.Destroy(game.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator DrawCalls_CapturedToThePerfReport_UnderTheEditorCeiling()
        {
            GameBootstrap game = GameBootstrap.CreateGame(TestConfig(), seed: 3, instantAnimations: true);
            using ProfilerRecorder setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            using ProfilerRecorder drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");

            for (int i = 0; i < 6; i++)
            {
                yield return null;
            }

            long setPassCount = setPass.LastValue;
            long drawCallCount = drawCalls.LastValue;
            UnityEngine.Object.Destroy(game.gameObject);

            string report =
                "# 8-UI editor perf capture (game scene: board+chrome+water+ring+snow)\n\n"
                + $"- captured: 2026-06-11, in-editor play-mode test (seed 3)\n"
                + $"- SetPass calls: {setPassCount} (material switches; the in-editor batching signal)\n"
                + $"- Draw calls: {drawCallCount} — this recorder may not populate in editor play mode\n"
                + "- Spec §9 budget: ≤80 draw calls ON DEVICE — editor numbers include scene/editor\n"
                + "  overhead and are NOT the device measurement; the device capture is a Gate C item\n"
                + "  (profiler attach on the §10 test phone, flagged in DECISIONS.md).\n"
                + "- Zero-alloc beat routing: covered by JuiceDirector_BeatRouting_DoesNotAllocate.\n"
                + "- Cold boot ≤2.5s: device item (Gate C).\n";
            string dir = Path.Combine(Application.dataPath, "..", "docs", "perf");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "8ui_editor_capture.md"), report);

            Assert.That(setPassCount, Is.GreaterThan(0), "recorder captured a real frame");
            Assert.That(drawCallCount, Is.LessThan(400),
                $"editor sanity ceiling (saw {drawCallCount}; device ≤80 is the Gate C check)");
        }
    }
}
