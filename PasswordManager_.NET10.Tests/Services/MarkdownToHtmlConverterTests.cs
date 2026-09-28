using PasswordManager_.NET10.Services.Implementation;

namespace PasswordManager_.NET10.Tests.Services;

public class MarkdownToHtmlConverterTests
{
    private const string PngDataUri = "data:image/png;base64,iVBORw0KGgo=";

    private readonly MarkdownToHtmlConverter _converter = new();

    // Los avisos secuspan en el body, no en el <style>. Separate permite afirmar
    // sobre el contenido sin que un comentario del CSS rompa la comparacion.
    private static string BodyOf(string html)
    {
        var start = html.IndexOf("<body>", StringComparison.Ordinal);

        return start < 0 ? html : html[start..];
    }

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

    [Fact]
    public void ConvertToHtml_WithCautionAlert_EmitsGitHubAlertMarkup()
    {
        // > [!CAUTION] es la unica forma de tener un bloque rojo en GitHub sin
        // escribir CSS, porque su sanitizador descarta el atributo style. Markdig lo
        // parsea dentro de UseAdvancedExtensions, que es una caja negra: si un
        // upgrade lo saca de ahi, la alerta deja de interpretarse y en la app
        // aparece el texto literal "[!CAUTION]".
        var html = _converter.ConvertToHtml("> [!CAUTION]\n> No se puede reemplazar.");
        var body = BodyOf(html);

        Assert.Contains("class=\"markdown-alert markdown-alert-caution\"", body);
        Assert.Contains("markdown-alert-title", body);
        // Si el texto sobrevive sin convertir, la alerta no se esta interpretando.
        Assert.DoesNotContain("[!CAUTION]", body);
    }

    [Fact]
    public void ConvertToHtml_WithImportantAlert_EmitsImportantMarkup()
    {
        var html = _converter.ConvertToHtml("> [!IMPORTANT]\n> Primer paso.");
        var body = BodyOf(html);

        Assert.Contains("class=\"markdown-alert markdown-alert-important\"", body);
        Assert.DoesNotContain("[!IMPORTANT]", body);
    }

    [Fact]
    public void ConvertToHtml_UsesAlertColorsInBothThemes()
    {
        var light = _converter.ConvertToHtml("texto", darkTheme: false);
        var dark = _converter.ConvertToHtml("texto", darkTheme: true);

        // GitHub colorea el recuadro con su propia CSS; la app necesita la suya para
        // que la alerta se vea igual. En oscuro el fondo del recuadro tiene que ser
        // oscuro o el texto claro del cuerpo queda ilegible.
        Assert.Contains(".markdown-alert-caution { border-color: #d1242f", light);
        Assert.Contains(".markdown-alert-caution { border-color: #f85149", dark);
        Assert.Contains(".markdown-alert-important { border-color: #0969da", light);
        Assert.Contains(".markdown-alert-important { border-color: #58a6ff", dark);
        // Sin esto el SVG del icono se dibuja en negro y no toma el color del titulo.
        Assert.Contains("fill: currentColor", light);
        Assert.Contains("fill: currentColor", dark);
    }
}
