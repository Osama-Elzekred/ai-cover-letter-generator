using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoverLetter.Infrastructure.Persistence;

/// <summary>
/// Application DbContext implementing both IQueryContext and IUnitOfWork.
/// IQueryContext: Read-only query access for handlers
/// IUnitOfWork: Transaction management (SaveChangesAsync)
/// </summary>
public class AppDbContext : DbContext, IQueryContext, IUnitOfWork
{
  public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
  {
  }

  public DbSet<Cv> Cvs { get; set; } = null!;
  public DbSet<CoverLetterEntity> CoverLetters { get; set; } = null!;
  public DbSet<PromptTemplate> PromptTemplates { get; set; } = null!;
  public DbSet<IdempotencyKey> IdempotencyKeys { get; set; } = null!;
  public DbSet<UserPrompt> UserPrompts { get; set; } = null!;

  IQueryable<Cv> IQueryContext.Cvs => Cvs;
  IQueryable<CoverLetterEntity> IQueryContext.CoverLetters => CoverLetters;
  IQueryable<PromptTemplate> IQueryContext.PromptTemplates => PromptTemplates;
  IQueryable<IdempotencyKey> IQueryContext.IdempotencyKeys => IdempotencyKeys;
  IQueryable<UserPrompt> IQueryContext.UserPrompts => UserPrompts;

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

    // Apply all configurations from this assembly
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
  }
}
