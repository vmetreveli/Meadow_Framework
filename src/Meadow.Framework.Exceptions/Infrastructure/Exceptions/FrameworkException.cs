using Meadow.Framework.Exceptions.Abstractions.Exceptions;

namespace Meadow.Framework.Exceptions.Infrastructure.Exceptions;

/// <summary>
/// </summary>
/// <param name="message"></param>
public sealed class FrameworkException(string message) : InflowException(message)
{
}