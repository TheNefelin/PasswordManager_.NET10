using Microsoft.Extensions.Logging.Abstractions;
using PasswordManager_.NET10.Tests.Fakes;
using PasswordManager_.NET10.ViewModels;

namespace PasswordManager_.NET10.Tests.ViewModels;

public class RegisterViewModelTests
{
    private readonly FakeAuthService _authService = new();
    private readonly FakeNavigationService _navigationService = new();

    private RegisterViewModel CreateViewModel() => new(
        NullLogger<RegisterViewModel>.Instance,
        _authService,
        _navigationService);

    [Fact]
    public async Task RegisterAsync_WithPasswordShorterThanPolicy_DoesNotCallService()
    {
        var viewModel = CreateViewModel();
        viewModel.Email = "test@example.com";
        viewModel.Password = "Pass1";
        viewModel.ConfirmPassword = "Pass1";

        await viewModel.RegisterCommand.ExecuteAsync(null);

        // La política de la API exige 6 caracteres: el cliente no debe llamar al
        // servicio para inevitablemente recibir un 400.
        Assert.False(_authService.RegisterCalled);
        Assert.Equal("La contraseña debe tener al menos 6 caracteres", viewModel.Message);
    }

    [Fact]
    public async Task RegisterAsync_WithPasswordAtPolicyMinimum_CallsServiceAndClosesModal()
    {
        var viewModel = CreateViewModel();
        viewModel.Email = "test@example.com";
        viewModel.Password = "Pass123";
        viewModel.ConfirmPassword = "Pass123";

        await viewModel.RegisterCommand.ExecuteAsync(null);

        Assert.True(_authService.RegisterCalled);
        Assert.Equal(1, _navigationService.PopModalCount);
        // Los campos se limpian al terminar, incluso en el camino exitoso.
        Assert.Equal(string.Empty, viewModel.Email);
        Assert.Equal(string.Empty, viewModel.Password);
        Assert.Equal(string.Empty, viewModel.ConfirmPassword);
    }

    [Fact]
    public async Task RegisterAsync_WithMismatchedPasswords_DoesNotCallService()
    {
        var viewModel = CreateViewModel();
        viewModel.Email = "test@example.com";
        viewModel.Password = "Pass123";
        viewModel.ConfirmPassword = "Otra999";

        await viewModel.RegisterCommand.ExecuteAsync(null);

        Assert.False(_authService.RegisterCalled);
        Assert.Equal("Las contraseñas no coinciden", viewModel.Message);
    }
}
