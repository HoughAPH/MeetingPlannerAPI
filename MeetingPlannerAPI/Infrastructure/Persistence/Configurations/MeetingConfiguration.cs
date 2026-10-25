using MeetingPlannerAPI.Domain.Meetings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MeetingPlannerAPI.Infrastructure.Persistence.Configurations;

internal sealed class MeetingConfiguration : IEntityTypeConfiguration<Meeting>, IEntityTypeConfiguration<Participant>
{
    // SQLite returns DateTime with Kind=Unspecified; the domain requires UTC, so restore the kind on read.
    private static readonly ValueConverter<DateTime, DateTime> UtcConverter =
        new(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private static readonly ValueConverter<DateTime?, DateTime?> NullableUtcConverter =
        new(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

    public void Configure(EntityTypeBuilder<Meeting> builder)
    {
        builder.ToTable("Meetings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Title).IsRequired().HasMaxLength(MeetingLimits.TitleMaxLength);
        builder.Property(e => e.Description).IsRequired().HasMaxLength(MeetingLimits.DescriptionMaxLength);
        builder.Property(e => e.StartTimeUtc).HasConversion(UtcConverter);
        builder.Property(e => e.EndTimeUtc).HasConversion(UtcConverter);
        builder.Property(e => e.CreatedAtUtc).HasConversion(UtcConverter);
        builder.Property(e => e.CancelledAtUtc).HasConversion(NullableUtcConverter);
        // Bumped by the domain on every aggregate change, so participant-only edits are guarded too (see Meeting.Version).
        builder.Property(e => e.Version).IsConcurrencyToken();
        builder.Ignore(e => e.IsCancelled);

        builder.HasIndex(e => new { e.StartTimeUtc, e.EndTimeUtc });

        builder.HasMany(e => e.Participants)
            .WithOne()
            .HasForeignKey("MeetingId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(e => e.Participants).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    public void Configure(EntityTypeBuilder<Participant> builder)
    {
        builder.ToTable("Participants");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Name).IsRequired().HasMaxLength(MeetingLimits.ParticipantNameMaxLength);
        builder.Property(a => a.Email).IsRequired().HasMaxLength(MeetingLimits.ParticipantEmailMaxLength);

        // Backs the "one participant per email per meeting" invariant at the database level.
        builder.HasIndex("MeetingId", nameof(Participant.Email)).IsUnique();
        // Supports filtering meetings by participant email.
        builder.HasIndex(a => a.Email);
    }
}
