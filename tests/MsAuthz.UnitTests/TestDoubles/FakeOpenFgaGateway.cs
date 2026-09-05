using MsAuthz.Application.Interfaces;

namespace MsAuthz.UnitTests.TestDoubles;

/// <summary>
/// In-memory stand-in for OpenFGA. Mirrors the two behaviors that matter for the tests in this
/// project: WriteTuples/DeleteTuples are set operations (so re-writing the same tuple is a no-op,
/// exactly like the real gateway's OnDuplicateWrites.Ignore), and Read returns whatever is currently
/// stored with no notion of tenant — exactly like OpenFGA, which only ever sees opaque ids.
/// </summary>
public class FakeOpenFgaGateway : IOpenFgaGateway
{
    private readonly HashSet<(string User, string Relation, string Object)> _tuples = [];

    public int WriteCallCount { get; private set; }
    public int DeleteCallCount { get; private set; }
    public IReadOnlyCollection<(string User, string Relation, string Object)> Tuples => _tuples;

    public Task WriteTuplesAsync(IReadOnlyCollection<OpenFgaTupleKey> tuples, CancellationToken cancellationToken = default)
    {
        WriteCallCount++;
        foreach (var t in tuples)
            _tuples.Add((t.User, t.Relation, t.Object)); // HashSet.Add is idempotent — same as OnDuplicateWrites.Ignore

        return Task.CompletedTask;
    }

    public Task DeleteTuplesAsync(IReadOnlyCollection<OpenFgaTupleKey> tuples, CancellationToken cancellationToken = default)
    {
        DeleteCallCount++;
        foreach (var t in tuples)
            _tuples.Remove((t.User, t.Relation, t.Object));

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> ReadObjectsForUserAsync(string user, string relation, string objectType, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> result = _tuples
            .Where(t => t.User == user && t.Relation == relation && t.Object.StartsWith($"{objectType}:", StringComparison.Ordinal))
            .Select(t => t.Object)
            .ToList();

        return Task.FromResult(result);
    }

    /// <summary>Seeds a tuple directly, bypassing WriteTuplesAsync — used to set up fixture state.</summary>
    public void Seed(string user, string relation, string @object) => _tuples.Add((user, relation, @object));
}
