using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Meadow.Abstractions.Diagnostics;

/// <summary>
///     Central diagnostics hub defining traces and metrics for the framework.
/// </summary>
public static class MeadowDiagnostics
{
    /// <summary>
    ///     The service name used for the ActivitySource and Meter.
    /// </summary>
    public const string ServiceName = "Meadow.Framework";
    
    /// <summary>
    ///     The central ActivitySource for tracing.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ServiceName);
    
    /// <summary>
    ///     The central Meter for metrics.
    /// </summary>
    public static readonly Meter Meter = new(ServiceName);
}
