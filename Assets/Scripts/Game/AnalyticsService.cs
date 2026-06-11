using System;
using System.Collections.Generic;
using System.Text;
using Riptide.Core;

namespace Riptide.Game
{
    /// <summary>External analytics sink (Firebase adapter at gate 4; fakes in tests).</summary>
    public interface IAnalyticsSink
    {
        void Log(string eventName, IReadOnlyList<(string key, string value)> parameters);
    }

    /// <summary>
    /// GDD 8.5 event pipe: verbatim names from AnalyticsSchema, fan-out to sinks,
    /// and a 20-event ring buffer for the debug overlay (contract 7D).
    /// </summary>
    public sealed class AnalyticsService
    {
        public const int RingSize = 20;

        private readonly List<IAnalyticsSink> sinks = new List<IAnalyticsSink>();
        private readonly Queue<string> ring = new Queue<string>(RingSize);

        public IReadOnlyCollection<string> LastEvents => ring;

        public void AddSink(IAnalyticsSink sink) => sinks.Add(sink);

        public void Log(string eventName, params (string key, string value)[] parameters)
        {
            var sb = new StringBuilder(eventName);
            if (parameters.Length > 0)
            {
                sb.Append(" {");
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(parameters[i].key).Append('=').Append(parameters[i].value);
                }

                sb.Append('}');
            }

            if (ring.Count >= RingSize)
            {
                ring.Dequeue();
            }

            ring.Enqueue(sb.ToString());

            foreach (IAnalyticsSink sink in sinks)
            {
                try
                {
                    sink.Log(eventName, parameters);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"Analytics sink failed: {ex.Message}");
                }
            }
        }

        // ---- GDD 8.5 helpers, parameters in schema order ----

        public void LogLevelStart(int zone, int level) =>
            Log(AnalyticsSchema.LevelStart, ("zone", zone.ToString()), ("level", level.ToString()));

        public void LogLevelEnd(int zone, int level, string result, int moves, int stars, int maxWater, int rescues) =>
            Log(AnalyticsSchema.LevelEnd, ("zone", zone.ToString()), ("level", level.ToString()),
                ("result", result), ("moves", moves.ToString()), ("stars", stars.ToString()),
                ("maxWater", maxWater.ToString()), ("rescues", rescues.ToString()));

        public void LogEndlessEnd(int placements, int tides, long score, string deathType) =>
            Log(AnalyticsSchema.EndlessEnd, ("placements", placements.ToString()), ("tides", tides.ToString()),
                ("score", score.ToString()), ("deathType", deathType));

        public void LogDailyAttempt(string result, long score, bool retryUsed) =>
            Log(AnalyticsSchema.DailyAttempt, ("result", result), ("score", score.ToString()),
                ("retryUsed", retryUsed ? "true" : "false"));

        public void LogBoosterUsed(string type, string source) =>
            Log(AnalyticsSchema.BoosterUsed, ("type", type), ("source", source));

        public void LogAdImpression(string format, string placement) =>
            Log(AnalyticsSchema.AdImpression, ("format", format), ("placement", placement));

        public void LogTidepoolPurchase(string itemId) =>
            Log(AnalyticsSchema.TidepoolPurchase, ("item", itemId));

        public void LogTutorialStep(int step) =>
            Log(AnalyticsSchema.TutorialStep, ("step", step.ToString()));
    }
}
