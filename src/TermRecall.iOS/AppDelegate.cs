using Avalonia;
using Avalonia.iOS;
using Foundation;
using Microsoft.Extensions.DependencyInjection;
using TermRecall;
using TermRecall.Hosting;
using UIKit;

namespace TermRecall.iOS;

[Register("AppDelegate")]
public partial class AppDelegate : AvaloniaAppDelegate<App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        AppHost.ConfigurePlatform = services =>
        {
            services.AddSingleton<TermRecall.Application.Abstractions.IReminderScheduler, IosReminderScheduler>();
        };
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}
