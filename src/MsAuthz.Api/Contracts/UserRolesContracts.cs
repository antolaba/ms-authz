namespace MsAuthz.Api.Contracts;

/// <summary>PUT /users/{id}/roles body.</summary>
public class SetUserRolesRequest
{
    public List<string> RoleCodes { get; set; } = [];
}
