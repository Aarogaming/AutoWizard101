using System.Collections.ObjectModel;

namespace ProjectMaelstrom.Utilities;

/// <summary>Freezes the exact request admitted by the scheduler. Source collections
/// are enumerated once; validation examines the private snapshot, never a different
/// view of caller-owned pins. The returned collection cannot be edited through a ticket.</summary>
internal static class OperationRequestSnapshot
{
    public static OperationRequest Freeze(OperationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Pins is null) throw new InvalidDataException("Operation pins are required.");
        var pins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pin in request.Pins)
        {
            if (pins.Count == 32) throw new InvalidDataException("An operation accepts at most 32 input pins.");
            try { pins.Add(pin.Key, pin.Value); }
            catch (ArgumentException error) { throw new InvalidDataException("Operation pins must have distinct path identities.", error); }
        }
        var snapshot = request with { Pins = new ReadOnlyDictionary<string, string>(pins) };
        OperationScheduler.Validate(snapshot);
        return snapshot;
    }
}
