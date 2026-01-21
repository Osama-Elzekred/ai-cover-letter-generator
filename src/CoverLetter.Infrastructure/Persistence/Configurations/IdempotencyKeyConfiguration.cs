using CoverLetter.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoverLetter.Infrastructure.Persistence.Configurations;

public class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKey>
{
  public void Configure(EntityTypeBuilder<IdempotencyKey> builder)
  {
    builder.ToTable("idempotency_keys");

    builder.HasKey(x => x.Id);

    builder.Property(x => x.Id)
        .ValueGeneratedNever();

    builder.Property(x => x.Key)
        .IsRequired()
        .HasMaxLength(200);

    builder.Property(x => x.UserId)
        .IsRequired();

    builder.Property(x => x.RequestPath)
        .IsRequired()
        .HasMaxLength(500);

    builder.Property(x => x.StatusCode)
        .IsRequired();

    builder.Property(x => x.CreatedAt)
        .IsRequired();

    builder.Property(x => x.ExpiresAt)
        .IsRequired();

    // Unique constraint on key + userId
    builder.HasIndex(x => new { x.Key, x.UserId })
        .IsUnique()
        .HasDatabaseName("ix_idempotency_keys_key_userid");

    // Index for cleanup queries
    builder.HasIndex(x => x.ExpiresAt)
        .HasDatabaseName("ix_idempotency_keys_expiresat");
  }
}
