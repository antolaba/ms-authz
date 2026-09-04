using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MsAuthz.Api.Authentication;

namespace MsAuthz.UnitTests.Api;

public class CatalogControllerTests(MsAuthzApiFactory factory) : IClassFixture<MsAuthzApiFactory>
{
    [Fact]
    public async Task Sync_with_an_empty_tenant_list_returns_400()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyAuthenticationDefaults.HeaderName, MsAuthzApiFactory.ApiKey);

        var response = await client.PostAsJsonAsync("/catalog/sync", new { tenantCodes = Array.Empty<string>() });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
