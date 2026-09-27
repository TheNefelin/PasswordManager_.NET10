using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PasswordManager_.NET10.Services.Interfaces;

namespace PasswordManager_.NET10.ViewModels;

/// <summary>
/// Pantalla temporal de la guia en Markdown. Lee guide/USER_GUIDE.md, incrusta las
/// imagenes como data URIs y muestra el HTML en un WebView.
/// </summary>
public partial class GuidePreviewViewModel : BaseViewModel
{
    private const string DarkTheme = "Dark";

    // Detecta ![alt](archivo.jpg) en el Markdown original para saber que imagenes cargar.
    [GeneratedRegex(@"!\[[^\]]*\]\(([^)]+)\)")]
    private static partial Regex MarkdownImageRegex();

    private readonly ILogger<GuidePreviewViewModel> _logger;
    private readonly IGuideService _guideService;
    private readonly IMarkdownToHtmlConverter _markdownToHtmlConverter;
    private readonly IThemeService _themeService;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    public partial string HtmlContent { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LoadedInfo { get; set; } = string.Empty;

    public GuidePreviewViewModel(
        ILogger<GuidePreviewViewModel> logger,
        IGuideService guideService,
        IMarkdownToHtmlConverter markdownToHtmlConverter,
        IThemeService themeService,
        INavigationService navigationService)
    {
        _logger = logger;
        _guideService = guideService;
        _markdownToHtmlConverter = markdownToHtmlConverter;
        _themeService = themeService;
        _navigationService = navigationService;

        Title = "Guía";
    }

    [RelayCommand]
    public async Task LoadGuideAsync()
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            LoadedInfo = string.Empty;

            var markdown = await _guideService.GetGuideMarkdownAsync();

            var imageDataUris = await LoadReferencedImagesAsync(markdown);

            // "Auto" no es un color: el WebView resuelve el tema del sistema con
            // prefers-color-scheme, asi que se manda claro y deja que el CSS decida.
            var theme = await _themeService.GetThemeAsync();
            var darkTheme = string.Equals(theme, DarkTheme, StringComparison.OrdinalIgnoreCase);

            HtmlContent = _markdownToHtmlConverter.ConvertToHtml(markdown, imageDataUris, darkTheme);

            LoadedInfo = $"{markdown.Length} caracteres, {imageDataUris.Count} de 15 imagenes, tema {theme}";

            _logger.LogInformation("[GuidePreviewViewModel-LoadGuideAsync] Guide rendered. {Info}", LoadedInfo);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"No se pudo cargar la guía: {ex.GetType().Name}: {ex.Message}";

            _logger.LogError(ex, "[GuidePreviewViewModel-LoadGuideAsync] Error: {Message}", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<Dictionary<string, string>> LoadReferencedImagesAsync(string markdown)
    {
        var fileNames = MarkdownImageRegex().Matches(markdown)
            .Select(match => match.Groups[1].Value)
            .Where(name => !name.Contains('/') && !name.Contains('\\'))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var fileName in fileNames)
        {
            var dataUri = await _guideService.GetImageDataUriAsync(fileName);

            if (dataUri is not null)
                result[fileName] = dataUri;
        }

        return result;
    }

    [RelayCommand]
    public async Task CloseAsync()
    {
        try
        {
            await _navigationService.PopModalAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing guide page");
        }
    }
}
