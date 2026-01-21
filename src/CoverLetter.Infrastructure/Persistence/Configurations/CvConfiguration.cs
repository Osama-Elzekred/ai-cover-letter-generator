using CoverLetter.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoverLetter.Infrastructure.Persistence.Configurations;

public class CvConfiguration : IEntityTypeConfiguration<Cv>
{
  public void Configure(EntityTypeBuilder<Cv> builder)
  {
    builder.ToTable("cvs");

    builder.HasKey(x => x.Id);

    builder.Property(x => x.Id)
        .ValueGeneratedNever(); // We'll use ULIDs/GUIDs from domain

    builder.Property(x => x.UserId)
        .IsRequired();

    builder.Property(x => x.FileName)
        .IsRequired()
        .HasMaxLength(500);

    builder.Property(x => x.Content)
        .IsRequired();

    builder.Property(x => x.FileStoragePath)
        .HasMaxLength(1000);

    builder.Property(x => x.IsActive)
        .IsRequired()
        .HasDefaultValue(true);

    builder.Property(x => x.CreatedAt)
        .IsRequired();

    builder.Property(x => x.UpdatedAt)
        .IsRequired();

    // Row version as shadow property
    builder.Property<uint>("Version")
        .IsRowVersion();

    // Indexes
    builder.HasIndex(x => new { x.UserId, x.IsActive })
        .HasDatabaseName("ix_cvs_userid_isactive");

    builder.HasIndex(x => x.CreatedAt)
        .HasDatabaseName("ix_cvs_createdat");
  }
}
