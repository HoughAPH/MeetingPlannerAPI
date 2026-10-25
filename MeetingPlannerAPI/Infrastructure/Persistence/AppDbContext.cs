using MeetingPlannerAPI.Application.Abstractions;
using MeetingPlannerAPI.Domain.Meetings;
using Microsoft.EntityFrameworkCore;

namespace MeetingPlannerAPI.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Meeting> Meetings => Set<Meeting>();

    // Every SaveChanges overload ends up here; translate EF's exception so Application never references EF.
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(
                "The meeting was modified by another request. Reload it and try again.", ex);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
