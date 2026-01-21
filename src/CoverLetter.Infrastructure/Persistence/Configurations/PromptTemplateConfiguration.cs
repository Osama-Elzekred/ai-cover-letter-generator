using CoverLetter.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoverLetter.Infrastructure.Persistence.Configurations;

public class PromptTemplateConfiguration : IEntityTypeConfiguration<PromptTemplate>
{
  public void Configure(EntityTypeBuilder<PromptTemplate> builder)
  {
    builder.ToTable("prompt_templates");

    builder.HasKey(x => x.Id);

    builder.Property(x => x.Id)
        .ValueGeneratedNever();

    builder.Property(x => x.Name)
        .IsRequired()
        .HasMaxLength(200);

    builder.Property(x => x.Template)
        .IsRequired();

    builder.Property(x => x.IsDefault)
        .IsRequired()
        .HasDefaultValue(false);

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
    builder.HasIndex(x => new { x.IsActive, x.IsDefault })
        .HasDatabaseName("ix_prompt_templates_isactive_isdefault");

    builder.HasIndex(x => x.Name)
        .HasDatabaseName("ix_prompt_templates_name");
  }
}
