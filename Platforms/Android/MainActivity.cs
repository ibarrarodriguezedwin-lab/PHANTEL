using Android.App;
using Android.Content.PM;
using Android.OS;

namespace PHANTEL
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            if (Window != null)
            {
                var fondoOscuro = Android.Graphics.Color.ParseColor("#121316");
                Window.SetStatusBarColor(fondoOscuro);
                Window.SetNavigationBarColor(fondoOscuro);
            }
        }
    }
}