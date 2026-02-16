# Content Delivery Test Matrix

This matrix validates optional-module behavior for the DevKit Addressables/CCD feature set.

## Environments

| Case | com.unity.addressables | com.unity.services.ccd.management | Expected compile define(s) |
|---|---|---|---|
| A | Installed | Installed | `PITECH_ADDR`, `PITECH_CCD` |
| B | Installed | Missing | `PITECH_ADDR` only |
| C | Missing | Missing | none |

## Required Checks

### Case A (Addressables + CCD)

- Guided Setup card shows `Addressables ready` + `CCD package present`.
- `Setup` creates/updates:
  - Addressables settings
  - module config asset
  - required profile/path values
  - one remote group for selected lab id
- `Validate` reports:
  - duplicate key checks
  - profile/path checks
  - remote schema checks
  - group-per-lab mapping checks
- Hidden `Build` action (enabled in config) runs and produces:
  - output path
  - content hash
  - catalog hash
  - report asset/json with state history

### Case B (Addressables only)

- Guided Setup card shows `Addressables ready` + `CCD optional`.
- Setup and Validate still work.
- Build still works for local Addressables build.
- CCD fields remain optional in report.

### Case C (No Addressables)

- Guided Setup card shows `Addressables missing`.
- Setup exits safely with clear message.
- Validate exits safely with clear diagnostics.
- No compile/runtime errors from Content Delivery module.

## Runtime Smoke Checks

- `AddressablesBootstrapper` initializes `IContentDeliveryService`.
- Launch context resolution order:
  1. external context (bridge registry)
  2. `ILaunchContextProvider` components
  3. fallback (Unity menu or direct)
- `LaunchContextReporter` emits `launch_resolved` payload with:
  - `launchRequestId`
  - `attempt_id`
  - `idempotency_key`
  - `data.resolvedVersionId`
- `EmitExperienceAbandoned()` emits duration fields:
  - `sessionDurationSeconds`
  - `durationMs`
- `AttemptReconciliationBridge.ReconcileAttempt()` updates local-first attempt identity.

## Idempotency + State Machine Checks

- Transition validity enforced by `PublishTransactionStateMachine`.
- Invalid transitions are rejected without mutating state.
- `PublishTransactionIdempotency.BuildKey()` is deterministic.

## Editor Test Suite

Run in Unity Test Runner (EditMode):

- `PublishTransactionStateMachineTests`
- `PublishTransactionIdempotencyTests`
- `AttemptIdentityManagerTests`
