using MsAuthz.Infrastructure.OpenFga;

namespace MsAuthz.UnitTests.TestDoubles;

public class FakeOpenFgaAdminApi : IOpenFgaAdminApi
{
    private readonly Dictionary<string, List<string>> _storesByName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _modelsByStore = new(StringComparer.Ordinal);
    private int _nextId;

    public string? ActiveStoreId { get; private set; }
    public string? ActiveModelId { get; private set; }
    public int CreateStoreCallCount { get; private set; }
    public int WriteModelCallCount { get; private set; }
    public string? LastWrittenModelJson { get; private set; }

    public FakeOpenFgaAdminApi WithStore(string name, string id, params string[] modelIds)
    {
        _storesByName.TryAdd(name, []);
        _storesByName[name].Add(id);
        _modelsByStore[id] = modelIds.ToList();
        return this;
    }

    public Task<IReadOnlyList<string>> FindStoreIdsByNameAsync(string storeName, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<string>>(_storesByName.TryGetValue(storeName, out var ids) ? ids.ToList() : []);

    public Task<string> CreateStoreAsync(string storeName, CancellationToken cancellationToken)
    {
        CreateStoreCallCount++;
        var id = $"store-{++_nextId}";
        WithStore(storeName, id);
        return Task.FromResult(id);
    }

    public Task<string?> GetLatestAuthorizationModelIdAsync(string storeId, CancellationToken cancellationToken)
        => Task.FromResult(_modelsByStore.TryGetValue(storeId, out var models) ? models.LastOrDefault() : null);

    public Task<string> WriteAuthorizationModelAsync(string storeId, string modelJson, CancellationToken cancellationToken)
    {
        WriteModelCallCount++;
        LastWrittenModelJson = modelJson;
        var id = $"model-{++_nextId}";
        _modelsByStore[storeId].Add(id);
        return Task.FromResult(id);
    }

    public void UseStore(string storeId, string authorizationModelId)
    {
        ActiveStoreId = storeId;
        ActiveModelId = authorizationModelId;
    }
}
