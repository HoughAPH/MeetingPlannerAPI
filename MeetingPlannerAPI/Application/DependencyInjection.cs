using MeetingPlannerAPI.Application.Meetings.CancelMeeting;
using MeetingPlannerAPI.Application.Meetings.CheckAvailability;
using MeetingPlannerAPI.Application.Meetings.CreateMeeting;
using MeetingPlannerAPI.Application.Meetings.GetMeeting;
using MeetingPlannerAPI.Application.Meetings.RespondToInvitation;
using MeetingPlannerAPI.Application.Meetings.SearchMeetings;
using MeetingPlannerAPI.Application.Meetings.UpdateMeeting;

namespace MeetingPlannerAPI.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateMeetingHandler>();
        services.AddScoped<UpdateMeetingHandler>();
        services.AddScoped<CancelMeetingHandler>();
        services.AddScoped<GetMeetingHandler>();
        services.AddScoped<SearchMeetingsHandler>();
        services.AddScoped<CheckAvailabilityHandler>();
        services.AddScoped<RespondToInvitationHandler>();
        return services;
    }
}
