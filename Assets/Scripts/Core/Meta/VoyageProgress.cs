using System;
using System.Collections.Generic;
using System.Text;

namespace Riptide.Core
{
    /// <summary>
    /// Best-star record per level plus unlock rules (GDD 3.1: stars gate nothing;
    /// levels unlock strictly in sequence). Serializes to a compact string for
    /// Phase 5 PlayerPrefs and the Phase 6 save file alike.
    /// </summary>
    public sealed class VoyageProgress
    {
        private readonly Dictionary<string, int> bestStars = new Dictionary<string, int>(StringComparer.Ordinal);

        public static string LevelId(int zone, int index) => $"z{zone}-l{index}";

        public int StarsFor(string levelId) => bestStars.TryGetValue(levelId, out int stars) ? stars : 0;

        public bool IsCompleted(string levelId) => StarsFor(levelId) > 0;

        public void Record(string levelId, int stars)
        {
            if (stars < 1 || stars > 3) throw new ArgumentOutOfRangeException(nameof(stars));
            if (!bestStars.TryGetValue(levelId, out int existing) || stars > existing)
            {
                bestStars[levelId] = stars;
            }
        }

        /// <summary>Level 1-1 is always open; otherwise the previous level must be completed.</summary>
        public bool IsUnlocked(int zone, int index)
        {
            if (zone == 1 && index == 1)
            {
                return true;
            }

            (int prevZone, int prevIndex) = index > 1 ? (zone, index - 1) : (zone - 1, 20);
            return IsCompleted(LevelId(prevZone, prevIndex));
        }

        /// <summary>Completed level count — drives the GDD 6 "none before level 8" gate.</summary>
        public int CompletedCount => bestStars.Count;

        public int TotalStars
        {
            get
            {
                int total = 0;
                foreach (int stars in bestStars.Values)
                {
                    total += stars;
                }

                return total;
            }
        }

        /// <summary>The next uncompleted unlocked level, for the Home "Continue" button.</summary>
        public (int zone, int index) NextLevel()
        {
            for (int zone = 1; zone <= 10; zone++)
            {
                for (int index = 1; index <= 20; index++)
                {
                    if (!IsCompleted(LevelId(zone, index)))
                    {
                        return (zone, index);
                    }
                }
            }

            return (10, 20);
        }

        public string Serialize()
        {
            var sb = new StringBuilder();
            foreach (KeyValuePair<string, int> entry in bestStars)
            {
                if (sb.Length > 0)
                {
                    sb.Append(';');
                }

                sb.Append(entry.Key).Append(':').Append(entry.Value);
            }

            return sb.ToString();
        }

        public static VoyageProgress Deserialize(string? text)
        {
            var progress = new VoyageProgress();
            if (string.IsNullOrEmpty(text))
            {
                return progress;
            }

            foreach (string entry in text!.Split(';'))
            {
                int colon = entry.LastIndexOf(':');
                if (colon <= 0)
                {
                    continue;
                }

                string id = entry.Substring(0, colon);
                if (int.TryParse(entry.Substring(colon + 1), out int stars) && stars >= 1 && stars <= 3)
                {
                    progress.Record(id, stars);
                }
            }

            return progress;
        }
    }
}
