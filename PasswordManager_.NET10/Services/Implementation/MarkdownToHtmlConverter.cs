using System.Text.RegularExpressions;
using Markdig;
using PasswordManager_.NET10.Services.Interfaces;

namespace PasswordManager_.NET10.Services.Implementation;

/// <summary>
/// Convierte el manual a HTML con Markdig y reemplaza las referencias a imagen
/// por data URIs, porque dentro de un WebView una ruta de archivo no resuelve:
/// el HTML se genera en memoria y no tiene acceso al sistema de archivos del paquete.
/// </summary>
public partial class MarkdownToHtmlConverter : IMarkdownToHtmlConverter
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    [GeneratedRegex("src=\"([^\"]+)\"")]
    private static partial Regex ImageSourceRegex();

    // Un parrafo que solo contiene imagenes es una fila de capturas: se envuelve en
    // un div con scroll horizontal, que es como las muestra el manual original
    // (ScrollView Orientation="Horizontal" + Border gris).
    [GeneratedRegex("<p>(\\s*<img[^>]*/?>\\s*)+</p>")]
    private static partial Regex ImageOnlyParagraphRegex();

    public string ConvertToHtml(
        string markdown,
        IReadOnlyDictionary<string, string>? imageDataUris = null,
        bool darkTheme = false)
    {
        var html = Markdown.ToHtml(markdown, Pipeline);

        if (imageDataUris is { Count: > 0 })
        {
            html = ImageSourceRegex().Replace(html, match =>
            {
                var source = match.Groups[1].Value;

                return imageDataUris.TryGetValue(source, out var dataUri)
                    ? $"src=\"{dataUri}\""
                    : match.Value;
            });
        }

        // Se envuelve despues de sustituir los src: el div no debe alterar el contenido.
        html = ImageOnlyParagraphRegex().Replace(
            html,
            match => $"<div class=\"img-row\">{match.Value[3..^4].Trim()}</div>");

        return BuildTemplate(html, darkTheme);
    }

    private static string BuildTemplate(string body, bool darkTheme)
    {
        // El tema llega desde IThemeService, no desde el sistema: la app tiene
        // Light/Dark/Auto propio y puede no coincidir con el del dispositivo.
        // Por eso no se usa prefers-color-scheme como unica fuente.
        var css = darkTheme ? DarkCss : LightCss;

        return $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1.0" />
                <style>
            {{css}}
                </style>
            </head>
            <body>
            {{body}}
            </body>
            </html>
            """;
    }

    private const string LightCss = """
        :root { color-scheme: light; }
        body { font-family: system-ui, -apple-system, sans-serif; margin: 20px; line-height: 1.5;
               background: #ffffff; color: #1f1f1f; }
        h1 { font-size: 24px; } h2 { font-size: 20px; } h3 { font-size: 17px; }
        img { max-width: 100%; height: auto; }
        .img-row { display: flex; flex-wrap: nowrap; gap: 10px; overflow-x: auto;
                   padding: 10px; background: #d3d3d3; border-radius: 6px; }
        .img-row img { max-height: 200px; max-width: 72vw; width: auto; height: auto;
                       flex: 0 0 auto; border-radius: 4px; }
        .img-row img:first-child { margin-left: auto; }
        .img-row img:last-child { margin-right: auto; }
        .alerta { color: #c62828; font-weight: 700; }
        table { width: 100%; border-collapse: collapse; display: table; }
        th, td { text-align: center; vertical-align: top; padding: 4px; }
        hr { border: none; border-top: 1px solid #d0d0d0; margin: 20px 0; }
        code { font-family: ui-monospace, Menlo, Consolas, monospace; }
        """;

    private const string DarkCss = """
        :root { color-scheme: dark; }
        body { font-family: system-ui, -apple-system, sans-serif; margin: 20px; line-height: 1.5;
               background: #1f1f1f; color: #f0f0f0; }
        h1 { font-size: 24px; } h2 { font-size: 20px; } h3 { font-size: 17px; }
        img { max-width: 100%; height: auto; }
        .img-row { display: flex; flex-wrap: nowrap; gap: 10px; overflow-x: auto;
                   padding: 10px; background: #3a3a3a; border-radius: 6px; }
        .img-row img { max-height: 200px; max-width: 72vw; width: auto; height: auto;
                       flex: 0 0 auto; border-radius: 4px; }
        .img-row img:first-child { margin-left: auto; }
        .img-row img:last-child { margin-right: auto; }
        .alerta { color: #ef9a9a; font-weight: 700; }
        table { width: 100%; border-collapse: collapse; display: table; }
        th, td { text-align: center; vertical-align: top; padding: 4px; }
        hr { border: none; border-top: 1px solid #3d3d3d; margin: 20px 0; }
        code { font-family: ui-monospace, Menlo, Consolas, monospace; }
        """;
}
