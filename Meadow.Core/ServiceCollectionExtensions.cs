using Meadow.Abstractions.Commands;
using Meadow.Abstractions.Dispatchers;
using Meadow.Abstractions.Queries;
using Meadow.Core.Commands;
using Meadow.Core.Dispatchers;
using Meadow.Core.Queries;

namespace Meadow.Core;

/// <summary>
///     Extension methods for registering Meadow.Core services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds Meadow.Core services: command dispatchers, query dispatchers, and unified dispatcher.
    ///     Scans the provided assemblies for command and query handlers.
    /// </summary>
    public static IServiceCollection AddMeadowCore(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        foreach (var assembly in assemblies)
        {
            services.AddCommands(assembly);
            services.AddQueries(assembly);
        }

        services.AddScoped<IDispatcher, Dispatcher>();

        return services;
    }

    private static IServiceCollection AddCommands(this IServiceCollection services, Assembly assembly)
    {
        services.AddScoped<ICommandDispatcher, CommandDispatcher>();

        IEnumerable<Type> commandHandlerTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.GetInterfaces()
                .Any(i => i.IsGenericType
                          && (i.GetGenericTypeDefinition() == typeof(ICommandHandler<,>)
                              || i.GetGenericTypeDefinition() == typeof(ICommandHandler<>))));

        foreach (var type in commandHandlerTypes)
        {
            var interfaces = type.GetInterfaces()
                .Where(i =>
                    i.IsGenericType &&
                    (i.GetGenericTypeDefinition() == typeof(ICommandHandler<>)
                     || i.GetGenericTypeDefinition() == typeof(ICommandHandler<,>)));

            foreach (var interfaceType in interfaces) services.AddScoped(interfaceType, type);
        }

        return services;
    }

    private static IServiceCollection AddQueries(this IServiceCollection services, Assembly assembly)
    {
        services.AddScoped<IQueryDispatcher, QueryDispatcher>();

        IEnumerable<Type> queryHandlerTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.GetInterfaces()
                .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQueryHandler<,>)));

        foreach (Type type in queryHandlerTypes)
        {
            var interfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQueryHandler<,>));

            foreach (Type interfaceType in interfaces) services.AddScoped(interfaceType, type);
        }

        return services;
    }
}

