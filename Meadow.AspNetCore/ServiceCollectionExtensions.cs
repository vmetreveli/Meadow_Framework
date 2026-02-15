using Meadow.Abstractions.Exceptions;
using Meadow.AspNetCore.Exceptions;
using Microsoft.AspNetCore.Builder;

namespace Meadow.AspNetCore;

/// <summary>
///     Extension methods for registering Meadow.AspNetCore services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds Meadow ASP.NET Core services: error handling middleware and exception mapping.
    /// </summary>
    public static IServiceCollection AddMeadowAspNetCore(this IServiceCollection services)
    {
        return services
            .AddScoped<ErrorHandlerMiddleware>()
            .AddSingleton<IExceptionToResponseMapper, ExceptionToResponseMapper>()
            .AddSingleton<IExceptionCompositionRoot, ExceptionCompositionRoot>();
    }

    /// <summary>
    ///     Registers the ErrorHandlerMiddleware in the application pipeline.
    /// </summary>
    public static IApplicationBuilder UseMeadowExceptionHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ErrorHandlerMiddleware>();
    }
}
