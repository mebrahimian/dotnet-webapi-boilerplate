using FSH.WebApi.Application.Multitenancy;
using FSH.WebApi.Infrastructure.Persistence;
using FSH.WebApi.Shared.Authorization;
using FSH.WebApi.Shared.Multitenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace FSH.WebApi.Infrastructure.Multitenancy;

internal static class Startup
{
    internal static IServiceCollection AddMultitenancy(this IServiceCollection services, IConfiguration config)
    {
        var databaseSettings = config.GetSection(nameof(DatabaseSettings)).Get<DatabaseSettings>();
        string? rootConnectionString = databaseSettings.ConnectionString;
        if (string.IsNullOrEmpty(rootConnectionString)) throw new InvalidOperationException("DB ConnectionString is not configured.");
        string? dbProvider = databaseSettings.DBProvider;
        if (string.IsNullOrEmpty(dbProvider)) throw new InvalidOperationException("DB Provider is not configured.");

        return services
            .AddDbContext<TenantDbContext>(m => m.UseDatabase(dbProvider, rootConnectionString))
            .AddMultiTenant<FSHTenantInfo>()
                .WithClaimStrategy(FSHClaims.Tenant)
                .WithHeaderStrategy(MultitenancyConstants.TenantIdName)
                .WithDelegateStrategy(context =>
                {
                    if (context is not HttpContext httpContext)
                    {
                        return Task.FromResult((string?)null);
                    }

                    httpContext.Request.Query.TryGetValue(MultitenancyConstants.TenantIdName, out StringValues tenantIdParam);

                    return Task.FromResult(tenantIdParam.ToString());
                })
                .WithEFCoreStore<TenantDbContext, FSHTenantInfo>()
                .Services
            .AddScoped<ITenantService, TenantService>();
    }

    internal static IApplicationBuilder UseMultiTenancy(this IApplicationBuilder app) =>
        app.UseMultiTenant();
}