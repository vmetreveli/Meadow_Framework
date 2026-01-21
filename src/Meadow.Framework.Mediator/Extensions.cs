using Meadow.Framework.Mediator.Abstractions.Commands;
using Meadow.Framework.Mediator.Abstractions.Dispatchers;
using Meadow.Framework.Mediator.Abstractions.Events;
using Meadow.Framework.Mediator.Abstractions.Queries;
using Meadow.Framework.Mediator.Infrastructure.Commands;
using Meadow.Framework.Mediator.Infrastructure.Dispatchers;
using Meadow.Framework.Mediator.Infrastructure.Events;
using Meadow.Framework.Mediator.Infrastructure.Queries.Dispatcher;

namespace Meadow.Framework.Mediator;

/// <summary>
///     Provides extension methods for configuring the Meadow Framework services and middleware.
/// </summary>
public static class Extensions
{
    /// <summary>
    ///     Extension method to add the Meadow Mediator services to the <see cref="IServiceCollection" />.
    ///     This includes command dispatching, query dispatching, and event dispatching with automatic handler registration.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to which the services will be added.</param>
    /// <param name="assemblies">The assemblies to scan for handlers (commands, queries, events).</param>
    /// <returns>The modified <see cref="IServiceCollection" />.</returns>
    public static IServiceCollection AddMeadowMediator(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        foreach (Assembly assembly in assemblies)
        {
            services.AddCommands(assembly);
            services.AddQueries(assembly);
            services.AddEvents(assembly);
        }

        services.AddScoped<IDispatcher, Dispatcher>();

        return services;
    }


    /// <summary>
    ///     Adds command dispatching and command handlers to the <see cref="IServiceCollection" />.
    ///     Registers all types that implement <see cref="ICommandHandler{TCommand}" /> and
    ///     <see cref="ICommandHandler{TCommand}" />.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add the services to.</param>
    /// <param name="assembly">The assembly to scan for command handlers.</param>
    /// <returns>The modified <see cref="IServiceCollection" />.</returns>
    private static IServiceCollection AddCommands(this IServiceCollection services, Assembly assembly)
    {
        services.AddScoped<ICommandDispatcher, CommandDispatcher>();

        // Get all types that implement ICommandHandler<>
        IEnumerable<Type> commandHandlerTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.GetInterfaces()
                .Any(i => i.IsGenericType
                          && (i.GetGenericTypeDefinition() == typeof(ICommandHandler<,>)
                              || i.GetGenericTypeDefinition() == typeof(ICommandHandler<>))));

        // Register each command handler as scoped
        foreach (Type type in commandHandlerTypes)
        {
            IEnumerable<Type> interfaces = type.GetInterfaces()
                .Where(i =>
                    i.IsGenericType &&
                    (i.GetGenericTypeDefinition() == typeof(ICommandHandler<>)
                     || i.GetGenericTypeDefinition() == typeof(ICommandHandler<,>)));

            foreach (Type interfaceType in interfaces) services.AddScoped(interfaceType, type);
        }

        return services;
    }

    /// <summary>
    ///     Adds query dispatching and query handlers to the <see cref="IServiceCollection" />.
    ///     Registers all types that implement <see cref="IQueryHandler{TQuery,TResult}" />.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add the services to.</param>
    /// <param name="assembly">The assembly to scan for query handlers.</param>
    /// <returns>The modified <see cref="IServiceCollection" />.</returns>
    private static IServiceCollection AddQueries(this IServiceCollection services, Assembly assembly)
    {
        services.AddScoped<IQueryDispatcher, QueryDispatcher>();

        // Get all types implementing IQueryHandler<,>
        IEnumerable<Type> queryHandlerTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.GetInterfaces()
                .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQueryHandler<,>)));

        // Register each query handler as scoped
        foreach (Type type in queryHandlerTypes)
        {
            IEnumerable<Type> interfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQueryHandler<,>));

            foreach (Type interfaceType in interfaces) services.AddScoped(interfaceType, type);
        }

        return services;
    }

    /// <summary>
    ///     Adds event dispatching and event handlers to the <see cref="IServiceCollection" />.
    ///     Registers all types that implement <see cref="IEventHandler{TEvent}" />.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add the services to.</param>
    /// <param name="assembly">The assembly to scan for event handlers.</param>
    /// <returns>The modified <see cref="IServiceCollection" />.</returns>
    private static IServiceCollection AddEvents(this IServiceCollection services, Assembly assembly)
    {
        services.AddScoped<IEventDispatcher, EventDispatcher>();

        // Get all types implementing IEventHandler<>
        IEnumerable<Type> eventHandlerTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.GetInterfaces()
                .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>)));

        // Register each event handler as scoped
        foreach (Type type in eventHandlerTypes)
        {
            IEnumerable<Type> interfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>));

            foreach (Type interfaceType in interfaces) services.AddScoped(interfaceType, type);
        }

        return services;
    }


    /// <summary>
    ///     Finds and returns all classes that implement <see cref="IEventConsumer{TEvent}" /> from all loaded assemblies.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan.</param>
    /// <returns>A collection of consumer types implementing <see cref="IEventConsumer{TEvent}" />.</returns>
    private static IEnumerable<Type> FindConsumers(IEnumerable<Assembly> assemblies)
    {
        Type consumerInterfaceType = typeof(IEventConsumer<>);
        List<Type> consumer = new();

        // Search for classes implementing IEventConsumer<> in loaded assemblies
        foreach (Assembly assembly in assemblies)
            consumer.AddRange(assembly.GetTypes()
                .Where(type => type is { IsClass: true, IsAbstract: false })
                .Where(type => type.GetInterfaces()
                    .ToList()
                    .Exists(interfaceType =>
                        interfaceType.IsGenericType &&
                        interfaceType.GetGenericTypeDefinition() == consumerInterfaceType)));

        return consumer;
    }
}