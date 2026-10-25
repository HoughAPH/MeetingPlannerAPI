using MeetingPlannerAPI.Application.Abstractions;
using MeetingPlannerAPI.Application.Interfaces;
using MeetingPlannerAPI.Domain.Meetings;
using MeetingPlannerAPI.Infrastructure.Notifications;
using MeetingPlannerAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MeetingPlannerAPI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IMeetingRepository, MeetingRepository>();
        services.AddSingleton<INotificationService, LoggingNotificationService>();

        return services;
    }
}
