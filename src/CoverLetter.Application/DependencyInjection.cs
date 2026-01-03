using System.Reflection;
using CoverLetter.Application.Common.Behaviors;
using CoverLetter.Application.Common.Interfaces;
using CoverLetter.Application.Common.Services;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace CoverLetter.Application;

/// <summary>
/// Dependency injection extensions for Application layer.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Register MediatR
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
        });

        // Register FluentValidation validators
        services.AddValidatorsFromAssembly(assembly);

        // Register memory cache for idempotency
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = 100; // Limit to 100 cached responses
        });

        // Register pipeline behaviors (order matters!)
        // 1. Log request details
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        // 2. Check idempotency (avoid duplicate work)
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(IdempotencyBehavior<,>));
        // 3. Validate request
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        // Register Application Services
        services.AddSingleton<IPromptRegistry, PromptRegistry>();
        services.AddSingleton<ICacheKeyBuilder, CacheKeyBuilder>();
        services.AddScoped<ICustomPromptService, CustomPromptService>();

        return services;
    }
}
