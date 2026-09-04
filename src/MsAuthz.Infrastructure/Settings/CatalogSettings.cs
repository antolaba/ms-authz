using System.ComponentModel.DataAnnotations;

namespace MsAuthz.Infrastructure.Settings;

/// <summary>
/// Points ms-authz at its catalog file (MS-AUTHZ-SPEC.md §5). ms-authz has no database of its own —
/// this is the only source of the role/permission catalog, read once at startup.
/// </summary>
public class CatalogSettings : IValidatableObject
{
    public const string SectionName = "Catalog";

    /// <summary>
    /// Path to the catalog JSON file. Relative paths are resolved against the process's current
    /// directory (the content root, both under `dotnet run` and inside the container).
    /// </summary>
    public string Path { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrEmpty(Path))
            yield return new ValidationResult($"{SectionName}.{nameof(Path)} is required", [nameof(Path)]);
    }
}
