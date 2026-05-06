# Code Review Findings

## Finding 1: Stale Manifest API Level Can Ship an Unloadable Plugin

**Priority:** P1  
**File:** `NamazuFlippers/NamazuFlippers.json`  
**Lines:** 7-10

The project is using `Dalamud.NET.Sdk` 15.0.0, but the source manifest hard-codes `DalamudApiLevel` as 12. Dalamud v15 is API level 15, and the current docs say DalamudPackager fills `AssemblyVersion`, `InternalName`, and `DalamudApiLevel` automatically.

**Requested fix:** Remove these generated fields from the source manifest, or ensure the packaged manifest is regenerated with API 15 before release.

## Finding 2: Home World Is Persisted Without Validation

**Priority:** P2  
**File:** `NamazuFlippers/NamazuFlippers.cs`  
**Lines:** 137-140

The first-run popup accepts any non-empty string and saves it directly. A typo or unsupported world name will later poison every scan request until the user can find and edit config.

**Requested fix:** Prefer a world picker from a known world/DC table, or at least validate the entered world name and keep the modal open with an inline error.

## Finding 3: CI Does Not Run for Workflow-Only PR Changes

**Priority:** P2  
**File:** `.github/workflows/build.yml`  
**Lines:** 9-12

The `push` trigger includes workflow changes, but `pull_request` only watches `NamazuFlippers/**`. A PR that edits this workflow can merge without the workflow validating itself.

**Requested fix:** Add `.github/workflows/**` to the `pull_request.paths` list.

## Finding 4: Config Completion Is Overstated Versus Implemented UI

**Priority:** P3  
**File:** `.planning/REQUIREMENTS.md`  
**Lines:** 28-38

The planning requirements mark user-settable config as complete, but the code only exposes the home-world prompt. The rest are default properties with no UI or validation yet.

**Requested fix:** Reclassify `CONF-02` through `CONF-08` as model-ready but not user-complete, or update phase tracking so future work does not hide missing user-facing config behavior.
