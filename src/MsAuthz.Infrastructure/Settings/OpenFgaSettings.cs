using System.ComponentModel.DataAnnotations;

namespace MsAuthz.Infrastructure.Settings;

/// <summary>
/// Points ms-authz at its OpenFGA instance. The store and the authorization model are resolved at
/// startup by <see cref="OpenFga.OpenFgaBootstrapHostedService"/>: the store is found by
/// <see cref="StoreName"/> or created, and the model is written if the store has none. Only
/// <see cref="ApiUrl"/> is required; <see cref="StoreId"/> and <see cref="AuthorizationModelId"/>
/// pin a specific store/model and skip the lookup. No credentials: this deployment's OpenFGA is
/// trusted only from inside the compose network, the same boundary ms-authz itself has.
/// </summary>
public class OpenFgaSettings : IValidatableObject
{
    public const string SectionName = "OpenFga";

    public string ApiUrl { get; set; } = string.Empty;
    public string StoreName { get; set; } = "ms-authz";
    public string? StoreId { get; set; }
    public string? AuthorizationModelId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrEmpty(ApiUrl))
            yield return new ValidationResult($"{SectionName}.{nameof(ApiUrl)} is required", [nameof(ApiUrl)]);

        if (string.IsNullOrWhiteSpace(StoreName) && string.IsNullOrWhiteSpace(StoreId))
            yield return new ValidationResult(
                $"{SectionName}.{nameof(StoreName)} or {SectionName}.{nameof(StoreId)} is required", [nameof(StoreName)]);
    }
}
