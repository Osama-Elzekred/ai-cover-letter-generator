using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Domain.Common;
using CoverLetter.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CoverLetter.Application.UseCases.ParseCv;

/// <summary>
/// Handler for parsing CV files.
/// Delegates to ICvParserService and caches the result.
/// </summary>
public sealed class ParseCvCommandHandler(
    ICvParserService cvParserService,
    IMemoryCache cache,
    ILogger<ParseCvCommandHandler> logger)
    : IRequestHandler<ParseCvCommand, Result<ParseCvResult>>
{
  private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

  public async Task<Result<ParseCvResult>> Handle(
      ParseCvCommand request,
      CancellationToken cancellationToken)
  {
    logger.LogInformation(
        "Parsing CV file: {FileName}, Format: {Format}",
        request.FileName, request.Format);

    // Parse the CV using the appropriate parser
    var parseResult = await cvParserService.ParseAsync(
        request.FileName,
        request.FileContent,
        request.Format,
        cancellationToken);

    if (parseResult.IsFailure)
    {
      logger.LogWarning(
          "CV parsing failed: {FileName}, Errors: {Errors}",
          request.FileName,
          string.Join(", ", parseResult.Errors));

      return Result<ParseCvResult>.Failure(parseResult.Errors, parseResult.Type);
    }

    var cvDocument = parseResult.Value!;

    // Cache the parsed CV document by ID for retrieval via GET endpoint
    var cacheKey = $"cv:{cvDocument.Id}";
    var cacheOptions = new MemoryCacheEntryOptions
    {
      AbsoluteExpirationRelativeToNow = CacheDuration,
      Size = 1  // Each CV counts as 1 unit toward the cache size limit
    };
    cache.Set(cacheKey, cvDocument, cacheOptions);

    logger.LogInformation(
        "Successfully parsed and cached CV: {CvId}, Format: {Format}, Characters: {CharCount}",
        cvDocument.Id, cvDocument.Format, cvDocument.Metadata.CharacterCount);

    var result = ParseCvResult.FromDocument(cvDocument);
    return Result.Success(result);
  }
}
