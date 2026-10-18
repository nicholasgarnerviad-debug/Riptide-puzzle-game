using Riptide.Core;
using Riptide.Game;

namespace Riptide.UI
{
    /// <summary>
    /// GDD §15 copy built from content ids: quest lines, badge names and goals,
    /// power names and descriptions (with their tuned numbers), event names and
    /// goals. Every key it can produce is listed in <see cref="UiStringKeys"/>.
    /// </summary>
    public static class ExpansionText
    {
        public static string Quest(GameFlow flow, QuestDef quest)
        {
            string key = "quest." + char.ToLowerInvariant(quest.Kind.ToString()[0]) + quest.Kind.ToString().Substring(1);
            string species = quest.Species >= 0 && quest.Species < flow.Roster.Count
                ? flow.Roster.Species[quest.Species].Name
                : "";
            return string.Format(flow.Strings.Get(key), ShareCard.GroupThousands(quest.Target), species);
        }

        public static string BadgeName(GameFlow flow, BadgeDef badge) => flow.Strings.Get("badge." + badge.Id);

        public static string BadgeGoal(GameFlow flow, BadgeDef badge)
        {
            string stat = badge.Stat.ToString();
            string key = "badgeStat." + char.ToLowerInvariant(stat[0]) + stat.Substring(1);
            return string.Format(flow.Strings.Get(key), ShareCard.GroupThousands(badge.Target));
        }

        public static CreaturePower PowerOf(int species) => CreaturePowersConfig.PowerOf((byte)species);

        public static string PowerName(GameFlow flow, int species) =>
            flow.Strings.Get("power." + PowerOf(species));

        public static string PowerDescription(GameFlow flow, int species)
        {
            CreaturePowersConfig p = flow.Expansion.Powers;
            CreaturePower power = PowerOf(species);
            int amount = power switch
            {
                CreaturePower.Pinch => p.CrabCoral,
                CreaturePower.Wish => p.StarfishScore,
                CreaturePower.Steady => p.SeahorseTideRelief,
                CreaturePower.Ink => p.OctopusCoral,
                CreaturePower.Calm => p.TurtleDrain,
                CreaturePower.Regrow => p.AxolotlDrain,
                _ => 0,
            };
            return string.Format(flow.Strings.Get($"power.{power}.desc"), amount);
        }

        /// <summary>"Crab Pinch!" — the in-run popup line.</summary>
        public static string PowerPopup(GameFlow flow, int species)
        {
            string name = species >= 0 && species < flow.Roster.Count ? flow.Roster.Species[species].Name : "";
            return string.Format(flow.Strings.Get("power.popup"), name, PowerName(flow, species));
        }

        public static string EventName(GameFlow flow, EventDef e) => flow.Strings.Get("event." + e.Id);

        public static string EventDescription(GameFlow flow, EventDef e) => flow.Strings.Get($"event.{e.Id}.desc");

        public static string EventGoal(GameFlow flow, EventDef e)
        {
            string key = "event.goal." + e.GoalKind.ToString().ToLowerInvariant();
            return string.Format(flow.Strings.Get(key), ShareCard.GroupThousands(e.GoalTarget));
        }

        /// <summary>Results' level line for an event run: "Crab Carnival · Rescue 4 creatures".</summary>
        public static string EventLabel(GameFlow flow, RunOutcome outcome)
        {
            foreach (EventDef e in flow.Expansion.Events)
            {
                if (e.Id == outcome.EventId)
                {
                    return $"{EventName(flow, e)} · {EventGoal(flow, e)}";
                }
            }

            return flow.Strings.Get("event.title");
        }

        /// <summary>"Crab buddy bonus +6 · 3 powers fired" — empty when neither happened.</summary>
        public static string RunBonus(GameFlow flow, RunOutcome outcome)
        {
            string text = "";
            int buddy = flow.Meta.Save.BuddySpecies;
            if (outcome.BuddyBonus > 0 && buddy >= 0 && buddy < flow.Roster.Count)
            {
                text = string.Format(flow.Strings.Get("results.buddyBonus"), flow.Roster.Species[buddy].Name,
                    outcome.BuddyBonus);
            }

            if (outcome.PowersFired > 0)
            {
                string powers = string.Format(flow.Strings.Get("results.powers"), outcome.PowersFired);
                text = text.Length > 0 ? $"{text}  ·  {powers}" : powers;
            }

            return text;
        }

        /// <summary>The event reward decoration's display name (falls back to its id).</summary>
        public static string DecorationName(GameFlow flow, string id)
        {
            foreach (Decoration deco in flow.Decorations)
            {
                if (deco.Id == id)
                {
                    return deco.Name;
                }
            }

            return id;
        }
    }
}
