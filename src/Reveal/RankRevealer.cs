using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.ProtobufDefinitions;

namespace Ranks.RevealAllRanks.Reveal;

/// <summary>Broadcasts the reveal-all user message.</summary>
/// <remarks>
/// <c>CCSUsrMsg_ServerRankRevealAll</c> is a signal, not data: the client reveals every player's
/// rank icon on receipt and the two payload fields are ignored for this purpose. There is no
/// corresponding "hide" message, which is why the triggers in config are all about <em>when</em>
/// to re-send rather than what to send.
/// </remarks>
internal sealed class RankRevealer
{
    private readonly ISwiftlyCore _core;
    private readonly ILogger _logger;

    public RankRevealer(ISwiftlyCore core, ILogger logger)
    {
        _core = core;
        _logger = logger;
    }

    public void RevealToAll() => Send(message => message.SendToAllPlayers(), "all players");

    public void RevealTo(int slot) => Send(message => message.SendToPlayer(slot), $"slot {slot}");

    /// <remarks>
    /// Guarded because a net message reaches native code: a malformed send would otherwise take
    /// the server down over a cosmetic feature. Warning rather than Error — nothing is lost, the
    /// next round's broadcast tries again.
    /// </remarks>
    private void Send(Action<CCSUsrMsg_ServerRankRevealAll> send, string audience)
    {
        try
        {
            using var message = _core.NetMessage.Create<CCSUsrMsg_ServerRankRevealAll>();

            send(message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ranks_RevealAllRanks: could not send the reveal message to {Audience}.", audience);
        }
    }
}
