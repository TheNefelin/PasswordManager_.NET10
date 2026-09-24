using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PasswordManager_.NET10.Services.Interfaces;

namespace PasswordManager_.NET10.ViewModels;

public partial class HelpViewModel : BaseViewModel
{
    private readonly ILogger<HelpViewModel> _logger;
    private readonly INavigationService _navigationService;

    public HelpViewModel(ILogger<HelpViewModel> logger, INavigationService navigationService)
    {
        _logger = logger;
        _navigationService = navigationService;
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
            _logger.LogError(ex, "Error closing help page");
        }
    }
}
