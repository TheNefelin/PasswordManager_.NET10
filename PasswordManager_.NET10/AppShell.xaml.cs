using Microsoft.Extensions.Logging;
using PasswordManager_.NET10.Services.Interfaces;

namespace PasswordManager_.NET10;

public partial class AppShell : Shell
{
    private readonly ISessionManager _sessionManager;

    public AppShell(ISessionManager sessionManager)
    {
        InitializeComponent();

        _sessionManager = sessionManager;
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        await _sessionManager.Logout(false);
    }
}