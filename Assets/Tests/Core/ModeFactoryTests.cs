using NUnit.Framework;
using Riptide.Core;

namespace Riptide.Core.Tests
{
    /// <summary>GDD 3.2/3.3 — mode assembly from economy.json.</summary>
    [TestFixture]
    public sealed class ModeFactoryTests
    {
        [Test]
        public void Endless_AssemblesFromEconomy()
        {
            LevelConfig config = ModeFactory.Endless(TestKit.Economy(), creatureSpeciesCount: 8);

            Assert.That(config.StartWaterLevel, Is.EqualTo(1), "GDD 2.2: endless starts at 1");
            Assert.That(config.TideInterval, Is.EqualTo(7), "GDD 3.2: starts gentle");
            Assert.That(config.CreatureSpawnIntervalTrays, Is.EqualTo(4));
            Assert.That(config.Goals.HasAny, Is.False, "endless never 'wins'");
            Assert.That(config.Scoring.AwardTideSurvival, Is.True, "GDD 10: survival points in endless");
            Assert.That(config.Escalation, Is.Not.Null);
            Assert.That(config.Escalation!.IntervalShrinkEveryTides, Is.EqualTo(4));
            Assert.That(config.Escalation.IntervalFloor, Is.EqualTo(3));
            Assert.That(config.PieceWeights[(int)PieceId.Mono1], Is.EqualTo(7), "band weights applied");
        }

        [Test]
        public void Daily_HasTheSurviveTidesGoal_AndEndlessRuleset()
        {
            LevelConfig config = ModeFactory.Daily(TestKit.Economy(), creatureSpeciesCount: 8);

            Assert.That(config.Goals.SurviveTidesTarget, Is.EqualTo(20), "GDD 3.3: survive 20 tides");
            Assert.That(config.Goals.RescueAllTarget, Is.Null);
            Assert.That(config.Scoring.AwardTideSurvival, Is.True);
            Assert.That(config.Escalation, Is.Not.Null, "daily escalates like endless");
            Assert.That(config.TideInterval, Is.EqualTo(7));
        }

        [Test]
        public void Daily_WithTheSameSeed_IsIdenticalWorldwide()
        {
            LevelConfig config = ModeFactory.Daily(TestKit.Economy(), 8);
            ulong seed = DailySeed.For(2026, 6, 11);

            Assert.That(GameState.NewGame(config, seed).ComputeHash(),
                Is.EqualTo(GameState.NewGame(config, seed).ComputeHash()));
        }
    }
}
