using Microsoft.Extensions.DependencyInjection;
using MsAuthz.Application.Services;

namespace MsAuthz.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IEffectivePermissionsService, EffectivePermissionsService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<IUserRoleService, UserRoleService>();

        return services;
    }
}
