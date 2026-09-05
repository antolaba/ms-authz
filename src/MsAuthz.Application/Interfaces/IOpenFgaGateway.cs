namespace MsAuthz.Application.Interfaces;

/// <summary>A single OpenFGA relationship tuple (user, relation, object).</summary>
public sealed record OpenFgaTupleKey(string User, string Relation, string Object);

/// <summary>
/// The only thing in ms-authz allowed to talk to OpenFGA for tuples. OpenFGA holds one kind of tuple:
/// a subject's role assignment in a tenant. Application-layer abstraction over the OpenFga.Sdk
/// client; implemented in MsAuthz.Infrastructure.
/// </summary>
public interface IOpenFgaGateway
{
    /// <summary>Writes tuples idempotently: a tuple that already exists is silently skipped, never an error.</summary>
    Task WriteTuplesAsync(IReadOnlyCollection<OpenFgaTupleKey> tuples, CancellationToken cancellationToken = default);

    /// <summary>Deletes tuples idempotently: a tuple that is already gone is silently skipped, never an error.</summary>
    Task DeleteTuplesAsync(IReadOnlyCollection<OpenFgaTupleKey> tuples, CancellationToken cancellationToken = default);

    /// <summary>
    /// Partial read: every object of <paramref name="objectType"/> connected to <paramref name="user"/>
    /// via <paramref name="relation"/>, following pagination to the end. OpenFGA requires the object
    /// type on a partial read. OpenFGA has no notion of tenant: callers still filter the result by
    /// tenant prefix (see <see cref="Common.Identifiers.OpenFgaIdentifiers"/>).
    /// </summary>
    Task<IReadOnlyList<string>> ReadObjectsForUserAsync(string user, string relation, string objectType, CancellationToken cancellationToken = default);
}
