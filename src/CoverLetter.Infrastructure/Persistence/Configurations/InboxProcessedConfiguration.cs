using CoverLetter.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoverLetter.Infrastructure.Persistence.Configurations;

public class InboxProcessedConfiguration : IEntityTypeConfiguration<InboxProcessed>
{
  public void Configure(EntityTypeBuilder<InboxProcessed> builder)
  {
    builder.ToTable("inbox_processed");

    // MessageId is the primary key (deduplication lookup).
    builder.HasKey(x => x.MessageId);

    builder.Property(x => x.MessageId)
        .ValueGeneratedNever();

    builder.Property(x => x.ProcessedAt)
        .IsRequired();

    builder.Property(x => x.JobId);
  }
}
