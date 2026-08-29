namespace MsAuthz.Application.Interfaces;

/// <summary>A single OpenFGA relationship tuple (user, relation, object) — see MS-AUTHZ-SPEC.md §4.</summary>
public sealed record OpenFgaTupleKey(string User, string Relation, string Object);

/// <summary>
/// The only thing in ms-authz allowed to talk to OpenFGA (MS-AUTHZ-SPEC.md §1: "ms-authz es el único
/// que le habla a OpenFGA"). Application-layer abstraction over the OpenFga.Sdk client; implemented in
/// MsAuthz.Infrastructure.
/// </summary>
public interface IOpenFgaGateway
{
    /// <summary>
    /// Writes tuples idempotently: a tuple that already exists is silently skipped, never an error.
    /// This is what makes tenant provisioning and catalog sync safe to re-run (MS-AUTHZ-SPEC.md §5).
    /// </summary>
    Task WriteTuplesAsync(IReadOnlyCollection<OpenFgaTupleKey> tuples, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes tuples idempotently: a tuple that is already gone is silently skipped, never an error.
    /// </summary>
    Task DeleteTuplesAsync(IReadOnlyCollection<OpenFgaTupleKey> tuples, CancellationToken cancellationToken = default);

    /// <summary>
    /// Partial read: every object of <paramref name="objectType"/> connected to <paramref name="user"/>
    /// via <paramref name="relation"/>, across every tenant. OpenFGA requires the object type on a
    /// partial read — a tuple key with only user and relation is rejected. Callers MUST filter the
    /// result by tenant prefix themselves
    /// (see <see cref="Common.Identifiers.OpenFgaIdentifiers"/>) — OpenFGA has no notion of tenant and
    /// will return matches across every tenant the user has a relation in, the same way ListObjects does.
    /// </summary>
    Task<IReadOnlyList<string>> ReadObjectsForUserAsync(string user, string relation, string objectType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Wraps OpenFGA's ListObjects. ⚠️ Verified empirically (MS-AUTHZ-SPEC.md §4): this returns objects
    /// across ALL tenants the subject has any role in, not just one. Callers MUST filter by tenant
    /// prefix — this method does not and cannot do it, since OpenFGA itself doesn't know what a
    /// tenant is.
    /// </summary>
    Task<IReadOnlyList<string>> ListObjectsAsync(string userId, string relation, string objectType, CancellationToken cancellationToken = default);
}
