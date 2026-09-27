namespace PasswordManager_.NET10.Services.Interfaces;

public interface IAppInfoService
{
    string Version { get; }
    string Build { get; }
}
