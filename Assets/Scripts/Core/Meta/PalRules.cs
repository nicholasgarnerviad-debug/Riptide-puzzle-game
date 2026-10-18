using System;
using System.Collections.Generic;

namespace Riptide.Core
{
    /// <summary>The Pod's combined perks at current stages (GDD §16.4). Neutral = no Pod.</summary>
    public readonly struct PalPerks
    {
        public int CoinsPercent { get; }
        public int StarCoins { get; }
        public int TideIntervalBonus { get; }
        public int BoosterDiscountPercent { get; }
        public int StartWaterReduction { get; }
        public int FreePops { get; }
        public int IncubationPercent { get; }
        public int FreeContinuesPerDay { get; }
        public int GiftPercent { get; }
        public int QuestPercent { get; }
        public int ShinyMultiplier { get; }
        public int EggUpgradePercent { get; }
        public int BondPercent { get; }

        public PalPerks(int coins, int starCoins, int tide, int discount, int water, int pops, int incubation,
            int continues, int gift, int quest, int shinyMultiplier, int eggUpgrade, int bondPercent)
        {
            CoinsPercent = coins;
            StarCoins = starCoins;
            TideIntervalBonus = tide;
            BoosterDiscountPercent = Math.Min(discount, 90);
            StartWaterReduction = water;
            FreePops = pops;
            IncubationPercent = incubation;
            FreeContinuesPerDay = continues;
            GiftPercent = gift;
            QuestPercent = quest;
            ShinyMultiplier = Math.Max(1, shinyMultiplier);
            EggUpgradePercent = Math.Min(eggUpgrade, 100);
            BondPercent = Math.Max(100, bondPercent);
        }

        public static PalPerks None => new PalPerks(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 100);
    }

    /// <summary>One hatch's outcome (§16.3).</summary>
    public readonly struct HatchResult
    {
        public int Egg { get; }
        public int Line { get; }
        public bool Shiny { get; }
        public bool IsNew { get; }
        public int BondGained { get; }

        public HatchResult(int egg, int line, bool shiny, bool isNew, int bondGained)
        {
            Egg = egg;
            Line = line;
            Shiny = shiny;
            IsNew = isNew;
            BondGained = bondGained;
        }
    }

    /// <summary>
    /// GDD §16 Sea Pals rules: owning, bond, evolution, the Pod and its perks, the
    /// nest/incubator, deterministic hatches. Pure — callers persist the PalSave.
    /// </summary>
    public static class PalRules
    {
        private const ulong Golden = 0x9E3779B97F4A7C15UL;

        // ---------------------------------------------------------------- owning

        /// <summary>§16.7: a v4 save's rescues become bond, met species are owned, the buddy leads.</summary>
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

        /// <summary>A board rescue: owns the line (true when new) and adds rescue bond.</summary>
        public static bool RecordRescue(PalSave pals, PalsConfig cfg, int species, PalPerks perks)
        {
            int line = cfg.LineForSpecies(species);
            if (line < 0)
            {
                return false;
            }

            pals.EnsureLines(cfg.Lines.Count);
            bool isNew = pals.Stage[line] == 0;
            if (isNew)
            {
                pals.Stage[line] = 1;
            }

            pals.Bond[line] += Boosted(cfg.RescueBond, perks.BondPercent);
            if (isNew && pals.Pod[0] < 0)
            {
                pals.Pod[0] = line;
            }

            return isNew;
        }

        /// <summary>A finished run: every Pod pal gains run bond, the leader more.</summary>
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

        // ------------------------------------------------------------- evolution

        /// <summary>Bond needed to reach the next stage, or null at stage III (or unowned).</summary>
        public static int? NextThreshold(PalsConfig cfg, int line, int stage)
        {
            if (stage < 1 || stage >= PalsLoader.StageCount)
            {
                return null;
            }

            return cfg.EvolveBond[(int)cfg.Lines[line].Rarity][stage - 1];
        }

        public static bool CanEvolve(PalSave pals, PalsConfig cfg, int line)
        {
            int? next = NextThreshold(cfg, line, pals.StageOf(line));
            return next.HasValue && pals.BondOf(line) >= next.Value;
        }

        public static bool TryEvolve(PalSave pals, PalsConfig cfg, int line)
        {
            if (!CanEvolve(pals, cfg, line))
            {
                return false;
            }

            pals.Stage[line]++;
            return true;
        }

        /// <summary>§16.5: the Dex line reward is due (all three stages reached, not yet paid).</summary>
        public static bool LineRewardDue(PalSave pals, int line) =>
            pals.StageOf(line) >= PalsLoader.StageCount && line < pals.LineRewarded.Length && !pals.LineRewarded[line];

        // ------------------------------------------------------------- the Pod

        public static int PodSlotsOpen(PalsConfig cfg, int ownedCount)
        {
            int open = 0;
            foreach (int needed in cfg.PodUnlocks)
            {
                open += ownedCount >= needed ? 1 : 0;
            }

            return Math.Min(open, PalSave.MaxPod);
        }

        /// <summary>Puts an owned pal in an open slot (swapping if it sat elsewhere); −1 empties a slot (never the leader).</summary>
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

        /// <summary>The Pod's perks at their current stages (§16.4; callers skip the Daily).</summary>
        public static PalPerks Perks(PalSave pals, PalsConfig cfg)
        {
            int coins = 0, starCoins = 0, tide = 0, discount = 0, water = 0, pops = 0, incubation = 0;
            int continues = 0, gift = 0, quest = 0, shiny = 1, upgrade = 0, bond = 100;
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
                int value = line.PerkAt(stage);
                switch (line.Perk)
                {
                    case PalPerk.Coins: coins += value; break;
                    case PalPerk.StarCoins: starCoins += value; break;
                    case PalPerk.TideInterval: tide += value; break;
                    case PalPerk.BoosterDiscount: discount += value; break;
                    case PalPerk.StartWater: water += value; break;
                    case PalPerk.FreePops: pops += value; break;
                    case PalPerk.Incubation: incubation += value; break;
                    case PalPerk.FreeContinues: continues += value; break;
                    case PalPerk.GiftBoost: gift += value; break;
                    case PalPerk.QuestBoost: quest += value; break;
                    case PalPerk.DeepLuck:
                        shiny = Math.Max(shiny, value);
                        upgrade += line.Perk2At(stage);
                        break;
                    case PalPerk.BondBoost: bond = Math.Max(bond, value); break;
                }
            }

            return new PalPerks(coins, starCoins, tide, discount, water, pops, incubation, continues, gift, quest,
                shiny, upgrade, bond);
        }

        /// <summary>§16.4: Σ stages × coins per stage, capped, then Pal Gifts.</summary>
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

        // ---------------------------------------------------------------- eggs

        /// <summary>
        /// Grants an egg (Deep Luck may lift it a tier first). It starts incubating when the
        /// incubator is empty, else joins the nest; a full nest pays its overflow coins
        /// instead (the return value; 0 when the egg was kept).
        /// </summary>
        public static int GrantEgg(PalSave pals, PalsConfig cfg, int egg, PalPerks perks, out int granted)
        {
            pals.EggsGranted++;
            granted = UpgradeRoll(cfg, egg, pals.Seed, pals.EggsGranted, perks.EggUpgradePercent);
            if (pals.Incubating < 0)
            {
                pals.Incubating = granted;
                pals.Warmth = 0;
                return 0;
            }

            if (pals.Nest.Count >= cfg.NestCapacity)
            {
                return cfg.Eggs[granted].OverflowCoins;
            }

            pals.Nest.Add(granted);
            return 0;
        }

        /// <summary>Cleared rows warm the incubating egg (Warm Glow speeds it).</summary>
        public static void Warm(PalSave pals, int rows, PalPerks perks)
        {
            if (pals.Incubating >= 0 && rows > 0)
            {
                pals.Warmth += rows * (100 + perks.IncubationPercent);
            }
        }

        /// <summary>Incubation progress 0..1.</summary>
        public static float WarmthFraction(PalSave pals, PalsConfig cfg) =>
            pals.Incubating < 0 ? 0f : Math.Min(1f, pals.Warmth / (cfg.Eggs[pals.Incubating].RowsToHatch * 100f));

        public static bool HatchReady(PalSave pals, PalsConfig cfg) =>
            pals.Incubating >= 0 && pals.Warmth >= cfg.Eggs[pals.Incubating].RowsToHatch * 100;

        /// <summary>Hatches the warm egg: a new pal (stage I) or a duplicate's bond; the next nest egg starts warming.</summary>
        public static HatchResult? Hatch(PalSave pals, PalsConfig cfg, PalPerks perks)
        {
            if (!HatchReady(pals, cfg))
            {
                return null;
            }

            int egg = pals.Incubating;
            (int line, bool shiny) = Roll(cfg, egg, pals.Seed, pals.EggsHatched + 1, perks.ShinyMultiplier);
            pals.EggsHatched++;
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
                bond = cfg.DuplicateBond[(int)cfg.Lines[line].Rarity];
                pals.Bond[line] += bond;
            }

            if (shiny && !pals.Sparkle[line])
            {
                pals.Sparkle[line] = true;
                pals.SparkleOn[line] = true;
            }

            pals.Incubating = -1;
            pals.Warmth = 0;
            if (pals.Nest.Count > 0)
            {
                pals.Incubating = pals.Nest[0];
                pals.Nest.RemoveAt(0);
            }

            return new HatchResult(egg, line, shiny, isNew, bond);
        }

        /// <summary>The pure hatch roll: a weighted line draw, then the Sparkle draw.</summary>
        public static (int line, bool shiny) Roll(PalsConfig cfg, int egg, ulong seed, int hatchIndex, int shinyMultiplier)
        {
            EggType type = cfg.Eggs[egg];
            DeterministicRng rng = DeterministicRng.FromSeed(seed ^ unchecked((ulong)hatchIndex * Golden));
            RngIntDraw pick = rng.NextInt(type.TotalWeight);
            int roll = pick.Value;
            int line = 0;
            for (int i = 0; i < type.Weights.Count; i++)
            {
                if (roll < type.Weights[i])
                {
                    line = i;
                    break;
                }

                roll -= type.Weights[i];
            }

            RngIntDraw sparkle = pick.Rng.NextInt(cfg.ShinyOneIn);
            return (line, sparkle.Value < Math.Max(1, shinyMultiplier));
        }

        /// <summary>Deep Luck: a percent chance an earned egg lifts one tier.</summary>
        public static int UpgradeRoll(PalsConfig cfg, int egg, ulong seed, int grantIndex, int percent)
        {
            if (percent <= 0 || egg >= cfg.Eggs.Count - 1)
            {
                return egg;
            }

            DeterministicRng rng = DeterministicRng.FromSeed(~seed ^ unchecked((ulong)grantIndex * Golden));
            return rng.NextInt(100).Value < percent ? egg + 1 : egg;
        }

        // --------------------------------------------------------- egg sources

        /// <summary>The egg a Voyage first clear earns (§16.3), or −1.</summary>
        public static int VoyageMilestoneEgg(PalsConfig cfg, int zone, int levelIndex)
        {
            if (!cfg.Sources.VoyageMilestones.TryGetValue(levelIndex, out int egg))
            {
                return -1;
            }

            return levelIndex == 20 && zone >= cfg.Sources.DeepZoneFrom ? cfg.Sources.DeepZoneLevel20 : egg;
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

        /// <summary>Second Wind: a free continue is left today.</summary>
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

        /// <summary>Hatch odds as percents per line (the egg card's disclosure, §16.3).</summary>
        public static IReadOnlyList<(int line, float percent)> Odds(PalsConfig cfg, int egg)
        {
            EggType type = cfg.Eggs[egg];
            var odds = new List<(int, float)>();
            for (int i = 0; i < type.Weights.Count; i++)
            {
                if (type.Weights[i] > 0)
                {
                    odds.Add((i, 100f * type.Weights[i] / type.TotalWeight));
                }
            }

            return odds;
        }
    }
}
