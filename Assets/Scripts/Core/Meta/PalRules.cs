using System;
using System.Collections.Generic;

namespace Riptide.Core
{
    /// <summary>The Pod's combined abilities at current stages (GDD §16.4). A new instance is neutral.</summary>
    public sealed class PalPerks
    {
        public int CoinsPercent { get; set; }
        public int StarCoins { get; set; }
        public int QuestPercent { get; set; }
        public int BoosterDiscountPercent { get; set; }
        public int GiftPercent { get; set; }
        public int RescueCoins { get; set; }
        public int EggPacePercent { get; set; }
        public int TideIntervalBonus { get; set; }
        public int FreePops { get; set; }
        public int FreeRerolls { get; set; }
        public int FreeDrains { get; set; }
        public int FreeContinuesPerDay { get; set; }
        public int SparkleMultiplier { get; set; } = 1;
        public int LegendaryPityCut { get; set; }
        public int StartWaterReduction { get; set; }
        public int BondPercent { get; set; } = 100;

        public static PalPerks None => new PalPerks();
    }

    /// <summary>What one board rescue did for the Tidelings (§16.2).</summary>
    public readonly struct RescueOutcome
    {
        public int Line { get; }
        public bool Befriended { get; }
        public int Eggs { get; }

        public RescueOutcome(int line, bool befriended, int eggs)
        {
            Line = line;
            Befriended = befriended;
            Eggs = eggs;
        }
    }

    /// <summary>One Call's arrival (§16.3).</summary>
    public readonly struct CallResult
    {
        public int Line { get; }
        public PalRarity Tier { get; }
        public bool Sparkle { get; }
        public bool IsNew { get; }
        public int BondGained { get; }

        /// <summary>A pity counter (or the ten-call guarantee) decided this arrival's tier.</summary>
        public bool Guaranteed { get; }

        public CallResult(int line, PalRarity tier, bool sparkle, bool isNew, int bondGained, bool guaranteed)
        {
            Line = line;
            Tier = tier;
            Sparkle = sparkle;
            IsNew = isNew;
            BondGained = bondGained;
            Guaranteed = guaranteed;
        }
    }

    /// <summary>
    /// GDD §16 Tidelings rules (internally "pals"): befriending Commons by rescue, eggs
    /// from rows and sources, the Call (tier odds, pity, ten-call guarantee — all
    /// deterministic from the save's seed), bond and growing up, the Pod and its
    /// abilities. Pure — callers persist the PalSave.
    /// </summary>
    public static class PalRules
    {
        private const ulong Golden = 0x9E3779B97F4A7C15UL;

        // ---------------------------------------------------------- befriending

        /// <summary>§16.7: every species a v4 save had met is kept (grandfathered); rescues become bond; the buddy leads.</summary>
        public static void MigrateLegacy(PalSave pals, PalsConfig cfg, int[] speciesRescues, int buddySpecies)
        {
            pals.EnsureLines(cfg.Lines.Count);
            for (int species = 0; species < speciesRescues.Length; species++)
            {
                int line = cfg.LineForSpecies(species);
                if (line >= 0 && speciesRescues[species] > 0)
                {
                    pals.Bond[line] = Math.Max(pals.Bond[line], speciesRescues[species]);
                    pals.Stage[line] = Math.Max(pals.Stage[line], 1);
                }
            }

            int leader = cfg.LineForSpecies(buddySpecies);
            if (leader >= 0 && pals.Owns(leader))
            {
                pals.Pod[0] = leader;
            }
        }

        /// <summary>
        /// A board rescue: a Common is befriended on its first rescue; a rarer creature on
        /// the board leaves eggs instead (rare Tidelings arrive only by Call). Owned
        /// Tidelings gain rescue bond either way.
        /// </summary>
        public static RescueOutcome RecordRescue(PalSave pals, PalsConfig cfg, int species, PalPerks perks)
        {
            int line = cfg.LineForSpecies(species);
            if (line < 0)
            {
                return new RescueOutcome(-1, false, 0);
            }

            pals.EnsureLines(cfg.Lines.Count);
            bool befriended = false;
            int eggs = 0;
            if (cfg.Lines[line].Rarity == PalRarity.Common)
            {
                if (pals.Stage[line] == 0)
                {
                    pals.Stage[line] = 1;
                    befriended = true;
                    if (pals.Pod[0] < 0)
                    {
                        pals.Pod[0] = line;
                    }
                }
            }
            else
            {
                eggs = cfg.Eggs.RareRescue;
                pals.Eggs += eggs;
            }

            if (pals.Owns(line))
            {
                pals.Bond[line] += Boosted(cfg.RescueBond, perks.BondPercent);
            }

            return new RescueOutcome(line, befriended, eggs);
        }

        /// <summary>A finished run: every Pod Tideling gains run bond, the leader more.</summary>
        public static void RecordRun(PalSave pals, PalsConfig cfg, PalPerks perks)
        {
            for (int slot = 0; slot < pals.Pod.Length; slot++)
            {
                int line = pals.Pod[slot];
                if (pals.Owns(line))
                {
                    pals.Bond[line] += Boosted(slot == 0 ? cfg.LeaderRunBond : cfg.RunBond, perks.BondPercent);
                }
            }
        }

        private static int Boosted(int amount, int percent) => (amount * percent + 99) / 100;

        // ------------------------------------------------------------ growing up

        /// <summary>Bond needed to grow to the next stage, or null at Grand (or unowned).</summary>
        public static int? NextThreshold(PalsConfig cfg, int line, int stage)
        {
            if (stage < 1 || stage >= PalsLoader.StageCount)
            {
                return null;
            }

            return cfg.GrowBond[(int)cfg.Lines[line].Rarity][stage - 1];
        }

        public static bool CanGrow(PalSave pals, PalsConfig cfg, int line)
        {
            int? next = NextThreshold(cfg, line, pals.StageOf(line));
            return next.HasValue && pals.BondOf(line) >= next.Value;
        }

        public static bool TryGrow(PalSave pals, PalsConfig cfg, int line)
        {
            if (!CanGrow(pals, cfg, line))
            {
                return false;
            }

            pals.Stage[line]++;
            return true;
        }

        /// <summary>§16.5: the Journal line reward is due (all three stages reached, not yet paid).</summary>
        public static bool LineRewardDue(PalSave pals, int line) =>
            pals.StageOf(line) >= PalsLoader.StageCount && line < pals.LineRewarded.Length && !pals.LineRewarded[line];

        // --------------------------------------------------------------- the Pod

        public static int PodSlotsOpen(PalsConfig cfg, int ownedCount)
        {
            int open = 0;
            foreach (int needed in cfg.PodUnlocks)
            {
                open += ownedCount >= needed ? 1 : 0;
            }

            return Math.Min(open, PalSave.MaxPod);
        }

        /// <summary>Puts an owned Tideling in an open slot (swapping if it sat elsewhere); −1 empties a slot (never the leader).</summary>
        public static bool TrySetPod(PalSave pals, PalsConfig cfg, int slot, int line)
        {
            if (slot < 0 || slot >= PodSlotsOpen(cfg, pals.OwnedCount()))
            {
                return false;
            }

            if (line < 0)
            {
                if (slot == 0)
                {
                    return false;
                }

                pals.Pod[slot] = -1;
                return true;
            }

            if (!pals.Owns(line))
            {
                return false;
            }

            int previous = Array.IndexOf(pals.Pod, line);
            if (previous >= 0)
            {
                pals.Pod[previous] = pals.Pod[slot];
            }

            pals.Pod[slot] = line;
            return true;
        }

        /// <summary>The Pod's abilities at their current stages (§16.4; callers skip the Daily).</summary>
        public static PalPerks Perks(PalSave pals, PalsConfig cfg)
        {
            var p = new PalPerks();
            int open = PodSlotsOpen(cfg, pals.OwnedCount());
            for (int slot = 0; slot < Math.Min(open, pals.Pod.Length); slot++)
            {
                int id = pals.Pod[slot];
                if (!pals.Owns(id) || id >= cfg.Lines.Count)
                {
                    continue;
                }

                PalLine line = cfg.Lines[id];
                int stage = pals.StageOf(id);
                int v = line.PerkAt(stage);
                switch (line.Perk)
                {
                    case PalPerk.Coins: p.CoinsPercent += v; break;
                    case PalPerk.StarCoins: p.StarCoins += v; break;
                    case PalPerk.QuestBoost: p.QuestPercent += v; break;
                    case PalPerk.BoosterDiscount: p.BoosterDiscountPercent = Math.Min(90, p.BoosterDiscountPercent + v); break;
                    case PalPerk.GiftBoost: p.GiftPercent += v; break;
                    case PalPerk.RescueCoins: p.RescueCoins += v; break;
                    case PalPerk.EggPace: p.EggPacePercent += v; break;
                    case PalPerk.TideInterval: p.TideIntervalBonus += v; break;
                    case PalPerk.FreePops: p.FreePops += v; break;
                    case PalPerk.FreeRerolls: p.FreeRerolls += v; break;
                    case PalPerk.FreeDrains: p.FreeDrains += v; break;
                    case PalPerk.FreeContinues: p.FreeContinuesPerDay += v; break;
                    case PalPerk.StartWater: p.StartWaterReduction += v; break;
                    case PalPerk.BondBoost: p.BondPercent = Math.Max(p.BondPercent, v); break;
                    case PalPerk.DeepLuck:
                        p.SparkleMultiplier = Math.Max(p.SparkleMultiplier, v);
                        p.LegendaryPityCut += line.Perk2At(stage);
                        break;
                    case PalPerk.WhaleSong:
                        p.TideIntervalBonus += v;
                        p.StartWaterReduction += line.Perk2At(stage);
                        break;
                }
            }

            return p;
        }

        /// <summary>§16.4: Σ stages × coins per stage, capped, then the Turtle's gift boost.</summary>
        public static int GiftCoins(PalSave pals, PalsConfig cfg, PalPerks perks)
        {
            int stages = 0;
            foreach (int stage in pals.Stage)
            {
                stages += stage;
            }

            int coins = Math.Min(stages * cfg.GiftCoinsPerStage, cfg.GiftCap);
            return coins * (100 + perks.GiftPercent) / 100;
        }

        // ------------------------------------------------------------------ eggs

        /// <summary>
        /// A finished board (any mode, won or lost — never a quit): every boardsPerEgg boards
        /// lays an egg, sooner with the Jellyfish. Returns eggs laid (0 or more).
        /// </summary>
        public static int AddBoard(PalSave pals, PalsConfig cfg, PalPerks perks)
        {
            pals.EggProgress += 100 + perks.EggPacePercent;
            int per = cfg.Eggs.BoardsPerEgg * 100;
            int laid = pals.EggProgress / per;
            pals.EggProgress -= laid * per;
            pals.Eggs += laid;
            return laid;
        }

        /// <summary>Progress toward the next board-laid egg, 0..1.</summary>
        public static float EggFraction(PalSave pals, PalsConfig cfg) =>
            Math.Min(1f, pals.EggProgress / (cfg.Eggs.BoardsPerEgg * 100f));

        /// <summary>Whole boards played toward the next egg ("3 / 5").</summary>
        public static int BoardsTowardEgg(PalSave pals) => pals.EggProgress / 100;

        public static void GrantEggs(PalSave pals, int count)
        {
            if (count > 0)
            {
                pals.Eggs += count;
            }
        }

        /// <summary>Eggs a Voyage first clear earns (§16.3), 0 when the level isn't a milestone.</summary>
        public static int VoyageMilestoneEggs(PalsConfig cfg, int zone, int levelIndex)
        {
            if (!cfg.Eggs.VoyageMilestones.TryGetValue(levelIndex, out int eggs))
            {
                return 0;
            }

            return levelIndex == 20 && zone >= cfg.Eggs.DeepZoneFrom ? cfg.Eggs.DeepZoneLevel20 : eggs;
        }

        /// <summary>Claims a one-time egg key; false when already claimed (or the key list is full).</summary>
        public static bool ClaimKey(PalSave pals, string key)
        {
            if (pals.EggKeys.Contains(key) || pals.EggKeys.Count >= PalSave.MaxEggKeys)
            {
                return false;
            }

            pals.EggKeys.Add(key);
            return true;
        }

        // ------------------------------------------------------------- the Call

        /// <summary>The Legendary pity after the Anglerfish's cut (never below the Epic pity).</summary>
        public static int LegendaryPity(PalsConfig cfg, PalPerks perks) =>
            Math.Max(cfg.Call.EpicPity, cfg.Call.LegendaryPity - perks.LegendaryPityCut);

        /// <summary>Calls left until a guaranteed Legendary (shown on the Call screen).</summary>
        public static int CallsUntilLegendary(PalSave pals, PalsConfig cfg, PalPerks perks) =>
            Math.Max(1, LegendaryPity(cfg, perks) - pals.SinceLegendary);

        /// <summary>One Call for <c>call.cost</c> eggs; null when the nest is short.</summary>
        public static CallResult? Call(PalSave pals, PalsConfig cfg, PalPerks perks)
        {
            if (pals.Eggs < cfg.Call.Cost)
            {
                return null;
            }

            pals.Eggs -= cfg.Call.Cost;
            return RollOne(pals, cfg, perks, PalRarity.Common);
        }

        /// <summary>Ten Calls for <c>call.tenCost</c> eggs, with at least one Rare-or-better when configured.</summary>
        public static IReadOnlyList<CallResult>? CallTen(PalSave pals, PalsConfig cfg, PalPerks perks)
        {
            if (pals.Eggs < cfg.Call.TenCost)
            {
                return null;
            }

            pals.Eggs -= cfg.Call.TenCost;
            var results = new List<CallResult>(10);
            bool rareSeen = false;
            for (int i = 0; i < 10; i++)
            {
                PalRarity floor = i == 9 && cfg.Call.TenCallGuaranteesRare && !rareSeen ? PalRarity.Rare : PalRarity.Common;
                CallResult result = RollOne(pals, cfg, perks, floor);
                rareSeen |= result.Tier >= PalRarity.Rare;
                results.Add(result);
            }

            return results;
        }

        private static CallResult RollOne(PalSave pals, PalsConfig cfg, PalPerks perks, PalRarity floor)
        {
            pals.Calls++;
            DeterministicRng rng = DeterministicRng.FromSeed(pals.Seed ^ unchecked((ulong)pals.Calls * Golden));
            int[] rates = cfg.Call.TierRatesPermille;
            bool guaranteed;
            PalRarity tier;
            if (pals.SinceLegendary + 1 >= LegendaryPity(cfg, perks))
            {
                tier = PalRarity.Legendary;
                guaranteed = true;
            }
            else
            {
                PalRarity minimum = pals.SinceEpic + 1 >= cfg.Call.EpicPity ? PalRarity.Epic : floor;
                RngIntDraw draw = rng.NextInt(RateFrom(rates, minimum));
                rng = draw.Rng;
                tier = TierFor(draw.Value, rates, minimum);
                guaranteed = minimum > PalRarity.Common;
            }

            IReadOnlyList<int> pool = cfg.LinesOfTier(tier);
            RngIntDraw pick = rng.NextInt(pool.Count);
            int line = pool[pick.Value];
            bool sparkle = pick.Rng.NextInt(cfg.SparkleOneIn).Value < Math.Max(1, perks.SparkleMultiplier);

            pals.SinceLegendary = tier == PalRarity.Legendary ? 0 : pals.SinceLegendary + 1;
            pals.SinceEpic = tier >= PalRarity.Epic ? 0 : pals.SinceEpic + 1;

            pals.EnsureLines(cfg.Lines.Count);
            bool isNew = pals.Stage[line] == 0;
            int bond = 0;
            if (isNew)
            {
                pals.Stage[line] = 1;
                if (pals.Pod[0] < 0)
                {
                    pals.Pod[0] = line;
                }
            }
            else
            {
                bond = cfg.DuplicateBond[(int)tier];
                pals.Bond[line] += bond;
            }

            if (sparkle && !pals.Sparkle[line])
            {
                pals.Sparkle[line] = true;
                pals.SparkleOn[line] = true;
            }

            return new CallResult(line, tier, sparkle, isNew, bond, guaranteed);
        }

        /// <summary>Sum of the rates from <paramref name="minimum"/> up (the draw's range).</summary>
        private static int RateFrom(int[] rates, PalRarity minimum)
        {
            int total = 0;
            for (int t = (int)minimum; t < rates.Length; t++)
            {
                total += rates[t];
            }

            return Math.Max(1, total);
        }

        /// <summary>The tier a roll in [0, sum of rates ≥ minimum) lands in, counting only tiers ≥ minimum.</summary>
        public static PalRarity TierFor(int roll, int[] rates, PalRarity minimum)
        {
            for (int t = (int)minimum; t < rates.Length; t++)
            {
                if (roll < rates[t])
                {
                    return (PalRarity)t;
                }

                roll -= rates[t];
            }

            return (PalRarity)(rates.Length - 1);
        }

        /// <summary>The Call's disclosure: each Tideling's percent (its tier's rate split evenly within the tier).</summary>
        public static IReadOnlyList<(int line, float percent)> Odds(PalsConfig cfg)
        {
            var odds = new List<(int, float)>();
            foreach (PalLine line in cfg.Lines)
            {
                int inTier = cfg.LinesOfTier(line.Rarity).Count;
                odds.Add((line.Id, cfg.Call.TierRatesPermille[(int)line.Rarity] / 10f / inTier));
            }

            return odds;
        }

        // --------------------------------------------------------- Second Wind

        /// <summary>A free continue is left today.</summary>
        public static bool FreeContinueLeft(PalSave pals, long today, PalPerks perks) =>
            perks.FreeContinuesPerDay > 0 && (pals.ContinueDay != today || pals.ContinuesUsed < perks.FreeContinuesPerDay);

        public static void UseFreeContinue(PalSave pals, long today)
        {
            if (pals.ContinueDay != today)
            {
                pals.ContinueDay = today;
                pals.ContinuesUsed = 0;
            }

            pals.ContinuesUsed++;
        }
    }
}
