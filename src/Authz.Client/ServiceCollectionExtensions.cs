using Authz.Client.Admin;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Authz.Client;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IAuthzClient"/> (typed HttpClient + IMemoryCache) and its options.
    ///
    /// It deliberately does NOT register
    /// <see cref="PermissionAuthorizationBehavior{TRequest,TResponse}"/>. Pipeline order matters and
    /// only the host knows it: the behavior has to run AFTER whatever behavior resolves the host's
    /// request context, and BEFORE validation (so an unauthorized caller never gets a validation
    /// error that discloses details about a resource it cannot access). MediatR collects every
    /// <c>cfg.AddBehavior</c> call into one contiguous block, so a registration made out here can
    /// only land entirely before or entirely after the host's own behaviors — never interleaved.
    ///
    /// The host therefore adds it in its own MediatR configuration, at the exact slot it needs:
    /// <code>
    /// services.AddAuthzClient(configuration);
    /// services.AddMediatR(cfg =>
    /// {
    ///     // ... host behaviors that resolve tenant/user ...
    ///     cfg.AddBehavior(typeof(IPipelineBehavior&lt;,&gt;), typeof(PermissionAuthorizationBehavior&lt;,&gt;));
    ///     // ... validation, etc ...
    /// });
    /// </code>
    ///
    /// The host system MUST also register its own <see cref="IAuthzRequestContextAccessor"/>
    /// implementation — Authz.Client cannot provide one generically (see that interface's doc
    /// comment). Registering this without one will make any request carrying
    /// <see cref="RequirePermissionAttribute"/> fail to resolve at runtime.
    /// </summary>
    public static IServiceCollection AddAuthzClient(this IServiceCollection services, IConfiguration configuration)
        => services.AddAuthzClient(options => configuration.GetSection(AuthzClientOptions.SectionName).Bind(options));

    public static IServiceCollection AddAuthzClient(this IServiceCollection services, Action<AuthzClientOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddMemoryCache();

        services.AddHttpClient<IAuthzClient, AuthzClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AuthzClientOptions>>().Value;

            if (string.IsNullOrEmpty(options.BaseUrl))
                throw new InvalidOperationException($"{AuthzClientOptions.SectionName}:BaseUrl is required.");

            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = options.RequestTimeout;

            if (!string.IsNullOrEmpty(options.ApiKey))
                client.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
        });

        return services;
    }

    /// <summary>
    /// Registers <see cref="IAuthzAdminClient"/> (typed HttpClient, no cache — see that interface's
    /// doc comment) for the role-administration operations (MS-AUTHZ-SPEC.md §12 step 8): catalog
    /// listing and reading/replacing a user's role assignments.
    ///
    /// Shares <see cref="AuthzClientOptions"/> / configuration section "Authz" with
    /// <see cref="AddAuthzClient(IServiceCollection, IConfiguration)"/> — same ms-authz instance, same
    /// BaseUrl/ApiKey. Call both when a host needs both the hot-path permission lookup and the
    /// administration operations; each registers its own typed HttpClient independently, so either can
    /// be added without the other.
    /// </summary>
    public static IServiceCollection AddAuthzAdminClient(this IServiceCollection services, IConfiguration configuration)
        => services.AddAuthzAdminClient(options => configuration.GetSection(AuthzClientOptions.SectionName).Bind(options));

    public static IServiceCollection AddAuthzAdminClient(this IServiceCollection services, Action<AuthzClientOptions> configureOptions)
    {
        services.Configure(configureOptions);

        services.AddHttpClient<IAuthzAdminClient, AuthzAdminClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AuthzClientOptions>>().Value;

            if (string.IsNullOrEmpty(options.BaseUrl))
                throw new InvalidOperationException($"{AuthzClientOptions.SectionName}:BaseUrl is required.");

            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = options.RequestTimeout;

            if (!string.IsNullOrEmpty(options.ApiKey))
                client.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
        });

        return services;
    }
}
