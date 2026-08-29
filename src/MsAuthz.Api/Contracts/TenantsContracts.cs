namespace MsAuthz.Api.Contracts;

/// <summary>POST /tenants body.</summary>
public class ProvisionTenantRequest
{
    public string TenantCode { get; set; } = string.Empty;
}
