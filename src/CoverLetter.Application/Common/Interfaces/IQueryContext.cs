using CoverLetter.Domain.Entities;

namespace CoverLetter.Application.Common.Interfaces;

/// <summary>
/// Provides direct access to DbSets for query operations.
/// Handlers build LINQ queries directly using these DbSets.
/// </summary>
public interface IQueryContext
{
  IQueryable<Cv> Cvs { get; }
  IQueryable<CoverLetterEntity> CoverLetters { get; }
  IQueryable<PromptTemplate> PromptTemplates { get; }
  IQueryable<IdempotencyKey> IdempotencyKeys { get; }
  IQueryable<UserPrompt> UserPrompts { get; }
}
