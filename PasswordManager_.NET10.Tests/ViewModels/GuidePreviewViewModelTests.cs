using Microsoft.Extensions.Logging.Abstractions;
using PasswordManager_.NET10.Tests.Fakes;
using PasswordManager_.NET10.ViewModels;

namespace PasswordManager_.NET10.Tests.ViewModels;

public class GuidePreviewViewModelTests
{
    private readonly FakeGuideService _guideService = new();
    private readonly FakeMarkdownToHtmlConverter _converter = new();
    private readonly FakeThemeService _themeService = new();
    private readonly FakeNavigationService _navigationService = new();

    private GuidePreviewViewModel CreateViewModel() => new(
        NullLogger<GuidePreviewViewModel>.Instance,
        _guideService,
        _converter,
        _themeService,
        _navigationService);

    // Asi es como el .md declara hoy las filas: HTML crudo con width por imagen,
    // porque width es lo unico que GitHub respeta y Markdown no fija el tamano.
    private const string HtmlRowMarkdown =
        "<p align=\"center\">\n"
        + "  <img src=\"doc01.jpg\" alt=\"Registro\" width=\"200\">\n"
        + "  <img src=\"doc02.jpg\" alt=\"Registro\" width=\"200\">\n"
        + "</p>";

    [Fact]
    public async Task LoadGuideAsync_WithHtmlImageRows_EmbedsEveryImageAsDataUri()
    {
        // Antes solo se buscaba ![alt](archivo) y el HTML crudo no coincidia, asi que
        // ningun src se resolvia y las imagenes no se veian en la app.
        _guideService.Markdown = HtmlRowMarkdown;

        await CreateViewModel().LoadGuideAsync();

        Assert.NotNull(_converter.ReceivedImageDataUris);
        Assert.Contains("doc01.jpg", _converter.ReceivedImageDataUris!);
        Assert.Contains("doc02.jpg", _converter.ReceivedImageDataUris!);
    }

    [Fact]
    public async Task LoadGuideAsync_WithMarkdownImageSyntax_StillEmbedsImages()
    {
        _guideService.Markdown = "![Registro](doc01.jpg) ![Registro](doc02.jpg)";

        await CreateViewModel().LoadGuideAsync();

        Assert.NotNull(_converter.ReceivedImageDataUris);
        Assert.Contains("doc01.jpg", _converter.ReceivedImageDataUris!);
        Assert.Contains("doc02.jpg", _converter.ReceivedImageDataUris!);
    }

    [Fact]
    public async Task LoadGuideAsync_WithRepeatedImage_RequestsItOnce()
    {
        _guideService.Markdown =
            "<p><img src=\"doc01.jpg\" width=\"200\"><img src=\"doc01.jpg\" width=\"200\"></p>";

        await CreateViewModel().LoadGuideAsync();

        Assert.Single(_guideService.RequestedImages);
    }

    [Fact]
    public async Task LoadGuideAsync_WithPathTraversal_DoesNotRequestIt()
    {
        _guideService.Markdown = "<p><img src=\"../secret.png\"></p>";

        await CreateViewModel().LoadGuideAsync();

        Assert.Empty(_guideService.RequestedImages);
    }

    [Fact]
    public async Task LoadGuideAsync_WithDarkTheme_PassesDarkFlagToConverter()
    {
        _themeService.Theme = "Dark";
        _guideService.Markdown = HtmlRowMarkdown;

        await CreateViewModel().LoadGuideAsync();

        Assert.True(_converter.ReceivedDarkTheme);
    }
}
