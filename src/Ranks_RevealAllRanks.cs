using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Ranks.RevealAllRanks.Api;
using Ranks.RevealAllRanks.Config;
using Ranks.RevealAllRanks.Reveal;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Plugins;

namespace Ranks.RevealAllRanks;

/// <summary>Reveals every player's rank icon on the scoreboard rather than only your own.</summary>
/// <remarks>
/// Kept out of the core plugin deliberately. Core writes each player's <em>own</em> rank data
/// (FR-SCB-01..03); revealing <em>everyone's</em> needs a broadcast that is plausibly outside
/// Valve's guidance — Valve's own Premier servers do it, but "probably fine" is not a basis for
/// shipping it in a core plugin, so it lives here and ships separately.
///
/// Ranks is a genuinely optional dependency: with it absent this still reveals whatever ranks the
/// server is already showing, and only the rank-change re-broadcast is lost.
/// </remarks>
[PluginMetadata(
    Id = "Ranks_RevealAllRanks",
    Version = "1.0.0",
    Name = "Ranks RevealAllRanks",
    Author = "Shmitzas",
    Description = "Broadcasts the reveal-all user message so every player sees everyone's rank icon.")]
public sealed class RanksRevealAllRanks : BasePlugin
{
    private ILogger _logger = null!;
    private RevealAllRanksConfig _config = null!;
    private RankRevealer _revealer = null!;
    private RanksBridge _bridge = null!;
    private IDisposable? _reload;

    public RanksRevealAllRanks(ISwiftlyCore core) : base(core)
    {
    }

    public override void Load(bool hotReload)
    {
        _logger = Core.LoggerFactory.CreateLogger("Ranks_RevealAllRanks");

        LoadConfiguration();

        _revealer = new RankRevealer(Core, _logger);
        _bridge = new RanksBridge(_logger);
        _bridge.RankChanged += OnRankChanged;

        // Handlers live on this class, so no Registrator call is needed — the framework finds
        // attribute handlers on the plugin type itself.
        Core.Event.OnClientPutInServer += OnPutInServer;

        // A hot reload lands mid-round with players already in, and the reveal they were sent is
        // long gone from the client's state.
        if (hotReload) _revealer.RevealToAll();
    }

    public override void UseSharedInterface(IInterfaceManager interfaceManager) => _bridge.TryAcquire(interfaceManager);

    public override void OnSharedInterfaceInjected(IInterfaceManager interfaceManager) => _bridge.TryAcquire(interfaceManager);

    /// <remarks>
    /// Reported once per load cycle rather than from the binder, which re-runs on every rebuild.
    /// Information, not Warning: Ranks being absent is a supported configuration here, unlike in a
    /// module that owns tables.
    /// </remarks>
    public override void OnAllPluginsLoaded()
    {
        if (_bridge.IsAvailable) return;

        _logger.LogInformation(
            "Ranks_RevealAllRanks: the Ranks plugin is not installed. Ranks are still revealed on " +
            "round start and on join; only the rank-change re-broadcast is unavailable.");
    }

    public override void Unload()
    {
        Core.Event.OnClientPutInServer -= OnPutInServer;

        _bridge?.Detach();
        _reload?.Dispose();
    }

    /// <remarks>
    /// Bound off <c>Core.Configuration.Manager</c>, the root the framework already owns and
    /// watches, so a live reload needs no DI container of its own.
    /// </remarks>
    private void LoadConfiguration()
    {
        Core.Configuration
            .InitializeJsonWithModel<RevealAllRanksConfig>("config.jsonc", "RevealAllRanks", true)
            .Configure(builder => builder.AddJsonFile(
                Core.Configuration.GetConfigPath("config.jsonc"), optional: false, reloadOnChange: true));

        _config = Bind();
        _reload = ChangeToken.OnChange(Core.Configuration.Manager.GetReloadToken, () => _config = Bind());
    }

    private RevealAllRanksConfig Bind()
    {
        var bound = Core.Configuration.Manager.GetSection("RevealAllRanks").Get<RevealAllRanksConfig>() ?? new RevealAllRanksConfig();
        bound.Clamp();

        return bound;
    }

    /// <remarks>
    /// Round start rather than freeze end: the scoreboard is open during the freeze, so revealing
    /// after it would show the icons appearing late to anyone already looking.
    /// </remarks>
    [GameEventHandler(HookMode.Post)]
    public HookResult OnRoundStart(EventRoundStart e)
    {
        var config = _config;

        if (config.Reveal.OnRoundStart)
        {
            _revealer.RevealToAll();
            Detail("Ranks_RevealAllRanks: revealed ranks at round start.");
        }

        return HookResult.Continue;
    }

    /// <remarks>
    /// Delayed: a client that has only just entered the server is not ready to act on the reveal,
    /// and the message carries no retry of its own.
    /// </remarks>
    private void OnPutInServer(IOnClientPutInServerEvent e)
    {
        var config = _config;

        if (!config.Reveal.OnPlayerJoin) return;

        var slot = e.PlayerId;

        Core.Scheduler.DelayBySeconds(config.Reveal.JoinDelaySeconds, () =>
        {
            // They may have left during the delay; sending to a vacated slot is pointless rather
            // than harmful, but the check keeps the log honest.
            if (!Core.PlayerManager.IsPlayerOnline(slot)) return;

            _revealer.RevealTo(slot);
            Detail("Ranks_RevealAllRanks: revealed ranks to joining slot {Slot}.", slot);
        });
    }

    private void OnRankChanged(ulong steamId)
    {
        if (!_config.Reveal.OnRankChange) return;

        // Broadcast rather than sent to the one player: their rank changed, so it is everyone
        // else's view that is now stale.
        _revealer.RevealToAll();
        Detail("Ranks_RevealAllRanks: revealed ranks after {SteamId} changed rank.", steamId);
    }

    private void Detail(string template, params object?[] args)
    {
        if (!_config.General.DetailedLogging) return;

        _logger.LogInformation(template, args);
    }
}
