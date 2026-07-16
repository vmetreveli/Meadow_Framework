using System.Text.Json.Serialization.Metadata;
using MassTransit;
using Meadow_Framework.Core.Abstractions;
using Meadow_Framework.Core.Abstractions.Commands;
using Meadow_Framework.Core.Abstractions.Dispatchers;
using Meadow_Framework.Core.Abstractions.Events;
using Meadow_Framework.Core.Abstractions.Exceptions;
using Meadow_Framework.Core.Abstractions.Kernel;
using Meadow_Framework.Core.Abstractions.Queries;
using Meadow_Framework.Core.Abstractions.Repository;
using Meadow_Framework.Core.Infrastructure.Commands;
using Meadow_Framework.Core.Infrastructure.Dispatchers;
using Meadow_Framework.Core.Infrastructure.Events;
using Meadow_Framework.Core.Infrastructure.Exceptions;
using Meadow_Framework.Core.Infrastructure.Interceptors;
using Meadow_Framework.Core.Infrastructure.Jobs;
using Meadow_Framework.Core.Infrastructure.Queries.Dispatcher;
using Meadow_Framework.Core.Infrastructure.Repository;
using Meadow_Framework.Core.Infrastructure.Security;
using Meadow_Framework.Core.Infrastructure.Seed;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Quartz;

namespace Meadow_Framework.Core.Infrastructure;

/// <summary>
///     Provides extension methods for configuring the Meadow Framework services and middleware.
/// </summary>
public static class Extensions
{
    /// <summary>
    ///     Adds the framework services to the <see cref="IServiceCollection" />: commands, queries, events,
    ///     the event bus, error handling, and the outbox.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to which the services will be added.</param>
    /// <param name="configuration">The application configuration used for settings like the broker connection.</param>
    /// <param name="needEvents">Whether the message broker and outbox job should be registered. Default is true.</param>
    /// <param name="assemblies">The assemblies to scan for handlers (commands, queries, events) and contexts.</param>
    /// <returns>The modified <see cref="IServiceCollection" />.</returns>
    public static IServiceCollection AddFramework(
        this IServiceCollection services,
        IConfiguration configuration,
        bool needEvents = true,
        params Assembly[] assemblies)
    {
        return services.AddFramework(
            configuration,
            options => options.EnableEventBus = needEvents,
            assemblies);
    }

    /// <summary>
    ///     Adds the framework services to the <see cref="IServiceCollection" />: commands, queries, events,
    ///     the event bus, error handling, and the outbox.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to which the services will be added.</param>
    /// <param name="configuration">The application configuration used for settings like the broker connection.</param>
    /// <param name="configure">Configures the framework. See <see cref="MeadowOptions" />.</param>
    /// <param name="assemblies">The assemblies to scan for handlers (commands, queries, events) and contexts.</param>
    /// <returns>The modified <see cref="IServiceCollection" />.</returns>
    public static IServiceCollection AddFramework(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<MeadowOptions> configure,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configure);

        if (assemblies.Length == 0)
            throw new FrameworkException(
                "AddFramework requires at least one assembly to scan for handlers and database contexts.");

        var options = new MeadowOptions();
        configure(options);
        services.AddSingleton(options);

        foreach (var assembly in assemblies)
        {
            services.AddCommands(assembly);
            services.AddQueries(assembly);
            services.AddEvents(assembly);
        }

        var dbContextTypes = FindDbContexts(assemblies).ToList();

        services.AddInterceptors(options);
        services.AddDbContexts(dbContextTypes, options);

        if (options.EnableEventBus)
            services.AddEventBus(configuration, assemblies, dbContextTypes, options);

        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddErrorHandling();

        return services;
    }

    /// <summary>
    ///     Attaches the framework's <see cref="SaveChangesInterceptor" />s to a context the application
    ///     registers itself. Only needed when <see cref="MeadowOptions.ConfigureDbContext" /> is not used.
    /// </summary>
    /// <param name="builder">The options builder being configured.</param>
    /// <param name="serviceProvider">The provider the interceptors are resolved from.</param>
    /// <returns>The modified <see cref="DbContextOptionsBuilder" />.</returns>
    public static DbContextOptionsBuilder UseMeadow(
        this DbContextOptionsBuilder builder,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var options = serviceProvider.GetRequiredService<MeadowOptions>();

        builder.AddInterceptors(
            serviceProvider.GetRequiredService<UpdateAuditableEntitiesInterceptor>(),
            serviceProvider.GetRequiredService<UpdateDeletableEntitiesInterceptor>());

        // The two domain-event interceptors both drain the aggregates' event buffers, so exactly one of
        // them may be attached.
        builder.AddInterceptors(options.DomainEventDispatchMode switch
        {
            DomainEventDispatchMode.InProcess =>
                serviceProvider.GetRequiredService<DomainEventEntitiesInterceptor>(),
            DomainEventDispatchMode.Outbox =>
                (IInterceptor)serviceProvider.GetRequiredService<InsertOutboxMessagesInterceptor>(),
            _ => throw new FrameworkException(
                $"Unsupported {nameof(DomainEventDispatchMode)} '{options.DomainEventDispatchMode}'.")
        });

        return builder;
    }

    /// <summary>
    ///     Applies pending database migrations and seeds initial data.
    /// </summary>
    /// <param name="app">The <see cref="IApplicationBuilder" /> to configure.</param>
    /// <typeparam name="TContext">The type of the <see cref="DbContext" />.</typeparam>
    /// <returns>The modified <see cref="IApplicationBuilder" />.</returns>
    public static IApplicationBuilder UseMigration<TContext>(this IApplicationBuilder app)
        where TContext : DbContext
    {
        MigrateDatabaseAsync<TContext>(app.ApplicationServices).GetAwaiter().GetResult();

        SeedDataAsync(app.ApplicationServices).GetAwaiter().GetResult();

        return app;
    }

    private static async Task MigrateDatabaseAsync<TContext>(IServiceProvider serviceProvider)
        where TContext : DbContext
    {
        using var scope = serviceProvider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        await context.Database.MigrateAsync();
    }

    private static async Task SeedDataAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var seeders = scope.ServiceProvider.GetServices<IDataSeeder>();
        foreach (var seeder in seeders) await seeder.SeedAllAsync();
    }

    /// <summary>
    ///     Registers the framework's interceptors so they can be attached to a context's options.
    /// </summary>
    private static IServiceCollection AddInterceptors(this IServiceCollection services, MeadowOptions options)
    {
        services.AddScoped<UpdateAuditableEntitiesInterceptor>();
        services.AddScoped<UpdateDeletableEntitiesInterceptor>();

        if (options.DomainEventDispatchMode == DomainEventDispatchMode.InProcess)
            services.AddScoped<DomainEventEntitiesInterceptor>();
        else
            services.AddScoped<InsertOutboxMessagesInterceptor>();

        return services;
    }

    /// <summary>
    ///     Registers an outbox repository and unit of work for every discovered context, and the contexts
    ///     themselves when <see cref="MeadowOptions.ConfigureDbContext" /> says how to configure them.
    /// </summary>
    private static IServiceCollection AddDbContexts(
        this IServiceCollection services,
        IReadOnlyCollection<Type> dbContextTypes,
        MeadowOptions options)
    {
        foreach (var dbContextType in dbContextTypes)
        {
            // Each context owns its own outbox table, so the repository is bound to that context.
            services.AddScoped(
                typeof(IOutboxRepository<>).MakeGenericType(dbContextType),
                typeof(OutboxRepository<>).MakeGenericType(dbContextType));

            services.AddScoped(typeof(UnitOfWork<>).MakeGenericType(dbContextType));

            if (options.ConfigureDbContext is null)
                continue;

            AddDbContext(services, dbContextType, options);
        }

        return services;
    }

    private static void AddDbContext(IServiceCollection services, Type dbContextType, MeadowOptions options)
    {
        var addDbContextMethod = typeof(EntityFrameworkServiceCollectionExtensions)
            .GetMethods()
            .First(m => m.Name == nameof(EntityFrameworkServiceCollectionExtensions.AddDbContext)
                        && m.IsGenericMethod
                        && m.GetGenericArguments().Length == 1
                        && m.GetParameters().Length == 4
                        && m.GetParameters()[1].ParameterType ==
                        typeof(Action<IServiceProvider, DbContextOptionsBuilder>))
            .MakeGenericMethod(dbContextType);

        Action<IServiceProvider, DbContextOptionsBuilder> configureOptions = (sp, builder) =>
        {
            options.ConfigureDbContext!(sp, builder);
            builder.UseMeadow(sp);
        };

        addDbContextMethod.Invoke(null,
            [services, configureOptions, ServiceLifetime.Scoped, ServiceLifetime.Scoped]);
    }

    /// <summary>
    ///     Adds command dispatching and command handlers to the <see cref="IServiceCollection" />.
    ///     Registers all types that implement <see cref="ICommandHandler{TCommand,TResult}" /> and
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
        foreach (var type in commandHandlerTypes)
        {
            IEnumerable<Type> interfaces = type.GetInterfaces()
                .Where(i =>
                    i.IsGenericType &&
                    (i.GetGenericTypeDefinition() == typeof(ICommandHandler<>)
                     || i.GetGenericTypeDefinition() == typeof(ICommandHandler<,>)));

            foreach (var interfaceType in interfaces) services.AddScoped(interfaceType, type);
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
        foreach (var type in queryHandlerTypes)
        {
            var interfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQueryHandler<,>));

            foreach (var interfaceType in interfaces) services.AddScoped(interfaceType, type);
        }

        return services;
    }

    /// <summary>
    ///     Adds event dispatching and event handlers to the <see cref="IServiceCollection" />.
    ///     Registers all types that implement <see cref="IDomainEventHandler{TEvent}" />.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add the services to.</param>
    /// <param name="assembly">The assembly to scan for event handlers.</param>
    /// <returns>The modified <see cref="IServiceCollection" />.</returns>
    private static IServiceCollection AddEvents(
        this IServiceCollection services,
        Assembly assembly)
    {
        services.AddScoped<IEventDispatcher, EventDispatcher>();

        IEnumerable<Type> eventHandlerTypes = assembly.GetTypes()
            .Where(t =>
                t is { IsClass: true, IsAbstract: false } &&
                t.GetInterfaces().Any(i =>
                    i.IsGenericType &&
                    i.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>)));

        foreach (var type in eventHandlerTypes)
        {
            IEnumerable<Type> interfaces = type.GetInterfaces()
                .Where(i =>
                    i.IsGenericType &&
                    i.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>));

            foreach (var interfaceType in interfaces) services.AddScoped(interfaceType, type);
        }

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

    /// <summary>
    ///     Adds and configures the EventBus using MassTransit and RabbitMQ, and schedules one outbox job per
    ///     discovered context.
    /// </summary>
    private static IServiceCollection AddEventBus(
        this IServiceCollection services,
        IConfiguration configuration,
        IEnumerable<Assembly> assemblies,
        IReadOnlyCollection<Type> dbContextTypes,
        MeadowOptions options)
    {
        var rabbitMq = ReadRabbitMqOptions(configuration);

        services.AddQuartz(q =>
        {
            foreach (var dbContextType in dbContextTypes)
            {
                var jobType = typeof(OutboxJob<>).MakeGenericType(dbContextType);

                var jobKey = new JobKey($"{dbContextType.Name}.OutboxJob");

                q.AddJob(jobType, jobKey);

                q.AddTrigger(t => t
                    .ForJob(jobKey)
                    .WithIdentity($"{jobKey.Name}.trigger")
                    .WithSimpleSchedule(x => x
                        .WithInterval(options.OutboxPollingInterval)
                        .RepeatForever()));
            }
        });

        services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

        services.AddMassTransit(configurator =>
        {
            var consumers = FindConsumers(assemblies).ToList();

            foreach (var consumer in consumers)
                configurator.AddConsumer(consumer);

            ConfigureRabbitMq(configurator, rabbitMq, consumers);
        });

        return services;
    }

    /// <summary>
    ///     Reads and validates the broker configuration, failing at startup rather than on first publish.
    /// </summary>
    private static RabbitMqOptions ReadRabbitMqOptions(IConfiguration configuration)
    {
        const string sectionName = "AppConfiguration:RabbitMQ";

        var rabbitMq = configuration.GetSection(sectionName).Get<RabbitMqOptions>()
                       ?? throw new FrameworkException(
                           $"The event bus is enabled but no configuration was found at '{sectionName}'. " +
                           "Provide it, or disable the bus with AddFramework(..., needEvents: false).");

        if (string.IsNullOrWhiteSpace(rabbitMq.Host))
            throw new FrameworkException($"'{sectionName}:Host' is required when the event bus is enabled.");

        return rabbitMq;
    }

    /// <summary>
    ///     Configures RabbitMQ host, credentials, and receive endpoints for consumers.
    /// </summary>
    private static void ConfigureRabbitMq(
        IBusRegistrationConfigurator configurator,
        RabbitMqOptions config,
        List<Type> consumers)
    {
        configurator.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(config.Host, h =>
            {
                h.Username(config.UserName ?? string.Empty);
                h.Password(config.Password ?? string.Empty);
            });

            ConfigureRabbitMqSensitiveData(cfg);

            // Configure receive endpoints
            foreach (var consumerType in consumers)
                cfg.ReceiveEndpoint(consumerType.Name,
                    endpoint => { endpoint.ConfigureConsumer(context, consumerType); });
        });
    }

    /// <summary>
    ///     Configures JSON serialization options for RabbitMQ to handle sensitive data masking.
    /// </summary>
    private static void ConfigureRabbitMqSensitiveData(IRabbitMqBusFactoryConfigurator cfg)
    {
        cfg.ConfigureJsonSerializerOptions(options =>
        {
            var resolver = options.TypeInfoResolver
                           ?? new DefaultJsonTypeInfoResolver();

            options.TypeInfoResolver = resolver.WithAddedModifier(typeInfo =>
            {
                foreach (var property in typeInfo.Properties)
                {
                    var attribute = (SensitiveDataAttribute?)property.AttributeProvider?
                        .GetCustomAttributes(typeof(SensitiveDataAttribute), false)
                        .FirstOrDefault();

                    if (attribute is not null)
                        property.CustomConverter = new MaskedStringJsonConverter(attribute.Mask);
                }
            });

            return options;
        });
    }

    /// <summary>
    ///     Finds and returns all classes that implement <see cref="IEventConsumer{TEvent}" /> from the given assemblies.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan.</param>
    /// <returns>A collection of consumer types implementing <see cref="IEventConsumer{TEvent}" />.</returns>
    private static IEnumerable<Type> FindConsumers(IEnumerable<Assembly> assemblies)
    {
        var consumerInterfaceType = typeof(IEventConsumer<>);

        return assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => type.GetInterfaces()
                .Any(interfaceType =>
                    interfaceType.IsGenericType &&
                    interfaceType.GetGenericTypeDefinition() == consumerInterfaceType));
    }

    /// <summary>
    ///     Finds the framework-aware database contexts to bind an outbox and outbox job to.
    /// </summary>
    private static IEnumerable<Type> FindDbContexts(IEnumerable<Assembly> assemblies)
    {
        return assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => typeof(DbContext).IsAssignableFrom(type))
            .Where(type => typeof(IDbContext).IsAssignableFrom(type));
    }
}
