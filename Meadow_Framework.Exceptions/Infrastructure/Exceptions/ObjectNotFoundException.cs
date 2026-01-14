namespace Meadow_Framework.Exceptions.Infrastructure.Exceptions;

/// <summary>
///     Represents an exception thrown when a requested object is not found in the system.
/// </summary>
public sealed class ObjectNotFoundException : InflowException
{
    /// <summary>
    /// </summary>
    /// <param name="objectType"></param>
    /// <param name="objectId"></param>
    public ObjectNotFoundException(string objectType, string? objectId)
        : base("OBJECT_NOT_FOUND", "Object not found.",
            $"{objectType}:{(objectId is not null ? $":{objectId}" : null)} not found", null, LogLevel.Warning)
    {
    }


    /// <summary>
    /// </summary>
    /// <param name="objectType"></param>
    /// <param name="objectId"></param>
    /// <param name="logLevel"></param>
    public ObjectNotFoundException(string objectType, string? objectId, LogLevel logLevel)
        : base("OBJECT_NOT_FOUND", "Object not found.",
            $"{objectType}:{(objectId is not null ? $":{objectId}" : null)} not found", null, logLevel)
    {
    }
}