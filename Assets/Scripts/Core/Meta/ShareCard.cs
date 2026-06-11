using System.Collections.Generic;
using System.Text;

namespace Riptide.Core
{
    /// <summary>
    /// GDD 3.3 emoji share card — text only, golden-tested verbatim. The water bar
    /// is 10 squares on the drown scale; up to 3 distinct rescued species appear
    /// in rescue order with emoji from creatures.json.
    /// </summary>
    public static class ShareCard
    {
        public const int MaxSpeciesShown = 3;

        public static string Compose(int dailyNumber, int finalWaterLevel, IReadOnlyList<string> rescuedSpeciesEmoji,
            int tidesSurvived, int tidesTarget, long score, int streak)
        {
            var sb = new StringBuilder();
            sb.Append("Riptide #").Append(dailyNumber).Append(" \U0001F30A\n");

            int water = finalWaterLevel < 0 ? 0 : (finalWaterLevel > BoardSpec.DrownWaterLevel ? BoardSpec.DrownWaterLevel : finalWaterLevel);
            for (int i = 0; i < water; i++)
            {
                sb.Append("\U0001F7E6");
            }

            for (int i = water; i < BoardSpec.DrownWaterLevel; i++)
            {
                sb.Append("⬛");
            }

            sb.Append('\n');

            int shown = 0;
            for (int i = 0; i < rescuedSpeciesEmoji.Count && shown < MaxSpeciesShown; i++, shown++)
            {
                sb.Append(rescuedSpeciesEmoji[i]);
            }

            if (shown > 0)
            {
                sb.Append(" rescued · ");
            }

            sb.Append(tidesSurvived).Append('/').Append(tidesTarget).Append(" tides\n");
            sb.Append("Score ").Append(GroupThousands(score)).Append(" · \U0001F525 streak ").Append(streak).Append('\n');
            sb.Append("riptide.game/d/").Append(dailyNumber);
            return sb.ToString();
        }

        /// <summary>Comma thousands grouping, culture-independent (share cards must match worldwide).</summary>
        public static string GroupThousands(long value)
        {
            bool negative = value < 0;
            ulong magnitude = negative ? (ulong)(-(value + 1)) + 1UL : (ulong)value;
            string digits = magnitude.ToString();
            var sb = new StringBuilder();
            if (negative)
            {
                sb.Append('-');
            }

            int lead = digits.Length % 3;
            if (lead == 0)
            {
                lead = 3;
            }

            sb.Append(digits, 0, lead);
            for (int i = lead; i < digits.Length; i += 3)
            {
                sb.Append(',').Append(digits, i, 3);
            }

            return sb.ToString();
        }
    }
}
