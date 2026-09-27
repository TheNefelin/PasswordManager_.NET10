using System.Text;
using Microsoft.Extensions.Logging;
using PasswordManager_.NET10.Services.Interfaces;

namespace PasswordManager_.NET10.Services.Implementation;

public class GuideService : IGuideService
{
    // El .md y las imagenes viven en Resources/Raw/guide/. El csproj declara
    // <MauiAsset Include="Resources\Raw\**" LogicalName="%(RecursiveDir)%(Filename)%(Extension)" />,
    // que le quita el prefijo Resources\Raw al empaquetar, así que dentro del paquete
    // la ruta es "guide/USER_GUIDE.md" y no "Resources/Raw/guide/USER_GUIDE.md".
    private const string GuideFolder = "guide";
    private const string GuideFileName = "USER_GUIDE.md";

    private const string JpegMimeType = "image/jpeg";
    private const string PngMimeType = "image/png";

    private static readonly Dictionary<string, string> MimeTypesByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = JpegMimeType,
        [".jpeg"] = JpegMimeType,
        [".png"] = PngMimeType
    };

    private readonly ILogger<GuideService> _logger;

    public GuideService(ILogger<GuideService> logger)
    {
        _logger = logger;
    }

    public async Task<string> GetGuideMarkdownAsync()
    {
        var path = $"{GuideFolder}/{GuideFileName}";

        try
        {
            await using var stream = await FileSystem.OpenAppPackageFileAsync(path);

            using var reader = new StreamReader(stream, Encoding.UTF8);
            var markdown = await reader.ReadToEndAsync();

            _logger.LogInformation("[GuideService-GetGuideMarkdownAsync] Guide loaded from {Path}, {Length} chars",
                path, markdown.Length);

            return markdown;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GuideService-GetGuideMarkdownAsync] Error reading {Path}: {Message}",
                path, ex.Message);

            throw;
        }
    }

    public async Task<string?> GetImageDataUriAsync(string fileName)
    {
        // Solo nombres simples dentro de guide/: el manual es un asset confiable,
        // pero no hace falta dejar que una referencia "../" salga de la carpeta.
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains('/') || fileName.Contains('\\'))
            return null;

        var path = $"{GuideFolder}/{fileName}";

        if (!MimeTypesByExtension.TryGetValue(Path.GetExtension(fileName), out var mimeType))
            return null;

        try
        {
            await using var stream = await FileSystem.OpenAppPackageFileAsync(path);
            await using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);

            var dataUri = $"data:{mimeType};base64,{Convert.ToBase64String(buffer.ToArray())}";

            _logger.LogInformation("[GuideService-GetImageDataUriAsync] {Path} loaded, {Bytes} bytes",
                path, buffer.Length);

            return dataUri;
        }
        catch (Exception ex)
        {
            // Una imagen que falta no debe tumbar el manual: se deja el src original
            // y el WebView muestra un hueco.
            _logger.LogWarning(ex, "[GuideService-GetImageDataUriAsync] Could not read {Path}: {Message}",
                path, ex.Message);

            return null;
        }
    }
}
