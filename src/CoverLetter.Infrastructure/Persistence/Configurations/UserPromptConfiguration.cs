using CoverLetter.Domain.Entities;
using CoverLetter.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoverLetter.Infrastructure.Persistence.Configurations;

public class UserPromptConfiguration : IEntityTypeConfiguration<UserPrompt>
{
  public void Configure(EntityTypeBuilder<UserPrompt> builder)
  {
    builder.ToTable("user_prompts");

    builder.HasKey(x => x.Id);

    builder.Property(x => x.Id)
      .ValueGeneratedNever();

    builder.Property(x => x.UserId)
      .IsRequired();

    // Store enum as string in database
    builder.Property(x => x.PromptType)
      .IsRequired()
      .HasMaxLength(100)
      .HasConversion<string>();

    builder.Property(x => x.Content)
      .IsRequired();

    builder.Property(x => x.CreatedAt)
      .IsRequired();

    builder.Property(x => x.UpdatedAt)
      .IsRequired();

    builder.HasIndex(x => new { x.UserId, x.PromptType })
      .IsUnique()
      .HasDatabaseName("ix_user_prompts_userid_prompttype");
  }
}
