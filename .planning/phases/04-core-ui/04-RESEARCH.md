# Phase 4: Core UI - Research

**Researched:** 2026-05-06
**Domain:** Dalamud ImGui plugin UI — WindowSystem, immediate-mode rendering, configuration UX
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** Three-plan split: `04-01` DailyRouteWindow layout + item rendering, `04-02` interactions (checkboxes, profit tally, progress, auto-collapse), `04-03` ConfigWindow.
- **D-02:** Phase 4 ships as a single unit. No intermediate merges to `main`; merge only after all UAT passes.
- **D-03:** `04-03` may run in parallel with `04-01`/`04-02` — minimal merge-conflict surface.
- **D-04:** Test strategy: `tests/phase04_nyquist.sh` source-validation script asserting required ImGui calls and color-usage rules, plus xUnit-style unit tests for pure-logic helpers. CI remains the authoritative compile/package gate.
- **D-05:** Adopt `Dalamud.Interface.Windowing.WindowSystem` with the `Window` base class for all three windows. Migrate `FirstRunWindow` as part of this phase.
- **D-06:** Keep `FirstRunWindow` as a dedicated first-run popup; migrating it to `Window` base class is mechanical refactor only.
- **D-07:** ConfigWindow opens from two entry points: `UiBuilder.OpenConfigUi` (gear icon / `/xlsettings`) AND in-window "Settings" button in DailyRouteWindow.
- **D-08:** Where `WindowSystem` lives (`NamazuFlippers.cs` vs new `UI/PluginUi.cs`) is planner's discretion.
- **D-09:** When `LatestScanResult` changes, wipe `boughtState` and `listedState`. No merge, no confirmation.
- **D-10:** Empty state renders status banner PLUS dimmed zeroed progress section to keep layout stable.
- **D-11:** In-memory bought/listed state survives window close+reopen within a game session. Phase 5 lifts to JSON.
- **D-12:** ConfigWindow edit flow: snapshot on open → live edit → dirty flag → close-prompt → revert-from-snapshot on Discard.
- **D-13:** Reset to Defaults requires a confirmation modal. UI-SPEC must be updated to document this modal. After confirmation, values revert to hardcoded defaults, dirty flag flips true, user must still click Save.

### Claude's Discretion

- Location of `WindowSystem` ownership (D-08).
- Exact shape of `tests/phase04_nyquist.sh` checks and which pure-logic helpers get unit tests (D-04).
- Exact dictionary types for bought/listed/auto-collapsed state.
- Exact placement of the in-window "Settings" button within the DailyRouteWindow top row.
- Exact wording of the "unsaved changes" close-prompt and Reset-to-Defaults confirmation modal.

### Deferred Ideas (OUT OF SCOPE)

- Lifetime / daily / weekly earnings tracker — post-v1.
- JSON persistence of bought/listed state across game restarts — Phase 5 (SESS-01, SESS-02).
- Mark All Bought / Mark All Listed bulk actions — Phase 5 (SESS-03).
- Shortage Predictor toggle wired up — Phase 6; control is rendered but inert in Phase 4.
- Market-board hook + server-travel auto-advance — Phase 7.
- Cross-configuration migrations / schema versioning UI — not needed yet.
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| UI-01 | DailyRouteWindow displays today's route: server stops in order, items to buy per stop with prices | WindowSystem + Window base class + RouteStop/RankedOpportunity data model fully mapped in UI-SPEC |
| UI-02 | Each item has a checkbox to mark "bought" at the purchase server | ImGui.Checkbox with `##bought-{itemId}` key; `Dictionary<int, bool>` boughtState on window |
| UI-03 | Home stop section shows items to list with "listed" checkboxes | Listed checkbox (`##listed-{itemId}`) rendered only for the home-world RouteStop |
| UI-04 | Running profit tally updates as items are marked listed | Computed each frame from listedState × ExpectedDailyProfit; TextColored with GilGold |
| UI-05 | Progress bar shows completion (bought/total and listed/total) | Two ImGui.ProgressBar calls with PushStyleColor on PlotHistogram; fractions computed each frame |
| UI-06 | OOS items are visually highlighted with a priority indicator | OosOrange TextColored for name and `[OOS]` badge; OutOfStock flag already on RankedOpportunity |
| UI-07 | Server stops auto-collapse after all items at that stop are bought | SetNextItemOpen(false, ImGuiCond.Always) on first-completion frame; per-stop bool in autoCollapsedStops |
| UI-08 | ConfigWindow provides settings UI matching CONF-01 through CONF-09 | All 14 Configuration props mapped to specific ImGui widgets in UI-SPEC widget inventory |
</phase_requirements>

---

## Summary

Phase 4 builds two new ImGui windows (`DailyRouteWindow`, `ConfigWindow`) and migrates `FirstRunWindow` to the `Dalamud.Interface.Windowing.Window` base class, all managed by a single `WindowSystem`. The windows consume already-built data types from Phases 1–3: `ScanEngineResult`, `RouteStop`, `RankedOpportunity`, and `Configuration`. No new backend logic is introduced in this phase — it is exclusively rendering and interaction.

The design contract is fully specified in `04-UI-SPEC.md`, which locks widget choices, exact `Vector4` color values, spacing overrides, copy strings, status-state handling, and auto-collapse mechanics. The implementation task is translating that contract into C# classes that override `Window.Draw()`. The main technical decisions are: WindowSystem wiring in the entry point, snapshot/dirty/discard pattern for ConfigWindow, auto-collapse state management via `SetNextItemOpen`, and progress bar color override via `PushStyleColor(ImGuiCol.PlotHistogram, ...)`.

The test strategy follows the Phase 3 pattern exactly: a `tests/phase04_nyquist.sh` bash script greps source files for required ImGui call patterns and color token values, giving fast (< 1 second) local feedback with no Dalamud runtime required. CI remains the compiler/package gate.

**Primary recommendation:** Implement windows by overriding `Window.Draw()`, keep all UI state on the window classes, wire `WindowSystem` in `NamazuFlippers.cs` (not a new indirection class unless entry-point complexity warrants it), and follow the Phase 3 nyquist.sh pattern for validation.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Route display (stops, items, prices) | ImGui window class (DailyRouteWindow) | NamazuFlippers.cs (provides LatestScanResult) | Window reads data each frame; plugin owns the data model |
| Bought/listed checkbox state | DailyRouteWindow (in-memory dictionaries) | — | State is ephemeral within session; Phase 5 lifts to JSON |
| Profit tally calculation | DailyRouteWindow (computed each Draw frame) | — | Pure function of listedState × ExpectedDailyProfit; no backend involvement |
| Progress bar rendering | DailyRouteWindow | — | Fraction computed in Draw; ProgressBar call with PlotHistogram color override |
| Auto-collapse on completion | DailyRouteWindow (autoCollapsedStops dict) | — | Per-stop bool flag; SetNextItemOpen triggers collapse only once |
| Config editing (snapshot/dirty/save) | ConfigWindow | NamazuFlippers.cs (owns Configuration instance) | Window holds snapshot; plugin holds live config; SavePluginConfig is the persistence call |
| WindowSystem lifecycle | NamazuFlippers.cs | — | Plugin entry point owns UiBuilder.Draw subscription and Dispose cleanup |
| First-run popup | FirstRunWindow (migrated to Window base class) | NamazuFlippers.cs | Same single-purpose behavior, only base class changes |
| ConfigWindow entry points | UiBuilder.OpenConfigUi + DailyRouteWindow button | — | D-07: two entry points; both set ConfigWindow.IsOpen = true |

---

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Dalamud.NET.Sdk | 15.0.0 | Build system; provides ImGui, Dalamud.Plugin, Dalamud.Interface assemblies | Already used in Phase 1; official SDK for Dalamud plugins |
| ImGuiNET (via Dalamud) | Dalamud-bundled | Immediate-mode UI rendering calls (ImGui.Text, Checkbox, ProgressBar, etc.) | Ships with Dalamud; no separate install needed |
| Dalamud.Interface.Windowing | Dalamud-bundled | WindowSystem + Window base class; Escape-key integration, pinning, opacity | Official Dalamud recommendation for all plugin windows |
| System.Numerics | .NET 10 BCL | Vector2/Vector4 for size and color parameters | Already in scope via existing code |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Dalamud.Interface.FontAwesomeIcon | Dalamud-bundled | Icon glyphs via ImGui font push/pop | If icon badges (beyond text `[OOS]`, `[Vendor]`) are needed — UI-SPEC uses text badges |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `Dalamud.Interface.Windowing.Window` | Raw `ImGui.Begin`/`ImGui.End` | WindowSystem adds Escape-key integration, native UI ordering, pinning, opacity for free — no reason to bypass it |
| Text `[OOS]` badge | FontAwesome icon glyph | Text is simpler, already specified in UI-SPEC copywriting contract |

**Installation:** No new packages. All dependencies already in `NamazuFlippers.csproj` via `Dalamud.NET.Sdk`.

**Version verification:** `[VERIFIED: context7/goatcorp/dalamud]` — Dalamud SDK 15.0.0 already pinned in project; ImGui and Windowing ship bundled.

---

## Architecture Patterns

### System Architecture Diagram

```
/nflip command OR Login event
        |
        v
NamazuFlippers.cs (plugin entry point)
  - Owns WindowSystem
  - Owns LatestScanResult, scanInProgress
  - UiBuilder.Draw += windowSystem.Draw
  - UiBuilder.OpenConfigUi += () => configWindow.IsOpen = true
        |
        v
WindowSystem.Draw() [each frame]
  |
  +---> FirstRunWindow.Draw()      [if IsPending && LatestScanResult == null or HomeWorld empty]
  |
  +---> DailyRouteWindow.Draw()    [if IsOpen]
  |       reads LatestScanResult
  |       reads scanInProgress
  |       maintains boughtState, listedState, autoCollapsedStops
  |       calls RunScanAsync(forceRefresh:true) via plugin ref on Rescan
  |
  +---> ConfigWindow.Draw()        [if IsOpen]
          reads/writes Configuration via plugin ref
          maintains snapshot, dirtyFlag
          calls pluginInterface.SavePluginConfig on Save
```

### Recommended Project Structure

```
NamazuFlippers/
├── UI/
│   ├── DailyRouteWindow.cs      # Window override; all route rendering logic
│   ├── ConfigWindow.cs          # Window override; all settings rendering logic
│   └── FirstRunWindow.cs        # Migrated from project root; Window override
├── Core/                        # Unchanged from Phase 3
├── API/                         # Unchanged from Phase 3
├── Data/                        # Unchanged from Phase 3
├── Configuration.cs             # Unchanged
├── NamazuFlippers.cs            # Modified: add WindowSystem, wire new windows
└── NamazuFlippers.csproj        # No changes needed
tests/
└── phase04_nyquist.sh           # New: source-level validation for UI-01..UI-08
```

### Pattern 1: Window Base Class with Constructor Injection

**What:** Subclass `Dalamud.Interface.Windowing.Window`, pass dependencies via constructor, override `Draw()`.
**When to use:** Every window in this plugin.

```csharp
// Source: context7/goatcorp/dalamud — verified pattern
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System.Numerics;

public class DailyRouteWindow : Window
{
    private readonly NamazuFlippers plugin;
    private readonly IPluginLog log;

    private Dictionary<int, bool> boughtState = new();
    private Dictionary<int, bool> listedState = new();
    private Dictionary<string, bool> autoCollapsedStops = new();
    private ScanEngineResult? lastSeenResult;

    public DailyRouteWindow(NamazuFlippers plugin, IPluginLog log)
        : base("Namazu Flippers — Daily Route", ImGuiWindowFlags.None)
    {
        this.plugin = plugin;
        this.log = log;
        this.Size = new Vector2(420, 560);
        this.SizeCondition = ImGuiCond.FirstUseEver;
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(320, 300),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public override void Draw()
    {
        // detect result change -> wipe state (D-09)
        // render status banner
        // render progress section
        // render route stops
    }
}
```

[VERIFIED: context7/goatcorp/dalamud — WindowSystem + Window base class]

### Pattern 2: WindowSystem Wiring in Plugin Entry Point

**What:** Create one `WindowSystem`, add all windows, swap `OnDraw` body to call `windowSystem.Draw()`, register `OpenConfigUi`.
**When to use:** Plugin entry point `NamazuFlippers.cs` constructor and `Dispose`.

```csharp
// Source: context7/goatcorp/dalamud — verified pattern
private readonly WindowSystem windowSystem = new("NamazuFlippers");

// In constructor, after creating window instances:
windowSystem.AddWindow(dailyRouteWindow);
windowSystem.AddWindow(configWindow);
windowSystem.AddWindow(firstRunWindow);

pluginInterface.UiBuilder.Draw -= OnDraw;          // remove old subscriber
pluginInterface.UiBuilder.Draw += windowSystem.Draw; // replace with WindowSystem
pluginInterface.UiBuilder.OpenConfigUi += () => configWindow.IsOpen = true;

// In Dispose:
pluginInterface.UiBuilder.Draw -= windowSystem.Draw;
pluginInterface.UiBuilder.OpenConfigUi -= ...;     // unsubscribe lambda (store reference)
windowSystem.RemoveAllWindows();
```

[VERIFIED: context7/goatcorp/dalamud]

### Pattern 3: Progress Bar Color Override

**What:** `ImGui.ProgressBar` does not accept a color parameter. Override via `PushStyleColor(ImGuiCol.PlotHistogram, ...)` immediately around the call.
**When to use:** Both bought (SuccessGreen) and listed (PurchaseCyan) progress bars.

```csharp
// Source: 04-UI-SPEC.md — locked specification
var SuccessGreen = new Vector4(0.2f, 0.8f, 0.3f, 1.0f);
var PurchaseCyan = new Vector4(0.2f, 0.85f, 0.9f, 1.0f);

ImGui.PushStyleColor(ImGuiCol.PlotHistogram, SuccessGreen);
ImGui.ProgressBar(boughtFraction, new Vector2(-1, 16), "");
ImGui.PopStyleColor();

ImGui.PushStyleColor(ImGuiCol.PlotHistogram, PurchaseCyan);
ImGui.ProgressBar(listedFraction, new Vector2(-1, 16), "");
ImGui.PopStyleColor();
```

[CITED: 04-UI-SPEC.md §Widget Inventory]

### Pattern 4: Auto-Collapse on Stop Completion

**What:** Detect the first frame where all items in a stop are bought; call `SetNextItemOpen(false, ImGuiCond.Always)` before the `CollapsingHeader`. Store a per-stop bool to prevent re-collapsing on subsequent frames.
**When to use:** Every `RouteStop` header rendering.
**Critical:** Do NOT use the `ref bool` overload of `CollapsingHeader` for this — that controls the close-button (X) visibility, not open/closed state.

```csharp
// Source: 04-UI-SPEC.md §Interaction Contracts — locked specification
bool allBought = stop.Items.All(item => boughtState.GetValueOrDefault(item.ItemId));

if (allBought && !autoCollapsedStops.GetValueOrDefault(stop.PurchaseSource))
{
    ImGui.SetNextItemOpen(false, ImGuiCond.Always);
    autoCollapsedStops[stop.PurchaseSource] = true;
}
else if (!allBought)
{
    autoCollapsedStops[stop.PurchaseSource] = false; // reset so next completion re-triggers
}

ImGui.PushStyleColor(ImGuiCol.Text, allBought ? CompletedGray : ImGui.GetStyle().Colors[(int)ImGuiCol.Text]);
bool headerOpen = ImGui.CollapsingHeader(allBought
    ? $"✓ {stop.PurchaseSource} — {stop.Items.Count} items — {stop.TotalExpectedDailyProfit:n0} gil/day"
    : headerLabel);
ImGui.PopStyleColor();

if (headerOpen) { /* render items */ }
```

[CITED: 04-UI-SPEC.md §Auto-collapse on completion]

### Pattern 5: ConfigWindow Snapshot/Dirty/Discard (D-12)

**What:** On `OnOpen()`, copy `Configuration` into a snapshot. Track dirty flag on any control change. Close-prompt modal on exit-while-dirty. `Discard` restores snapshot. `Save` persists and updates snapshot.
**When to use:** `ConfigWindow` only.

```csharp
// Source: 04-CONTEXT.md D-12 — locked decision
private Configuration? snapshot;
private bool isDirty;
private bool showUnsavedModal;

public override void OnOpen()
{
    // Deep-copy all Configuration properties into snapshot
    snapshot = CloneConfiguration(plugin.Configuration);
    isDirty = false;
}

// In Draw(), after every control that can change a value:
if (ImGui.SliderInt("Min ROI %%", ref plugin.Configuration.PreferredRoi, 0, 100))
    isDirty = true;

// Save button:
if (ImGui.Button("Save Settings"))
{
    pluginInterface.SavePluginConfig(plugin.Configuration);
    snapshot = CloneConfiguration(plugin.Configuration);
    isDirty = false;
}

// Close-while-dirty: intercept close by setting IsOpen = true inside OnClose,
// then triggering the modal. Alternatively: use PreDraw/Update to open the modal
// when IsOpen flips to false while dirty. The pattern below uses a pending-close flag:
public override void OnClose()
{
    if (isDirty)
    {
        IsOpen = true;           // re-open to keep window alive
        showUnsavedModal = true; // trigger modal on next frame
    }
}
```

[CITED: 04-CONTEXT.md D-12]

**Note on OnClose intercept:** `Window.OnClose()` fires after `IsOpen` is set to false. Setting `IsOpen = true` inside `OnClose()` effectively cancels the close. The unsaved-changes modal is then drawn in the next `Draw()` frame. This is the standard pattern for dirty-state confirmation in Dalamud plugins because there is no built-in "intercept close" hook. [ASSUMED — no official Dalamud doc explicitly documents this pattern; derived from Window class behavior and community practice]

### Pattern 6: Modal Confirmation (Reset to Defaults, D-13)

**What:** Button click sets a `bool openModal = true` flag. In Draw(), if flag is set, call `ImGui.OpenPopup("ConfirmReset")` and reset the flag. Then call `ImGui.BeginPopupModal("ConfirmReset", ...)` every frame to render the popup when active.
**When to use:** Reset to Defaults button and unsaved-changes close-prompt.
**Critical:** `OpenPopup` must be called in the same frame as the button click, not inside `BeginPopupModal`. Store popup trigger as a boolean field.

```csharp
// Source: ImGui immediate-mode pattern — verified via FirstRunWindow.cs existing usage
if (ImGui.Button("Reset to Defaults"))
    ImGui.OpenPopup("ConfirmReset##config");

if (ImGui.BeginPopupModal("ConfirmReset##config", ref dummyOpen, ImGuiWindowFlags.AlwaysAutoResize))
{
    ImGui.Text("Reset all settings to defaults?");
    ImGui.Spacing();
    if (ImGui.Button("Reset", new Vector2(120, 0)))
    {
        RestoreDefaults(plugin.Configuration);
        isDirty = true;
        ImGui.CloseCurrentPopup();
    }
    ImGui.SameLine();
    if (ImGui.Button("Cancel", new Vector2(120, 0)))
        ImGui.CloseCurrentPopup();
    ImGui.EndPopup();
}
```

[VERIFIED: existing FirstRunWindow.cs uses BeginPopupModal; pattern confirmed]

### Anti-Patterns to Avoid

- **Using `ref bool` overload of `CollapsingHeader` for collapse control:** The `ref bool` controls close-button (X) visibility, not open/closed state. Use `SetNextItemOpen` instead. [CITED: 04-UI-SPEC.md]
- **Calling `ImGui.OpenPopup` inside `BeginPopupModal`:** The popup trigger must be called outside the modal block or it never fires. Store trigger as a frame-level bool.
- **Applying `PushStyleVar(ItemSpacing)` globally:** Override spacing only within item list render loops; restore immediately after with `PopStyleVar`. Altering it globally breaks other sections.
- **Storing `WindowSystem` inside a window class:** `WindowSystem` belongs to the plugin entry point so `Dispose` can call `RemoveAllWindows` and unsubscribe the draw event cleanly.
- **Skipping `UiBuilder.OpenConfigUi` unsubscribe in Dispose:** Lambda event subscriptions must be stored as named delegates or removed with matching lambda — anonymous lambdas can't be unsubscribed. Store the handler as a field.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Window lifecycle (open/close/Escape key) | Custom bool + `ImGui.Begin`/`ImGui.End` | `Dalamud.Interface.Windowing.Window` | WindowSystem gives Escape key, pin, opacity, native UI ordering for free |
| Window size persistence across sessions | Manual config serialization of window rect | `Window.SizeCondition = ImGuiCond.FirstUseEver` | Dalamud WindowSystem persists size/position automatically |
| Config serialization | Custom JSON writer | `pluginInterface.SavePluginConfig(configuration)` | Already wired in Phase 1; Dalamud handles versioning |
| Progress bar color | Drawing filled rectangles manually | `PushStyleColor(ImGuiCol.PlotHistogram, ...)` + `ProgressBar` | One-line idiom; custom drawing is fragile with DPI/scale |
| World dropdown | Text input with validation | `ImGui.BeginCombo` over `WorldData.KnownWorlds` | Already implemented in `FirstRunWindow.cs`; no duplication |

**Key insight:** Every widget in this phase has a one- or two-line ImGui idiom. The complexity is in state management (auto-collapse, snapshot/dirty), not in rendering.

---

## Runtime State Inventory

> Phase 4 is new UI on top of existing data structures. It is not a rename/refactor phase.

| Category | Items Found | Action Required |
|----------|-------------|-----------------|
| Stored data | Phase 3 scan cache in `ConfigDirectory/scan-cache.json` | No action — format unchanged; DailyRouteWindow reads `LatestScanResult` in memory |
| Live service config | None — plugin has no external service config | None |
| OS-registered state | None | None |
| Secrets/env vars | None | None |
| Build artifacts | `obj/` and `bin/` under `NamazuFlippers/` | Normal build outputs; no stale state |

---

## Common Pitfalls

### Pitfall 1: Lambda Event Handler Cannot Be Unsubscribed
**What goes wrong:** Registering `pluginInterface.UiBuilder.OpenConfigUi += () => configWindow.IsOpen = true` in the constructor and then trying to unsubscribe the same lambda in `Dispose` — C# creates a new delegate instance per lambda expression, so `-=` does nothing and the handler leaks.
**Why it happens:** Lambda expressions are not reference-equal across invocations.
**How to avoid:** Store the handler as a named field: `private Action openConfigUiHandler = () => configWindow.IsOpen = true;` and use that field for both `+=` and `-=`.
**Warning signs:** Plugin crashes or hangs on `/xlplugins` reload; Dalamud log shows draw callbacks after unload.

### Pitfall 2: `SetNextItemOpen` Called Every Frame Locks the Header
**What goes wrong:** Calling `ImGui.SetNextItemOpen(false, ImGuiCond.Always)` on every frame where `allBought == true` means the user can never re-expand the header.
**Why it happens:** `ImGuiCond.Always` overrides user interaction unconditionally.
**How to avoid:** Set a per-stop `autoCollapsedStops[key] = true` flag on the first completion frame and skip `SetNextItemOpen` on subsequent frames. Only call it once per completion event.
**Warning signs:** Header collapses but clicking the triangle does nothing.

### Pitfall 3: `OnClose` Re-Entrancy with `IsOpen = true`
**What goes wrong:** Setting `IsOpen = true` inside `OnClose()` without also setting a flag to open the modal can produce an infinite loop: close sets IsOpen=false → OnClose fires → IsOpen=true → repeat.
**Why it happens:** The window close cycle involves multiple property reads within one frame.
**How to avoid:** Only set `IsOpen = true` in `OnClose()` if `isDirty` is true, and simultaneously set `showUnsavedModal = true` so `Draw()` opens the popup next frame. Once the modal resolves (Save/Discard/Cancel), the modal clears both flags.
**Warning signs:** ConfigWindow flickers or the close button appears to do nothing.

### Pitfall 4: `PushStyleColor` Count Mismatch
**What goes wrong:** Early-return paths inside render loops leave `PushStyleColor` calls without matching `PopStyleColor`, corrupting ImGui's color stack for the rest of the frame.
**Why it happens:** Early returns or exceptions (even caught) in immediate-mode code skip the pop.
**How to avoid:** Use try/finally or structure the code so `PopStyleColor` is always called if `PushStyleColor` was called. Never use early `return` between a push and its pop.
**Warning signs:** Colors bleed into unrelated widgets later in the same frame.

### Pitfall 5: `PushStyleVar(ItemSpacing)` Left Open
**What goes wrong:** Setting compact `ItemSpacing` for the item list and forgetting `PopStyleVar` causes all subsequent sections in the frame (separators, config window widgets, etc.) to use compact spacing.
**Why it happens:** Style overrides are frame-global in ImGui.
**How to avoid:** Always bracket `PushStyleVar`/`PopStyleVar` tightly around the item list loop. Never push at start of Draw and pop at end — that pattern breaks if any early exit occurs.

### Pitfall 6: `LatestScanResult` Read Without Null Guard
**What goes wrong:** `LatestScanResult` on the plugin is null until the first scan completes. Accessing `.Status`, `.RouteStops`, etc. without null-checking crashes the Draw loop (or more likely causes a NullReferenceException in Dalamud's draw callback, silently swallowing the error).
**Why it happens:** D-10 specifies an empty-state render path, but developers forget to guard the null case.
**How to avoid:** Always check `plugin.LatestScanResult` for null at the top of `DailyRouteWindow.Draw()`. The null state renders the "Scanning for opportunities..." banner.

---

## Code Examples

Verified patterns from official sources:

### WindowSystem AddWindow and Draw Registration
```csharp
// Source: context7/goatcorp/dalamud [VERIFIED]
private readonly WindowSystem windowSystem = new("NamazuFlippers");
// constructor:
windowSystem.AddWindow(dailyRouteWindow);
pluginInterface.UiBuilder.Draw += windowSystem.Draw;
// Dispose:
pluginInterface.UiBuilder.Draw -= windowSystem.Draw;
windowSystem.RemoveAllWindows();
```

### Window Constructor with SizeConstraints
```csharp
// Source: context7/goatcorp/dalamud [VERIFIED]
public DailyRouteWindow(...) : base("Namazu Flippers — Daily Route", ImGuiWindowFlags.None)
{
    this.Size = new Vector2(420, 560);
    this.SizeCondition = ImGuiCond.FirstUseEver;
    this.SizeConstraints = new WindowSizeConstraints
    {
        MinimumSize = new Vector2(320, 300),
        MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
    };
}
```

### Tooltip Pattern
```csharp
// Source: 04-UI-SPEC.md [CITED]
if (ImGui.IsItemHovered())
{
    ImGui.BeginTooltip();
    ImGui.Text($"Avg {item.SalesPerDay:F1} sales/day");
    ImGui.EndTooltip();
}
```

### Rescan Button with Disabled State
```csharp
// Source: 04-UI-SPEC.md + NamazuFlippers.cs existing pattern [CITED/VERIFIED]
ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X - buttonWidth);
if (plugin.scanInProgress == 1)
    ImGui.BeginDisabled();
if (ImGui.Button("Rescan Route"))
    _ = plugin.RunScanAsync(forceRefresh: true, scanCts.Token);  // plugin exposes RunScanAsync
if (plugin.scanInProgress == 1)
    ImGui.EndDisabled();
```

**Note on RunScanAsync access:** `NamazuFlippers.RunScanAsync` is currently `private`. The window will need either: (a) the method made `internal`, (b) a public wrapper method on the plugin class, or (c) a delegate/callback injected at window construction. The planner should choose; option (b) is simplest. [ASSUMED — need to verify current access modifier in NamazuFlippers.cs before implementing]

### Gil Formatting
```csharp
// Source: 04-UI-SPEC.md [CITED]
ImGui.TextColored(GilGold, $"{item.ExpectedDailyProfit:n0} gil/day");
// e.g., "1,234,567 gil/day" — C# n0 format specifier handles thousands separator
```

### BeginCombo Dropdown (ConfigWindow HomeWorld — mirrors FirstRunWindow)
```csharp
// Source: FirstRunWindow.cs [VERIFIED — existing code]
var preview = selectedIndex >= 0 ? WorldData.KnownWorlds[selectedIndex] : "(select world)";
if (ImGui.BeginCombo("##home-world-combo", preview))
{
    for (int i = 0; i < WorldData.KnownWorlds.Length; i++)
    {
        bool isSelected = i == selectedIndex;
        if (ImGui.Selectable(WorldData.KnownWorlds[i], isSelected))
            selectedIndex = i;
        if (isSelected) ImGui.SetItemDefaultFocus();
    }
    ImGui.EndCombo();
}
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Raw `ImGui.Begin`/`End` with manual bool | `Dalamud.Interface.Windowing.Window` base class | Dalamud v9 (2023) | Escape key, native close ordering, pin/opacity — mandatory for modern plugins |
| `UiBuilder` named "Builder" prefix on interfaces | Renamed to `IUiBuilder` | Dalamud v10 | Already correct in this codebase |
| `DalamudPluginInterface` (no I prefix) | `IDalamudPluginInterface` | Dalamud v10 | Already correct in this codebase |

**Deprecated/outdated:**
- Raw `ImGui.Begin`/`ImGui.End` for settings/utility windows: replaced by `Window` base class per Dalamud docs ("if it looks like a window, use the Windowing API"). [CITED: dalamud-docs technical-considerations]

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Setting `IsOpen = true` inside `Window.OnClose()` effectively cancels the close, enabling dirty-state interception | Pitfall 3, Pattern 5 | If OnClose fires after window is removed from draw cycle, this pattern won't work and an alternative (PreDraw flag check) is needed |
| A2 | `NamazuFlippers.RunScanAsync` will need to be made `internal` or wrapped to be callable from window classes | Code Examples | If the method is kept private, windows can't call it; planner must add a public/internal wrapper |
| A3 | `scanInProgress` field on NamazuFlippers needs to be accessible from DailyRouteWindow (currently `private`) | Code Examples | If kept private, BeginDisabled guard can't be applied; need `public int ScanInProgress => scanInProgress` property |

---

## Open Questions

1. **`RunScanAsync` and `scanInProgress` access**
   - What we know: Both are `private` in `NamazuFlippers.cs`; window classes are in a different file/class.
   - What's unclear: Whether the planner wraps them with public properties/methods, or uses internal visibility.
   - Recommendation: Add `public bool ScanInProgress => Interlocked.CompareExchange(ref scanInProgress, 0, 0) == 1;` and `public Task RescanAsync(CancellationToken ct) => RunScanAsync(true, ct);` to plugin class. Minimal surface area change.

2. **`OpenConfigUi` lambda storage for unsubscription**
   - What we know: The Dispose pattern must unsubscribe the handler.
   - What's unclear: Whether the planner uses a stored field or a named method.
   - Recommendation: Named private method `private void OnOpenConfigUi() => configWindow.IsOpen = true;` registered and unregistered by name.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| Dalamud SDK (DALAMUD_HOME) | Compile/package | ✗ locally (macOS) | — | GitHub Actions CI (Ubuntu) downloads SDK; source-grep validation runs locally |
| dotnet | Build tooling | ✓ | .NET 10 (inferred from csproj) | — |
| bash | Nyquist test script | ✓ | macOS zsh-compatible | — |
| GitHub Actions | Compile/package gate | ✓ | Existing workflow | — |

**Missing dependencies with no fallback:** None that block Phase 4 — the macOS Dalamud SDK gap is a known project constraint documented in STATE.md.

**Missing dependencies with fallback:** Dalamud SDK not available locally → GitHub Actions CI as authoritative build gate.

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | Bash source-level Nyquist validation (same pattern as phase03_nyquist.sh) |
| Config file | `tests/phase04_nyquist.sh` (Wave 0 gap — does not exist yet) |
| Quick run command | `bash tests/phase04_nyquist.sh` |
| Full suite command | `bash tests/phase04_nyquist.sh` locally; GitHub Actions for compile/package |

### Phase Requirements to Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| UI-01 | DailyRouteWindow renders RouteStop headers and RankedOpportunity rows | source | `bash tests/phase04_nyquist.sh` | Wave 0 gap |
| UI-02 | Bought checkbox (`##bought-{itemId}`) flips boughtState; CompletedGray applied when bought | source | `bash tests/phase04_nyquist.sh` | Wave 0 gap |
| UI-03 | Listed checkbox (`##listed-{itemId}`) present in home stop section only | source | `bash tests/phase04_nyquist.sh` | Wave 0 gap |
| UI-04 | Profit tally computed from listedState × ExpectedDailyProfit; rendered in GilGold | source | `bash tests/phase04_nyquist.sh` | Wave 0 gap |
| UI-05 | Two ProgressBar calls with PlotHistogram color push; fractions computed from bought/listed counts | source | `bash tests/phase04_nyquist.sh` | Wave 0 gap |
| UI-06 | OosOrange color applied to OOS item name and `[OOS]` badge; OutOfStock flag gate | source | `bash tests/phase04_nyquist.sh` | Wave 0 gap |
| UI-07 | SetNextItemOpen(false, ImGuiCond.Always) called on first completion frame; autoCollapsedStops tracks per-stop state | source | `bash tests/phase04_nyquist.sh` | Wave 0 gap |
| UI-08 | ConfigWindow renders controls for all 14 Configuration properties; Save calls SavePluginConfig; Reset opens modal | source | `bash tests/phase04_nyquist.sh` | Wave 0 gap |
| CONF-01..09 | All CONF- properties have a corresponding ImGui widget in ConfigWindow | source | `bash tests/phase04_nyquist.sh` | Wave 0 gap |

**Manual-only verifications:**

| Behavior | Requirement | Why Manual |
|----------|-------------|------------|
| Window appears in-game at correct size/position | UI-01 | Requires Dalamud runtime |
| Escape key closes DailyRouteWindow via WindowSystem integration | UI-01 | Requires Dalamud runtime |
| Dirty-state close-prompt modal renders and Save/Discard/Cancel behave correctly | UI-08 | Requires Dalamud runtime |
| ConfigWindow accessible via `/xlsettings` gear icon | UI-08 | Requires Dalamud runtime |

### Sampling Rate

- **Per task commit:** `bash tests/phase04_nyquist.sh`
- **Per wave merge:** `bash tests/phase04_nyquist.sh` + check GitHub Actions
- **Phase gate:** Full source validation green + GitHub Actions build green before `/gsd-verify-work`

### Wave 0 Gaps

- [ ] `tests/phase04_nyquist.sh` — covers UI-01 through UI-08 and CONF-01..09

**Key source patterns the nyquist.sh should assert:**

For UI-01 (route rendering):
- `NamazuFlippers/UI/DailyRouteWindow.cs` exists
- `WindowSystem` + `AddWindow` present in `NamazuFlippers.cs`
- `CollapsingHeader` called with RouteStop PurchaseSource
- `ImGui.Checkbox` with `##bought-` pattern

For UI-05 (progress bars):
- `PlotHistogram` + `ProgressBar` appear together
- `SuccessGreen` color value `(0.2f, 0.8f, 0.3f` appears in source
- `PurchaseCyan` color value `(0.2f, 0.85f, 0.9f` appears in source

For UI-07 (auto-collapse):
- `SetNextItemOpen` appears in `DailyRouteWindow.cs`
- `autoCollapsedStops` or equivalent dictionary key pattern appears

For UI-08 (ConfigWindow):
- `NamazuFlippers/UI/ConfigWindow.cs` exists
- `SavePluginConfig` called from ConfigWindow
- `BeginPopupModal` for reset confirmation
- `SliderInt` for PreferredRoi, MaxItemsPerSession, MaxServersToVisit, CacheDurationHours
- `InputInt` for MinProfitAmount, MinDesiredAvgPpu, MaxBudgetPerItem
- `BeginCombo` for HomeWorld dropdown

For color token integrity (UI-SPEC compliance):
- `GilGold` assigned `(1.0f, 0.85f, 0.1f`
- `OosOrange` assigned `(1.0f, 0.55f, 0.1f`
- `ErrorRed` assigned `(0.9f, 0.2f, 0.2f`
- `CompletedGray` assigned `(0.5f, 0.5f, 0.5f`

---

## Security Domain

> `security_enforcement` not explicitly set to false in config.json — included by default.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | Plugin has no auth surface; Dalamud handles plugin trust |
| V3 Session Management | No | Session state is in-memory only (Phase 4); no tokens or sessions |
| V4 Access Control | No | Single-user, single-machine plugin |
| V5 Input Validation | Yes (low severity) | ImGui `InputInt` has min/max clamp; ConfigWindow should clamp values on change |
| V6 Cryptography | No | No crypto in UI layer |

### Known Threat Patterns for this Stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Negative/overflowed int inputs in ConfigWindow | Tampering | Clamp `InputInt` results: `Math.Max(0, value)` for gil amounts; `Math.Clamp(val, min, max)` for sliders |
| Unvalidated HomeWorld string persisted to config | Tampering | Constrain to `WorldData.KnownWorlds` membership check before saving (already enforced by dropdown pattern from FirstRunWindow) |
| Draw callback crash from unguarded null | Denial of Service | Null-guard `LatestScanResult` at top of every `Draw()` call |

---

## Sources

### Primary (HIGH confidence)

- `context7/goatcorp/dalamud` — WindowSystem, Window base class, UiBuilder.Draw, OpenConfigUi, SizeCondition, BeginCombo, PushStyleColor, ProgressBar, BeginPopupModal patterns
- `04-UI-SPEC.md` — locked color values, widget inventory, layout structure, spacing, auto-collapse mechanism, copy strings
- `04-CONTEXT.md` — locked implementation decisions D-01..D-13
- Existing project source files: `NamazuFlippers.cs`, `FirstRunWindow.cs`, `Configuration.cs`, `ScanEngineResult.cs`, `RouteStop.cs`, `RankedOpportunity.cs`
- `tests/phase03_nyquist.sh` — pattern reference for Phase 4 nyquist script

### Secondary (MEDIUM confidence)

- `context7/goatcorp/dalamud-docs` — Windowing API usage note ("if it looks like a window, use the Windowing API"); v9/v10 class renames
- `dalamud.dev/api/Dalamud.Interface.Windowing/` — Window property list (IsOpen, Size, SizeCondition, SizeConstraints, Flags, OnClose, OnOpen)

### Tertiary (LOW confidence)

- [A1] `IsOpen = true` in `OnClose()` as close-intercept: community pattern, not officially documented

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — already in use; no new packages
- Architecture: HIGH — WindowSystem pattern verified, UI-SPEC locked, data models already built
- Pitfalls: HIGH — most derived from ImGui immediate-mode fundamentals and UI-SPEC explicit warnings
- Validation architecture: HIGH — direct copy of Phase 3 pattern

**Research date:** 2026-05-06
**Valid until:** 2026-06-06 (Dalamud SDK stable; no fast-moving dependencies in this phase)
