using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using System.Reflection;
using System.Text.Json.Serialization.Metadata;
using Meadow_Framework.Core.Abstractions;
using Meadow_Framework.Core.Abstractions.Commands;
using Meadow_Framework.Core.Abstractions.Dispatchers;
using Meadow_Framework.Core.Abstractions.Events;
using Meadow_Framework.Core.Abstractions.Exceptions;
using Meadow_Framework.Core.Abstractions.Queries;
using Meadow_Framework.Core.Abstractions.Repository;
using Meadow_Framework.Core.Infrastructure.Commands;
using Meadow_Framework.Core.Infrastructure.Context;
using Meadow_Framework.Core.Infrastructure.Dispatchers;
using Meadow_Framework.Core.Infrastructure.Events;
using Meadow_Framework.Core.Infrastructure.Exceptions;
using Meadow_Framework.Core.Infrastructure.Jobs;
using Meadow_Framework.Core.Infrastructure.Queries.Dispatcher;
using IEventDispatcher = Meadow_Framework.Core.Abstractions.Dispatchers.IEventDispatcher;

namespace Meadow_Framework.Core.Infrastructure;

/// <summary>
///
/// </summary>
public static class Extensions
{
    public static IServiceCollection AddFramework(
        this IServiceCollection services,
        IConfiguration configuration,
        bool needEvents = true,
        params Assembly[] assemblies)
    {
        foreach (var assembly in assemblies)
        {
            services.AddCommands(assembly);
            services.AddQueries(assembly);

        //    if (needEvents)
                services.AddEvents(assembly);
        }

        if (needEvents)
            services.AddEventBus(configuration, assemblies);

        services.AddScoped<IDispatcher, Dispatcher>();

        services.AddErrorHandling();

        return services;
    }

    private static IServiceCollection AddEventBus(
        this IServiceCollection services,
        IConfiguration configuration,
        IEnumerable<Assembly> assemblies)
    {
        var dbContextTypes = FindDbContexts(assemblies).ToList();

        var rabbitMq = configuration.GetSection("AppConfiguration:RabbitMQ").Get<RabbitMqOptions>();
        var encryptionKey = configuration["AppConfiguration:RabbitMQ:EncryptionKey"];

        services.AddQuartz(q =>
        {
            q.UseMicrosoftDependencyInjectionJobFactory();

            foreach (var dbContextType in dbContextTypes)
            {
                var jobType = typeof(OutboxJob<>).MakeGenericType(dbContextType);

                var jobKey = new JobKey($"{dbContextType.Name}.OutboxJob");

                q.AddJob(jobType, jobKey);

                q.AddTrigger(t => t
                    .ForJob(jobKey)
                    .WithIdentity($"{jobKey.Name}.trigger")
                    .WithSimpleSchedule(x => x
                        .WithInterval(TimeSpan.FromSeconds(10))
                        .RepeatForever()));
            }
        });

        services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

        services.AddMassTransit(configurator =>
        {
            var consumers = FindConsumers(assemblies).ToList();

            foreach (var consumer in consumers)
                configurator.AddConsumer(consumer);

            ConfigureRabbitMq(configurator, rabbitMq, encryptionKey, consumers);
        });

        return services;
    }

    // =========================
    // COMMANDS
    // =========================

    private static IServiceCollection AddCommands(this IServiceCollection services, Assembly assembly)
    {
        services.AddScoped<ICommandDispatcher, CommandDispatcher>();

        var handlers = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } &&
                        t.GetInterfaces().Any(i =>
                            i.IsGenericType &&
                            (i.GetGenericTypeDefinition() == typeof(ICommandHandler<,>) ||
                             i.GetGenericTypeDefinition() == typeof(ICommandHandler<>))));

        foreach (var handler in handlers)
        {
            foreach (var i in handler.GetInterfaces()
                         .Where(i => i.IsGenericType))
            {
                services.AddScoped(i, handler);
            }
        }

        return services;
    }

    // =========================
    // QUERIES
    // =========================

    private static IServiceCollection AddQueries(this IServiceCollection services, Assembly assembly)
    {
        services.AddScoped<IQueryDispatcher, QueryDispatcher>();

        IEnumerable<Type> handlers = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } &&
                        t.GetInterfaces().Any(i =>
                            i.IsGenericType &&
                            i.GetGenericTypeDefinition() == typeof(IQueryHandler<,>)));

        foreach (var handler in handlers)
        {
            foreach (var i in handler.GetInterfaces()
                         .Where(i => i.IsGenericType))
            {
                services.AddScoped(i, handler);
            }
        }

        return services;
    }

    // =========================
    // EVENTS
    // =========================

    private static IServiceCollection AddEvents(this IServiceCollection services, Assembly assembly)
    {
        services.AddScoped<IEventDispatcher, EventDispatcher>();

        IEnumerable<Type> handlers = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } &&
                        t.GetInterfaces().Any(i =>
                            i.IsGenericType &&
                            i.GetGenericTypeDefinition() == typeof(IEventHandler<>)));

        foreach (Type handler in handlers)
        {
            foreach (var i in handler.GetInterfaces()
                         .Where(i => i.IsGenericType))
            {
                services.AddScoped(i, handler);
            }
        }

        return services;
    }

    // =========================
    // ERROR HANDLING
    // =========================

    private static IServiceCollection AddErrorHandling(this IServiceCollection services)
    {
        return services
            .AddScoped<ErrorHandlerMiddleware>()
            .AddSingleton<IExceptionToResponseMapper, ExceptionToResponseMapper>()
            .AddSingleton<IExceptionCompositionRoot, ExceptionCompositionRoot>();
    }

    public static IApplicationBuilder UseErrorHandling(this IApplicationBuilder app)
        => app.UseMiddleware<ErrorHandlerMiddleware>();

    // =========================
    // DB CONTEXTS
    // =========================

    private static IEnumerable<Type> FindDbContexts(IEnumerable<Assembly> assemblies)
    {
        var dbContextInterface = typeof(IDbContext);

        return assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract &&
                        typeof(DbContext).IsAssignableFrom(t) &&
                        t.GetInterfaces().Contains(dbContextInterface));
    }

    // =========================
    // CONSUMERS
    // =========================

    private static IEnumerable<Type> FindConsumers(IEnumerable<Assembly> assemblies)
    {
        var consumerInterface = typeof(IEventConsumer<>);

        return assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false } &&
                        t.GetInterfaces().Any(i =>
                            i.IsGenericType &&
                            i.GetGenericTypeDefinition() == consumerInterface));
    }

    // =========================
    // RABBITMQ
    // =========================

    private static void ConfigureRabbitMq(
        IBusRegistrationConfigurator configurator,
        RabbitMqOptions? config,
        string? encryptionKey,
        List<Type> consumers)
    {
        configurator.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(config!.Host, h =>
            {
                h.Username(config.UserName);
                h.Password(config.Password);
            });

            foreach (var consumerType in consumers)
            {
                cfg.ReceiveEndpoint(consumerType.Name, e =>
                {
                    e.ConfigureConsumer(context, consumerType);
                });
            }
        });
    }
}