using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MsAuthz.Infrastructure.Settings;

namespace MsAuthz.Infrastructure.OpenFga;

/// <summary>
/// Resolves the OpenFGA store and authorization model before the API starts serving, so a fresh
/// deployment needs nothing beyond a reachable OpenFGA: the store is found by name or created, and
/// the model embedded in this assembly (generated from openfga/model.fga) is written when the store
/// has none. Retries while OpenFGA is still coming up; a failure after that aborts startup.
/// </summary>
public class OpenFgaBootstrapHostedService(
    IOpenFgaAdminApi adminApi,
    OpenFgaSettings settings,
    ILogger<OpenFgaBootstrapHostedService> logger) : IHostedService
{
    public const string ModelResourceName = "model.json";

    private const int MaxAttempts = 10;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await BootstrapAsync(cancellationToken);
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts && ex is not OperationCanceledException)
            {
                logger.LogWarning(ex,
                    "OpenFGA bootstrap attempt {Attempt}/{MaxAttempts} against {ApiUrl} failed; retrying in {Delay}s",
                    attempt, MaxAttempts, settings.ApiUrl, RetryDelay.TotalSeconds);
                await Task.Delay(RetryDelay, cancellationToken);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task BootstrapAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Bootstrapping OpenFGA with {@Request}",
            new { settings.ApiUrl, settings.StoreName, settings.StoreId, settings.AuthorizationModelId });

        var storeId = settings.StoreId ?? await ResolveStoreIdAsync(cancellationToken);
        var modelId = settings.AuthorizationModelId ?? await ResolveModelIdAsync(storeId, cancellationToken);

        adminApi.UseStore(storeId, modelId);

        logger.LogInformation(
            "OpenFGA ready: store {StoreId} ({StoreName}), authorization model {AuthorizationModelId}",
            storeId, settings.StoreName, modelId);
    }

    private async Task<string> ResolveStoreIdAsync(CancellationToken cancellationToken)
    {
        var existing = await adminApi.FindStoreIdsByNameAsync(settings.StoreName, cancellationToken);

        if (existing.Count > 1)
            logger.LogWarning(
                "Found {Count} OpenFGA stores named {StoreName}; using {StoreId}. Pin OpenFga:StoreId to silence this",
                existing.Count, settings.StoreName, existing[0]);

        if (existing.Count > 0)
            return existing[0];

        var created = await adminApi.CreateStoreAsync(settings.StoreName, cancellationToken);
        logger.LogInformation("Created OpenFGA store {StoreId} named {StoreName}", created, settings.StoreName);
        return created;
    }

    private async Task<string> ResolveModelIdAsync(string storeId, CancellationToken cancellationToken)
    {
        var latest = await adminApi.GetLatestAuthorizationModelIdAsync(storeId, cancellationToken);
        if (latest is not null)
            return latest;

        var written = await adminApi.WriteAuthorizationModelAsync(storeId, ReadEmbeddedModel(), cancellationToken);
        logger.LogInformation("Wrote the authorization model to store {StoreId}: {AuthorizationModelId}", storeId, written);
        return written;
    }

    public static string ReadEmbeddedModel()
    {
        using var stream = typeof(OpenFgaBootstrapHostedService).Assembly.GetManifestResourceStream(ModelResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ModelResourceName}' is missing from the assembly.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
