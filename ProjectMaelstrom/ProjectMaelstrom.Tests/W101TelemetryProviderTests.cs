using System;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class W101TelemetryProviderTests
{
    private class MockTelemetryProvider : IW101TelemetryProvider
    {
        public string Name => "MockMemoryProvider";
        public bool IsAvailable { get; set; } = true;
        public W101TelemetryData? DataToReturn { get; set; }

        public W101TelemetryData? GetTelemetry() => DataToReturn;
    }

    [Fact]
    public void DefaultProviderIsNullAndReturnsNullTelemetry()
    {
        W101TelemetryService.ResetToDefault();
        var provider = W101TelemetryService.CurrentProvider;
        Assert.False(provider.IsAvailable);
        Assert.Null(provider.GetTelemetry());
        Assert.False(W101TelemetryService.TryGetTelemetry(out _));
    }

    [Fact]
    public void ProviderBlockedWhenPolicyDisabled()
    {
        try
        {
            W101InteractionPolicy.IsProcessObservationAllowed = false;
            var mock = new MockTelemetryProvider
            {
                DataToReturn = new W101TelemetryData(10.0, 20.0, 30.0, ZoneId: "Commons")
            };
            W101TelemetryService.SetProvider(mock);

            Assert.IsType<NullW101TelemetryProvider>(W101TelemetryService.CurrentProvider);
            Assert.False(W101TelemetryService.TryGetTelemetry(out var data));
            Assert.Null(data);
        }
        finally
        {
            W101TelemetryService.ResetToDefault();
            W101InteractionPolicy.IsProcessObservationAllowed = false;
        }
    }

    [Fact]
    public void ProviderSuppliesAdditiveDataWhenPolicyExceptionActive()
    {
        try
        {
            W101InteractionPolicy.IsProcessObservationAllowed = true;
            var expected = new W101TelemetryData(
                X: 100.5,
                Y: 200.25,
                Z: 15.0,
                Yaw: 1.57,
                ZoneId: "PetPavilion",
                CurrentHealth: 2500,
                MaxHealth: 3000,
                CurrentMana: 250,
                MaxMana: 250,
                CurrentEnergy: 80,
                MaxEnergy: 100,
                InCombat: false,
                TimestampUtc: DateTime.UtcNow
            );

            var mock = new MockTelemetryProvider { DataToReturn = expected };
            W101TelemetryService.SetProvider(mock);

            Assert.True(W101TelemetryService.TryGetTelemetry(out var telemetry));
            Assert.NotNull(telemetry);
            Assert.True(telemetry.HasPosition);
            Assert.Equal(100.5, telemetry.X);
            Assert.Equal(200.25, telemetry.Y);
            Assert.Equal(15.0, telemetry.Z);
            Assert.Equal("PetPavilion", telemetry.ZoneId);
            Assert.Equal(80, telemetry.CurrentEnergy);
        }
        finally
        {
            W101TelemetryService.ResetToDefault();
            W101InteractionPolicy.IsProcessObservationAllowed = false;
        }
    }

    [Fact]
    public void UnavailableProviderGracefullyFailsWithoutError()
    {
        try
        {
            W101InteractionPolicy.IsProcessObservationAllowed = true;
            var mock = new MockTelemetryProvider { IsAvailable = false };
            W101TelemetryService.SetProvider(mock);

            Assert.False(W101TelemetryService.TryGetTelemetry(out var telemetry));
            Assert.Null(telemetry);
        }
        finally
        {
            W101TelemetryService.ResetToDefault();
            W101InteractionPolicy.IsProcessObservationAllowed = false;
        }
    }

    [Fact]
    public void StaleOrDefaultTimestampIsRejected()
    {
        try
        {
            W101InteractionPolicy.IsProcessObservationAllowed = true;
            // Default timestamp (not set)
            var mockDefault = new MockTelemetryProvider
            {
                DataToReturn = new W101TelemetryData(X: 1.0, Y: 2.0, Z: 3.0, TimestampUtc: default)
            };
            W101TelemetryService.SetProvider(mockDefault);
            Assert.False(W101TelemetryService.TryGetTelemetry(out _));

            // Stale timestamp (10 seconds ago)
            var mockStale = new MockTelemetryProvider
            {
                DataToReturn = new W101TelemetryData(X: 1.0, Y: 2.0, Z: 3.0, TimestampUtc: DateTime.UtcNow.AddSeconds(-10))
            };
            W101TelemetryService.SetProvider(mockStale);
            Assert.False(W101TelemetryService.TryGetTelemetry(out _));
        }
        finally
        {
            W101TelemetryService.ResetToDefault();
            W101InteractionPolicy.IsProcessObservationAllowed = false;
        }
    }

    [Fact]
    public void NonFiniteCoordinatesAndInvalidStatsAreRejected()
    {
        try
        {
            W101InteractionPolicy.IsProcessObservationAllowed = true;
            var mockNaN = new MockTelemetryProvider
            {
                DataToReturn = new W101TelemetryData(X: double.NaN, Y: 2.0, Z: 3.0, TimestampUtc: DateTime.UtcNow)
            };
            W101TelemetryService.SetProvider(mockNaN);
            Assert.False(W101TelemetryService.TryGetTelemetry(out _));

            var mockInvalidPips = new MockTelemetryProvider
            {
                DataToReturn = new W101TelemetryData(X: 1.0, Y: 2.0, Z: 3.0, Pips: 99, TimestampUtc: DateTime.UtcNow)
            };
            W101TelemetryService.SetProvider(mockInvalidPips);
            Assert.False(W101TelemetryService.TryGetTelemetry(out _));
        }
        finally
        {
            W101TelemetryService.ResetToDefault();
            W101InteractionPolicy.IsProcessObservationAllowed = false;
        }
    }

    private class ThrowingTelemetryProvider : IW101TelemetryProvider
    {
        public string Name => "ThrowingProvider";
        public bool IsAvailable => throw new InvalidOperationException("Process crashed");
        public W101TelemetryData? GetTelemetry() => throw new AccessViolationException("Memory unreadable");
    }

    [Fact]
    public void ThrowingProviderFailsSafelyWithoutCrashingCaller()
    {
        try
        {
            W101InteractionPolicy.IsProcessObservationAllowed = true;
            W101TelemetryService.SetProvider(new ThrowingTelemetryProvider());

            Assert.False(W101TelemetryService.TryGetTelemetry(out var telemetry));
            Assert.Null(telemetry);
        }
        finally
        {
            W101TelemetryService.ResetToDefault();
            W101InteractionPolicy.IsProcessObservationAllowed = false;
        }
    }
}
