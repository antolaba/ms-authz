using System.Diagnostics.CodeAnalysis;

namespace Authz.Client;

/// <summary>
/// Marks a MediatR request as requiring one or more ms-authz permission codes. Copied in form from
/// EstudioContable.Application/Common/Attributes/RequireIntrospectionAttribute.cs
/// (MS-AUTHZ-SPEC.md §8, §9). ALL listed codes are required — this is an AND, not an OR: a request
/// needing two distinct capabilities lists both.
///
/// <code>
/// [RequirePermission("Sales.Write")]
/// public class CreateInvoiceCommandRequest : IRequest&lt;CreateInvoiceCommandResponse&gt; { }
/// </code>
///
/// A request with no attribute is unaffected by <see cref="PermissionAuthorizationBehavior{TRequest,TResponse}"/> —
/// this is what makes the rollout incremental (MS-AUTHZ-SPEC.md §9: "no hace falta anotar los 200+
/// commands/queries de una vez").
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class RequirePermissionAttribute : Attribute
{
    // [SetsRequiredMembers]: without it, `[RequirePermission("Sales.Write")]` (attribute-constructor
    // syntax, not an object initializer) fails to compile — the compiler cannot otherwise verify the
    // `required` property below is set. RequireIntrospectionAttribute's identical shape has never
    // actually been applied anywhere in estudio-contable-backend (verified by grep), so this gap
    // never surfaced there.
    [SetsRequiredMembers]
    public RequirePermissionAttribute(params string[] codes)
    {
        Codes = codes;
    }

    public required string[] Codes { get; set; }
}
