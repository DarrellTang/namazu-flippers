# Phase 4: Core UI - Pattern Map

**Mapped:** 2026-05-06
**Files analyzed:** 5 new/modified files
**Analogs found:** 5 / 5

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `NamazuFlippers/UI/DailyRouteWindow.cs` | component (Window subclass) | request-response (reads plugin state each frame) | `NamazuFlippers/FirstRunWindow.cs` | role-match |
| `NamazuFlippers/UI/ConfigWindow.cs` | component (Window subclass) | request-response + CRUD (reads/writes Configuration) | `NamazuFlippers/FirstRunWindow.cs` | exact (same ImGui modal + dropdown + SavePluginConfig pattern) |
| `NamazuFlippers/UI/FirstRunWindow.cs` | component (Window subclass, migrated) | request-response | `NamazuFlippers/FirstRunWindow.cs` | exact (is the file; migration is base-class change only) |
| `NamazuFlippers/NamazuFlippers.cs` | controller (plugin entry point, modified) | event-driven | `NamazuFlippers/NamazuFlippers.cs` | exact (modifying the existing file) |
| `tests/phase04_nyquist.sh` | test (bash source-pattern validation) | batch (grep-based) | `tests/phase03_nyquist.sh` | exact |

---

## Pattern Assignments

### `NamazuFlippers/UI/DailyRouteWindow.cs` (component, request-response)

**Analog:** `NamazuFlippers/FirstRunWindow.cs`

**Imports pattern** (FirstRunWindow.cs lines 1-6):
```csharp
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;
using NamazuFlippers.Data;
using System.Numerics;
```

For DailyRouteWindow, extend with:
```csharp
using Dalamud.Interface.Windowing;
using NamazuFlippers.Core;
```

**Window base class constructor pattern** (from RESEARCH.md Pattern 1 — Dalamud verified):
```csharp
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

    public override void Draw() { /* ... */ }
}
```

**Null guard for LatestScanResult at top of Draw** (RESEARCH.md Pitfall 6 + D-10):
```csharp
public override void Draw()
{
    var result = plugin.LatestScanResult;
    if (result == null)
    {
        ImGui.TextWrapped("Scanning for opportunities... Use /nflip scan to refresh.");
        return;
    }
    // ... rest of Draw
}
```

**Result-change detection — wipe bought/listed state** (D-09):
```csharp
if (!ReferenceEquals(result, lastSeenResult))
{
    boughtState.Clear();
    listedState.Clear();
    autoCollapsedStops.Clear();
    lastSeenResult = result;
}
```

**Status banner pattern — all five ScanEngineStatus values** (UI-SPEC §Status States):
```csharp
switch (result.Status)
{
    case ScanEngineStatus.UsingCache:
        ImGui.TextColored(CacheBlue, $"Using cached route from {result.CreatedAtUtc.ToLocalTime():HH:mm}. /nflip scan to refresh.");
        break;
    case ScanEngineStatus.UsingStaleCache:
        ImGui.TextColored(StaleAmber, "Route is outdated. /nflip scan to refresh.");
        break;
    case ScanEngineStatus.Empty:
        ImGui.TextWrapped("No opportunities matched your current settings.");
        break;
    case ScanEngineStatus.Error:
        ImGui.TextColored(ErrorRed, result.UserMessage);
        break;
    // Success: no banner
}
ImGui.Separator();
```

**Progress bar with PlotHistogram color override** (UI-SPEC §Widget Inventory + RESEARCH.md Pattern 3):
```csharp
ImGui.PushStyleColor(ImGuiCol.PlotHistogram, SuccessGreen);
ImGui.ProgressBar(boughtFraction, new Vector2(-1, 16), "");
ImGui.PopStyleColor();

ImGui.PushStyleColor(ImGuiCol.PlotHistogram, PurchaseCyan);
ImGui.ProgressBar(listedFraction, new Vector2(-1, 16), "");
ImGui.PopStyleColor();
```

**Rescan button — right-aligned, disabled while scanning** (UI-SPEC §Interaction Contracts):
```csharp
ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X - buttonWidth);
if (plugin.ScanInProgress)
    ImGui.BeginDisabled();
if (ImGui.Button("Rescan Route"))
    _ = plugin.RescanAsync(scanCts.Token);
if (plugin.ScanInProgress)
    ImGui.EndDisabled();
```

**Auto-collapse on stop completion — one-shot trigger** (UI-SPEC §Auto-collapse, RESEARCH.md Pattern 4):
```csharp
bool allBought = stop.Items.All(item => boughtState.GetValueOrDefault(item.ItemId));

if (allBought && !autoCollapsedStops.GetValueOrDefault(stop.PurchaseSource))
{
    ImGui.SetNextItemOpen(false, ImGuiCond.Always);
    autoCollapsedStops[stop.PurchaseSource] = true;
}
else if (!allBought)
{
    autoCollapsedStops[stop.PurchaseSource] = false;
}

ImGui.PushStyleColor(ImGuiCol.Text, allBought ? CompletedGray : ImGui.GetStyle().Colors[(int)ImGuiCol.Text]);
bool headerOpen = ImGui.CollapsingHeader(allBought
    ? $"✓ {stop.PurchaseSource} — {stop.Items.Count} items — {stop.TotalExpectedDailyProfit:n0} gil/day"
    : headerLabel);
ImGui.PopStyleColor();
```

**Item checkbox row with OOS and CompletedGray** (UI-SPEC §Widget Inventory):
```csharp
ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4, 4));
bool bought = boughtState.GetValueOrDefault(item.ItemId);
if (ImGui.Checkbox($"##bought-{item.ItemId}", ref bought))
    boughtState[item.ItemId] = bought;

ImGui.SameLine();
if (bought)
    ImGui.TextColored(CompletedGray, item.Name);
else if (item.OutOfStock)
    ImGui.TextColored(OosOrange, item.Name);
else
    ImGui.Text(item.Name);

if (item.OutOfStock)
{
    ImGui.SameLine(0, 4);
    ImGui.TextColored(OosOrange, "[OOS]");
}
if (item.IsVendorSource)
{
    ImGui.SameLine(0, 4);
    ImGui.TextColored(VendorCyan, "[Vendor]");
}

ImGui.SameLine();
ImGui.TextColored(PurchaseCyan, $"Buy: {item.PurchasePrice:n0}");
ImGui.SameLine();
ImGui.TextColored(GilGold, $"+{item.ExpectedDailyProfit:n0}/day");

if (ImGui.IsItemHovered())
{
    ImGui.BeginTooltip();
    ImGui.Text($"Avg {item.SalesPerDay:F1} sales/day");
    ImGui.EndTooltip();
}
ImGui.PopStyleVar();
```

**Listed checkbox (home stop only)** (UI-SPEC §Widget Inventory, UI-03):
```csharp
bool listed = listedState.GetValueOrDefault(item.ItemId);
if (ImGui.Checkbox($"##listed-{item.ItemId}", ref listed))
    listedState[item.ItemId] = listed;
ImGui.SameLine();
ImGui.TextColored(GilGold, $"List: {item.HomePrice:n0}");
```

**Error handling:** No try/catch inside Draw — PushStyleColor/PopStyleColor must always be paired. Use try/finally if any early-return exists between a push and pop. See RESEARCH.md Pitfall 4.

---

### `NamazuFlippers/UI/ConfigWindow.cs` (component, CRUD)

**Analog:** `NamazuFlippers/FirstRunWindow.cs` (SavePluginConfig, BeginCombo over KnownWorlds, BeginPopupModal pattern)

**Imports pattern** (mirrors FirstRunWindow.cs lines 1-6, plus Windowing):
```csharp
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;
using NamazuFlippers.Data;
using System.Numerics;
```

**Window constructor** (mirrors DailyRouteWindow, different size per UI-SPEC):
```csharp
public class ConfigWindow : Window
{
    private readonly NamazuFlippers plugin;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IPluginLog log;

    private Configuration? snapshot;
    private bool isDirty;
    private bool showUnsavedModal;

    public ConfigWindow(NamazuFlippers plugin, IDalamudPluginInterface pluginInterface, IPluginLog log)
        : base("Namazu Flippers — Settings", ImGuiWindowFlags.None)
    {
        this.plugin = plugin;
        this.pluginInterface = pluginInterface;
        this.log = log;
        this.Size = new Vector2(400, 500);
        this.SizeCondition = ImGuiCond.FirstUseEver;
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(300, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }
}
```

**Snapshot on open — D-12** (RESEARCH.md Pattern 5):
```csharp
public override void OnOpen()
{
    snapshot = CloneConfiguration(plugin.Configuration);
    isDirty = false;
}
```

**Dirty flag + close-intercept** (RESEARCH.md Pattern 5 + Pitfall 3):
```csharp
public override void OnClose()
{
    if (isDirty)
    {
        IsOpen = true;           // cancel the close
        showUnsavedModal = true; // trigger modal on next Draw frame
    }
}
```

**HomeWorld dropdown — mirrors FirstRunWindow.cs lines 62-74 exactly:**
```csharp
// FirstRunWindow.cs lines 58-74 — copy this structure for ConfigWindow HomeWorld
var preview = selectedWorldIndex >= 0 && selectedWorldIndex < WorldData.KnownWorlds.Length
    ? WorldData.KnownWorlds[selectedWorldIndex]
    : "Choose a world...";

if (ImGui.BeginCombo("##home-world-combo", preview))
{
    for (int i = 0; i < WorldData.KnownWorlds.Length; i++)
    {
        var isSelected = i == selectedWorldIndex;
        if (ImGui.Selectable(WorldData.KnownWorlds[i], isSelected))
            selectedWorldIndex = i;
        if (isSelected)
            ImGui.SetItemDefaultFocus();
    }
    ImGui.EndCombo();
}
```

**Widget-per-setting pattern with dirty flag** (UI-SPEC §ConfigWindow Widgets):
```csharp
// SliderInt example
if (ImGui.SliderInt("Min ROI %%", ref plugin.Configuration.PreferredRoi, 0, 100))
    isDirty = true;

// InputInt with floor clamp
if (ImGui.InputInt("Min Profit (gil)", ref plugin.Configuration.MinProfitAmount))
{
    plugin.Configuration.MinProfitAmount = Math.Max(0, plugin.Configuration.MinProfitAmount);
    isDirty = true;
}

// Checkbox
if (ImGui.Checkbox("Region-wide search", ref plugin.Configuration.RegionWide))
    isDirty = true;

if (ImGui.IsItemHovered())
{
    ImGui.BeginTooltip();
    ImGui.Text("Search all data centers, not just your DC");
    ImGui.EndTooltip();
}
```

**Save and Reset buttons** (UI-SPEC §Widget Inventory, D-13):
```csharp
if (ImGui.Button("Save Settings"))
{
    pluginInterface.SavePluginConfig(plugin.Configuration);
    snapshot = CloneConfiguration(plugin.Configuration);
    isDirty = false;
}

ImGui.SameLine();
ImGui.PushStyleColor(ImGuiCol.Text, ErrorRed);
if (ImGui.Button("Reset to Defaults"))
    ImGui.OpenPopup("ConfirmReset##config");
ImGui.PopStyleColor();
```

**Confirmation modals — mirrors FirstRunWindow.cs BeginPopupModal pattern (lines 52-98):**
```csharp
// Reset confirmation (D-13)
var dummyOpen = true;
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

// Unsaved-changes modal (D-12)
if (showUnsavedModal)
{
    ImGui.OpenPopup("UnsavedChanges##config");
    showUnsavedModal = false;
}
if (ImGui.BeginPopupModal("UnsavedChanges##config", ref dummyOpen, ImGuiWindowFlags.AlwaysAutoResize))
{
    ImGui.Text("Save changes before closing?");
    ImGui.Spacing();
    if (ImGui.Button("Save", new Vector2(120, 0)))
    {
        pluginInterface.SavePluginConfig(plugin.Configuration);
        isDirty = false;
        IsOpen = false;
        ImGui.CloseCurrentPopup();
    }
    ImGui.SameLine();
    if (ImGui.Button("Discard", new Vector2(120, 0)))
    {
        CopySnapshot(snapshot!, plugin.Configuration);
        isDirty = false;
        IsOpen = false;
        ImGui.CloseCurrentPopup();
    }
    ImGui.SameLine();
    if (ImGui.Button("Cancel", new Vector2(120, 0)))
        ImGui.CloseCurrentPopup();
    ImGui.EndPopup();
}
```

**Error handling:** No try/catch in Draw. All PushStyleColor/PushStyleVar calls must be matched. See Shared Patterns below.

---

### `NamazuFlippers/UI/FirstRunWindow.cs` (component, migrated)

**Analog:** `NamazuFlippers/FirstRunWindow.cs` — this IS the source file; migration changes only the base class.

**Migration delta — add Window inheritance:**
```csharp
// Before (FirstRunWindow.cs line 15):
public class FirstRunWindow

// After:
public class FirstRunWindow : Window
```

Add constructor call to `Window` base:
```csharp
public FirstRunWindow(Configuration configuration, IDalamudPluginInterface pluginInterface, IPluginLog log)
    : base("Welcome to Namazu Flippers", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize)
{
    // existing field assignments unchanged
}
```

Replace the manual `Draw()` guard logic with `Window.IsOpen` semantics — the `Window` base class manages visibility. The `IsPending` check and the `OpenPopup` call move into `OnOpen()` or the Draw override with appropriate guards.

**All other code (BeginCombo, Selectable, BeginPopupModal, Button pattern) is unchanged — copy it as-is.** The full pattern is lines 44–101 of `NamazuFlippers/FirstRunWindow.cs`.

---

### `NamazuFlippers/NamazuFlippers.cs` (controller, modified)

**Analog:** `NamazuFlippers/NamazuFlippers.cs` — modifying the existing file.

**Key existing state (lines 29-40) — read before modifying:**
```csharp
private int scanInProgress;          // line 29 — needs public accessor for windows
private bool isVisible;              // line 30 — replaced by DailyRouteWindow.IsOpen

public string? LastApiError { get; private set; }       // line 36
public ScanEngineResult? LatestScanResult { get; private set; }  // line 38
public Configuration Configuration { get; set; }         // line 40
```

**New public accessors to expose for window classes** (RESEARCH.md Open Questions 1, 2):
```csharp
// Add after existing public properties:
public bool ScanInProgress => Interlocked.CompareExchange(ref scanInProgress, 0, 0) == 1;
public Task RescanAsync(CancellationToken ct) => RunScanAsync(true, ct);
// Make RunScanAsync internal (not private) so window classes in same assembly can call directly if preferred.
```

**WindowSystem wiring in constructor** (RESEARCH.md Pattern 2 — Dalamud verified):
```csharp
// Add field:
private readonly WindowSystem windowSystem = new("NamazuFlippers");

// In constructor, after creating window instances:
windowSystem.AddWindow(dailyRouteWindow);
windowSystem.AddWindow(configWindow);
windowSystem.AddWindow(firstRunWindow);

// Replace existing line 69:
// pluginInterface.UiBuilder.Draw += OnDraw;
pluginInterface.UiBuilder.Draw += windowSystem.Draw;

// Store handler for unsubscription (RESEARCH.md Pitfall 1):
private void OnOpenConfigUi() => configWindow.IsOpen = true;
pluginInterface.UiBuilder.OpenConfigUi += OnOpenConfigUi;
```

**Dispose — extend existing pattern (lines 77-85):**
```csharp
public void Dispose()
{
    clientState.Login -= OnLogin;
    scanCts.Cancel();
    scanCts.Dispose();
    pluginInterface.UiBuilder.Draw -= windowSystem.Draw;           // changed
    pluginInterface.UiBuilder.OpenConfigUi -= OnOpenConfigUi;     // added
    windowSystem.RemoveAllWindows();                               // added
    commandManager.RemoveHandler(CommandName);
    log.Information("Namazu Flippers unloaded.");
}
```

**OnCommand — toggle DailyRouteWindow.IsOpen instead of isVisible (lines 87-110):**
```csharp
// Replace: isVisible = !isVisible;
// With:
dailyRouteWindow.IsOpen = !dailyRouteWindow.IsOpen;
```

**OnDraw — remove or gut (lines 112-117):** The body becomes empty or removed; WindowSystem.Draw handles all rendering. If `OnDraw` is kept for any residual logic, the `firstRunWindow.Draw()` call is removed since FirstRunWindow is now managed by WindowSystem.

---

### `tests/phase04_nyquist.sh` (test, batch)

**Analog:** `tests/phase03_nyquist.sh` — copy the entire shell structure verbatim.

**Helper functions to copy from phase03_nyquist.sh lines 1-88 (copy verbatim):**
- `pass()` — prints "ok - {label}"
- `fail()` — prints "not ok - {label}", increments `$failures`
- `require_file()` — checks file existence
- `require_pattern()` — greps for single pattern in file
- `require_absent_pattern()` — asserts pattern NOT present
- `require_order()` — asserts first pattern appears before second (by line number)
- `require_all_patterns()` — greps for multiple patterns, reports all missing ones

**Script structure to follow** (phase03_nyquist.sh lines 89-228):
```bash
#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

failures=0

# [paste helper functions here — identical to phase03_nyquist.sh lines 9-87]

echo "Phase 04 Nyquist validation"

require_file "NamazuFlippers/UI/DailyRouteWindow.cs"
require_file "NamazuFlippers/UI/ConfigWindow.cs"
require_file "NamazuFlippers/UI/FirstRunWindow.cs"

echo
echo "UI-01: WindowSystem wiring and DailyRouteWindow scaffolding"
require_all_patterns "NamazuFlippers/NamazuFlippers.cs" "WindowSystem registered and all windows added" \
  "WindowSystem" \
  "AddWindow" \
  "windowSystem\.Draw"
require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" ": Window$|: Window\b" "DailyRouteWindow extends Window base class"
require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" "CollapsingHeader" "route stops rendered as CollapsingHeader"

echo
echo "UI-02: bought checkbox and boughtState"
require_all_patterns "NamazuFlippers/UI/DailyRouteWindow.cs" "bought checkbox uses itemId key and updates boughtState" \
  "##bought-" \
  "boughtState"

echo
echo "UI-03: listed checkbox in home stop"
require_all_patterns "NamazuFlippers/UI/DailyRouteWindow.cs" "listed checkbox uses itemId key and updates listedState" \
  "##listed-" \
  "listedState"

echo
echo "UI-04: profit tally in GilGold"
require_all_patterns "NamazuFlippers/UI/DailyRouteWindow.cs" "profit tally rendered with GilGold color" \
  "GilGold\|1\.0f, 0\.85f, 0\.1f" \
  "ExpectedDailyProfit\|listedState"

echo
echo "UI-05: progress bars with PlotHistogram color override"
require_all_patterns "NamazuFlippers/UI/DailyRouteWindow.cs" "progress bars use PlotHistogram color push" \
  "PlotHistogram" \
  "ProgressBar"
require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" "0\.2f, 0\.8f, 0\.3f" "SuccessGreen color value present"
require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" "0\.2f, 0\.85f, 0\.9f" "PurchaseCyan color value present"

echo
echo "UI-06: OOS highlighting with OosOrange"
require_all_patterns "NamazuFlippers/UI/DailyRouteWindow.cs" "OOS items highlighted with OosOrange and [OOS] badge" \
  "OosOrange\|1\.0f, 0\.55f, 0\.1f" \
  "\[OOS\]" \
  "OutOfStock"

echo
echo "UI-07: auto-collapse on stop completion"
require_all_patterns "NamazuFlippers/UI/DailyRouteWindow.cs" "auto-collapse uses SetNextItemOpen and per-stop flag" \
  "SetNextItemOpen" \
  "autoCollapsedStops"

echo
echo "UI-08: ConfigWindow scaffolding, Save, and Reset modal"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" ": Window$|: Window\b" "ConfigWindow extends Window base class"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "SavePluginConfig" "ConfigWindow calls SavePluginConfig on Save"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "BeginPopupModal" "ConfigWindow uses BeginPopupModal for Reset confirmation"
require_pattern "NamazuFlippers/NamazuFlippers.cs" "OpenConfigUi" "UiBuilder.OpenConfigUi registered for gear icon access"

echo
echo "CONF-01..09: all configuration widgets present"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "BeginCombo" "HomeWorld dropdown (CONF-01)"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "SliderInt.*PreferredRoi\|PreferredRoi.*SliderInt" "PreferredRoi slider (CONF-02)"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "InputInt.*MinProfitAmount\|MinProfitAmount.*InputInt\|MinProfitAmount" "MinProfitAmount input (CONF-02)"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "MinDesiredAvgPpu" "MinDesiredAvgPpu input (CONF-02)"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "MaxBudgetPerItem" "MaxBudgetPerItem input (CONF-02)"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "MinSalesPerDay\|SliderFloat" "MinSalesPerDay slider (CONF-03)"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "MinSalesPerWeek" "MinSalesPerWeek slider (CONF-03)"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "RegionWide" "RegionWide checkbox (CONF-04)"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "CategoryFilters\|Furniture\|Collectible\|Glamour" "CategoryFilters checkboxes (CONF-05)"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "IncludeVendors" "IncludeVendors checkbox (CONF-06)"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "ShowOutOfStock" "ShowOutOfStock checkbox (CONF-06)"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "MaxItemsPerSession" "MaxItemsPerSession slider (CONF-07)"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "MaxServersToVisit" "MaxServersToVisit slider (CONF-07)"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "CacheDurationHours" "CacheDurationHours slider (CONF-08)"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "EnableShortagePredictor" "EnableShortagePredictor checkbox visible but inert (Phase 6)"

echo
echo "Color token integrity (UI-SPEC compliance)"
require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" "1\.0f, 0\.85f, 0\.1f" "GilGold color value"
require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" "1\.0f, 0\.55f, 0\.1f" "OosOrange color value"
require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" "0\.9f, 0\.2f, 0\.2f" "ErrorRed color value"
require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" "0\.5f, 0\.5f, 0\.5f" "CompletedGray color value"

if [[ "$failures" -ne 0 ]]; then
  printf '\nPhase 04 Nyquist validation failed: %d check(s) failed.\n' "$failures" >&2
  exit 1
fi

printf '\nPhase 04 Nyquist validation passed.\n'
```

---

## Shared Patterns

### PushStyleColor / PopStyleColor Safety
**Source:** `NamazuFlippers/FirstRunWindow.cs` lines 79-86 (BeginDisabled pattern is the same push/pop discipline)
**Apply to:** All Draw methods in DailyRouteWindow and ConfigWindow
```csharp
// Every PushStyleColor must have a matching PopStyleColor in the same frame.
// Never place a return statement between a push and its pop.
// If early exit is needed, restructure with try/finally or pre-compute the condition.
ImGui.PushStyleColor(ImGuiCol.PlotHistogram, SuccessGreen);
ImGui.ProgressBar(fraction, new Vector2(-1, 16), "");
ImGui.PopStyleColor();
```

### BeginDisabled Guard
**Source:** `NamazuFlippers/FirstRunWindow.cs` lines 79-86
**Apply to:** Rescan Route button (DailyRouteWindow), Confirm button (FirstRunWindow)
```csharp
// FirstRunWindow.cs lines 79-86
var canConfirm = selectedWorldIndex >= 0 && selectedWorldIndex < WorldData.KnownWorlds.Length;
if (!canConfirm)
    ImGui.BeginDisabled();

bool confirmPressed = ImGui.Button("Confirm", new Vector2(120, 0));

if (!canConfirm)
    ImGui.EndDisabled();
```

### SavePluginConfig Persistence
**Source:** `NamazuFlippers/FirstRunWindow.cs` line 91; `NamazuFlippers/NamazuFlippers.cs` line 53
**Apply to:** ConfigWindow Save button
```csharp
// FirstRunWindow.cs line 91
pluginInterface.SavePluginConfig(configuration);
```

### BeginPopupModal Pattern
**Source:** `NamazuFlippers/FirstRunWindow.cs` lines 49-98 (the full popup lifecycle)
**Apply to:** ConfigWindow Reset confirmation modal, ConfigWindow unsaved-changes modal
```csharp
// Open (called outside BeginPopupModal):
ImGui.OpenPopup("Welcome to Namazu Flippers");  // FirstRunWindow.cs line 49

// Render (called every frame — renders when popup is active):
var popupOpen = true;
if (ImGui.BeginPopupModal("Welcome to Namazu Flippers", ref popupOpen,
    ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize))
{
    // ... content ...
    ImGui.EndPopup();
}
```

### Lambda Event Handler Storage
**Source:** `NamazuFlippers/NamazuFlippers.cs` lines 67-69 (existing event subscriptions pattern)
**Apply to:** `UiBuilder.OpenConfigUi` subscription in NamazuFlippers.cs
```csharp
// Existing pattern to mirror (NamazuFlippers.cs line 67-68):
clientState.Login += OnLogin;
pluginInterface.UiBuilder.Draw += OnDraw;
// ... and in Dispose:
clientState.Login -= OnLogin;
pluginInterface.UiBuilder.Draw -= OnDraw;

// Apply same named-method pattern for OpenConfigUi — NOT an anonymous lambda:
private void OnOpenConfigUi() => configWindow.IsOpen = true;
// In constructor: pluginInterface.UiBuilder.OpenConfigUi += OnOpenConfigUi;
// In Dispose:    pluginInterface.UiBuilder.OpenConfigUi -= OnOpenConfigUi;
```

### Interlocked Exchange Guard
**Source:** `NamazuFlippers/NamazuFlippers.cs` lines 145-148 (exact pattern for scanInProgress)
**Apply to:** `ScanInProgress` public accessor; DailyRouteWindow's BeginDisabled guard
```csharp
// NamazuFlippers.cs lines 145-148
if (Interlocked.Exchange(ref scanInProgress, 1) == 1)
{
    log.Information("/nflip: scan already running.");
    return;
}
```

---

## Color Palette Constants

**Source:** `04-UI-SPEC.md` §Color (locked values — do not deviate)
**Apply to:** Both DailyRouteWindow.cs and ConfigWindow.cs as private static readonly fields or a shared `UiColors.cs` static class

```csharp
// Declare as static readonly fields (in each window class or in a shared UiColors class)
private static readonly Vector4 GilGold       = new(1.0f, 0.85f, 0.1f,  1.0f);
private static readonly Vector4 PurchaseCyan  = new(0.2f, 0.85f, 0.9f,  1.0f);
private static readonly Vector4 VendorCyan    = new(0.2f, 0.85f, 0.9f,  1.0f);  // same as PurchaseCyan
private static readonly Vector4 OosOrange     = new(1.0f, 0.55f, 0.1f,  1.0f);
private static readonly Vector4 StaleAmber    = new(0.9f, 0.7f,  0.1f,  1.0f);
private static readonly Vector4 ErrorRed      = new(0.9f, 0.2f,  0.2f,  1.0f);
private static readonly Vector4 SuccessGreen  = new(0.2f, 0.8f,  0.3f,  1.0f);
private static readonly Vector4 CompletedGray = new(0.5f, 0.5f,  0.5f,  0.7f);
private static readonly Vector4 CacheBlue     = new(0.4f, 0.7f,  1.0f,  1.0f);
```

**Planner note:** If D-08 results in a `UI/PluginUi.cs` or similar container, `UiColors.cs` alongside it is the natural place for these constants — single source of truth for nyquist.sh color-value assertions.

---

## No Analog Found

All five target files have analogs. No files require falling back to RESEARCH.md patterns exclusively.

| File | Note |
|---|---|
| All five files | Strong analogs found. RESEARCH.md patterns fill gaps where the analog lacks a direct example (e.g., `Window` base class subclassing, `WindowSystem` registration). |

---

## Metadata

**Analog search scope:** `NamazuFlippers/` (all .cs files), `tests/` (all .sh files)
**Files scanned:** 19 source files + 1 test script
**Key source files read:** `NamazuFlippers.cs`, `FirstRunWindow.cs`, `Configuration.cs`, `ScanEngineResult.cs`, `RouteStop.cs`, `RankedOpportunity.cs`, `WorldData.cs` (header), `tests/phase03_nyquist.sh`
**Pattern extraction date:** 2026-05-06
