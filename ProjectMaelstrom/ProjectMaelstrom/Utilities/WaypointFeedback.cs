namespace ProjectMaelstrom.Utilities;

internal sealed record WaypointPose(double X, double Y, double Z, double Heading, long Timestamp);
internal sealed record WaypointDecision(string State, string Reason, int PulseMs, double Distance, double HeadingError);

/// <summary>Pure local forward controller. No OS/input dependencies. Turning is
/// withheld until a separately measured turn model exists. Never learns from
/// movement that increases distance, changes level or produces implausible jumps.</summary>
internal sealed class WaypointFeedback(WaypointPose destination, int maximumPulseMs)
{
    private WaypointPose? _previous;
    private int _lastPulse, _stalls, _arrivals, _moves;
    private double _speed = 0.6;
    internal WaypointDecision Observe(WaypointPose pose)
    {
        double dx = destination.X - pose.X, dy = destination.Y - pose.Y;
        double distance = Math.Sqrt(dx * dx + dy * dy);
        double error = Math.IEEERemainder(Math.Atan2(-dx, -dy) - pose.Heading, Math.PI * 2);
        WaypointDecision Stop(string reason) => new("stopped", reason, 0, distance, error);
        if (maximumPulseMs is < 20 or > 100 || !new[] { pose.X, pose.Y, pose.Z, pose.Heading, destination.X, destination.Y, destination.Z, distance, error }.All(double.IsFinite))
            return Stop("invalid-pose-or-budget");
        if (Math.Abs(pose.Z - destination.Z) > 10) return Stop("level-changed");
        if (_previous is { } prior)
        {
            if (pose.Timestamp <= prior.Timestamp) return Stop("pose-not-newer");
            var moved = Math.Sqrt(Math.Pow(pose.X - prior.X, 2) + Math.Pow(pose.Y - prior.Y, 2));
            if (moved > 120) return Stop("unexpected-displacement");
            if (_lastPulse > 0)
            {
                var oldDistance = Math.Sqrt(Math.Pow(destination.X - prior.X, 2) + Math.Pow(destination.Y - prior.Y, 2));
                if (distance > oldDistance + 10) return Stop("moving-away-from-destination");
                _stalls = moved < 2 ? _stalls + 1 : 0;
                if (_stalls >= 2) return Stop("movement-stalled");
                if (moved >= 2) _speed = Math.Clamp(moved / _lastPulse, 0.2, 1.2);
            }
        }
        _previous = pose;
        _lastPulse = 0;
        if (distance <= 10)
            return new(++_arrivals >= 2 ? "arrived" : "confirm", "coordinate-arrival", 0, distance, error);
        _arrivals = 0;
        if (Math.Abs(error) > 0.15) return Stop("heading-correction-needs-calibration");
        if (_moves >= 3) return Stop("movement-budget-exhausted");
        _lastPulse = Math.Clamp((int)Math.Floor(distance / _speed * 0.8), 20, maximumPulseMs);
        _moves++;
        return new("move", "bounded-forward-correction", _lastPulse, distance, error);
    }
}
