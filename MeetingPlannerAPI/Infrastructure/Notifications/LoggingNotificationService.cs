using MeetingPlannerAPI.Application.Interfaces;

namespace MeetingPlannerAPI.Infrastructure.Notifications;

/// <summary>Simulates Email, iCal and message-queue dispatch by writing to the console.</summary>
public sealed class LoggingNotificationService : INotificationService
{
    public Task SendAsync(Notification notification, CancellationToken cancellationToken)
    {
        foreach (string email in notification.RecipientEmails)
        {
            Console.WriteLine($"[Email] {notification.Type} '{notification.Title}' -> {email}");
            Console.WriteLine($"[iCal] Invite for meeting {notification.MeetingId}: {notification.StartTimeUtc:O} - {notification.EndTimeUtc:O} -> {email}");
        }

        Console.WriteLine($"[MQ] Published {notification.Type} for meeting {notification.MeetingId} ({notification.RecipientEmails.Count} recipients)");

        return Task.CompletedTask;
    }
}
