using Foundation;
using UIKit;

namespace TermRecall.iOS;

[Register("SceneDelegate")]
public class SceneDelegate : UIResponder, IUIWindowSceneDelegate
{
    [Export("window")]
    public UIWindow? Window { get; set; }

    [Export("scene:willConnectToSession:options:")]
    public void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
    {
        if (scene is not UIWindowScene windowScene || AppDelegate.RootController is not UIViewController controller)
        {
            return;
        }

        var window = new UIWindow(windowScene);
        window.RootViewController = controller;
        Window = window;
        if (UIApplication.SharedApplication.Delegate is AppDelegate appDelegate)
        {
            appDelegate.Window = window;
        }

        window.MakeKeyAndVisible();
    }
}