using System.Reflection;
using CoverLetter.Application.Common.Behaviors;
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

    // Register pipeline behaviors (order matters!)
    services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

    return services;
  }
}
