using Authz.Client.Exceptions;
using Authz.Client.UnitTests.TestDoubles;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Authz.Client.UnitTests;

public class PermissionAuthorizationBehaviorTests
{
    [RequirePermission("Sales.Write")]
    private class ProtectedRequest;

    private class UnprotectedRequest;

    [RequirePermission("Sales.Write", "Sales.Approve")]
    private class MultiPermissionRequest;

    private static Task<string> Next(CancellationToken _) => Task.FromResult("handled");

    [Fact]
    public async Task Handle_passes_through_when_the_request_has_no_attribute()
    {
        var authzClient = new FakeAuthzClient(); // no permissions granted at all
        var accessor = new FakeAuthzRequestContextAccessor(tenantCode: null, subjectId: null);
        var sut = new PermissionAuthorizationBehavior<UnprotectedRequest, string>(
            authzClient, accessor, NullLogger<PermissionAuthorizationBehavior<UnprotectedRequest, string>>.Instance);

        var result = await sut.Handle(new UnprotectedRequest(), Next, CancellationToken.None);

        result.Should().Be("handled");
        authzClient.CallCount.Should().Be(0, "an unprotected request must never call ms-authz");
    }

    [Fact]
    public async Task Handle_passes_through_when_the_subject_has_the_required_permission()
    {
        var authzClient = new FakeAuthzClient("Sales.Write");
        var accessor = new FakeAuthzRequestContextAccessor("jurol", "user-1");
        var sut = new PermissionAuthorizationBehavior<ProtectedRequest, string>(
            authzClient, accessor, NullLogger<PermissionAuthorizationBehavior<ProtectedRequest, string>>.Instance);

        var result = await sut.Handle(new ProtectedRequest(), Next, CancellationToken.None);

        result.Should().Be("handled");
    }

    [Fact]
    public async Task Handle_throws_when_the_subject_is_missing_the_required_permission()
    {
        var authzClient = new FakeAuthzClient("Sales.Read"); // has a different permission, not Sales.Write
        var accessor = new FakeAuthzRequestContextAccessor("jurol", "user-1");
        var sut = new PermissionAuthorizationBehavior<ProtectedRequest, string>(
            authzClient, accessor, NullLogger<PermissionAuthorizationBehavior<ProtectedRequest, string>>.Instance);

        var act = () => sut.Handle(new ProtectedRequest(), Next, CancellationToken.None);

        await act.Should().ThrowAsync<AuthzForbiddenAccessException>();
    }

    [Fact]
    public async Task Handle_requires_all_codes_when_multiple_are_declared()
    {
        var authzClient = new FakeAuthzClient("Sales.Write"); // missing Sales.Approve
        var accessor = new FakeAuthzRequestContextAccessor("jurol", "user-1");
        var sut = new PermissionAuthorizationBehavior<MultiPermissionRequest, string>(
            authzClient, accessor, NullLogger<PermissionAuthorizationBehavior<MultiPermissionRequest, string>>.Instance);

        var act = () => sut.Handle(new MultiPermissionRequest(), Next, CancellationToken.None);

        await act.Should().ThrowAsync<AuthzForbiddenAccessException>();
    }

    [Fact]
    public async Task Handle_passes_through_when_all_declared_codes_are_present()
    {
        var authzClient = new FakeAuthzClient("Sales.Write", "Sales.Approve");
        var accessor = new FakeAuthzRequestContextAccessor("jurol", "user-1");
        var sut = new PermissionAuthorizationBehavior<MultiPermissionRequest, string>(
            authzClient, accessor, NullLogger<PermissionAuthorizationBehavior<MultiPermissionRequest, string>>.Instance);

        var result = await sut.Handle(new MultiPermissionRequest(), Next, CancellationToken.None);

        result.Should().Be("handled");
    }

    [Fact]
    public async Task Handle_throws_when_there_is_no_tenant_subject_context_at_all()
    {
        var authzClient = new FakeAuthzClient("Sales.Write");
        var accessor = new FakeAuthzRequestContextAccessor(tenantCode: null, subjectId: null);
        var sut = new PermissionAuthorizationBehavior<ProtectedRequest, string>(
            authzClient, accessor, NullLogger<PermissionAuthorizationBehavior<ProtectedRequest, string>>.Instance);

        var act = () => sut.Handle(new ProtectedRequest(), Next, CancellationToken.None);

        await act.Should().ThrowAsync<AuthzForbiddenAccessException>();
        authzClient.CallCount.Should().Be(0, "must fail fast without calling ms-authz when there's no context to evaluate");
    }
}
