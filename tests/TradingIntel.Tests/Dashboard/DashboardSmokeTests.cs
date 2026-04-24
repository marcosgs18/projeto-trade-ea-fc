using System.Net;
using FluentAssertions;
using Xunit;

namespace TradingIntel.Tests.Dashboard;

/// <summary>
/// Keep these asserts as cheap as possible: Blazor Server pre-renders every
/// routed page on the initial GET before hydrating the circuit, so a 200 plus
/// a visible heading is enough to confirm DI + database bootstrap + routing.
/// Interactive behaviour (EditForm submits, OnClick) is intentionally out of
/// scope; it would require a WebSocket client and bUnit.
/// </summary>
public sealed class DashboardSmokeTests : IClassFixture<DashboardHostFactory>
{
    private readonly DashboardHostFactory _factory;

    public DashboardSmokeTests(DashboardHostFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/watchlist")]
    [InlineData("/opportunities")]
    public async Task Dashboard_routes_return_200_and_render_shell(string path)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("TradingIntel"); // sidebar brand proves the layout rendered
    }

    [Fact]
    public async Task Dashboard_unknown_route_returns_404()
    {
        // Blazor Web (MapRazorComponents) resolves unmatched routes at the
        // endpoint layer before the client router can render <NotFound>, so
        // an HTTP GET sees a plain 404. We still assert the shape to catch
        // future regressions (e.g. an accidental global catch-all route).
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/this-does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
