using CoverLetter.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoverLetter.Infrastructure.Persistence.Configurations;

public class CompileJobConfiguration : IEntityTypeConfiguration<CompileJob>
{
  public void Configure(EntityTypeBuilder<CompileJob> builder)
  {
    builder.ToTable("compile_jobs");

    builder.HasKey(x => x.Id);

    builder.Property(x => x.Id)
        .ValueGeneratedNever(); // Domain generates identity in CompileJob.Create

    builder.Property(x => x.Status)
        .IsRequired()
        .HasMaxLength(50)
        .HasConversion<string>();

    builder.Property(x => x.UserId)
        .HasMaxLength(100);

    builder.Property(x => x.IdempotencyKey)
        .HasMaxLength(200);

    builder.Property(x => x.ResultPath)
        .HasMaxLength(1000);

    builder.Property(x => x.Error)
        .HasColumnType("text");

    builder.Property(x => x.CreatedAt)
        .IsRequired();

    builder.Property(x => x.UpdatedAt)
        .IsRequired();

    // Row version as shadow property (matches the rest of the model).
    builder.Property<uint>("Version")
        .IsRowVersion();

    // Unique partial index for DB-backed idempotency: only one job per (user, key)
    // when an idempotency key is supplied. Anonymous / keyless jobs are not constrained.
    builder.HasIndex(x => new { x.UserId, x.IdempotencyKey })
        .IsUnique()
        .HasFilter("\"idempotency_key\" IS NOT NULL")
        .HasDatabaseName("ix_compile_jobs_userid_idempotencykey");

    // Index for consumer/dispatcher status scans.
    builder.HasIndex(x => x.Status)
        .HasDatabaseName("ix_compile_jobs_status");

    builder.HasIndex(x => x.CreatedAt)
        .HasDatabaseName("ix_compile_jobs_createdat");
  }
}
