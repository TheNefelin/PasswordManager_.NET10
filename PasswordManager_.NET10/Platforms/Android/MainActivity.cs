using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace PasswordManager_.NET10
{
    // WindowSoftInputMode = AdjustResize: sin esto, el teclado de Android se dibuja
    // encima de la ventana en vez de reducirla, y tapa los campos que estan debajo
    // del enfocado junto con el boton de guardar.Hace falta ademas que cada pagina
    // de formulario tenga un ScrollView, para que al reducirse la ventana haya algo
    // que se pueda desplazar.
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, WindowSoftInputMode = SoftInput.AdjustResize, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
    }
}
