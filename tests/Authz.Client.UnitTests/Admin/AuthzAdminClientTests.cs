using System.Net;
using Authz.Client.Admin;
using Authz.Client.Exceptions;
using Authz.Client.UnitTests.TestDoubles;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Authz.Client.UnitTests.Admin;

/// <summary>
/// Covers MS-AUTHZ-SPEC.md §12 step 8: IAuthzAdminClient's three administration operations against
/// ms-authz's real request/response shapes (RolesController, UsersController). No IMemoryCache
/// involved anywhere here — see IAuthzAdminClient's doc comment for why.
/// </summary>
public class AuthzAdminClientTests
{
    private static AuthzAdminClient CreateClient(StubHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://ms-authz.local/") };
        return new AuthzAdminClient(httpClient, NullLogger<AuthzAdminClient>.Instance);
    }

    [Fact]
    public async Task GetRoleCatalogAsync_ParsesRoleListFromRolesEndpoint()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK,
            """[{"code":"admin","name":"Administrador","description":"Acceso total","permissionCodes":["Iam.Read","Iam.ManageRoles"]}]""");
        var client = CreateClient(handler);

        // Act
        var roles = await client.GetRoleCatalogAsync();

        // Assert
        // BeEquivalentTo, not Be: AuthzRoleDto's record-generated Equals compares PermissionCodes by
        // reference (List<T> doesn't override Equals), so two structurally-identical lists never
        // satisfy record ==/Equals — this is a property of List<T>, not a bug in the DTO.
        roles.Should().ContainSingle();
        roles[0].Should().BeEquivalentTo(new AuthzRoleDto("admin", "Administrador", "Acceso total", ["Iam.Read", "Iam.ManageRoles"]));
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/roles");
    }

    [Fact]
    public async Task GetUserRolesAsync_SendsTenantAsQueryParameter_AndParsesAssignedRoles()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK,
            """[{"code":"vendedor","name":"Vendedor"}]""");
        var client = CreateClient(handler);

        // Act
        var roles = await client.GetUserRolesAsync("jurol", "8f3c1a94-abcd");

        // Assert
        roles.Should().ContainSingle().Which.Should().Be(new AuthzAssignedRoleDto("vendedor", "Vendedor"));
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/users/8f3c1a94-abcd/roles?tenant=jurol");
    }

    [Fact]
    public async Task ReplaceUserRolesAsync_SendsRoleCodesBody_AndReturnsUpdatedAssignment()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK,
            """[{"code":"admin","name":"Administrador"},{"code":"gerente","name":"Gerente"}]""");
        var client = CreateClient(handler);

        // Act
        var roles = await client.ReplaceUserRolesAsync("jurol", "8f3c1a94-abcd", ["admin", "gerente"]);

        // Assert — replace-set semantics live in ms-authz's UserRoleService; the client's job is just
        // to send the desired full set and hand back what ms-authz confirms.
        roles.Should().BeEquivalentTo(
        [
            new AuthzAssignedRoleDto("admin", "Administrador"),
            new AuthzAssignedRoleDto("gerente", "Gerente")
        ]);
        handler.LastRequest!.Method.Should().Be(HttpMethod.Put);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/users/8f3c1a94-abcd/roles?tenant=jurol");
        handler.LastRequestBody.Should().Be("""{"roleCodes":["admin","gerente"]}""");
    }

    [Fact]
    public async Task ReplaceUserRolesAsync_UnknownRoleCode_ThrowsAuthzInvalidRoleCodesExceptionWithProblemDetail()
    {
        // Arrange — ms-authz's InvalidCatalogRequestException surfaces as 400 with a ProblemDetails
        // body whose "detail" carries the message (MsAuthz.Api/Extensions/GlobalExceptionHandler.cs).
        var handler = new StubHttpMessageHandler(HttpStatusCode.BadRequest,
            """{"status":400,"title":"Invalid request","detail":"Unknown role code(s): bogus"}""");
        var client = CreateClient(handler);

        // Act
        var act = () => client.ReplaceUserRolesAsync("jurol", "8f3c1a94-abcd", ["bogus"]);

        // Assert
        (await act.Should().ThrowAsync<AuthzInvalidRoleCodesException>())
            .WithMessage("Unknown role code(s): bogus");
    }

    [Fact]
    public async Task ReplaceUserRolesAsync_ServerError_ThrowsWithoutSwallowing()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.InternalServerError, null);
        var client = CreateClient(handler);

        // Act
        var act = () => client.ReplaceUserRolesAsync("jurol", "8f3c1a94-abcd", ["admin"]);

        // Assert — anything other than the specific 400/InvalidCatalogRequestException case propagates
        // as a plain HttpRequestException, same as GetEffectivePermissionsAsync's EnsureSuccessStatusCode.
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task SyncCatalogAsync_PostsTenantCodes_AndParsesSyncedTenantList()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, """["jurol","otraempresa"]""");
        var client = CreateClient(handler);

        // Act
        var synced = await client.SyncCatalogAsync(["jurol", "otraempresa"]);

        // Assert
        synced.Should().BeEquivalentTo(["jurol", "otraempresa"]);
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/catalog/sync");
        handler.LastRequestBody.Should().Be("""{"tenantCodes":["jurol","otraempresa"]}""");
    }

    [Fact]
    public async Task SyncCatalogAsync_EmptyTenantList_ThrowsAuthzInvalidRequestExceptionWithProblemDetail()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.BadRequest,
            """{"status":400,"title":"Invalid request","detail":"'tenantCodes' is required."}""");
        var client = CreateClient(handler);

        // Act
        var act = () => client.SyncCatalogAsync([]);

        // Assert
        (await act.Should().ThrowAsync<AuthzInvalidRequestException>())
            .WithMessage("'tenantCodes' is required.");
    }
}
