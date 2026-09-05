using MsAuthz.Application.Interfaces;

namespace MsAuthz.UnitTests.TestDoubles;

/// <summary>
/// In-memory stand-in for OpenFGA. Mirrors the two behaviors that matter for the tests in this
/// project: WriteTuples/DeleteTuples are set operations (so re-writing the same tuple is a no-op,
/// exactly like the real gateway's OnDuplicateWrites.Ignore), and ListObjects/Read return whatever is
/// currently stored with no notion of tenant — exactly like OpenFGA, which only ever sees opaque ids.
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

    public Task<IReadOnlyList<string>> ListObjectsAsync(string userId, string relation, string objectType, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> result = _tuples
            .Where(t => t.Relation == relation && t.Object.StartsWith($"{objectType}:", StringComparison.Ordinal))
            .SelectMany(t => ResolveUsersetChain(userId, t))
            .Distinct()
            .ToList();

        return Task.FromResult(result);
    }

    /// <summary>Seeds a tuple directly, bypassing WriteTuplesAsync — used to set up fixture state.</summary>
    public void Seed(string user, string relation, string @object) => _tuples.Add((user, relation, @object));

    private IEnumerable<string> ResolveUsersetChain(string userId, (string User, string Relation, string Object) grantedTuple)
    {
        // grantedTuple.User is either a direct "user:..." or a userset reference "role:t|code#assignee".
        // Resolve one level: is `userId` an assignee (directly or transitively) of that role?
        if (grantedTuple.User == userId)
        {
            yield return grantedTuple.Object;
            yield break;
        }

        if (!grantedTuple.User.EndsWith("#assignee", StringComparison.Ordinal))
            yield break;

        var roleObject = grantedTuple.User[..^"#assignee".Length];
        if (IsAssignee(userId, roleObject, []))
            yield return grantedTuple.Object;
    }

    private bool IsAssignee(string userId, string roleObject, HashSet<string> visited)
    {
        if (!visited.Add(roleObject))
            return false; // cycle guard

        foreach (var t in _tuples.Where(t => t.Relation == "assignee" && t.Object == roleObject))
        {
            if (t.User == userId)
                return true;

            if (t.User.EndsWith("#assignee", StringComparison.Ordinal))
            {
                var nestedRole = t.User[..^"#assignee".Length];
                if (IsAssignee(userId, nestedRole, visited))
                    return true;
            }
        }

        return false;
    }
}
