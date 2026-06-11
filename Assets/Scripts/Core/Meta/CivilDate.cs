using System;

namespace Riptide.Core
{
    /// <summary>
    /// Pure civil-calendar math (Hinnant days_from_civil) so streak/daily logic can
    /// do date arithmetic on plain ints — Core never touches DateTime (GDD 8.3).
    /// Epoch day 0 = 1970-01-01. Week index is Monday-aligned.
    /// </summary>
    public static class CivilDate
    {
        public static long ToEpochDays(int year, int month, int day)
        {
            if (year < 1970 || year > 2200) throw new ArgumentOutOfRangeException(nameof(year));
            if (month < 1 || month > 12) throw new ArgumentOutOfRangeException(nameof(month));
            if (day < 1 || day > 31) throw new ArgumentOutOfRangeException(nameof(day));

            int y = year - (month <= 2 ? 1 : 0);
            long era = y / 400;
            long yoe = y - era * 400;
            long doy = (153L * (month + (month > 2 ? -3 : 9)) + 2) / 5 + day - 1;
            long doe = yoe * 365 + yoe / 4 - yoe / 100 + doy;
            return era * 146097 + doe - 719468;
        }

        /// <summary>Monday-aligned week index (1970-01-01 was a Thursday).</summary>
        public static int WeekIndex(long epochDays) => (int)((epochDays + 3) / 7);

        /// <summary>Parses strict "yyyy-MM-dd". Returns false on any malformation.</summary>
        public static bool TryParseIsoDate(string text, out int year, out int month, out int day)
        {
            year = 0;
            month = 0;
            day = 0;
            if (text == null || text.Length != 10 || text[4] != '-' || text[7] != '-')
            {
                return false;
            }

            return TryParseInt(text, 0, 4, out year)
                && TryParseInt(text, 5, 2, out month)
                && TryParseInt(text, 8, 2, out day)
                && month >= 1 && month <= 12 && day >= 1 && day <= 31;
        }

        private static bool TryParseInt(string text, int start, int length, out int value)
        {
            value = 0;
            for (int i = start; i < start + length; i++)
            {
                char c = text[i];
                if (c < '0' || c > '9')
                {
                    return false;
                }

                value = value * 10 + (c - '0');
            }

            return true;
        }
    }
}
