namespace MeetingPlannerAPI.Application.Interfaces;

public enum NotificationType
{
    MeetingCreated,
    MeetingUpdated,
    MeetingCancelled
}

public sealed record Notification(
    NotificationType Type,
    Guid MeetingId,
    string Title,
    DateTime StartTimeUtc,
    DateTime EndTimeUtc,
    IReadOnlyList<string> RecipientEmails);

public interface INotificationService
{
    Task SendAsync(Notification notification, CancellationToken cancellationToken);
}
