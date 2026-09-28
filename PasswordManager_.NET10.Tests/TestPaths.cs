namespace PasswordManager_.NET10.Tests;

/// <summary>
/// Localiza la raíz del repositorio desde la carpeta de salida del test, para que
/// los tests puedan leer archivos del proyecto sin depender de la ruta de trabajo
/// ni de copiar esos archivos como contenido.
/// </summary>
internal static class TestPaths
{
    public static string RepoRoot { get; } = FindRepoRoot();

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "PasswordManager_.NET10.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException(
            "No se encontro la raiz del repositorio subiendo desde " + AppContext.BaseDirectory);
    }
}
