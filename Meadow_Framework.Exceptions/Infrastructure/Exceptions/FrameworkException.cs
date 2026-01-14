namespace Meadow_Framework.Exceptions.Infrastructure.Exceptions;

/// <summary>
/// </summary>
/// <param name="message"></param>
public sealed class FrameworkException(string message) : InflowException(message)
{
}