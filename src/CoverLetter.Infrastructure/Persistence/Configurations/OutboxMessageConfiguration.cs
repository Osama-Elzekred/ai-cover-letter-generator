using CoverLetter.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoverLetter.Infrastructure.Persistence.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
  public void Configure(EntityTypeBuilder<OutboxMessage> builder)
  {
    builder.ToTable("outbox_messages");

    builder.HasKey(x => x.Id);

    // bigint identity column (DB-generated), as per the plan's BIGSERIAL.
    builder.Property(x => x.Id)
        .ValueGeneratedOnAdd();

    builder.Property(x => x.MessageId)
        .IsRequired();

    builder.Property(x => x.Topic)
        .IsRequired()
        .HasMaxLength(200);

    // JSONB payload carrying the CompileJobMessage contract.
    builder.Property(x => x.Payload)
        .IsRequired()
        .HasColumnType("jsonb");

    builder.Property(x => x.Attempts)
        .IsRequired()
        .HasDefaultValue(0);

    builder.Property(x => x.DispatchedAt);

    builder.Property(x => x.NextAttemptAt);

    builder.Property(x => x.CreatedAt)
        .IsRequired();

    // Deduplication / lookup indexes.
    builder.HasIndex(x => x.MessageId)
        .IsUnique()
        .HasDatabaseName("ix_outbox_messages_messageid");

    // Partial index optimising the dispatcher's undispatched-batch poll.
    builder.HasIndex(x => x.NextAttemptAt)
        .HasFilter("\"dispatched_at\" IS NULL")
        .HasDatabaseName("ix_outbox_messages_undispatched");
  }
}
