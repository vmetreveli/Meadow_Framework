using Meadow_Framework.Exceptions.Abstractions.Exceptions;

namespace Meadow_Framework.Exceptions.Infrastructure.Exceptions;

public sealed class FrameworkException(string message) : InflowException(message)
{
}