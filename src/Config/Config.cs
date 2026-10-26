using System.ComponentModel;

namespace Ranks.RevealAllRanks.Config;

/// <summary>When the reveal is broadcast.</summary>
/// <remarks>
/// All three triggers default on because the reveal does not persist: the client re-hides other
/// players' ranks on its own, so one broadcast at load would work for a single round and then
/// quietly stop.
/// </remarks>
internal sealed class RevealConfig
{
    [Description("Re-broadcast at the start of every round.")]
    public bool OnRoundStart { get; set; } = true;

    [Description("Send to a player as they join, so they see ranks without waiting for the next round.")]
    public bool OnPlayerJoin { get; set; } = true;

    [Description("Re-send to a player when Ranks reports their rank changed. Ignored when the Ranks plugin is not installed.")]
    public bool OnRankChange { get; set; } = true;

    [Description("Seconds to wait before sending to a joining player. Too early and the client is not ready to act on it.")]
    public float JoinDelaySeconds { get; set; } = 2.0f;
}

internal sealed class GeneralConfig
{
    [Description("Narrate expected flow. Warnings and errors are never gated by this.")]
    public bool DetailedLogging { get; set; }
}

/// <summary>The module's <c>config.jsonc</c>.</summary>
internal sealed class RevealAllRanksConfig
{
    public RevealConfig Reveal { get; set; } = new();

    public GeneralConfig General { get; set; } = new();

    /// <remarks>Clamps rather than throws — a bad delay is a tuning error, not a data-loss risk.</remarks>
    public void Clamp() => Reveal.JoinDelaySeconds = Math.Clamp(Reveal.JoinDelaySeconds, 0f, 30f);
}
