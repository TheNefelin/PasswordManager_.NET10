using Microsoft.AspNetCore.Mvc.Testing;

namespace WebApiCore.Tests.Http;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string ApiKey = "Testing-777";

    public HttpClient CreateClientWithApiKey(string ip)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("ApiKey", ApiKey);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
        return client;
    }

    public HttpClient CreateClientWithoutApiKey(string ip)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
        return client;
    }
}