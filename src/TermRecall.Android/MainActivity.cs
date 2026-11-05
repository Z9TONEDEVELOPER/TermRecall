using Android.App;
using Android.Content.PM;
using Android.OS;
using Avalonia;
using Avalonia.Android;
using Microsoft.Extensions.DependencyInjection;
using TermRecall;
using TermRecall.Hosting;

namespace TermRecall.Android;

[Activity(
    Label = "TermRecall",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        AppHost.ConfigurePlatform = services =>
        {
            services.AddSingleton<TermRecall.Application.Abstractions.IReminderScheduler, AndroidReminderScheduler>();
        };
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            RequestPermissions(["android.permission.POST_NOTIFICATIONS"], 100);
        }
    }
}
