# Addressables Content Delivery Guide

This guide covers the optional Addressables/CCD workflow in the Pi tech XR DevKit.

## Scope

- Setup + Prefab Mapping + Validate + Build is the default flow.
- Use the dedicated `Addressables Builder` window (not Guided Setup scene card).
- Runtime integration is optional and isolated from `SceneManager`.

## Prerequisites

- Unity 2022.3+
- `com.unity.addressables`
- Optional: `com.unity.services.ccd.management`

## 1) Open Builder Window

Open `Pi tech -> Addressables Builder`.

Use buttons in order:

1. `1) Setup`
2. choose `Prefab to include`
3. `2) Map Prefab`
4. `3) Validate`
5. `4) Build`

Fast option: `Fast: One Minute Build`.

Setup is idempotent and ensures:

- `AddressablesModuleConfig` asset exists
- Addressables settings exist
- target profile exists and has remote path values
- one remote group exists for the selected lab id

## 2) Map Prefab (Explicit)

In `Addressables Builder`:

- set `Lab Id (group key)`
- assign `Prefab to include`
- click **Map Prefab**

This maps the selected prefab to the lab group and assigns a deterministic address key.

## 3) Validate (Non-Destructive)

Primary action: **Validate**

Validation checks include:

- duplicate address keys
- remote profile/path sanity
- expected remote group + schema
- one-group-per-lab mapping
- empty groups
- build target visibility

Validation emits diagnostics and writes a publish transaction report JSON/asset.

Warnings are intentionally kept in the report (non-blocking) for audit/debug visibility.
Only validation errors block the build gate.

## 4) Build

Build flow:

1. Re-runs validation gate
2. Starts build state transitions in report
3. Executes local Addressables build
4. Computes deterministic content and catalog hashes
5. Persists report asset + JSON

Builder output fields:

- `Upload folder`: the folder to upload to portal/CCD (primary output)
- `Internal path`: Unity Addressables internal build cache path (normally under `Library`)

Best practice:

- keep Addressables build output outside `Assets` (default workspace: `Build/ContentDelivery`)
- avoid storing bundle outputs under `Assets` to prevent unnecessary imports/churn

### Manual portal handoff (current recommended)

After local Build completes:

1. Upload build artifacts using the web portal Version Publishing UX.
2. Publish/activate the new lab version in portal control-plane metadata.
3. Ensure the launch payload uses the new `resolvedVersionId` and `runtimeUrl`.

## 5) Runtime Integration (Optional)

Add `AddressablesBootstrapper` to scene.

Behavior:

- registers `IContentDeliveryService`
- resolves launch context from bridge/provider/fallback
- optionally defers and restarts SceneManager via reflection

No heavy content-delivery logic is added to `SceneManager`.

Then add `ContentDeliverySpawner` to the scene (or use SceneManager editor action):

- set `labId`
- set `addressKey`
- assign `spawnParent` (or auto-create from inspector button)
- keep `prefabAsset` assigned for offline/local fallback
- optional: set `runtimeCatalogUrl` for Unity-only online tests

For runtime dialogs, create your own Canvas UI and bind it to `ContentDeliveryStatusOverlay`:

- bind `titleText`, `messageText`, `progressText` (TMPro)
- bind `primaryButton` / `secondaryButton` (and optional label TMP texts)
- bind `progressSlider` and/or `progressFillImage`
- assign that `ContentDeliveryStatusOverlay` to `ContentDeliverySpawner.statusOverlay`
- optional: customize `ContentDeliveryStatusOverlay.runtimeUiOverride` (`Runtime UI Override`) to override titles/messages/button labels
- template tokens supported in copy fields: `{labId}`, `{resolvedVersionId}`, `{size}`, `{addressKey}`, `{error}`

Runtime learner UX sequence:

1. Checking content
2. "New Content Found" prompt (Download vs Use Cached/Use Local based on policy)
3. Download progress dialog
4. Retry/fallback prompt on failures
5. "Content Ready" before spawn

## 6) Launch Sources

Supported launch context sources:

- RN/UaaL bridge payload via `BridgeLaunchContextReceiver`
- Unity-only menu via `SerializedLaunchContextProvider`
- direct fallback via bootstrap defaults

All flows carry:

- `attemptId`
- `idempotencyKey`
- `launchRequestId`
- `resolvedVersionId`

## 7) Local-First Attempt Reconciliation

For Unity-only launches:

- local attempt identity is generated first
- backend canonical attempt id can be reconciled later via `AttemptReconciliationBridge`
- lineage remains stable through `launchRequestId`

## 8) Report Artifacts

Reports are written to:

- `Assets/Settings/ContentDelivery/Reports/*.asset`
- `<localReportsFolder or localWorkspaceRoot>/Reports/*.json` (default `Build/ContentDelivery/Reports/*.json`)

Schema version:

- `publish_transaction.v1`

## Troubleshooting

### Addressables package missing

- Install `com.unity.addressables`
- Reimport scripts so `PITECH_ADDR` resolves

### Validate fails with missing profile/group

- Run Setup again (safe/idempotent)
- Confirm config profile name and lab id inputs

### Where do I assign the prefab to build?

- Open `Pi tech -> Addressables Builder`
- Assign `Prefab to include`
- Click `Map Prefab` before `Build`

### External launch not used

- Ensure bridge calls `BridgeLaunchContextReceiver.ReceiveLaunchContextJson`
- Check bootstrap order and reporter logs
- Ensure the bridge payload includes `runtimeUrl` for online catalog resolution

### Report JSON not written

- Confirm report folder from `AddressablesModuleConfig` is writable (default `Build/ContentDelivery/Reports`)
- Re-run validation/build and inspect console logs
