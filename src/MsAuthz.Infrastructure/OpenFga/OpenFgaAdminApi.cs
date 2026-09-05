using OpenFga.Sdk.Client;
using OpenFga.Sdk.Client.Model;
using OpenFga.Sdk.Model;

namespace MsAuthz.Infrastructure.OpenFga;

public class OpenFgaAdminApi(OpenFgaClient client) : IOpenFgaAdminApi
{
    public async Task<IReadOnlyList<string>> FindStoreIdsByNameAsync(string storeName, CancellationToken cancellationToken)
    {
        var ids = new List<string>();
        string? continuationToken = null;

        do
        {
            var response = await client.ListStores(
                new ClientListStoresRequest { Name = storeName },
                new ClientListStoresOptions { ContinuationToken = continuationToken },
                cancellationToken);

            ids.AddRange((response.Stores ?? []).Where(s => s.Name == storeName).Select(s => s.Id));
            continuationToken = string.IsNullOrEmpty(response.ContinuationToken) ? null : response.ContinuationToken;
        } while (continuationToken is not null);

        return ids;
    }

    public async Task<string> CreateStoreAsync(string storeName, CancellationToken cancellationToken)
    {
        var response = await client.CreateStore(new ClientCreateStoreRequest { Name = storeName }, cancellationToken: cancellationToken);
        return response.Id;
    }

    public async Task<string?> GetLatestAuthorizationModelIdAsync(string storeId, CancellationToken cancellationToken)
    {
        var response = await client.ReadAuthorizationModels(
            new ClientReadAuthorizationModelsOptions { StoreId = storeId, PageSize = 1 }, cancellationToken);

        return response.AuthorizationModels?.FirstOrDefault()?.Id;
    }

    public async Task<string> WriteAuthorizationModelAsync(string storeId, string modelJson, CancellationToken cancellationToken)
    {
        var model = WriteAuthorizationModelRequest.FromJson(modelJson);
        var request = new ClientWriteAuthorizationModelRequest
        {
            SchemaVersion = model.SchemaVersion,
            TypeDefinitions = model.TypeDefinitions,
            Conditions = model.Conditions,
        };

        var response = await client.WriteAuthorizationModel(
            request, new ClientWriteOptions { StoreId = storeId }, cancellationToken);

        return response.AuthorizationModelId;
    }

    public void UseStore(string storeId, string authorizationModelId)
    {
        client.StoreId = storeId;
        client.AuthorizationModelId = authorizationModelId;
    }
}
