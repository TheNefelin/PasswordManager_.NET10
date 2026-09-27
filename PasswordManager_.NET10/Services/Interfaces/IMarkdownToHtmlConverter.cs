namespace PasswordManager_.NET10.Services.Interfaces;

/// <summary>
/// Convierte el manual en Markdown a HTML para mostrarlo en un WebView.
/// </summary>
public interface IMarkdownToHtmlConverter
{
    string ConvertToHtml(
        string markdown,
        IReadOnlyDictionary<string, string>? imageDataUris = null,
        bool darkTheme = false);
}
