namespace PasswordManager_.NET10.Services.Interfaces;

/// <summary>
/// Lee el manual de usuario en Markdown que se empaqueta con la app.
/// </summary>
public interface IGuideService
{
    Task<string> GetGuideMarkdownAsync();

    /// <summary>
    /// Devuelve la imagen del guide como data URI, lista para incrustar en el HTML.
    /// </summary>
    Task<string?> GetImageDataUriAsync(string fileName);
}
