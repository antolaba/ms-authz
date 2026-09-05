using Microsoft.Extensions.Logging;
using MsAuthz.Application.Interfaces;
using OpenFga.Sdk.Client;
using OpenFga.Sdk.Client.Model;

namespace MsAuthz.Infrastructure.OpenFga;

/// <summary>
/// The only class in ms-authz that reads or writes OpenFGA tuples. Everything else goes through
/// <see cref="IOpenFgaGateway"/>.
/// </summary>
public class OpenFgaGateway(OpenFgaClient client, ILogger<OpenFgaGateway> logger) : IOpenFgaGateway
{
    /// <summary>
    /// OpenFGA rejects a Write with more than 100 operations in a single transaction, so writes are
    /// chunked. Kept below the limit rather than at it, since the cap is a server-side setting.
    /// </summary>
    private const int MaxOperationsPerWrite = 50;

    public async Task WriteTuplesAsync(IReadOnlyCollection<OpenFgaTupleKey> tuples, CancellationToken cancellationToken = default)
    {
        if (tuples.Count == 0)
            return;

        // OnDuplicateWrites.Ignore is what makes this idempotent: writing a tuple that already
        // exists is a silent no-op instead of an error. That idempotency is also what makes chunking
        // safe: a batch that fails half-way can simply be re-run.
        var options = new ClientWriteOptions
        {
            Conflict = new ConflictOptions { OnDuplicateWrites = OnDuplicateWrites.Ignore },
        };

        foreach (var batch in tuples.Chunk(MaxOperationsPerWrite))
        {
            var request = new ClientWriteRequest
            {
                Writes = batch
                    .Select(t => new ClientTupleKey { User = t.User, Relation = t.Relation, Object = t.Object })
                    .ToList(),
            };

            await client.Write(request, options, cancellationToken);
        }

        logger.LogDebug("Wrote {TupleCount} OpenFGA tuple(s) in batches of {BatchSize} (duplicates ignored)",
            tuples.Count, MaxOperationsPerWrite);
    }

    public async Task DeleteTuplesAsync(IReadOnlyCollection<OpenFgaTupleKey> tuples, CancellationToken cancellationToken = default)
    {
        if (tuples.Count == 0)
            return;

        // OnMissingDeletes.Ignore mirrors the write side: deleting a tuple that is already gone is a
        // silent no-op instead of an error — and, as above, is what makes chunking safe to re-run.
        var options = new ClientWriteOptions
        {
            Conflict = new ConflictOptions { OnMissingDeletes = OnMissingDeletes.Ignore },
        };

        foreach (var batch in tuples.Chunk(MaxOperationsPerWrite))
        {
            var request = new ClientWriteRequest
            {
                Deletes = batch
                    .Select(t => new ClientTupleKeyWithoutCondition { User = t.User, Relation = t.Relation, Object = t.Object })
                    .ToList(),
            };

            await client.Write(request, options, cancellationToken);
        }

        logger.LogDebug("Deleted {TupleCount} OpenFGA tuple(s) in batches of {BatchSize} (missing ones ignored)",
            tuples.Count, MaxOperationsPerWrite);
    }

    public async Task<IReadOnlyList<string>> ReadObjectsForUserAsync(
        string user, string relation, string objectType, CancellationToken cancellationToken = default)
    {
        var objects = new List<string>();
        string? continuationToken = null;

        do
        {
            // Object must carry at least the type. OpenFGA rejects a Read whose tuple_key has a user
            // and relation but no object with "the 'tuple_key' field was provided but the object type
            // field is required and both the object id and user cannot be empty". The convention for
            // "every object of this type" is the type followed by a colon and an empty id.
            var request = new ClientReadRequest { User = user, Relation = relation, Object = $"{objectType}:" };
            var options = new ClientReadOptions { ContinuationToken = continuationToken };

            var response = await client.Read(request, options, cancellationToken);

            objects.AddRange((response.Tuples ?? []).Select(t => t.Key.Object));
            continuationToken = string.IsNullOrEmpty(response.ContinuationToken) ? null : response.ContinuationToken;
        } while (continuationToken is not null);

        return objects;
    }
}
