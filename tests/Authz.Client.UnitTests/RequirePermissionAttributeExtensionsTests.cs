using Authz.Client;
using Authz.Client.Extensions;
using FluentAssertions;

namespace Authz.Client.UnitTests;

public class RequirePermissionAttributeExtensionsTests
{
    [RequirePermission("Sales.Write")]
    private class SingleCodeRequest;

    [RequirePermission("Sales.Write", "Sales.Approve")]
    private class MultiCodeRequest;

    private class UndecoratedRequest;

    [Fact]
    public void RequiresPermission_is_false_for_a_type_without_the_attribute()
    {
        typeof(UndecoratedRequest).RequiresPermission().Should().BeFalse();
    }

    [Fact]
    public void RequiresPermission_is_true_for_a_type_with_the_attribute()
    {
        typeof(SingleCodeRequest).RequiresPermission().Should().BeTrue();
    }

    [Fact]
    public void GetRequiredPermissionCodes_returns_the_single_code()
    {
        typeof(SingleCodeRequest).GetRequiredPermissionCodes().Should().BeEquivalentTo(["Sales.Write"]);
    }

    [Fact]
    public void GetRequiredPermissionCodes_returns_all_codes_when_multiple_are_declared()
    {
        typeof(MultiCodeRequest).GetRequiredPermissionCodes().Should().BeEquivalentTo(["Sales.Write", "Sales.Approve"]);
    }

    [Fact]
    public void GetRequiredPermissionCodes_returns_empty_for_an_undecorated_type()
    {
        typeof(UndecoratedRequest).GetRequiredPermissionCodes().Should().BeEmpty();
    }
}
