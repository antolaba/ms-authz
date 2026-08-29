using System.ComponentModel.DataAnnotations;

namespace MsAuthz.Infrastructure.Settings;

/// <summary>
/// Connection settings for the `authz` catalog database (MS-AUTHZ-SPEC.md §6). Same
/// IValidatableObject shape as estudio-contable-backend's Domain/Configurations classes — ms-authz
/// has no Domain project, so this lives in Infrastructure, the only layer that consumes it.
/// </summary>
public class DatabaseSettings : IValidatableObject
{
    public const string SectionName = "Database";

    public string ConnectionString { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrEmpty(ConnectionString))
            yield return new ValidationResult(
                $"{SectionName}.{nameof(ConnectionString)} is required", [nameof(ConnectionString)]);
    }
}
