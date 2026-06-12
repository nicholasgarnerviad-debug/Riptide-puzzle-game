using Riptide.Game;

namespace Riptide.UI
{
    public enum BackAction
    {
        /// <summary>Consume the press, do nothing (first-run age gate).</summary>
        Blocked,

        /// <summary>A sheet is up — back dismisses it before anything else.</summary>
        DismissSheet,

        /// <summary>In play, back never quits — it raises the Pause sheet (§6.2).</summary>
        OpenPauseSheet,

        /// <summary>Navigate to a target screen (stack pops to it).</summary>
        GoTo,

        /// <summary>At the root: the press is NOT consumed; the OS backgrounds the app (§2/§4.1).</summary>
        BackgroundApp,
    }

    /// <summary>One Android-back decision (action + GoTo target when applicable).</summary>
    public readonly struct BackDecision
    {
        public BackAction Action { get; }
        public FlowScreen Target { get; }

        public BackDecision(BackAction action, FlowScreen target = FlowScreen.Home)
        {
            Action = action;
            Target = target;
        }
    }

    /// <summary>
    /// Spec 5-UI-a back-button matrix, as a pure table so the navigation
    /// state-machine tests need no frames. Priority: age gate → open sheet →
    /// per-screen rule. <paramref name="previous"/> is the screen beneath the
    /// top of the stack (voyage results back out to the map, endless to home).
    /// </summary>
    public static class BackRouter
    {
        public static BackDecision Decide(FlowScreen screen, bool sheetOpen, bool consentGateOpen,
            FlowScreen? previous = null)
        {
            if (consentGateOpen)
            {
                return new BackDecision(BackAction.Blocked);
            }

            if (sheetOpen)
            {
                return new BackDecision(BackAction.DismissSheet);
            }

            switch (screen)
            {
                case FlowScreen.Playing:
                    // Back never abandons a live run silently (§6.2).
                    return new BackDecision(BackAction.OpenPauseSheet);
                case FlowScreen.Home:
                    return new BackDecision(BackAction.BackgroundApp);
                default:
                    return new BackDecision(BackAction.GoTo, previous ?? FlowScreen.Home);
            }
        }
    }
}
