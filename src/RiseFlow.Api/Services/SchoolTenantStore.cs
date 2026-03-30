using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Data;

namespace RiseFlow.Api.Services;

/// <summary>
/// Finbuckle tenant store backed by School rows.
/// </summary>
public class SchoolTenantStore : IMultiTenantStore<SchoolTenantInfo>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public SchoolTenantStore(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<SchoolTenantInfo?> GetByIdentifierAsync(string identifier)
    {
        if (!Guid.TryParse(identifier, out var schoolId))
            return null;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RiseFlowDbContext>();
        var school = await db.Schools
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == schoolId && s.IsActive);

        return school is null
            ? null
            : new SchoolTenantInfo
            {
                Id = school.Id.ToString(),
                Identifier = school.Id.ToString(),
                Name = school.Name,
            };
    }

    public Task<SchoolTenantInfo?> GetAsync(string id) => GetByIdentifierAsync(id);

    public Task<IEnumerable<SchoolTenantInfo>> GetAllAsync() => GetAllAsync(0, 0);

    public async Task<IEnumerable<SchoolTenantInfo>> GetAllAsync(int take, int skip)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RiseFlowDbContext>();
        var query = db.Schools
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new SchoolTenantInfo
            {
                Id = s.Id.ToString(),
                Identifier = s.Id.ToString(),
                Name = s.Name,
            });

        if (skip > 0)
            query = query.Skip(skip);
        if (take > 0)
            query = query.Take(take);

        var schools = await query.ToListAsync();

        return schools;
    }

    public Task<bool> AddAsync(SchoolTenantInfo tenantInfo) => Task.FromResult(false);
    public Task<bool> UpdateAsync(SchoolTenantInfo tenantInfo) => Task.FromResult(false);
    public Task<bool> RemoveAsync(string identifier) => Task.FromResult(false);
}
