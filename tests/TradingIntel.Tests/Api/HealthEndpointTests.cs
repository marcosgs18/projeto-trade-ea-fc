using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace TradingIntel.Tests.Api;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<global::Program>>
{
    private readonly WebApplicationFactory<global::Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<global::Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_returns_OK()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(new Uri("/health", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
