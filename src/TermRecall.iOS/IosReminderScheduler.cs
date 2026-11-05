using Foundation;
using TermRecall.Application.Abstractions;
using TermRecall.Domain.Entities;
using UserNotifications;

namespace TermRecall.iOS;

public sealed class IosReminderScheduler : IReminderScheduler
{
    public Task ApplyAsync(UserSettings settings, CancellationToken cancellationToken)
    {
        var center = UNUserNotificationCenter.Current;
        center.RemovePendingNotificationRequests(["termrecall.daily"]);
        if (!settings.ReminderEnabled)
        {
            return Task.CompletedTask;
        }

        center.RequestAuthorization(UNAuthorizationOptions.Alert | UNAuthorizationOptions.Sound, (granted, _) =>
        {
            if (!granted)
            {
                return;
            }

            var content = new UNMutableNotificationContent
            {
                Title = "TermRecall",
                Body = "Пора повторить термины.",
                Sound = UNNotificationSound.Default
            };
            var components = new NSDateComponents
            {
                Hour = settings.ReminderTime.Hours,
                Minute = settings.ReminderTime.Minutes
            };
            var trigger = UNCalendarNotificationTrigger.CreateTrigger(components, true);
            var request = UNNotificationRequest.FromIdentifier("termrecall.daily", content, trigger);
            center.AddNotificationRequest(request, null);
        });
        return Task.CompletedTask;
    }
}
