using PasswordManager_.NET10.Services.Implementation;

namespace PasswordManager_.NET10.Tests.Services;

public class MarkdownToHtmlConverterTests
{
    private const string PngDataUri = "data:image/png;base64,iVBORw0KGgo=";

    private readonly MarkdownToHtmlConverter _converter = new();

    [Fact]
    public void ConvertToHtml_WithMarkdown_ProducesHtmlStructure()
    {
        var html = _converter.ConvertToHtml("# Título\n\nTexto de la guía.");

        Assert.Contains("<h1", html);
        // UseAdvancedExtensions agrega id automatico, por eso no se compara el tag completo.
        Assert.Contains(">Título</h1>", html);
        Assert.Contains("<p>Texto de la guía.</p>", html);
        Assert.Contains("<!DOCTYPE html>", html);
    }

    [Fact]
    public void ConvertToHtml_WithImageAndDataUri_ReplacesSourceWithDataUri()
    {
        // El HTML se genera en memoria, asi que una ruta como "doc01.jpg" no
        // resolveria dentro del WebView: tiene que venir ya incrustada.
        var html = _converter.ConvertToHtml(
            "![Registro](doc01.jpg)",
            new Dictionary<string, string> { ["doc01.jpg"] = PngDataUri });

        Assert.Contains($"src=\"{PngDataUri}\"", html);
        Assert.DoesNotContain("doc01.jpg", html);
    }

    [Fact]
    public void ConvertToHtml_WithImageMissingFromDictionary_KeepsOriginalSource()
    {
        var html = _converter.ConvertToHtml(
            "![Registro](doc01.jpg)",
            new Dictionary<string, string> { ["otra.jpg"] = PngDataUri });

        Assert.Contains("src=\"doc01.jpg\"", html);
    }

    [Fact]
    public void ConvertToHtml_WithNoDictionary_LeavesImagesUntouched()
    {
        var html = _converter.ConvertToHtml("![Registro](doc01.jpg)");

        Assert.Contains("src=\"doc01.jpg\"", html);
    }

    [Fact]
    public void ConvertToHtml_WithDarkTheme_UsesDarkBackground()
    {
        var dark = _converter.ConvertToHtml("texto", darkTheme: true);
        var light = _converter.ConvertToHtml("texto", darkTheme: false);

        Assert.Contains("#1f1f1f", dark);
        Assert.Contains("#ffffff", light);
        Assert.NotEqual(dark, light);
    }

    [Fact]
    public void ConvertToHtml_WithTable_ProducesTableMarkup()
    {
        var html = _converter.ConvertToHtml(
            "| | |\n|---|---|\n| ![A](a.jpg) | ![B](b.jpg) |",
            new Dictionary<string, string>
            {
                ["a.jpg"] = PngDataUri,
                ["b.jpg"] = PngDataUri
            });

        Assert.Contains("<table>", html);
        // Las dos imagenes de la fila deben quedar resueltas.
        Assert.DoesNotContain("src=\"a.jpg\"", html);
        Assert.DoesNotContain("src=\"b.jpg\"", html);
    }

    [Fact]
    public void ConvertToHtml_WithImagesOnlyParagraph_WrapsThemInScrollableRow()
    {
        var html = _converter.ConvertToHtml("![A](a.jpg) ![B](b.jpg) ![C](c.jpg)");

        Assert.Contains("<div class=\"img-row\">", html);
        // El scroll horizontal es CSS, no markup: no puede quedar como atributo.
        Assert.Contains("overflow-x: auto", html);
        // Ninguna imagen debe quedar suelta en un <p>.
        Assert.DoesNotContain("<p><img", html);
        // Las TRES deben sobrevivir al envoltado. Esto cubria un bug real: con el
        // cuantificador fuera del grupo, .NET devolvia solo la ultima iteracion y
        // el reemplazo descartaba las imagenes anteriores.
        Assert.Contains("a.jpg", html);
        Assert.Contains("b.jpg", html);
        Assert.Contains("c.jpg", html);
    }

    [Fact]
    public void ConvertToHtml_WithSingleImage_WrapsItInRow()
    {
        var html = _converter.ConvertToHtml("![A](a.jpg)");

        Assert.Contains("<div class=\"img-row\">", html);
    }

    [Fact]
    public void ConvertToHtml_WithImageRowAndText_OnlyWrapsTheImageParagraph()
    {
        var html = _converter.ConvertToHtml("![A](a.jpg)\n\nTexto normal.");

        Assert.Contains("<div class=\"img-row\">", html);
        Assert.Contains("<p>Texto normal.</p>", html);
    }

    [Fact]
    public void ConvertToHtml_WithAlertSpan_PreservesClassForAppStyling()
    {
        // El .md marca la alerta con una clase; el rojo lo aplica la app, no el .md.
        var html = _converter.ConvertToHtml(
            "<span class=\"alerta\">IMPORTANTE</span>");

        Assert.Contains("<span class=\"alerta\">", html);
        Assert.Contains(".alerta { color: #c62828", html);
    }

    [Fact]
    public void ConvertToHtml_WithRawHtmlImageRow_WrapsItAndKeepsAttributes()
    {
        // El .md escribe las filas como HTML crudo porque width es lo único que
        // GitHub respeta. El conversor debe envolver igual la fila, sin perder
        // ninguna imagen y conservando los atributos del <p>.
        var html = _converter.ConvertToHtml(
            "<p align=\"center\">\n"
            + "  <img src=\"a.jpg\" alt=\"A\" width=\"200\">\n"
            + "  <img src=\"b.jpg\" alt=\"B\" width=\"200\">\n"
            + "</p>");

        Assert.Contains("<div class=\"img-row\" align=\"center\">", html);
        Assert.Contains("overflow-x: auto", html);
        Assert.Contains("a.jpg", html);
        Assert.Contains("b.jpg", html);
    }

    [Fact]
    public void ConvertToHtml_UsesFixedBackdropForImageRowInBothThemes()
    {
        var light = _converter.ConvertToHtml("![A](a.jpg)", darkTheme: false);
        var dark = _converter.ConvertToHtml("![A](a.jpg)", darkTheme: true);

        // El gris es fijo a propósito: es el fondo de una captura, no del documento,
        // así que no cambia con el tema. GitHub descarta el CSS, por eso lo define
        // la app y no el .md.
        Assert.Contains("background: #d3d3d3", light);
        Assert.Contains("background: #d3d3d3", dark);
        Assert.DoesNotContain("background: #3a3a3a", dark);
    }

    [Fact]
    public void ConvertToHtml_DoesNotOverrideImageSizeDeclaredInMarkdown()
    {
        var html = _converter.ConvertToHtml(
            "<p align=\"center\">\n  <img src=\"a.jpg\" width=\"200\">\n</p>");

        // El tamaño lo declara el .md para que se vea igual en la app y en GitHub.
        // Si la app vuelve a impose un max-width, deja de verse igual en los dos.
        Assert.DoesNotContain("max-width: 72vw", html);
        Assert.DoesNotContain("max-height: 200px", html);
    }
}
