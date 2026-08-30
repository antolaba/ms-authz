using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MsAuthz.Api.Authentication;

namespace MsAuthz.UnitTests.Api;

public class ApiKeyAuthenticationTests(MsAuthzApiFactory factory) : IClassFixture<MsAuthzApiFactory>
{
    [Fact]
    public async Task Health_endpoint_is_reachable_without_an_api_key()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_protected_endpoint_rejects_a_request_with_no_api_key_header()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/tenants", new { tenantCode = "jurol" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_protected_endpoint_rejects_a_request_with_the_wrong_api_key()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyAuthenticationDefaults.HeaderName, "wrong-key");

        var response = await client.PostAsJsonAsync("/tenants", new { tenantCode = "jurol" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_protected_endpoint_accepts_a_request_with_the_correct_api_key()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyAuthenticationDefaults.HeaderName, MsAuthzApiFactory.ApiKey);

        var response = await client.PostAsJsonAsync("/tenants", new { tenantCode = "jurol" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task An_invalid_tenant_code_is_mapped_to_400_by_the_global_exception_handler()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyAuthenticationDefaults.HeaderName, MsAuthzApiFactory.ApiKey);

        var response = await client.PostAsJsonAsync("/tenants", new { tenantCode = "acme|prod" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
