using System.Diagnostics;

namespace Meadow_Framework.Exceptions.Infrastructure.Exceptions;

/// <summary>
/// </summary>
public class ApiProblemDetails : ProblemDetails
{
    public bool IsApiProblemDetails { get; } = true;
    public string? TraceId { get; } = Activity.Current?.TraceId.ToString();
    public string ExternalEndpoint { get; set; }
    public Dictionary<string, string[]> ValidationErrors { get; set; } = new();
    public LogLevel Severity { get; set; } = LogLevel.Error;
    public new IDictionary<string, object> Extensions { get; set; } = new Dictionary<string, object>();
}