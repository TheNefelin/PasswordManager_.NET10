using System.Net;
using Microsoft.AspNetCore.Http;
using WebApiCore.Helpers;

namespace WebApiCore.Tests.Security;

public class ClientIpResolverTests
{
    [Fact]
    public void Resolve_ReturnsRemoteIpAddress_IgnoringForgedForwardedFor()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.10.5");
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.7";

        Assert.Equal("192.168.10.5", ClientIpResolver.Resolve(context));
    }

    [Fact]
    public void Resolve_WithoutRemoteIpAddress_ReturnsUnknown()
    {
        var context = new DefaultHttpContext();

        Assert.Equal("unknown", ClientIpResolver.Resolve(context));
    }
}