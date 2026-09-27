using PasswordManager_.NET10.Services.Interfaces;

namespace PasswordManager_.NET10.Tests.Fakes;

public class FakeAppInfoService : IAppInfoService
{
    public string Version { get; set; } = "1.0.0";

    public string Build { get; set; } = "1";
}
