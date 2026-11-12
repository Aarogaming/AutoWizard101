using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Validated declarative data; identity matches are evidence, not proof.</summary>
internal sealed record DodgeGameContext(string Goal, int ViewportWidth, int ViewportHeight,
    int PlayerSpeed, int PlayerRadius, int ThreatRadius, int EdgeMargin, int HorizonMs,
    int MaxTrackAgeMs, int MaxObservationAgeMs, int MaxThreatSpeed, int MaxActionDurationMs, int IdentityThreshold,
    IReadOnlyDictionary<string, int> IdentityWeights, int MaskX, int MaskY, int MaskWidth,
    int MaskHeight, IReadOnlyDictionary<string, byte[]> StateMasks, string AvoidancePolicy,
    int RiskWeightPercent, int EdgeWeightPercent)
{
    internal int IdentityScore(bool ownedSession, bool viewport, bool visualState) =>
        (ownedSession ? IdentityWeights["owned_session"] : 0) +
        (viewport ? IdentityWeights["viewport"] : 0) +
        (visualState ? IdentityWeights["visual_state"] : 0);

    internal static DodgeGameContext Parse(JsonElement root)
    {
        if (root.TryGetProperty("avoidance_policy", out _))
            Fields(root, "version", "goal", "states", "action_states", "identity", "constraints", "state_masks", "avoidance_policy");
        else Fields(root, "version", "goal", "states", "action_states", "identity", "constraints", "state_masks");
        var policy = root.TryGetProperty("avoidance_policy", out var policyElement) ? policyElement.GetString() : "motion-v1";
        if (policy is not ("motion-v1" or "resilient-v2" or "guarded-v3" or "guarded-v4" or "geometry-v5" or "edge-aware-v6" or "safe-edge-v7" or "diagonal-v8" or "diagonal-v9" or "lookahead-v10" or "center-guard-v11" or "center-first-v12" or "adaptive-center-v13" or "trajectory-v14" or "center-track-v15" or "center-continuous-v16" or "center-continuous-track-v17" or "center-consensus-v18" or "center-anticipate-v19" or "center-configurable-v20" or "adaptive-horizon-v21" or "rollout-v22" or "velocity-guard-v23" or "edge-fallback-v24" or "guarded-edge-v25" or "directional-v26" or "directional-guard-v27" or "directional-carry-v28" or "directional-smooth-v29" or "directional-configurable-v30" or "directional-two-phase-v31" or "directional-pulse-v32" or "directional-learned-v33" or "directional-outcome-v34" or "directional-explore-v35" or "directional-outcome-turn-v36" or "directional-outcome-reference-v37" or "directional-outcome-escape-v38")) throw new InvalidDataException("Unsupported Dodge avoidance policy.");
        if (root.GetProperty("version").GetInt32() != 1) throw new InvalidDataException("Unsupported context version.");
        var goal = root.GetProperty("goal").GetString();
        if (string.IsNullOrWhiteSpace(goal) || goal.Length > 200) throw new InvalidDataException("Invalid game goal.");
        var states = root.GetProperty("states").EnumerateArray().Select(x => x.GetString()).ToArray();
        var active = root.GetProperty("action_states").EnumerateArray().Select(x => x.GetString()).ToArray();
        if (states.Length != 5 || !states.ToHashSet().SetEquals(new[] { "menu", "ready", "playing", "game_over", "unknown" }) || active.Length != 1 || active[0] != "playing")
            throw new InvalidDataException("Unsupported Dodge game states.");
        var identity = root.GetProperty("identity");
        Fields(identity, "minimum_score", "signals");
        var signals = identity.GetProperty("signals");
        Fields(signals, "owned_session", "viewport", "visual_state");
        var weights = new Dictionary<string, int>();
        foreach (var name in new[] { "owned_session", "viewport", "visual_state" }) weights[name] = Number(signals, name, 1, 10);
        var threshold = Number(identity, "minimum_score", 1, 30);
        if (threshold != weights.Values.Sum()) throw new InvalidDataException("Dodge requires all identity evidence signals.");
        var c = root.GetProperty("constraints");
        if (policy == "directional-configurable-v30")
            Fields(c, "viewport_width", "viewport_height", "player_speed_px_s", "player_radius_px", "threat_radius_px", "edge_margin_px", "prediction_horizon_ms", "max_tracking_age_ms", "max_observation_age_ms", "max_threat_speed_px_s", "max_action_duration_ms", "risk_weight_percent", "edge_weight_percent");
        else Fields(c, "viewport_width", "viewport_height", "player_speed_px_s", "player_radius_px", "threat_radius_px", "edge_margin_px", "prediction_horizon_ms", "max_tracking_age_ms", "max_observation_age_ms", "max_threat_speed_px_s", "max_action_duration_ms");
        var width = Number(c, "viewport_width", 480, 480); var height = Number(c, "viewport_height", 720, 720);
        var speed = Number(c, "player_speed_px_s", 1, 1000); var player = Number(c, "player_radius_px", 1, 100);
        var threat = Number(c, "threat_radius_px", 1, 100); var edge = Number(c, "edge_margin_px", 1, 100);
        var horizon = Number(c, "prediction_horizon_ms", 100, 2000); var trackAge = Number(c, "max_tracking_age_ms", 100, 3000);
        var observationAge = Number(c, "max_observation_age_ms", 100, 2000); var threatSpeed = Number(c, "max_threat_speed_px_s", 1, 1000);
        var maxAction = Number(c, "max_action_duration_ms", 1, 200);
        var riskWeight = policy == "directional-configurable-v30" ? Number(c, "risk_weight_percent", 25, 400) : 100;
        var edgeWeight = policy == "directional-configurable-v30" ? Number(c, "edge_weight_percent", 0, 300) : 100;
        var masks = root.GetProperty("state_masks");
        Fields(masks, "x", "y", "width", "height", "data");
        var x = Number(masks, "x", 0, 159); var y = Number(masks, "y", 0, 249);
        var w = Number(masks, "width", 1, 160); var h = Number(masks, "height", 1, 250);
        if (x + w > 160 || y + h > 250) throw new InvalidDataException("State mask is outside the frame.");
        var data = masks.GetProperty("data");
        Fields(data, "menu", "ready", "game_over");
        var decoded = new Dictionary<string, byte[]>();
        foreach (var name in new[] { "menu", "ready", "game_over" })
        {
            var encoded = data.GetProperty(name).GetString() ?? "";
            if (encoded.Length > 7000) throw new InvalidDataException("State mask is too large.");
            var bytes = Convert.FromBase64String(encoded);
            if (bytes.Length != (w * h + 7) / 8 || bytes.Sum(b => System.Numerics.BitOperations.PopCount((uint)b)) < 50)
                throw new InvalidDataException("Invalid state mask.");
            decoded[name] = bytes;
        }
        return new(goal, width, height, speed, player, threat, edge, horizon, trackAge, observationAge, threatSpeed, maxAction, threshold, weights, x, y, w, h, decoded, policy, riskWeight, edgeWeight);
    }

    private static int Number(JsonElement item, string name, int min, int max)
    {
        if (!item.GetProperty(name).TryGetInt32(out var value) || value < min || value > max) throw new InvalidDataException("Invalid context value: " + name);
        return value;
    }

    private static void Fields(JsonElement item, params string[] names)
    {
        if (item.ValueKind != JsonValueKind.Object || !item.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal).SetEquals(names))
            throw new InvalidDataException("Unsupported or missing context fields.");
    }
}
