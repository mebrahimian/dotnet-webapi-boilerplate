﻿using Finbuckle.MultiTenant;
using FSH.WebApi.Infrastructure.Multitenancy;
using FSH.WebApi.Shared.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FSH.WebApi.Infrastructure.Persistence.Initialization;

internal class DatabaseInitializer
{
    private readonly TenantDbContext _tenantDbContext;
    private readonly IMultiTenantStore<FSHTenantInfo> _tenantStore;
    private readonly DatabaseSettings _dbSettings;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(TenantDbContext tenantDbContext, IMultiTenantStore<FSHTenantInfo> tenantStore, IOptions<DatabaseSettings> dbSettings, IServiceProvider serviceProvider, ILogger<DatabaseInitializer> logger)
    {
        _tenantDbContext = tenantDbContext;
        _tenantStore = tenantStore;
        _dbSettings = dbSettings.Value;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task InitializeDatabasesAsync(CancellationToken cancellationToken)
    {
        // First initialize the tenant db context
        if (_tenantDbContext.Database.GetPendingMigrations().Any())
        {
            _logger.LogInformation("Applying Root Migrations.");
            await _tenantDbContext.Database.MigrateAsync(cancellationToken);
        }

        await SeedRootTenantAsync();

        // Then initialize the application db's for each tenant
        foreach (var tenant in (await _tenantStore.GetAllAsync()).ToList())
        {
            await InitializeApplicationDbForTenantAsync(tenant, cancellationToken);
        }

        _logger.LogInformation("For documentations and guides, visit https://www.fullstackhero.net");
        _logger.LogInformation("To Sponsor this project, visit https://opencollective.com/fullstackhero");
    }

    private async Task SeedRootTenantAsync()
    {
        if (await _tenantStore.TryGetAsync(MultitenancyConstants.Root.Id) is null)
        {
            var rootTenant = new FSHTenantInfo(
                MultitenancyConstants.Root.Id,
                MultitenancyConstants.Root.Name,
                _dbSettings.ConnectionString!,
                MultitenancyConstants.Root.EmailAddress);

            rootTenant.SetValidity(DateTime.UtcNow.AddYears(1));

            await _tenantStore.TryAddAsync(rootTenant);
        }
    }

    private async Task InitializeApplicationDbForTenantAsync(FSHTenantInfo tenant, CancellationToken cancellationToken)
    {
        // First create a new scope
        using var scope = _serviceProvider.CreateScope();

        // Then set current tenant so the right connectionstring is used
        _serviceProvider.GetRequiredService<IMultiTenantContextAccessor>()
            .MultiTenantContext = new MultiTenantContext<FSHTenantInfo>()
            {
                TenantInfo = tenant
            };

        // Then run the initialization in the new scope
        await scope.ServiceProvider.GetRequiredService<ApplicationDbInitializer>()
            .InitializeAsync(cancellationToken);
    }
}