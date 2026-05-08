---
status: resolved
trigger: "Discard does not revert the change. Save does save the change correctly. Cancel closes the modal but keeps the ConfigWindow open."
created: 2026-05-07T09:30:00Z
updated: 2026-05-08T01:00:00Z
symptoms_prefilled: true
goal: find_root_cause_only
---

## Current Focus

hypothesis: CONFIRMED — OnOpen() is spuriously fired on the frame after OnClose() re-opens the window (the dirty-cancel path), overwriting the clean snapshot with the edited values before the user sees the modal.

test: traced internalLastIsOpen state machine through Dalamud WindowHost.DrawInternal source
expecting: OnOpen fires only on true open transition, not on re-open caused by OnClose re-entry
next_action: DIAGNOSIS COMPLETE

## Symptoms

expected: Discard button restores snapshot to plugin.Configuration, clears isDirty, closes window (D-12)
actual: Change persists after Discard — reopening ConfigWindow shows the edited value, not the pre-edit value
errors: none
reproduction: Open ConfigWindow, change a setting, close window, click Discard in modal, reopen ConfigWindow, observe value unchanged
started: Discovered during Phase 4 UAT

## Eliminated

- hypothesis: "Snapshot is a reference copy (same object) — mutating Configuration mutates snapshot"
  evidence: "Snapshot() method creates new Configuration object with each field individually copied. Arrays use .Clone(). Not a reference copy."
  timestamp: 2026-05-07T09:30:00Z

- hypothesis: "RestoreFrom() is missing one or more properties"
  evidence: "RestoreFrom() lists all 17 properties, mirrors Snapshot() field-for-field. No missing properties."
  timestamp: 2026-05-07T09:30:00Z

- hypothesis: "CategoryFilters clone is shallow — array items are shared references"
  evidence: "CategoryFilters is int[] (value type elements). .Clone() on int[] produces fully independent copy."
  timestamp: 2026-05-07T09:30:00Z

- hypothesis: "plugin.Configuration is reassigned after Discard"
  evidence: "NamazuFlippers.cs: Configuration { get; set; } assigned only once at line 66 (plugin init). No reassignment."
  timestamp: 2026-05-07T09:30:00Z

## Evidence

- timestamp: 2026-05-07T09:30:00Z
  checked: "ConfigWindow.cs Snapshot() method (lines 334-356)"
  found: "Creates new Configuration instance; copies all 17 fields individually; CategoryFilters uses (int[])source.CategoryFilters.Clone(); PreferredCategories uses (string[])source.PreferredCategories.Clone()"
  implication: "Snapshot IS a true deep copy. Mutation of plugin.Configuration after OnOpen does NOT corrupt snapshot."

- timestamp: 2026-05-07T09:30:00Z
  checked: "ConfigWindow.cs RestoreFrom() method (lines 358-377)"
  found: "Copies all 17 fields from snapshot to target; arrays cloned. Matches Snapshot() property list exactly."
  implication: "RestoreFrom() is mechanically complete — no missing properties."

- timestamp: 2026-05-07T09:30:00Z
  checked: "ConfigWindow.cs Discard handler (lines 300-306)"
  found: "if (snapshot != null) RestoreFrom(snapshot, plugin.Configuration); isDirty = false; IsOpen = false; ImGui.CloseCurrentPopup();"
  implication: "Discard code runs in correct order. BUT: if snapshot was overwritten before Discard runs, restoration has no effect."

- timestamp: 2026-05-07T09:45:00Z
  checked: "Dalamud WindowHost.DrawInternal source (github.com/goatcorp/Dalamud/blob/master/Dalamud/Interface/Windowing/WindowHost.cs lines 138-193)"
  found: |
    OnClose and OnOpen fire based on internalLastIsOpen state transitions, NOT inline in the same frame.
    The exact sequence when user closes a dirty window:

    Frame N: Dalamud sets IsOpen=false (user clicked X). internalLastIsOpen=true.

    Frame N+1: DrawInternal runs.
      - !IsOpen branch entered.
      - IsOpen(false) != internalLastIsOpen(true) → true
      - LINE 142: internalLastIsOpen = false  ← CRITICAL: set BEFORE OnClose() fires
      - LINE 143: OnClose() fires.
        * isDirty=true → sets IsOpen=true, showUnsavedModal=true
      - Code returns early (still in !IsOpen branch, evaluated at branch entry).

    Frame N+2: DrawInternal runs.
      - IsOpen=true (re-set by OnClose). !IsOpen branch skipped.
      - LINE 190: internalLastIsOpen(false) != IsOpen(true) && IsOpen → TRUE
      - LINE 192: internalLastIsOpen = true
      - LINE 193: OnOpen() fires!
        * snapshot = Snapshot(plugin.Configuration) ← SNAPSHOT OVERWRITTEN WITH EDITED VALUES
      - Draw() runs. showUnsavedModal=true → modal opens.

    User sees modal, clicks Discard:
      - RestoreFrom(snapshot, plugin.Configuration) ← restores the EDITED snapshot
      - No visible change — values remain as user edited them.
  implication: |
    The snapshot is overwritten by a spurious OnOpen() call before the user interacts with the modal.
    The root cause is that OnClose() setting IsOpen=true causes internalLastIsOpen to be out of sync
    (it was set to false before OnClose ran), which makes Dalamud believe the window was re-opened
    and fires OnOpen(), corrupting the snapshot.

- timestamp: 2026-05-07T09:45:00Z
  checked: "ConfigWindow.cs OnClose() (lines 52-59)"
  found: "if (isDirty) { IsOpen = true; showUnsavedModal = true; }"
  implication: |
    This is the trigger. Setting IsOpen=true inside OnClose() causes the internalLastIsOpen
    mismatch. After OnClose fires, internalLastIsOpen=false but IsOpen=true, so next frame
    Dalamud fires OnOpen() which overwrites snapshot.

## Resolution

root_cause: |
  Dalamud's WindowHost.DrawInternal fires OnOpen() whenever internalLastIsOpen transitions
  false→true. This transition is triggered unintentionally when OnClose() re-opens the
  window (sets IsOpen=true) to cancel a dirty close:

  Frame N+1: DrawInternal sets internalLastIsOpen=false, then calls OnClose().
  OnClose() sets IsOpen=true (to cancel close) and showUnsavedModal=true.

  Frame N+2: DrawInternal sees internalLastIsOpen=false, IsOpen=true → fires OnOpen().
  OnOpen() calls snapshot = Snapshot(plugin.Configuration), overwriting the clean
  snapshot with the already-edited values.

  When the user then clicks Discard in the modal, RestoreFrom(snapshot, plugin.Configuration)
  restores the corrupted snapshot (edited values) into plugin.Configuration — producing
  no visible change.

  Evidence: WindowHost.DrawInternal source (goatcorp/Dalamud, lines 138-193). The
  internalLastIsOpen assignment (line 142) precedes the OnClose() call (line 143),
  creating the false→true transition that triggers OnOpen() on the next frame.

fix: "No fix applied — diagnosis only per goal: find_root_cause_only"
verification: ""
files_changed: []
