using Meadow.Framework.Exceptions.Abstractions.Exceptions;
using Meadow.Framework.Exceptions.Infrastructure.Exceptions;
using Microsoft.AspNetCore.Builder;

namespace Meadow.Framework.Exceptions;

/// <summary>
///     Provides extension methods for configuring the Meadow Framework services and middleware.
/// </summary>
public static class Extensions
{
    /// <summary>
    ///     Adds Meadow Framework Exceptions services for centralized error handling.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add the services to.</param>
    /// <returns>The modified <see cref="IServiceCollection" />.</returns>
    public static IServiceCollection AddMeadowExceptions(
        this IServiceCollection services)
    {
        services.AddErrorHandling();
        return services;
    }


    /// <summary>
    ///     Adds error handling middleware and exception handling services.
    ///     Registers <see cref="ErrorHandlerMiddleware" />, <see cref="IExceptionToResponseMapper" />, and
    ///     <see cref="IExceptionCompositionRoot" />.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add the services to.</param>
    /// <returns>The modified <see cref="IServiceCollection" />.</returns>
    private static IServiceCollection AddErrorHandling(this IServiceCollection services)
    {
        return services
            .AddScoped<ErrorHandlerMiddleware>() // Middleware for error handling
            .AddSingleton<IExceptionToResponseMapper, ExceptionToResponseMapper>() // Maps exceptions to responses
            .AddSingleton<IExceptionCompositionRoot, ExceptionCompositionRoot>(); // Exception handler root
    }

    /// <summary>
    ///     Extension method to add the error handling middleware to the application pipeline.
    ///     This middleware catches exceptions and returns proper responses.
    /// </summary>
    /// <param name="app">The <see cref="IApplicationBuilder" /> to add the middleware to.</param>
    /// <returns>The modified <see cref="IApplicationBuilder" />.</returns>
    public static IApplicationBuilder UseErrorHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ErrorHandlerMiddleware>();
    }

}