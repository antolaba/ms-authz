namespace Authz.Client.Extensions;

/// <summary>
/// Copied in form from
/// EstudioContable.Application/Common/Extensions/IntrospectionAttributeExtensions.cs.
/// </summary>
public static class RequirePermissionAttributeExtensions
{
    public static bool RequiresPermission(this Type type)
    {
        return type.GetCustomAttributes(typeof(RequirePermissionAttribute), true).Any();
    }

    public static string[] GetRequiredPermissionCodes(this Type type)
    {
        var attribute = type.GetCustomAttributes(typeof(RequirePermissionAttribute), true)
            .Cast<RequirePermissionAttribute>()
            .FirstOrDefault();

        return attribute?.Codes ?? [];
    }
}
