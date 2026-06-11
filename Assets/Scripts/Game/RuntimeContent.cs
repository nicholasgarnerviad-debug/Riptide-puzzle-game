using System;
using Riptide.Core;
using UnityEngine;

namespace Riptide.Game
{
    /// <summary>
    /// Loads game content from Resources/Content (DECISIONS.md: Resources for v1,
    /// Addressables out of scope). All parsing goes through the Core loaders so
    /// runtime and tooling can never disagree about a file.
    /// </summary>
    public static class RuntimeContent
    {
        public static EconomyConfig LoadEconomy() =>
            EconomyLoader.Load(LoadText("Content/economy"), "economy.json");

        public static CreatureRoster LoadCreatures() =>
            CreatureLoader.Load(LoadText("Content/creatures"), "creatures.json");

        public static LevelConfig EndlessConfig()
        {
            EconomyConfig economy = LoadEconomy();
            return ModeFactory.Endless(economy, LoadCreatures().Count);
        }

        public static LevelConfig DailyConfig()
        {
            EconomyConfig economy = LoadEconomy();
            return ModeFactory.Daily(economy, LoadCreatures().Count);
        }

        private static string LoadText(string path)
        {
            TextAsset? asset = Resources.Load<TextAsset>(path);
            if (asset == null)
            {
                throw new InvalidOperationException($"Missing content resource '{path}' under Assets/Resources.");
            }

            return asset.text;
        }
    }
}
