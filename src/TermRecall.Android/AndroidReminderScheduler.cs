using Android.App;
using Android.Content;
using TermRecall.Application.Abstractions;
using TermRecall.Domain.Entities;
namespace TermRecall.Android;

public sealed class AndroidReminderScheduler : IReminderScheduler
{
    public const string ChannelId = "termrecall.review";

    public Task ApplyAsync(UserSettings settings, CancellationToken cancellationToken)
    {
        var context = global::Android.App.Application.Context;
        var manager = (AlarmManager?)context.GetSystemService(Context.AlarmService);
        var intent = new Intent(context, typeof(ReminderReceiver));
        var pending = PendingIntent.GetBroadcast(context, 41, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        if (manager is null || pending is null)
        {
            return Task.CompletedTask;
        }

        if (!settings.ReminderEnabled)
        {
            manager.Cancel(pending);
            return Task.CompletedTask;
        }

        EnsureChannel(context);
        var now = DateTime.Now;
        var next = now.Date.Add(settings.ReminderTime);
        if (next <= now)
        {
            next = next.AddDays(1);
        }

        var trigger = new DateTimeOffset(next).ToUnixTimeMilliseconds();
        manager.SetInexactRepeating(AlarmType.RtcWakeup, trigger, AlarmManager.IntervalDay, pending);
        return Task.CompletedTask;
    }

    internal static void EnsureChannel(Context context)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return;
        }

        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        var channel = new NotificationChannel(ChannelId, "Повторение", NotificationImportance.Default)
        {
            Description = "Напоминание открыть TermRecall"
        };
        manager?.CreateNotificationChannel(channel);
    }
}

[BroadcastReceiver(Enabled = true, Exported = false)]
public class ReminderReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null)
        {
            return;
        }

        AndroidReminderScheduler.EnsureChannel(context);
        var launch = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName ?? string.Empty);
        var pending = launch is null
            ? null
            : PendingIntent.GetActivity(context, 0, launch, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        var notification = new Notification.Builder(context, AndroidReminderScheduler.ChannelId)
            .SetContentTitle("TermRecall")
            .SetContentText("Пора повторить термины.")
            .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
            .SetAutoCancel(true)
            .SetContentIntent(pending)
            .Build();
        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        manager?.Notify(41, notification);
    }
}
