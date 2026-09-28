namespace PasswordManager_.NET10.Tests;

/// <summary>
/// Las capturas del manual están duplicadas en dos carpetas porque sus consumidores
/// tienen requisitos distintos que no se pueden unificar: la app solo puede leer
/// archivos que estén dentro de su paquete, así que los toma de
/// <c>Resources/Raw/guide/</c> como <c>MauiAsset</c>; y el README los sirve por ruta
/// relativa, así que los toma de <c>img/</c> en la raíz.
///
/// Unificar implicaría <c>MauiAsset Include="..\img\*"</c>, es decir meter una ruta
/// fuera de la carpeta del proyecto en el camino crítico de "la app muestra las
/// capturas", a cambio de 2,7 MB en un repositorio que GitHub no avisa hasta 1 GB.
/// En vez de eso, estas pruebas garantizan la invariante que sí importa: las dos
/// copias son idénticas.
/// </summary>
public class GuideImageConsistencyTests
{
    private static readonly string AppImages = Path.Combine(
        TestPaths.RepoRoot, "PasswordManager_.NET10", "Resources", "Raw", "guide");

    private static readonly string ReadmeImages = Path.Combine(TestPaths.RepoRoot, "img");

    [Fact]
    public void GuideImages_ReadmeCopyHasExactlyTheSameFiles()
    {
        var deLaApp = GuideImagesIn(AppImages);
        var delReadme = GuideImagesIn(ReadmeImages);

        // Sin este guardia el test pasaria aunque las dos carpetas estuvieran vacias.
        Assert.NotEmpty(deLaApp);

        // Una captura agregada en una sola carpeta deja al consumidor de la otra sin
        // imagen, y el markdown la referencia igual.
        Assert.Equal(deLaApp.Order(), delReadme.Order());
    }

    [Fact]
    public void GuideImages_EveryFileIsByteIdentical()
    {
        foreach (var nombre in GuideImagesIn(AppImages))
        {
            var deLaApp = File.ReadAllBytes(Path.Combine(AppImages, nombre));
            var delReadme = File.ReadAllBytes(Path.Combine(ReadmeImages, nombre));

            Assert.True(
                deLaApp.AsSpan().SequenceEqual(delReadme),
                nombre + " difiere entre Resources/Raw/guide/ (app) e img/ (README). "
                + "Copia la version de Resources/Raw/guide/ sobre img/ para dejarlas iguales.");
        }
    }

    private static string[] GuideImagesIn(string carpeta)
    {
        return Directory
            .GetFiles(carpeta, "doc*.jpg", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .OfType<string>()
            .ToArray();
    }
}
