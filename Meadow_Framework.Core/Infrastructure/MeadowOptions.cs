namespace Meadow_Framework.Core.Infrastructure;

/// <summary>
///     Determines what happens to the domain events buffered on an aggregate root when it is saved.
/// </summary>
public enum DomainEventDispatchMode
{
    /// <summary>
    ///     Dispatch domain events to in-process <see cref="Abstractions.Kernel.IDomainEventHandler{TEvent}" />
    ///     implementations as part of the same <c>SaveChanges</c> call and transaction.
    /// </summary>
    InProcess = 0,

    /// <summary>
    ///     Write domain events to the outbox in the same transaction, to be published to the message broker
    ///     by the outbox job.
    /// </summary>
    Outbox = 1
}

/// <summary>
///     Configures the Meadow framework services registered by
///     <see cref="Extensions.AddFramework(IServiceCollection, Microsoft.Extensions.Configuration.IConfiguration, Action{MeadowOptions}, System.Reflection.Assembly[])" />.
/// </summary>
public sealed class MeadowOptions
{
    /// <summary>
    ///     Registers the MassTransit/RabbitMQ event bus and the outbox job. Disable this for applications that
    ///     have no message broker. Defaults to <see langword="true" />.
    /// </summary>
    public bool EnableEventBus { get; set; } = true;

    /// <summary>
    ///     Selects how domain events buffered on aggregate roots are handled on save.
    ///     Defaults to <see cref="DomainEventDispatchMode.InProcess" />.
    /// </summary>
    public DomainEventDispatchMode DomainEventDispatchMode { get; set; } = DomainEventDispatchMode.InProcess;

    /// <summary>
    ///     How often the outbox job looks for messages to publish. Defaults to 10 seconds.
    /// </summary>
    public TimeSpan OutboxPollingInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     The maximum number of outbox messages published per job run. Defaults to 100.
    /// </summary>
    public int OutboxBatchSize { get; set; } = 100;

    /// <summary>
    ///     The number of failed publication attempts after which an outbox message is marked
    ///     <see cref="Abstractions.Outbox.OutboxMessageState.Failed" /> and stops being retried. Defaults to 5.
    /// </summary>
    public int OutboxMaxRetries { get; set; } = 5;

    /// <summary>
    ///     Configures the <see cref="DbContext" /> registration for every discovered
    ///     <see cref="Context.BaseDbContext" />. When left <see langword="null" /> the framework registers no
    ///     context and the application is responsible for doing so — in which case it must call
    ///     <see cref="Extensions.UseMeadow" /> on its options builder to attach the framework's interceptors.
    /// </summary>
    public Action<IServiceProvider, DbContextOptionsBuilder>? ConfigureDbContext { get; set; }
}
