using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
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
    internal static UIViewController? RootController { get; private set; }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        AppHost.ConfigurePlatform = services =>
        {
            services.AddSingleton<TermRecall.Application.Abstractions.IReminderScheduler, IosReminderScheduler>();
        };
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }

    /// <summary>
    /// Xcode 27 закрывает приложение без UIScene. Окно создаёт SceneDelegate,
    /// поэтому базовый FinishedLaunching, который делает UIWindow без сцены, не вызывается.
    /// </summary>
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties, "Avalonia.iOS.SingleViewLifetime", "Avalonia.iOS")]
    [Export("application:didFinishLaunchingWithOptions:")]
    public new bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
    {
        _ = application;
        _ = launchOptions;
        var lifetimeType = Type.GetType("Avalonia.iOS.SingleViewLifetime, Avalonia.iOS", throwOnError: true)!;
        var lifetime = Activator.CreateInstance(lifetimeType)
            ?? throw new InvalidOperationException("Не удалось создать жизненный цикл Avalonia.");
        var viewProperty = lifetimeType.GetProperty("View", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("У жизненного цикла Avalonia нет свойства View.");

        var builder = CustomizeAppBuilder(CreateAppBuilder());
        builder.AfterSetup(_ =>
        {
            var view = new AvaloniaView();
            viewProperty.SetValue(lifetime, view);
            var controller = new DefaultAvaloniaViewController { View = view };
            view.InitWithController(controller);
            RootController = controller;
        });
        builder.SetupWithLifetime((IApplicationLifetime)lifetime);
        return true;
    }
}
