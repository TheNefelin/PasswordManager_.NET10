using PasswordManager_.NET10.Services.Interfaces;

namespace PasswordManager_.NET10.Tests.Fakes;

public class FakeBiometricService : IBiometricService
{
    public bool Available { get; set; }
    public bool Enabled { get; set; }

    public Task<bool> IsBiometricAvailableAsync() => Task.FromResult(Available);

    public Task<string?> AuthenticateWithBiometricAsync() => Task.FromResult<string?>(null);

    public Task EnableBiometricAsync(bool isEnabled)
    {
        Enabled = isEnabled;
        return Task.CompletedTask;
    }

    public Task<bool> IsBiometricEnabledAsync() => Task.FromResult(Enabled);
}