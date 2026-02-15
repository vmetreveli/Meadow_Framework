using System.Text.Json.Serialization.Metadata;
using MassTransit;
using Meadow.Abstractions.Events;
using Meadow.Abstractions.Security;
using Meadow.Core.Security;
using Microsoft.Extensions.Configuration;
using Quartz;

namespace Meadow.MassTransit;

/// <summary>
///     Extension methods for registering Meadow.MassTransit services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds Meadow MassTransit services: event dispatcher, MassTransit with RabbitMQ, Quartz for outbox processing.
    /// </summary>
    public static IServiceCollection AddMeadowMassTransit(
        this IServiceCollection services,
        IConfiguration configuration,
        params Assembly[] assemblies)
    {
        // Register event handlers from assemblies
        foreach (var assembly in assemblies)
            services.AddEvents(assembly);

        var config = configuration.GetSection("AppConfiguration:RabbitMQ").Get<RabbitMqOptions>();
        var encryptionKey = configuration["AppConfiguration:RabbitMQ:EncryptionKey"];

        // Add Quartz.NET for outbox job scheduling
        services.AddQuartz(q =>
        {
            q.AddJobAndTrigger<OutboxJob>(configuration);
        });

        services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

        services.AddMassTransit(configurator =>
        {
            var consumers = FindConsumers(assemblies).ToList();

            foreach (var consumer in consumers)
                configurator.AddConsumer(consumer);

            ConfigureRabbitMq(configurator, config, encryptionKey, consumers);
        });

        return services;
    }

    private static IServiceCollection AddEvents(this IServiceCollection services, Assembly assembly)
    {
        services.AddScoped<IEventDispatcher, EventDispatcher>();

        var eventHandlerTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.GetInterfaces()
                .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>)));

        foreach (Type type in eventHandlerTypes)
        {
            var interfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>));

            foreach (Type interfaceType in interfaces) services.AddScoped(interfaceType, type);
        }

        return services;
    }

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

            ConfigureRabbitMqSensitiveData(cfg);

            foreach (Type consumerType in consumers)
            {
                cfg.ReceiveEndpoint(consumerType.Name, endpoint =>
                {
                    endpoint.ConfigureConsumer(context, consumerType);
                });
            }
        });
    }

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

    private static IEnumerable<Type> FindConsumers(IEnumerable<Assembly> assemblies)
    {
        var consumerInterfaceType = typeof(IEventConsumer<>);
        var consumer = new List<Type>();

        foreach (var assembly in assemblies)
            consumer.AddRange(assembly.GetTypes()
                .Where(type => type is { IsClass: true, IsAbstract: false })
                .Where(type => type.GetInterfaces()
                    .ToList()
                    .Exists(interfaceType =>
                        interfaceType.IsGenericType &&
                        interfaceType.GetGenericTypeDefinition() == consumerInterfaceType)));

        return consumer;
    }

    private static void AddJobAndTrigger<T>(
        this IServiceCollectionQuartzConfigurator quartz,
        IConfiguration config)
        where T : IJob
    {
        var jobName = typeof(T).Name;
        var configKey = $"AppConfiguration:Quartz:{jobName}";
        var cronSchedule = config[configKey];

        if (string.IsNullOrEmpty(cronSchedule))
            throw new Exception($"No Quartz.NET Cron schedule found for job in configuration at {configKey}");

        var jobKey = new JobKey(jobName);

        quartz.AddJob<T>(opts => opts.WithIdentity(jobKey));
        quartz.AddTrigger(opts => opts
            .ForJob(jobKey)
            .WithIdentity(jobName + "-trigger")
            .WithCronSchedule(cronSchedule));
    }
}
