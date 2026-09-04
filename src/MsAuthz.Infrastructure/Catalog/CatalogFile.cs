namespace MsAuthz.Infrastructure.Catalog;

/// <summary>Deserialization shape of the catalog JSON file's top level — see docs/catalog.example.json.</summary>
public sealed record CatalogFileDocument(List<CatalogFilePermission>? Permissions, List<CatalogFileRole>? Roles);

/// <summary>Deserialization shape of one entry in the catalog file's "permissions" array.</summary>
public sealed record CatalogFilePermission(string Code, string Module, string? Description);

/// <summary>
/// Deserialization shape of one entry in the catalog file's "roles" array. "Permissions" may be
/// exactly <c>["*"]</c>, meaning every permission in the catalog, instead of an explicit list.
/// </summary>
public sealed record CatalogFileRole(string Code, string Name, string? Description, List<string>? Permissions);
