using System;
using System.Collections.Generic;
using System.Text;

namespace Riptide.Core
{
    /// <summary>
    /// Versioned save schema v1 (contract 6D). Pure string ↔ data: serialization
    /// and parsing live in Core so corruption handling is dotnet-testable; the
    /// Game layer only does file IO. ANY malformation parses to null — callers
    /// start fresh and flag analytics; a save can never crash the game.
    /// </summary>
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

        public long Coins;
        public string VoyageProgress = "";
        public StreakState Streak = StreakState.Empty;
        public long EndlessBest;
        public long DailyAttemptDay = -1;
        public bool DailyRetryUsed;
        public int[] SpeciesRescues = new int[0];
        public List<string> DecorationsOwned = new List<string>();
        public long ChestDay = -1;
        public int ChestClaims;
        public bool RemoveAds;
        public bool RecoveredFromCorruption;

        public string Serialize()
        {
            var sb = new StringBuilder(512);
            sb.Append("{\n");
            sb.Append($"  \"version\": {CurrentVersion},\n");
            sb.Append($"  \"coins\": {Coins},\n");
            sb.Append($"  \"voyage\": \"{Escape(VoyageProgress)}\",\n");
            sb.Append($"  \"streak\": [{Streak.Current}, {Streak.Best}, {Streak.FreezesHeld}, {Streak.LastCompletedEpochDay}, {Streak.LastFreezePurchaseWeek}],\n");
            sb.Append($"  \"endlessBest\": {EndlessBest},\n");
            sb.Append($"  \"dailyAttemptDay\": {DailyAttemptDay},\n");
            sb.Append($"  \"dailyRetryUsed\": {(DailyRetryUsed ? "true" : "false")},\n");
            sb.Append("  \"speciesRescues\": [");
            for (int i = 0; i < SpeciesRescues.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(SpeciesRescues[i]);
            }

            sb.Append("],\n");
            sb.Append("  \"decorations\": [");
            for (int i = 0; i < DecorationsOwned.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append('"').Append(Escape(DecorationsOwned[i])).Append('"');
            }

            sb.Append("],\n");
            sb.Append($"  \"chestDay\": {ChestDay},\n");
            sb.Append($"  \"chestClaims\": {ChestClaims},\n");
            sb.Append($"  \"removeAds\": {(RemoveAds ? "true" : "false")}\n");
            sb.Append("}");
            return sb.ToString();
        }

        /// <summary>Null on ANY malformation — version, shape, types, ranges (contract 6D).</summary>
        public static SaveData? TryParse(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            try
            {
                JsonObject root = JsonParser.Parse(text!).AsObject();
                int version = root.Require("version").AsInt();
                if (version != CurrentVersion)
                {
                    // v1 is the first schema; unknown versions get a fresh save.
                    // Future schema bumps migrate here (contract 6D migration harness).
                    return null;
                }

                var save = new SaveData
                {
                    Coins = Math.Max(0, root.Require("coins").AsLong()),
                    VoyageProgress = root.Require("voyage").AsString(),
                    EndlessBest = Math.Max(0, root.Require("endlessBest").AsLong()),
                    DailyAttemptDay = root.Require("dailyAttemptDay").AsLong(),
                    DailyRetryUsed = root.Require("dailyRetryUsed").AsBool(),
                    ChestDay = root.Require("chestDay").AsLong(),
                    ChestClaims = Math.Max(0, root.Require("chestClaims").AsInt()),
                    RemoveAds = root.Require("removeAds").AsBool(),
                };

                JsonArray streak = root.Require("streak").AsArray();
                if (streak.Count != 5)
                {
                    return null;
                }

                int current = streak.Items[0].AsInt();
                int best = streak.Items[1].AsInt();
                int freezes = streak.Items[2].AsInt();
                long lastDay = streak.Items[3].AsLong();
                int lastWeek = streak.Items[4].AsInt();
                if (current < 0 || best < 0 || freezes < 0 || freezes > StreakLogic.MaxFreezesHeld)
                {
                    return null;
                }

                save.Streak = new StreakState(current, best, freezes, lastDay, lastWeek);

                JsonArray rescues = root.Require("speciesRescues").AsArray();
                save.SpeciesRescues = new int[rescues.Count];
                for (int i = 0; i < rescues.Count; i++)
                {
                    save.SpeciesRescues[i] = Math.Max(0, rescues.Items[i].AsInt());
                }

                JsonArray decorations = root.Require("decorations").AsArray();
                foreach (JsonValue item in decorations.Items)
                {
                    string id = item.AsString();
                    if (!save.DecorationsOwned.Contains(id))
                    {
                        save.DecorationsOwned.Add(id);
                    }
                }

                return save;
            }
            catch (JsonParseException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        public int RescuesFor(int speciesId) =>
            speciesId >= 0 && speciesId < SpeciesRescues.Length ? SpeciesRescues[speciesId] : 0;

        public void RecordRescue(int speciesId, int speciesCount)
        {
            if (SpeciesRescues.Length < speciesCount)
            {
                var grown = new int[speciesCount];
                Array.Copy(SpeciesRescues, grown, SpeciesRescues.Length);
                SpeciesRescues = grown;
            }

            if (speciesId >= 0 && speciesId < SpeciesRescues.Length)
            {
                SpeciesRescues[speciesId]++;
            }
        }

        /// <summary>GDD 5.2: rewarded coin chest, capped 3/day. Pure cap logic; the ad is Phase 7.</summary>
        public bool TryClaimChest(long todayEpochDay, int capPerDay)
        {
            if (ChestDay != todayEpochDay)
            {
                ChestDay = todayEpochDay;
                ChestClaims = 0;
            }

            if (ChestClaims >= capPerDay)
            {
                return false;
            }

            ChestClaims++;
            return true;
        }

        private static string Escape(string text)
        {
            var sb = new StringBuilder(text.Length + 8);
            foreach (char c in text)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(c);
                        }

                        break;
                }
            }

            return sb.ToString();
        }
    }
}
