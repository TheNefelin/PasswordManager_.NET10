using PasswordManager_.NET10.Services.Interfaces;

namespace PasswordManager_.NET10.Tests.Fakes;

public class FakeMarkdownToHtmlConverter : IMarkdownToHtmlConverter
{
    /// <summary>Data URIs que el ViewModel entrego en la ultima conversion.</summary>
    public IReadOnlyDictionary<string, string>? ReceivedImageDataUris { get; private set; }

    public bool ReceivedDarkTheme { get; private set; }

    public string ConvertToHtml(
        string markdown,
        IReadOnlyDictionary<string, string>? imageDataUris = null,
        bool darkTheme = false)
    {
        ReceivedImageDataUris = imageDataUris;
        ReceivedDarkTheme = darkTheme;

        return "<html><body>ok</body></html>";
    }
}
