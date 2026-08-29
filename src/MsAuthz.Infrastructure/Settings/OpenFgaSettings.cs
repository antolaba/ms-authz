using System.ComponentModel.DataAnnotations;

namespace MsAuthz.Infrastructure.Settings;

/// <summary>
/// Points ms-authz at its OpenFGA instance and store (MS-AUTHZ-SPEC.md §6). No credentials: this
/// deployment's OpenFGA has no authn configured (`docker-compose.yml`'s `openfga` service), trusted
/// only from inside the compose network — the same trust boundary ms-authz itself has (§7).
/// </summary>
public class OpenFgaSettings : IValidatableObject
{
    public const string SectionName = "OpenFga";

    public string ApiUrl { get; set; } = string.Empty;
    public string StoreId { get; set; } = string.Empty;

    /// <summary>Optional — omit to use the store's latest authorization model.</summary>
    public string? AuthorizationModelId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrEmpty(ApiUrl))
            yield return new ValidationResult($"{SectionName}.{nameof(ApiUrl)} is required", [nameof(ApiUrl)]);

        if (string.IsNullOrEmpty(StoreId))
            yield return new ValidationResult($"{SectionName}.{nameof(StoreId)} is required", [nameof(StoreId)]);
    }
}
