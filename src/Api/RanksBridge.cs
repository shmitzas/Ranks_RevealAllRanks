using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Ranks.Contract;
using SwiftlyS2.Shared;

namespace Ranks.RevealAllRanks.Api;

/// <summary>The only file in this plugin that names a <c>Ranks.Contract</c> type.</summary>
/// <remarks>
/// The CLR resolves every type a method <em>names</em> when it JITs that method, so on a server
/// without Ranks a field typed <see cref="IRanksApi"/> throws FileNotFoundException at the
/// caller's frame — and SwiftlyS2 then fails hosting for <b>every</b> plugin installed. Every
/// member touching a contract type is therefore <c>[MethodImpl(NoInlining)]</c>, and everywhere
/// else reads <see cref="IsAvailable"/>, a plain bool.
///
/// The stakes are lower here than in a table-owning module and the dependency is genuinely
/// optional: this plugin reveals ranks perfectly well on a server running Valve's own Premier
/// ranks with no Ranks plugin at all. The isolation is kept identical anyway, because the failure
/// it prevents is the host, not this feature.
/// </remarks>
internal sealed class RanksBridge
{
    private readonly ILogger _logger;

    // Deliberately not IRanksApi: a field of that type would make every method of this class a
    // JIT hazard rather than only the ones marked NoInlining.
    private object? _api;
    private Action<RanksRankChangedArgs>? _handler;

    public RanksBridge(ILogger logger) => _logger = logger;

    public bool IsAvailable { get; private set; }

    /// <summary>Raised with the SteamID whose rank changed.</summary>
    public event Action<ulong>? RankChanged;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void TryAcquire(IInterfaceManager interfaceManager)
    {
        try
        {
            if (!interfaceManager.HasSharedInterface(IRanksApi.InterfaceKey))
            {
                // Fail-closed rather than an early return: a stale reference would point at a
                // provider the next handshake rebuild has already disposed.
                Detach();
                return;
            }

            var api = interfaceManager.GetSharedInterface<IRanksApi>(IRanksApi.InterfaceKey);

            if (api is null)
            {
                Detach();
                return;
            }

            Attach(api);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ranks_RevealAllRanks: could not acquire the Ranks API.");
            Detach();
        }
    }

    /// <remarks>
    /// Unsubscribes first: the handshake re-runs on every plugin load and unload, so an unguarded
    /// += accumulates one duplicate handler per rebuild.
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Attach(IRanksApi api)
    {
        if (_handler is not null) api.RankChanged -= _handler;

        _handler = e => RankChanged?.Invoke(e.SteamId);
        api.RankChanged += _handler;

        _api = api;
        IsAvailable = true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Detach()
    {
        if (_api is IRanksApi api && _handler is not null) api.RankChanged -= _handler;

        _handler = null;
        _api = null;
        IsAvailable = false;
    }
}
