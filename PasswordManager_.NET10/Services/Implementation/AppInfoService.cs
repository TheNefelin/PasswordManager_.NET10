using PasswordManager_.NET10.Services.Interfaces;

namespace PasswordManager_.NET10.Services.Implementation;

public class AppInfoService : IAppInfoService
{
    // La versión sale del manifest de cada plataforma: en Android, VersionString es
    // android:versionName y BuildString es android:versionCode. Ambos vienen de
    // ApplicationDisplayVersion y ApplicationVersion del .csproj, que es la única
    // fuente que hay que mantener.
    //
    // AppInfo va detrás de una interfaz porque es un estático de plataforma que
    // lanza NotSupportedOrImplementedException en el build sin plataforma (el que
    // usan los tests), así que el ViewModel no puede tocarlo directamente.
    public string Version => AppInfo.Current.VersionString;

    public string Build => AppInfo.Current.BuildString;
}
