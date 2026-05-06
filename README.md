# Namazu Flippers

A Dalamud plugin for Final Fantasy XIV that finds daily cross-server arbitrage opportunities. One scan, one route, done in under 20 minutes — consistent daily gil profit.

Named after the commerce-obsessed Namazu beast tribe.

## Installation

Add this custom repository URL to XIV Launcher:

```
https://raw.githubusercontent.com/DarrellTang/namazu-flippers/main/pluginmaster.json
```

**In XIV Launcher:**
1. Open Settings → Experimental
2. Under "Custom Plugin Repositories", paste the URL and click **+**
3. Save, then find "Namazu Flippers" in the Plugin Installer

The plugin auto-updates when new versions are pushed.

## Usage

1. Type `/nflip` in-game to open the plugin
2. On first run, select your home world from the dropdown and click Confirm
3. (Route scanning and UI coming in future updates)

## Current Status

| Phase | Status |
|-------|--------|
| 1. Plugin Shell & Configuration | ✓ Complete |
| 2. API Integration (HTTP client, models, rate limiter) | ✓ Complete |
| 3. Scan Engine & Route Optimizer | Coming next |
| 4. Core UI | Planned |
| 5. Session Persistence | Planned |
| 6. Optional Features | Planned |
| 7. Polish & Ship | Planned |

## Development

### Prerequisites

- .NET 10 SDK
- [XIV Launcher](https://github.com/goatcorp/FFXIVQuickLauncher) with Dalamud dev plugins enabled

### Build

```powershell
# Clone
git clone https://github.com/DarrellTang/namazu-flippers.git
cd namazu-flippers

# Build (Windows with XIV Launcher installed)
dotnet build NamazuFlippers\NamazuFlippers.csproj -c Release
```

### Test locally

Copy the build output to Dalamud's dev plugin folder:

```powershell
xcopy NamazuFlippers\bin\Release\NamazuFlippers\* %APPDATA%\XIVLauncher\devPlugins\NamazuFlippers\ /Y
```

Then enable Dev Plugins in XIV Launcher Settings → Experimental. Launch FFXIV and test with `/nflip`.

### CI/CD

GitHub Actions auto-builds on every push to `main`:
- Downloads Dalamud SDK
- Builds with .NET 10
- Auto-bumps version to `1.0.{run}.0`
- Creates a GitHub Release with `NamazuFlippers.zip`
- Updates `pluginmaster.json` so the custom repo picks up the new version

## Tech Stack

- **Language:** C# (.NET 10)
- **Framework:** Dalamud plugin SDK v15
- **UI:** ImGui (via Dalamud's windowing system)
- **API:** Saddlebag Exchange REST API (`POST /api/scan`)
- **Data:** Universalis crowdsourced market board data

## License

MIT
