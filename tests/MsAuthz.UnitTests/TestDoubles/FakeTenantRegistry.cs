using MsAuthz.Application.Interfaces;

namespace MsAuthz.UnitTests.TestDoubles;

public class FakeTenantRegistry : ITenantRegistry
{
    private readonly List<string> _tenantCodes = [];

    public int RegisterCallCount { get; private set; }

    public Task RegisterAsync(string tenantCode, CancellationToken cancellationToken = default)
    {
        RegisterCallCount++;
        if (!_tenantCodes.Contains(tenantCode, StringComparer.Ordinal))
            _tenantCodes.Add(tenantCode);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetAllTenantCodesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>(_tenantCodes.ToList());
}
