using System;

namespace ProjectMaelstrom.Utilities;

/// <summary>
/// Immutable telemetry snapshot from read-only client process observation.
/// Nullable fields represent values that may be unmapped or temporarily unavailable.
/// </summary>
public record W101TelemetryData(
    double? X = null,
    double? Y = null,
    double? Z = null,
    double? Yaw = null,
    string? ZoneId = null,
    int? CurrentHealth = null,
    int? MaxHealth = null,
    int? CurrentMana = null,
    int? MaxMana = null,
    int? CurrentEnergy = null,
    int? MaxEnergy = null,
    int? Pips = null,
    int? PowerPips = null,
    int? ShadowPips = null,
    bool? InCombat = null,
    bool? IsMyTurn = null,
    DateTime TimestampUtc = default
)
{
    public bool HasPosition => X.HasValue && Y.HasValue && Z.HasValue &&
        double.IsFinite(X.Value) && double.IsFinite(Y.Value) && double.IsFinite(Z.Value);

    public bool IsValid =>
        (!X.HasValue || double.IsFinite(X.Value)) &&
        (!Y.HasValue || double.IsFinite(Y.Value)) &&
        (!Z.HasValue || double.IsFinite(Z.Value)) &&
        (!Yaw.HasValue || double.IsFinite(Yaw.Value)) &&
        (!CurrentHealth.HasValue || CurrentHealth.Value >= 0) &&
        (!MaxHealth.HasValue || MaxHealth.Value > 0) &&
        (!Pips.HasValue || Pips.Value is >= 0 and <= 14);
}

/// <summary>
/// Contract for additive telemetry providers.
/// Implementations must be strictly read-only and non-blocking.
/// </summary>
public interface IW101TelemetryProvider
{
    string Name { get; }
    bool IsAvailable { get; }
    W101TelemetryData? GetTelemetry();
}

/// <summary>
/// Default null provider ensuring graceful fallback to pure visual perception.
/// </summary>
public sealed class NullW101TelemetryProvider : IW101TelemetryProvider
{
    public static NullW101TelemetryProvider Instance { get; } = new();
    public string Name => "Null / UI-Only Baseline";
    public bool IsAvailable => false;
    public W101TelemetryData? GetTelemetry() => null;
}

/// <summary>
/// Service coordinating telemetry access with policy enforcement.
/// </summary>
public static class W101TelemetryService
{
    private static IW101TelemetryProvider _provider = NullW101TelemetryProvider.Instance;
    private static readonly object _sync = new();

    public static IW101TelemetryProvider CurrentProvider
    {
        get
        {
            if (!W101InteractionPolicy.IsProcessObservationAllowed)
                return NullW101TelemetryProvider.Instance;
            return _provider;
        }
    }

    public static void SetProvider(IW101TelemetryProvider? provider)
    {
        lock (_sync)
        {
            _provider = provider ?? NullW101TelemetryProvider.Instance;
        }
    }

    public static void ResetToDefault()
    {
        lock (_sync)
        {
            _provider = NullW101TelemetryProvider.Instance;
        }
    }

    public static bool TryGetTelemetry(out W101TelemetryData? telemetry, TimeSpan? maxAge = null)
    {
        telemetry = null;
        if (!W101InteractionPolicy.IsProcessObservationAllowed)
            return false;

        try
        {
            var provider = CurrentProvider;
            if (!provider.IsAvailable)
                return false;

            var data = provider.GetTelemetry();
            if (data == null || !data.IsValid)
                return false;

            // Reject missing, future, or stale timestamps
            var allowedAge = maxAge ?? TimeSpan.FromSeconds(3);
            var now = DateTime.UtcNow;
            if (data.TimestampUtc == default || data.TimestampUtc > now.AddSeconds(2) || now - data.TimestampUtc > allowedAge)
                return false;

            telemetry = data;
            return true;
        }
        catch
        {
            telemetry = null;
            return false;
        }
    }
}
