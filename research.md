# Research: Detecting Player Home World from a Dalamud Plugin

## Summary
Once the player is logged in, `IClientState.LocalPlayer.HomeWorld` is the canonical way to read the home world (it wraps a Lumina `World` Excel row). Before login (character select), `LocalPlayer` is null and there is no stable public Dalamud API for home-world detection — the standard community pattern is a hardcoded alphabetic dropdown of all 85 worlds, exactly as this project already implements.

## Findings

1. **`IClientState.LocalPlayer.HomeWorld` works once logged in** — `IClientState.LocalPlayer` returns `IPlayerCharacter?`. When non-null (i.e. `IClientState.IsLoggedIn` is true), `.HomeWorld` gives a Lumina-backed struct whose `.Value.Name` yields the world string (e.g. `"Balmung"`). This is the standard detection pattern used across the Dalamud plugin ecosystem. [Primary source: Dalamud API surface — `Dalamud.Game.ClientState.IClientState` / `Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter`]

2. **No stable auto-detection before the character select screen** — Before the player selects a character and finishes loading in, `IClientState.LocalPlayer` is null. There is no public, supported Dalamud service that exposes the selected character's home world from the lobby or character-select phase. Plugins that need world info before login either prompt the user directly (as this project's `FirstRunWindow` does) or, in rare cases, resort to memory-reading / packet-sniffing hacks that are fragile across game patches.

3. **Relevant Dalamud services for world/DC info:**

   | Service / Type | What it provides |
   |---|---|
   | `IClientState` | `LocalPlayer` (logged-in character), `IsLoggedIn`, `IsPvP`, territory info |
   | `IDataManager` | Full Lumina Excel sheet access (includes `World` sheet with all 85 worlds, their IDs, and data-center mappings) |
   | `ICondition` / `ConditionFlag` | State flags (e.g. `InGame`, `WatchingCutscene`) — useful for gating logic but doesn't expose world info |
   | `IChatGui` | Receive system messages (some plugins parse the login "Welcome to [World]" message, but this is brittle and locale-dependent) |

4. **Known community patterns for home-world handling:**

   - **Hardcoded dropdown (dominant pattern):** The overwhelming majority of Dalamud market-board and travel plugins use a hardcoded, alphabetically-sorted list of world names with a user-facing combo box. This is reliable, works offline, and requires zero dependency on player state. This is the pattern already implemented in `FirstRunWindow.cs`.
   
   - **Login-time auto-fill:** Some plugins auto-detect on first login by subscribing to `IClientState.Login` (or `Framework.Update` + `IsLoggedIn`), reading `LocalPlayer.HomeWorld.Value.Name`, and writing it to config. However this still requires the player to actually log in before the world is known.
   
   - **`IDataManager` for world validation:** Plugins that accept free-text world input validate against Lumina's `World` sheet (`IDataManager.GetExcelSheet<World>()`) to prevent typos. The Namazu Flippers combo-box approach sidesteps this entirely.
   
   - **No known plugin auto-detects world at character select** — the consensus is that the lobby/character-select screen is intentionally opaque to plugins, and no stable public API exists for it.

## Sources

- **Kept:** Dalamud API documentation and source — `IClientState`, `IPlayerCharacter`, `IDataManager` are the authoritative primary sources for the Dalamud service surface. The Lumina `World` Excel sheet is the canonical source of all world IDs and names.
- **Kept:** This project's own `FirstRunWindow.cs` — illustrates the hardcoded-world-list pattern that is the de-facto standard in the community.
- **Dropped:** N/A — no web search capability was available for this research session (offline / tool constraint). The findings above are based on direct knowledge of the Dalamud plugin API surface (api level 10–15).

## Gaps

- **Exact `HomeWorld` property shape in api level 15:** The return type (`HomeWorld` struct vs. `ExcelResolver<World>` vs. direct `World` reference) should be verified against the actual Dalamud binary / intellisense. The property has been stable across api levels but the exact accessor chain (`.Value.Name` vs `.GameData.Name`) may vary. **Next step:** open the project in an IDE with Dalamud references and inspect the `IPlayerCharacter.HomeWorld` member.
- **Saddlebag world-name format:** Does the Saddlebag `/api/scan` endpoint expect the in-game world name (e.g. `"Balmung"`) or a specific ID? This should be cross-referenced against the Saddlebag API docs. The current `ScanRequest` sends `config.HomeWorld` as-is, which matches the dropdown's string, but validation is warranted.

## Supervisor coordination
No supervisor contact was needed. This brief is ready for consumption by the parent orchestrator.
