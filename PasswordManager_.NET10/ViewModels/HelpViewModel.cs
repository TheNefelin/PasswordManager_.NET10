using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace PasswordManager_.NET10.ViewModels;

public partial class HelpViewModel : BaseViewModel
{
    private readonly ILogger<HelpViewModel> _logger;

    public HelpViewModel(ILogger<HelpViewModel> logger)
    {
        _logger = logger;
    }

    [RelayCommand]
    public async Task CloseAsync()
    {
        try
        {
            await Application.Current!.Windows[0].Page!.Navigation.PopModalAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing help page");
        }
    }
}
