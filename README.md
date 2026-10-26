<div align="center">
  <h1><strong>Ranks RevealAllRanks</strong></h1>
  <p>Keeps every player's competitive rank icon visible on the scoreboard for the whole map.</p>
</div>

<p align="center">
  <a href="https://github.com/shmitzas/Ranks_RevealAllRanks/releases/latest"><img src="https://img.shields.io/github/v/release/shmitzas/Ranks_RevealAllRanks?style=flat&logo=github&cacheSeconds=3600" alt="Latest Release"></a>
  <img src="https://img.shields.io/badge/SwiftlyS2-1.5.1--beta.12-blue?style=flat" alt="SwiftlyS2 Version">
  <img src="https://img.shields.io/github/downloads/shmitzas/Ranks_RevealAllRanks/total?style=flat&logo=github&cacheSeconds=3600" alt="Downloads">
  <img src="https://img.shields.io/github/stars/shmitzas/Ranks_RevealAllRanks?style=flat&logo=github&cacheSeconds=3600" alt="Stars">
</p>

---

## Features

- 🏅 **Persistent rank icons** - re-broadcasts the reveal message CS2 only sends once per map
- 🔁 **Every round** - icons are back before the scoreboard is opened
- 🚪 **On join** - new players see ranks immediately instead of waiting for the next round
- 📈 **On rank change** - re-reveals a player whose rank moved, when [Ranks](https://github.com/shmitzas/Ranks) is installed
- 🧩 **Standalone** - Ranks is optional; the plugin works without it

---

## Requirements

- SwiftlyS2
- [Ranks](https://github.com/shmitzas/Ranks) — **optional**, only for the rank-change trigger

---

## Installation

1. Drop the plugin folder into `addons/swiftlys2/plugins/`.
2. Restart the server, or load it with `sw_plugin load Ranks_RevealAllRanks`.
3. `config.jsonc` is created on first load with the defaults below.

---

## Configuration

Config file: `addons/swiftlys2/plugins/Ranks_RevealAllRanks/config.jsonc`

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Reveal.OnRoundStart` | bool | `true` | Re-broadcast to everyone at the start of every round |
| `Reveal.OnPlayerJoin` | bool | `true` | Send to a player as they join |
| `Reveal.OnRankChange` | bool | `true` | Re-send to a player when Ranks reports their rank changed; ignored without Ranks |
| `Reveal.JoinDelaySeconds` | float | `2.0` | Seconds to wait before sending to a joining player (clamped `0`–`30`) |
| `General.DetailedLogging` | bool | `false` | Narrate expected flow; warnings and errors always print |

Changes apply on save — no restart needed.

### Example Configuration

```jsonc
{
  "RevealAllRanks": {
    "Reveal": {
      "OnRoundStart": true,
      "OnPlayerJoin": true,
      "OnRankChange": true,
      "JoinDelaySeconds": 2.0
    },
    "General": {
      "DetailedLogging": false
    }
  }
}
```

---

## How It Works

**Overview**

CS2 sends `ServerRankRevealAll` once, when the map loads. Anyone who connects later — or whose
client drops the message — sees blank rank icons for the rest of the map. This plugin re-sends
that same message at the points where it matters.

**The message is a one-shot reveal, not a toggle.** There is no matching "hide", so re-sending is
always safe and the only real question is *when*. That is what every config key controls.

**Triggers:**

- **Round start**, not freeze end. The scoreboard is already open during the freeze, so revealing
  afterwards makes the icons visibly pop in for anyone looking at it.
- **Player join**, after `JoinDelaySeconds`. A client that has only just entered the server is not
  ready to act on the reveal, and the message carries no retry of its own.
- **Rank change**, via the Ranks shared interface. Only active when Ranks is installed.

**Without Ranks installed** the plugin runs normally and simply never fires the third trigger. It
is compiled against the Ranks contract but never requires it at runtime.

---

## Troubleshooting

| Problem | Possible Cause | Solution |
|---------|----------------|----------|
| Icons still blank for joining players | `JoinDelaySeconds` too low on a slow connection | Raise it to `3`–`5` |
| Icons blank for everyone | Plugin not loaded | Check the console for `Ranks_RevealAllRanks` at startup |
| Rank-change reveals never fire | Ranks not installed, or `OnRankChange` is `false` | Install Ranks, or set `OnRankChange` to `true` |
| Nothing in the console | `DetailedLogging` is `false` | Set it to `true`; warnings and errors print regardless |

---

## Notes

Valve use this same reveal on their own Premier servers, so the behaviour is not unusual — but
this plugin is maintained separately from Ranks rather than shipped as an official module, since
it manipulates a user message rather than reading one.
