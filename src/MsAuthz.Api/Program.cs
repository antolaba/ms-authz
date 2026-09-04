using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.OpenApi.Models;
using MsAuthz.Api.Authentication;
using MsAuthz.Api.Extensions;
using MsAuthz.Application;
using MsAuthz.Application.Interfaces;
using MsAuthz.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ── Authentication / authorization ─────────────────────────────────────────────────────────────
// ⚠️ API key only. ms-authz does not validate JWTs — see ApiKeyAuthenticationHandler's security-
// boundary note (MS-AUTHZ-SPEC.md §7). Every endpoint requires it: the AuthorizeFilter below applies
// to all controllers, there is no anonymous endpoint in this service besides /health.
builder.Services
    .AddAuthentication(ApiKeyAuthenticationDefaults.SchemeName)
    .AddScheme<ApiKeyAuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationDefaults.SchemeName,
        options => options.ApiKey = builder.Configuration["Security:ApiKey"] ?? string.Empty);

builder.Services.AddAuthorization();

builder.Services.AddControllers(options =>
{
    var policy = new AuthorizationPolicyBuilder(ApiKeyAuthenticationDefaults.SchemeName)
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new AuthorizeFilter(policy));
});

// ── Application / Infrastructure ────────────────────────────────────────────────────────────────
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ── Problem Details / exception handling ────────────────────────────────────────────────────────
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// ── Swagger ──────────────────────────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ms-authz",
        Version = "v1",
        Description = "Authorization component over OpenFGA. Internal only — see README's security boundary.",
    });

    var apiKeyScheme = new OpenApiSecurityScheme
    {
        Name = ApiKeyAuthenticationDefaults.HeaderName,
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "API key issued to the consuming system.",
    };
    options.AddSecurityDefinition(ApiKeyAuthenticationDefaults.SchemeName, apiKeyScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = ApiKeyAuthenticationDefaults.SchemeName } }, [] },
    });
});

var app = builder.Build();

// Force the catalog file to load now, not on the first request — an invalid or missing catalog
// must fail the process at startup (MS-AUTHZ-SPEC.md §5), not surface as a 500 on the first call.
app.Services.GetRequiredService<ICatalogRepository>();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();

app.Run();

// Exposed so integration tests (WebApplicationFactory-style) can reference the entry point, if added later.
public partial class Program;
