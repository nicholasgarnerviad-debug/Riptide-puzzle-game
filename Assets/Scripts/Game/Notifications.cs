using System.Collections.Generic;

namespace Riptide.Game
{
    /// <summary>
    /// ROADMAP M8: local-notification seam, SdkAdapters-pattern — the PLANNING is
    /// real and tested now; the OS scheduler arrives with the SDK pass (the real
    /// adapter sits inert behind RIPTIDE_NOTIFICATIONS in SdkAdapters.cs).
    /// </summary>
    public enum NotificationKind
    {
        /// <summary>Morning ping: a fresh Daily Riptide is live.</summary>
        NewDaily,

        /// <summary>Evening ping: the streak dies at midnight without an attempt.</summary>
        StreakRisk,
    }

    public readonly struct PlannedNotification
    {
        public NotificationKind Kind { get; }

        /// <summary>Local hour (0–23) the ping should fire at.</summary>
        public int HourLocal { get; }

        public PlannedNotification(NotificationKind kind, int hourLocal)
        {
            Kind = kind;
            HourLocal = hourLocal;
        }
    }

    /// <summary>Pure planning: what to schedule given today's daily state.</summary>
    public static class NotificationPlanner
    {
        public const int NewDailyHour = 10;
        public const int StreakRiskHour = 20;

        public static IReadOnlyList<PlannedNotification> Plan(bool attemptedToday, int streak)
        {
            var plan = new List<PlannedNotification>
            {
                new PlannedNotification(NotificationKind.NewDaily, NewDailyHour),
            };

            if (streak > 0 && !attemptedToday)
            {
                plan.Add(new PlannedNotification(NotificationKind.StreakRisk, StreakRiskHour));
            }

            return plan;
        }
    }

    public interface INotificationScheduler
    {
        void CancelAll();

        void Schedule(PlannedNotification notification);
    }

    /// <summary>Test/editor double: records what WOULD be scheduled.</summary>
    public sealed class FakeNotificationScheduler : INotificationScheduler
    {
        public readonly List<PlannedNotification> Scheduled = new List<PlannedNotification>();

        public void CancelAll() => Scheduled.Clear();

        public void Schedule(PlannedNotification notification) => Scheduled.Add(notification);
    }

    /// <summary>Recomputes the schedule from save state (call on background/quit).</summary>
    public sealed class NotificationService
    {
        private readonly INotificationScheduler scheduler;

        public NotificationService(INotificationScheduler scheduler)
        {
            this.scheduler = scheduler;
        }

        public void Refresh(MetaServices meta)
        {
            scheduler.CancelAll();
            bool attemptedToday = !meta.CanAttemptDailyToday();
            foreach (PlannedNotification notification in
                NotificationPlanner.Plan(attemptedToday, meta.Streak.Current))
            {
                scheduler.Schedule(notification);
            }
        }
    }
}
