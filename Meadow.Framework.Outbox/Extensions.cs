using System.Text.Json.Serialization.Metadata;
using MassTransit;
using Meadow_Framework.Core.Abstractions.Events;
using Meadow_Framework.Core.Abstractions.Repository;
using Meadow_Framework.Core.Infrastructure.Repository;
using Meadow_Framework.Core.Infrastructure.Security;
using Meadow_Framework.Core.Infrastructure.Seed;
using Meadow_Framework.Exceptions.Infrastructure.Exceptions;
using Meadow.Framework.Outbox.Abstractions.Repository;
using Meadow.Framework.Outbox.Infrastructure.Context;
using Meadow.Framework.Outbox.Infrastructure.Repository;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Meadow.Framework.Outbox;

/// <summary>
///     Provides extension methods for configuring the Meadow Framework services and middleware.
/// </summary>
public static class Extensions
{
    /// <summary>
    ///     Extension method to add the framework services to the <see cref="IServiceCollection" />.
    ///     This includes commands, queries, events, event bus, error handling, and database configuration.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to which the services will be added.</param>
    /// <param>The application configuration used for settings like database connections.</param>
    /// <returns>The modified <see cref="IServiceCollection" />.</returns>
    public static IServiceCollection AddFramework(this IServiceCollection services)
    {

        services.AddScoped<IOutboxRepository, OutboxRepository>();


        services.AddScoped<IUnitOfWork, UnitOfWork<BaseDbContext>>();


        return services;
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
        using IServiceScope scope = serviceProvider.CreateScope();

        TContext context = scope.ServiceProvider.GetRequiredService<TContext>();
        await context.Database.MigrateAsync();
    }

    private static async Task SeedDataAsync(IServiceProvider serviceProvider)
    {
        using IServiceScope scope = serviceProvider.CreateScope();
        IEnumerable<IDataSeeder> seeders = scope.ServiceProvider.GetServices<IDataSeeder>();
        foreach (IDataSeeder seeder in seeders) await seeder.SeedAllAsync();
    }



    // /// <summary>
    // ///     Adds event dispatching and event handlers to the <see cref="IServiceCollection" />.
    // ///     Registers all types that implement <see cref="IEventHandler{TEvent}" />.
    // /// </summary>
    // /// <param name="services">The <see cref="IServiceCollection" /> to add the services to.</param>
    // /// <param name="assembly">The assembly to scan for event handlers.</param>
    // /// <returns>The modified <see cref="IServiceCollection" />.</returns>
    // private static IServiceCollection AddEvents(this IServiceCollection services, Assembly assembly)
    // {
    //     services.AddScoped<IEventDispatcher, EventDispatcher>();
    //
    //     // Get all types implementing IEventHandler<>
    //     IEnumerable<Type> eventHandlerTypes = assembly.GetTypes()
    //         .Where(t => t is { IsClass: true, IsAbstract: false } && t.GetInterfaces()
    //             .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>)));
    //
    //     // Register each event handler as scoped
    //     foreach (Type type in eventHandlerTypes)
    //     {
    //         IEnumerable<Type> interfaces = type.GetInterfaces()
    //             .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>));
    //
    //         foreach (Type interfaceType in interfaces) services.AddScoped(interfaceType, type);
    //     }
    //
    //     return services;
    // }




    /// <summary>
    ///     Configures RabbitMQ host, credentials, and receive endpoints for consumers.
    /// </summary>
    private static void ConfigureRabbitMq(IBusRegistrationConfigurator configurator, RabbitMqOptions? config, string? encryptionKey, List<Type> consumers)
    {
        configurator.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(config!.Host, h =>
            {
                h.Username(config.UserName);
                h.Password(config.Password);
            });

            ConfigureRabbitMqSensitiveData(cfg);

            // Configure receive endpoints
            foreach (Type consumerType in consumers)
            {
                cfg.ReceiveEndpoint(consumerType.Name, endpoint =>
                {
                    endpoint.ConfigureConsumer(context, consumerType);
                });
            }
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
                     if (property.AttributeProvider?
                             .IsDefined(typeof(SensitiveDataAttribute), inherit: false) == true)
                     {
                         SensitiveDataAttribute attribute = (SensitiveDataAttribute)
                             property.AttributeProvider!
                                 .GetCustomAttributes(typeof(SensitiveDataAttribute), false)
                                 .FirstOrDefault()!;

                         property.CustomConverter =
                             new MaskedStringJsonConverter(attribute.Mask);
                     }
                 }
             });

            return options;
        });
    }


    /// <summary>
    ///     Adds and schedules a Quartz job using a cron schedule from configuration.
    /// </summary>
    /// <typeparam name="T">The type of the Quartz job to schedule.</typeparam>
    /// <param name="quartz">The Quartz.NET configuration object.</param>
    /// <param name="config">The configuration object used to retrieve the cron schedule.</param>
    /// <exception cref="Exception"></exception>
    private static void AddJobAndTrigger<T>(
        this IServiceCollectionQuartzConfigurator quartz,
        IConfiguration config)
        where T : IJob
    {
        var jobName = typeof(T).Name;
        var configKey = $"AppConfiguration:Quartz:{jobName}";
        var cronSchedule = config[configKey];

        // Validate that the cron schedule exists
        if (string.IsNullOrEmpty(cronSchedule))
            throw new FrameworkException($"No Quartz.NET Cron schedule found for job in configuration at {configKey}");

        var jobKey = new JobKey(jobName);

        // Register the job and trigger using the cron schedule from configuration
        quartz.AddJob<T>(opts => opts.WithIdentity(jobKey));
        quartz.AddTrigger(opts => opts
            .ForJob(jobKey)
            .WithIdentity(jobName + "-trigger")
            .WithCronSchedule(cronSchedule));
    }
}