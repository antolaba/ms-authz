using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MsAuthz.Application.Interfaces;
using MsAuthz.Infrastructure.Catalog;
using MsAuthz.Infrastructure.OpenFga;
using MsAuthz.Infrastructure.Settings;
using OpenFga.Sdk.Client;

namespace MsAuthz.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var catalogSettings = BindAndValidate<CatalogSettings>(configuration, CatalogSettings.SectionName);
        var openFgaSettings = BindAndValidate<OpenFgaSettings>(configuration, OpenFgaSettings.SectionName);

        services.AddSingleton(openFgaSettings);
        services.AddSingleton(_ => new OpenFgaClient(new ClientConfiguration
        {
            ApiUrl = openFgaSettings.ApiUrl,
            StoreId = openFgaSettings.StoreId,
            AuthorizationModelId = openFgaSettings.AuthorizationModelId,
        }));
        services.AddSingleton<IOpenFgaAdminApi, OpenFgaAdminApi>();
        services.AddHostedService<OpenFgaBootstrapHostedService>();

        services.AddSingleton<ICatalogRepository>(_ =>
            new FileCatalogRepository(CatalogFileLoader.Load(ResolveCatalogPath(catalogSettings.Path))));

        services.AddScoped<IOpenFgaGateway, OpenFgaGateway>();

        return services;
    }

    private static string ResolveCatalogPath(string path)
        => System.IO.Path.IsPathRooted(path) ? path : System.IO.Path.Combine(Directory.GetCurrentDirectory(), path);

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
