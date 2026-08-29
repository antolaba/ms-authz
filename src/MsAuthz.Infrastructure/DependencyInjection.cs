using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MsAuthz.Application.Interfaces;
using MsAuthz.Infrastructure.OpenFga;
using MsAuthz.Infrastructure.Persistence;
using MsAuthz.Infrastructure.Persistence.Repositories;
using MsAuthz.Infrastructure.Settings;
using OpenFga.Sdk.Client;

namespace MsAuthz.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var databaseSettings = BindAndValidate<DatabaseSettings>(configuration, DatabaseSettings.SectionName);
        var openFgaSettings = BindAndValidate<OpenFgaSettings>(configuration, OpenFgaSettings.SectionName);

        services.AddDbContext<AuthzDbContext>(options =>
            options.UseNpgsql(databaseSettings.ConnectionString));

        services.AddSingleton(_ => new OpenFgaClient(new ClientConfiguration
        {
            ApiUrl = openFgaSettings.ApiUrl,
            StoreId = openFgaSettings.StoreId,
            AuthorizationModelId = openFgaSettings.AuthorizationModelId,
        }));

        services.AddScoped<ICatalogRepository, CatalogRepository>();
        services.AddScoped<ITenantRegistry, TenantRegistry>();
        services.AddScoped<IOpenFgaGateway, OpenFgaGateway>();

        return services;
    }

    private static T BindAndValidate<T>(IConfiguration configuration, string sectionName) where T : IValidatableObject, new()
    {
        var settings = new T();
        configuration.GetSection(sectionName).Bind(settings);

        var validationContext = new ValidationContext(settings);
        var results = new List<ValidationResult>(settings.Validate(validationContext));
        if (results.Count > 0)
        {
            var messages = string.Join("; ", results.Select(r => r.ErrorMessage));
            throw new InvalidOperationException($"Invalid configuration for section '{sectionName}': {messages}");
        }

        return settings;
    }
}
