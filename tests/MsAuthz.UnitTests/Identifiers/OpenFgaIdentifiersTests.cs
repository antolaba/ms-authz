using FluentAssertions;
using MsAuthz.Application.Common.Identifiers;

namespace MsAuthz.UnitTests.Identifiers;

public class OpenFgaIdentifiersTests
{
    [Fact]
    public void User_builds_the_expected_object_string()
    {
        OpenFgaIdentifiers.User("8f3c1a94-abcd").Should().Be("user:8f3c1a94-abcd");
    }

    [Fact]
    public void Role_builds_the_expected_object_string_using_pipe_as_tenant_separator()
    {
        OpenFgaIdentifiers.Role("jurol", "vendedor").Should().Be("role:jurol|vendedor");
    }

    [Fact]
    public void Permission_builds_the_expected_object_string_using_pipe_as_tenant_separator()
    {
        OpenFgaIdentifiers.Permission("jurol", "Sales.Write").Should().Be("permission:jurol|Sales.Write");
    }

    [Fact]
    public void RoleAssigneeUserset_appends_the_assignee_relation()
    {
        OpenFgaIdentifiers.RoleAssigneeUserset("jurol", "vendedor").Should().Be("role:jurol|vendedor#assignee");
    }

    [Theory]
    [InlineData("jur#ol")]
    [InlineData("jur:ol")]
    [InlineData("jur|ol")]
    [InlineData("jur ol")]
    [InlineData("")]
    [InlineData(" ")]
    public void Role_rejects_a_tenant_code_containing_a_reserved_character_or_whitespace(string invalidTenant)
    {
        var act = () => OpenFgaIdentifiers.Role(invalidTenant, "vendedor");
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("vende#dor")]
    [InlineData("vende:dor")]
    [InlineData("vende|dor")]
    public void Role_rejects_a_role_code_containing_a_reserved_character(string invalidRoleCode)
    {
        var act = () => OpenFgaIdentifiers.Role("jurol", invalidRoleCode);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TryStripTenantPrefix_strips_the_code_when_the_object_belongs_to_the_given_tenant()
    {
        var found = OpenFgaIdentifiers.TryStripTenantPrefix("permission:jurol|Sales.Write", "jurol", out var code);

        found.Should().BeTrue();
        code.Should().Be("Sales.Write");
    }

    [Fact]
    public void TryStripTenantPrefix_returns_false_for_a_different_tenant_prefix()
    {
        // This is the critical cross-tenant-leak guard from MS-AUTHZ-SPEC.md §4: ListObjects returns
        // objects for every tenant the subject has a role in, and this must reject anything that
        // doesn't belong to the tenant being asked about.
        var found = OpenFgaIdentifiers.TryStripTenantPrefix("permission:otraempresa|Purchasing.ApproveOrder", "jurol", out var code);

        found.Should().BeFalse();
        code.Should().BeEmpty();
    }

    [Fact]
    public void TryStripTenantPrefix_returns_false_for_a_role_object_even_if_the_tenant_matches()
    {
        var found = OpenFgaIdentifiers.TryStripTenantPrefix("role:jurol|vendedor", "jurol", out _);
        found.Should().BeFalse();
    }

    [Fact]
    public void TryStripTenantPrefix_does_not_false_positive_on_a_tenant_code_that_is_a_prefix_of_another()
    {
        // "jur" must not match objects that belong to tenant "jurol" just because the string starts
        // the same way — the '|' separator has to land exactly after the tenant code.
        var found = OpenFgaIdentifiers.TryStripTenantPrefix("permission:jurol|Sales.Write", "jur", out _);
        found.Should().BeFalse();
    }

    [Fact]
    public void TryStripTenantRolePrefix_strips_the_code_when_the_object_belongs_to_the_given_tenant()
    {
        var found = OpenFgaIdentifiers.TryStripTenantRolePrefix("role:jurol|vendedor", "jurol", out var code);

        found.Should().BeTrue();
        code.Should().Be("vendedor");
    }

    [Fact]
    public void TryStripTenantRolePrefix_returns_false_for_a_different_tenant()
    {
        var found = OpenFgaIdentifiers.TryStripTenantRolePrefix("role:otraempresa|gerente", "jurol", out _);
        found.Should().BeFalse();
    }
}
