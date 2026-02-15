using Meadow.Abstractions.Exceptions;

namespace Meadow.AspNetCore.Exceptions;

public sealed class FrameworkException(string message) : InflowException(message)
{
}