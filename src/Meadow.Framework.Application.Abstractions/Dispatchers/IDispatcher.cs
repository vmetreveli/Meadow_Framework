using Meadow.Framework.Application.Abstractions.Commands;
using Meadow.Framework.Application.Abstractions.Queries;

namespace Meadow.Framework.Application.Abstractions.Dispatchers;

/// <summary>
///     Defines the contract for a dispatcher that can handle both commands and queries.
/// </summary>
/// <remarks>
///     This interface combines the responsibilities of both <see cref="ICommandDispatcher" /> and
///     <see cref="IQueryDispatcher" />. Implementations of this interface are expected to
///     support dispatching commands and queries, providing a unified way to handle
///     different types of operations within the application.
/// </remarks>
public interface IDispatcher : ICommandDispatcher, IQueryDispatcher
{
}
