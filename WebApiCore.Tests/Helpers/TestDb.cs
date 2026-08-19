using WebApiCore.Infrastructure.Data;

namespace WebApiCore.Tests.Helpers;

public static class TestDb
{
    public const string DefaultConnectionString =
        "Server=localhost; Database=db_testing; User ID=testing; Password=testing; TrustServerCertificate=True;";

    public static string ConnectionString
        => Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING") ?? DefaultConnectionString;

    public static IDapperContext CreateContext()
        => new DapperContext(ConnectionString);
}