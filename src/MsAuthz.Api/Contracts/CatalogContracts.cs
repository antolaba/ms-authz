namespace MsAuthz.Api.Contracts;

/// <summary>POST /catalog/sync body.</summary>
public class SyncCatalogRequest
{
    public List<string> TenantCodes { get; set; } = [];
}
