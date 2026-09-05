using FluentAssertions;
using MsAuthz.Application.Common.Identifiers;

namespace MsAuthz.UnitTests.Identifiers;

public class OpenFgaIdentifiersTests
{
    [Fact]
    public void User_builds_the_expected_object_string_scoped_to_the_tenant()
    {
        OpenFgaIdentifiers.User("jurol", "8f3c1a94-abcd").Should().Be("user:jurol|8f3c1a94-abcd");
    }

    [Theory]
    [InlineData("8f3c#abcd")]
    [InlineData("8f3c|abcd")]
    [InlineData("")]
    public void User_rejects_a_subject_id_containing_a_reserved_character(string invalidSubject)
    {
        var act = () => OpenFgaIdentifiers.User("jurol", invalidSubject);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Role_builds_the_expected_object_string_using_pipe_as_tenant_separator()
    {
        OpenFgaIdentifiers.Role("jurol", "vendedor").Should().Be("role:jurol|vendedor");
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
    public void TryStripTenantRolePrefix_strips_the_code_when_the_object_belongs_to_the_given_tenant()
    {
        var found = OpenFgaIdentifiers.TryStripTenantRolePrefix("role:jurol|vendedor", "jurol", out var code);

        found.Should().BeTrue();
        code.Should().Be("vendedor");
    }

    [Fact]
    public void TryStripTenantRolePrefix_returns_false_for_a_user_object_even_if_the_tenant_matches()
    {
        OpenFgaIdentifiers.TryStripTenantRolePrefix("user:jurol|vendedor", "jurol", out _).Should().BeFalse();
    }

    [Fact]
    public void TryStripTenantRolePrefix_does_not_false_positive_on_a_tenant_code_that_is_a_prefix_of_another()
    {
        OpenFgaIdentifiers.TryStripTenantRolePrefix("role:jurol|vendedor", "jur", out _).Should().BeFalse();
    }

    [Fact]
    public void TryStripTenantRolePrefix_returns_false_for_a_different_tenant()
    {
        var found = OpenFgaIdentifiers.TryStripTenantRolePrefix("role:otraempresa|gerente", "jurol", out _);
        found.Should().BeFalse();
    }
}
