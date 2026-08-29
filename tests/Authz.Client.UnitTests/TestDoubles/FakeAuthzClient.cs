namespace Authz.Client.UnitTests.TestDoubles;

public class FakeAuthzClient : IAuthzClient
{
    private readonly HashSet<string> _permissions;

    public FakeAuthzClient(params string[] permissions) => _permissions = [..permissions];

    public int CallCount { get; private set; }

    public Task<IReadOnlyCollection<string>> GetEffectivePermissionsAsync(
        string tenantCode, string subjectId, CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult<IReadOnlyCollection<string>>(_permissions);
    }
}
