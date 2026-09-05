namespace MsAuthz.Infrastructure.OpenFga;

/// <summary>
/// The store/model administration slice of the OpenFGA API that startup bootstrapping needs, kept
/// behind an interface so the bootstrap logic can be tested without an OpenFGA server.
/// </summary>
public interface IOpenFgaAdminApi
{
    Task<IReadOnlyList<string>> FindStoreIdsByNameAsync(string storeName, CancellationToken cancellationToken);

    Task<string> CreateStoreAsync(string storeName, CancellationToken cancellationToken);

    Task<string?> GetLatestAuthorizationModelIdAsync(string storeId, CancellationToken cancellationToken);

    Task<string> WriteAuthorizationModelAsync(string storeId, string modelJson, CancellationToken cancellationToken);

    void UseStore(string storeId, string authorizationModelId);
}
