using PasswordManager_.NET10.Services.Interfaces;

namespace PasswordManager_.NET10.Tests.Fakes;

public class FakeGuideService : IGuideService
{
    public string Markdown { get; set; } = string.Empty;

    /// <summary>Nombres pedidos, en orden y sin repetir.</summary>
    public List<string> RequestedImages { get; } = new();

    public string DataUriPrefix { get; set; } = "data:image/jpeg;base64,AAAA";

    public Task<string> GetGuideMarkdownAsync() => Task.FromResult(Markdown);

    public Task<string?> GetImageDataUriAsync(string fileName)
    {
        RequestedImages.Add(fileName);

        return Task.FromResult<string?>(DataUriPrefix + fileName);
    }
}
