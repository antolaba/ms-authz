using FluentAssertions;
using MsAuthz.Infrastructure.OpenFga;
using MsAuthz.Infrastructure.Settings;
using MsAuthz.UnitTests.TestDoubles;
using OpenFga.Sdk.Model;

namespace MsAuthz.UnitTests.OpenFga;

public class OpenFgaBootstrapHostedServiceTests
{
    private static OpenFgaBootstrapHostedService CreateSut(FakeOpenFgaAdminApi adminApi, OpenFgaSettings? settings = null)
        => new(adminApi, settings ?? new OpenFgaSettings { ApiUrl = "http://openfga" }, TestLogger.For<OpenFgaBootstrapHostedService>());

    [Fact]
    public async Task StartAsync_creates_the_store_and_writes_the_model_on_a_fresh_server()
    {
        var adminApi = new FakeOpenFgaAdminApi();

        await CreateSut(adminApi).StartAsync(CancellationToken.None);

        adminApi.CreateStoreCallCount.Should().Be(1);
        adminApi.WriteModelCallCount.Should().Be(1);
        adminApi.ActiveStoreId.Should().Be("store-1");
        adminApi.ActiveModelId.Should().Be("model-2");
    }

    [Fact]
    public async Task StartAsync_reuses_an_existing_store_and_its_latest_model()
    {
        var adminApi = new FakeOpenFgaAdminApi().WithStore("ms-authz", "existing-store", "old-model", "latest-model");

        await CreateSut(adminApi).StartAsync(CancellationToken.None);

        adminApi.CreateStoreCallCount.Should().Be(0);
        adminApi.WriteModelCallCount.Should().Be(0);
        adminApi.ActiveStoreId.Should().Be("existing-store");
        adminApi.ActiveModelId.Should().Be("latest-model");
    }

    [Fact]
    public async Task StartAsync_writes_the_model_into_an_existing_store_that_has_none()
    {
        var adminApi = new FakeOpenFgaAdminApi().WithStore("ms-authz", "empty-store");

        await CreateSut(adminApi).StartAsync(CancellationToken.None);

        adminApi.WriteModelCallCount.Should().Be(1);
        adminApi.ActiveStoreId.Should().Be("empty-store");
    }

    [Fact]
    public async Task StartAsync_looks_up_the_store_by_the_configured_name()
    {
        var adminApi = new FakeOpenFgaAdminApi()
            .WithStore("ms-authz", "default-store", "m1")
            .WithStore("other-system", "other-store", "m2");
        var settings = new OpenFgaSettings { ApiUrl = "http://openfga", StoreName = "other-system" };

        await CreateSut(adminApi, settings).StartAsync(CancellationToken.None);

        adminApi.ActiveStoreId.Should().Be("other-store");
    }

    [Fact]
    public async Task StartAsync_honours_a_pinned_store_and_model_without_touching_the_server()
    {
        var adminApi = new FakeOpenFgaAdminApi();
        var settings = new OpenFgaSettings
        {
            ApiUrl = "http://openfga", StoreId = "pinned-store", AuthorizationModelId = "pinned-model",
        };

        await CreateSut(adminApi, settings).StartAsync(CancellationToken.None);

        adminApi.CreateStoreCallCount.Should().Be(0);
        adminApi.WriteModelCallCount.Should().Be(0);
        adminApi.ActiveStoreId.Should().Be("pinned-store");
        adminApi.ActiveModelId.Should().Be("pinned-model");
    }

    [Fact]
    public async Task StartAsync_writes_the_embedded_model_generated_from_the_dsl()
    {
        var adminApi = new FakeOpenFgaAdminApi();

        await CreateSut(adminApi).StartAsync(CancellationToken.None);

        var model = WriteAuthorizationModelRequest.FromJson(adminApi.LastWrittenModelJson!);
        model.SchemaVersion.Should().Be("1.1");
        model.TypeDefinitions.Select(t => t.Type).Should().BeEquivalentTo(["user", "role", "permission"]);
        model.TypeDefinitions.Single(t => t.Type == "role").Relations.Should().ContainKey("assignee");
        model.TypeDefinitions.Single(t => t.Type == "permission").Relations.Should().ContainKey("granted");
    }
}
