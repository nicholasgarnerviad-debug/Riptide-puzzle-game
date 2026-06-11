using System;
using System.IO;
using Riptide.Core;
using UnityEngine;

namespace Riptide.Game
{
    /// <summary>
    /// Thin IO wrapper around the pure SaveData schema (contract 6D): atomic
    /// temp-file swap on write; any unreadable file becomes a fresh save with a
    /// corruption flag (analytics event in Phase 7) — never a crash. Imports the
    /// Phase 5 PlayerPrefs meta once when no save file exists yet.
    /// </summary>
    public sealed class SaveStore
    {
        private readonly string path;
        private readonly string tempPath;

        public SaveData Data { get; private set; } = new SaveData();

        /// <summary>True when a file existed but could not be parsed (flag for analytics).</summary>
        public bool RecoveredFromCorruption { get; private set; }

        public SaveStore(string? overridePath = null)
        {
            path = overridePath ?? Path.Combine(Application.persistentDataPath, "riptide_save.json");
            tempPath = path + ".tmp";
        }

        public void Load()
        {
            RecoveredFromCorruption = false;
            if (File.Exists(path))
            {
                string text;
                try
                {
                    text = File.ReadAllText(path);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Riptide save unreadable ({ex.GetType().Name}); starting fresh.");
                    Data = new SaveData { RecoveredFromCorruption = true };
                    RecoveredFromCorruption = true;
                    return;
                }

                SaveData? parsed = SaveData.TryParse(text);
                if (parsed != null)
                {
                    Data = parsed;
                    return;
                }

                Debug.LogWarning("Riptide save corrupt; starting fresh (contract 6D).");
                Data = new SaveData { RecoveredFromCorruption = true };
                RecoveredFromCorruption = true;
                return;
            }

            Data = new SaveData();
            ImportLegacyPlayerPrefs();
        }

        public void Save()
        {
            try
            {
                File.WriteAllText(tempPath, Data.Serialize());
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                File.Move(tempPath, path);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Riptide save write failed: {ex.Message}");
            }
        }

        /// <summary>One-time Phase 5 → Phase 6 migration (DECISIONS.md).</summary>
        private void ImportLegacyPlayerPrefs()
        {
            if (!PlayerPrefs.HasKey("riptide.voyage") && !PlayerPrefs.HasKey("riptide.streak"))
            {
                return;
            }

            Data.VoyageProgress = PlayerPrefs.GetString("riptide.voyage", "");
            if (long.TryParse(PlayerPrefs.GetString("riptide.endless.best", "0"), out long best))
            {
                Data.EndlessBest = best;
            }

            if (long.TryParse(PlayerPrefs.GetString("riptide.daily.attemptDay", ""), out long day))
            {
                Data.DailyAttemptDay = day;
            }

            Data.DailyRetryUsed = PlayerPrefs.GetInt("riptide.daily.retryUsed", 0) == 1;

            string[] parts = PlayerPrefs.GetString("riptide.streak", "").Split('|');
            if (parts.Length == 5
                && int.TryParse(parts[0], out int current) && int.TryParse(parts[1], out int bestStreak)
                && int.TryParse(parts[2], out int freezes) && long.TryParse(parts[3], out long lastDay)
                && int.TryParse(parts[4], out int lastWeek))
            {
                Data.Streak = new StreakState(current, bestStreak, freezes, lastDay, lastWeek);
            }

            Save();
        }
    }
}
