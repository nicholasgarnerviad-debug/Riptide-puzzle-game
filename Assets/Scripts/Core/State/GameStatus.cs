namespace Riptide.Core
{
    /// <summary>
    /// Game lifecycle per GDD 2.2/2.3 (drown, stuck), 2.5 (creature-loss fail)
    /// and 3.1 (goal completion).
    /// </summary>
    public enum GameStatus : byte
    {
        InProgress = 0,
        Won = 1,
        LostDrowned = 2,
        LostStuck = 3,
        LostCreature = 4,
    }

    public static class GameStatusExtensions
    {
        public static bool IsTerminal(this GameStatus status) => status != GameStatus.InProgress;
    }
}
