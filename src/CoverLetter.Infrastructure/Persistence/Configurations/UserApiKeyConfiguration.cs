using CoverLetter.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoverLetter.Infrastructure.Persistence.Configurations;

public class UserApiKeyConfiguration : IEntityTypeConfiguration<UserApiKey>
{
  public void Configure(EntityTypeBuilder<UserApiKey> builder)
  {
    builder.ToTable("user_api_keys");

    builder.HasKey(x => x.Id);

    builder.Property(x => x.Id)
      .ValueGeneratedNever();

    builder.Property(x => x.UserId)
      .IsRequired()
      .HasMaxLength(100);

    builder.Property(x => x.Provider)
      .IsRequired()
      .HasMaxLength(50)
      .HasConversion<string>();

    builder.Property(x => x.ApiKey)
      .IsRequired()
      .HasMaxLength(500);

    builder.Property(x => x.CreatedAt)
      .IsRequired();

    builder.Property(x => x.UpdatedAt)
      .IsRequired();

    builder.HasIndex(x => new { x.UserId, x.Provider })
      .IsUnique()
      .HasDatabaseName("ix_user_api_keys_userid_provider");
  }
}
