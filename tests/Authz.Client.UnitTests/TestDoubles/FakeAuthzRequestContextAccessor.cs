namespace Authz.Client.UnitTests.TestDoubles;

public class FakeAuthzRequestContextAccessor(string? tenantCode, string? subjectId) : IAuthzRequestContextAccessor
{
    public string? TenantCode { get; } = tenantCode;
    public string? SubjectId { get; } = subjectId;
}
