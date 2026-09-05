using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MsAuthz.Application.Interfaces;
using MsAuthz.Infrastructure.OpenFga;
using MsAuthz.UnitTests.TestDoubles;

namespace MsAuthz.UnitTests.Api;

public class MsAuthzApiFactory : WebApplicationFactory<Program>
{
    public const string ApiKey = "test-api-key";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:ApiKey"] = ApiKey,
                ["Catalog:Path"] = "unused.json",
                ["OpenFga:ApiUrl"] = "http://localhost",
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IOpenFgaGateway>();
            services.AddSingleton<IOpenFgaGateway>(new FakeOpenFgaGateway());

            services.RemoveAll<IOpenFgaAdminApi>();
            services.AddSingleton<IOpenFgaAdminApi>(new FakeOpenFgaAdminApi());

            services.RemoveAll<ICatalogRepository>();
            services.AddSingleton<ICatalogRepository>(new FakeCatalogRepository());
        });
    }
}
