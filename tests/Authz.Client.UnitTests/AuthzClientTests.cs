using System.Net;
using Authz.Client.UnitTests.TestDoubles;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Authz.Client.UnitTests;

public class AuthzClientTests
{
    private static AuthzClient CreateClient(HttpMessageHandler handler, IMemoryCache cache)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://ms-authz.local/") };
        var options = Options.Create(new AuthzClientOptions { CacheTtl = TimeSpan.FromMinutes(5) });
        return new AuthzClient(httpClient, cache, options, NullLogger<AuthzClient>.Instance);
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_caches_the_result_for_the_same_tenant_and_subject()
    {
        var handler = new SequencedHttpMessageHandler((HttpStatusCode.OK, """["Sales.Read"]"""));
        var cache = new MemoryCache(new MemoryCacheOptions());
        var client = CreateClient(handler, cache);

        var first = await client.GetEffectivePermissionsAsync("jurol", "user-1");
        var second = await client.GetEffectivePermissionsAsync("jurol", "user-1");

        first.Should().BeEquivalentTo(["Sales.Read"]);
        second.Should().BeEquivalentTo(["Sales.Read"]);
        handler.RequestedUris.Should().ContainSingle();
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_does_not_collide_across_tenant_subject_pairs_that_would_share_a_naive_string_key()
    {
        var handler = new SequencedHttpMessageHandler(
            (HttpStatusCode.OK, """["Sales.Read"]"""),
            (HttpStatusCode.OK, """["Iam.Read"]"""));
        var cache = new MemoryCache(new MemoryCacheOptions());
        var client = CreateClient(handler, cache);

        var first = await client.GetEffectivePermissionsAsync("a", "b:c");
        var second = await client.GetEffectivePermissionsAsync("a:b", "c");

        first.Should().BeEquivalentTo(["Sales.Read"]);
        second.Should().BeEquivalentTo(["Iam.Read"]);
        handler.RequestedUris.Should().HaveCount(2);
    }

    [Fact]
    public async Task InvalidateAsync_forces_the_next_call_for_that_pair_to_re_hit_ms_authz()
    {
        var handler = new SequencedHttpMessageHandler(
            (HttpStatusCode.OK, """["Sales.Read"]"""),
            (HttpStatusCode.OK, """["Sales.Read","Sales.Write"]"""));
        var cache = new MemoryCache(new MemoryCacheOptions());
        var client = CreateClient(handler, cache);

        await client.GetEffectivePermissionsAsync("jurol", "user-1");
        await client.InvalidateAsync("jurol", "user-1");
        var afterInvalidation = await client.GetEffectivePermissionsAsync("jurol", "user-1");

        afterInvalidation.Should().BeEquivalentTo(["Sales.Read", "Sales.Write"]);
        handler.RequestedUris.Should().HaveCount(2);
    }

    [Fact]
    public async Task InvalidateAsync_does_not_evict_a_different_tenant_subject_pair()
    {
        var handler = new SequencedHttpMessageHandler(
            (HttpStatusCode.OK, """["Sales.Read"]"""),
            (HttpStatusCode.OK, """["Iam.Read"]"""));
        var cache = new MemoryCache(new MemoryCacheOptions());
        var client = CreateClient(handler, cache);

        await client.GetEffectivePermissionsAsync("jurol", "user-1");
        await client.GetEffectivePermissionsAsync("jurol", "user-2");

        await client.InvalidateAsync("jurol", "user-1");

        var stillCached = await client.GetEffectivePermissionsAsync("jurol", "user-2");

        stillCached.Should().BeEquivalentTo(["Iam.Read"]);
        handler.RequestedUris.Should().HaveCount(2);
    }
}
