using CoverLetter.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoverLetter.Infrastructure.Persistence.Configurations;

public class CoverLetterConfiguration : IEntityTypeConfiguration<CoverLetterEntity>
{
  public void Configure(EntityTypeBuilder<CoverLetterEntity> builder)
  {
    builder.ToTable("cover_letters");

    builder.HasKey(x => x.Id);

    builder.Property(x => x.Id)
        .ValueGeneratedNever();

    builder.Property(x => x.UserId)
        .IsRequired();

    builder.Property(x => x.CvId)
        .IsRequired();

    builder.Property(x => x.JobDescription)
        .IsRequired();

    builder.Property(x => x.CompanyName)
        .HasMaxLength(500);

    builder.Property(x => x.Content)
        .IsRequired();

    builder.Property(x => x.Status)
        .IsRequired()
        .HasMaxLength(50);

    builder.Property(x => x.CreatedAt)
        .IsRequired();

    builder.Property(x => x.UpdatedAt)
        .IsRequired();

    // Row version as shadow property
    builder.Property<uint>("Version")
        .IsRowVersion();

    // Relationships
    builder.HasOne(x => x.Cv)
        .WithMany(x => x.CoverLetters)
        .HasForeignKey(x => x.CvId)
        .OnDelete(DeleteBehavior.Cascade);

    // Indexes
    builder.HasIndex(x => new { x.UserId, x.Status })
        .HasDatabaseName("ix_cover_letters_userid_status");

    builder.HasIndex(x => x.CvId)
        .HasDatabaseName("ix_cover_letters_cvid");

    builder.HasIndex(x => x.CreatedAt)
        .HasDatabaseName("ix_cover_letters_createdat");
  }
}
