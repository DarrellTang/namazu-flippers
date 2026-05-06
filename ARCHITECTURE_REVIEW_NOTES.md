# Architecture Review Notes

These notes capture C#/.NET and architecture issues that should be addressed early, before API, scan, routing, and UI layers are built on top of the current scaffold.

## Fix Before Building More Features

### 1. Separate Plugin Lifecycle From UI Rendering

**File:** `NamazuFlippers/NamazuFlippers.cs`

`NamazuFlippers.cs` currently handles plugin lifecycle, command registration, config loading, UI state, and ImGui rendering. This is acceptable for a stub, but it will compound quickly once the route window, config window, scan state, and session state are added.

**Recommendation:** Keep the plugin entry point thin. Move popup/window rendering into dedicated window classes before adding more UI.

### 2. Add Validation Boundaries

**Files:** `NamazuFlippers/Configuration.cs`, future UI/API boundary code

`Configuration` is a simple persisted model, which is fine, but config values need validation before they are saved or used for API calls. This matters for home world, ROI, profit amounts, max items, max servers, category filters, and cache duration.

**Recommendation:** Add validation in the config UI and/or scan-engine boundary. Do not allow invalid persisted config to silently poison API requests.

### 3. Avoid Magic Category IDs Leaking Across the App

**File:** `NamazuFlippers/Configuration.cs`

`CategoryFilters` stores raw Saddlebag category IDs directly. This is acceptable for serialization, but future UI and API code should not spread numeric IDs everywhere.

**Recommendation:** Define named category presets or constants, such as furniture/glamour/collectibles mappings, in one place. Let config store the serialized values, but let application code work with named concepts.

### 4. Avoid Directly Exposing Mutable Arrays Long Term

**File:** `NamazuFlippers/Configuration.cs`

`int[] CategoryFilters` and `string[] PreferredCategories` are mutable reference types. Any code can mutate them in place without an explicit save path or validation pass.

**Recommendation:** For persisted config this may be tolerable, but internal code should copy arrays before using them, or expose helper methods/properties that keep mutation controlled.

### 5. Add a Git Ignore File

**Repo-level issue**

The repository currently has generated/tool output sitting untracked, including `bin/`, `obj/`, `.firecrawl/`, and `.pi/`.

**Recommendation:** Add a `.gitignore` for .NET/Dalamud build artifacts and local tool output before further development creates noisy diffs or accidental commits.

### 6. Do Not Let the Main Plugin Class Become a Service Locator

**File:** `NamazuFlippers/NamazuFlippers.cs`

As `SaddlebagClient`, `ScanEngine`, `RouteOptimizer`, `SessionStore`, and UI windows are added, avoid piling all mutable application state and orchestration directly into the plugin entry point.

**Recommendation:** Create small classes with clear ownership:

- `SaddlebagClient` for HTTP/API access
- `ScanEngine` for scan orchestration and filtering
- `RouteOptimizer` for route grouping/order logic
- `SessionStore` for local JSON persistence
- dedicated window classes for ImGui rendering

## C#/.NET Practices To Adopt Early

- Use `CancellationToken` for future async API calls.
- Keep a long-lived `HttpClient` or dedicated API client; do not create/dispose `HttpClient` per request.
- Do not block the UI thread with `.Result`, `.Wait()`, or synchronous network calls.
- Keep pure logic testable without Dalamud dependencies, especially route optimization and scan filtering.
- Use integer gil amounts or `decimal` for money-like values; avoid floating point for prices/profit.

## Lower Priority Cleanup

- XML comments are currently verbose but harmless. Reduce or focus them later once the code stabilizes.
- Remove `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` from the project file until unsafe code is actually needed.
- Consider making `Configuration`'s setter private later: `public Configuration Configuration { get; private set; }`.

## Main Compounding Risk

The biggest architectural risk is letting `NamazuFlippers.cs` grow into a large entry-point class that owns lifecycle, service orchestration, rendering, API calls, cache state, and user interactions. Split UI and core services before Phase 2/3 so future changes have clear boundaries.
