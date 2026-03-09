using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Application;
using RiseFlow.Domain;
using RiseFlow.Domain.Entities;
using RiseFlow.Infrastructure.Identity;

namespace RiseFlow.Infrastructure.Data;

/// <summary>
/// Primary EF Core DbContext for RiseFlow.
/// All tenant-scoped entities implement ITenantEntity; a global query filter ensures every query
/// is automatically constrained to the current SchoolId resolved from ISchoolContext.
/// </summary>
public class RiseFlowDbContext
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    private readonly ISchoolContext _schoolContext;

    public RiseFlowDbContext(
        DbContextOptions<RiseFlowDbContext> options,
        ISchoolContext schoolContext) : base(options)
    {
        _schoolContext = schoolContext;
    }

    public DbSet<Student> Students => Set<Student>();
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ApplyTenantQueryFilters(modelBuilder);
    }

    /// <summary>
    /// Applies a global query filter to every entity that implements ITenantEntity:
    /// e =&gt; e.SchoolId == _schoolContext.SchoolId.
    /// When SchoolId is Guid.Empty (no tenant), queries return no rows by default.
    /// </summary>
    private void ApplyTenantQueryFilters(ModelBuilder modelBuilder)
    {
        var tenantInterfaceType = typeof(ITenantEntity);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!tenantInterfaceType.IsAssignableFrom(entityType.ClrType))
                continue;

            var entity = modelBuilder.Entity(entityType.ClrType);

            // Build expression: (e) => ((ITenantEntity)e).SchoolId == _schoolContext.SchoolId
            var parameter = Expression.Parameter(entityType.ClrType, "e");

            // Cast to ITenantEntity to access SchoolId consistently
            var cast = Expression.Convert(parameter, tenantInterfaceType);
            var schoolIdProperty = Expression.Property(cast, nameof(ITenantEntity.SchoolId));

            var contextConstant = Expression.Constant(_schoolContext);
            var contextSchoolIdProperty = Expression.Property(contextConstant, nameof(ISchoolContext.SchoolId));

            var equal = Expression.Equal(schoolIdProperty, contextSchoolIdProperty);
            var lambda = Expression.Lambda(equal, parameter);

            entity.HasQueryFilter(lambda);
        }
    }
}

