using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TradingIntel.Tests.Api;

/// <summary>
/// Mesmo host de teste com ambiente Development para expor Swagger.
/// </summary>
public sealed class TradingIntelSwaggerApiFactory : TradingIntelApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseEnvironment("Development");
    }
}
