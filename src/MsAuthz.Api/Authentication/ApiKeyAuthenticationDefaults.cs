namespace MsAuthz.Api.Authentication;

public static class ApiKeyAuthenticationDefaults
{
    public const string SchemeName = "ApiKey";

    /// <summary>Header consumers must send. Same convention as ms-filestore's X-Api-Key.</summary>
    public const string HeaderName = "X-Api-Key";
}
