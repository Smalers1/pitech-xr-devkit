# Addressables Content Delivery Sample

This sample describes the minimal scene wiring for optional content delivery.

## Components

Add to your scene:

- `AddressablesBootstrapper`
- `SerializedLaunchContextProvider` (Unity-menu flow), or
- `BridgeLaunchContextReceiver` (RN/UaaL flow)
- `ContentDeliverySpawner`
- `LaunchContextReporter` (optional diagnostics/bridge callbacks)

## Suggested Setup

1. Open DevKit Hub -> Guided Setup.
2. Open `Pi tech -> Addressables Builder`:
   - Setup
   - assign `Prefab to include`
   - Map Prefab
   - Validate
   - Build
3. Assign the generated `AddressablesModuleConfig` in bootstrapper.
4. Configure `ContentDeliverySpawner` (`labId`, `addressKey`, `prefabAsset`, `spawnParent`).
5. Create your own Canvas dialog/progress UI and add `ContentDeliveryStatusOverlay` on that UI root.
6. Bind TMP texts/buttons/progress refs in `ContentDeliveryStatusOverlay`, then assign it in `ContentDeliverySpawner.statusOverlay`.
7. Optional: customize `ContentDeliveryStatusOverlay.runtimeUiOverride` (`Runtime UI Override`) for your own copy/messages/labels.
8. For Unity-only flow, configure `SerializedLaunchContextProvider`.
9. For bridge flow, send payload JSON to `BridgeLaunchContextReceiver.ReceiveLaunchContextJson`.

## Expected Lifecycle

1. Bootstrapper initializes `IContentDeliveryService`.
2. Launch context is resolved from bridge/provider/fallback.
3. Reporter emits `launch_resolved` payload JSON.
4. `experience_abandoned` payload can be emitted with duration fields.
5. Attempt id can be reconciled later via `AttemptReconciliationBridge`.

## Addressable lab prefabs (SceneManager + scenario)

Labs shipped as Addressables should be **self-contained prefab roots**: environment, **Scene Manager**, Scenario asset reference, Selection Lists, Selectables Manager, Canvas / quiz UI, etc. Wire all **Scene Manager** serialized fields on the **prefab asset** (Prefab mode), not only in a saved scene.

- **ContentDeliverySpawner** resolves `SceneManager` only under `spawnParent`, then under the **spawned instance** — it does not scan the whole loaded world, so a host “shell” scene cannot steal the wrong manager.
- **AddressablesBootstrapper** auto-detects `SceneManager` only **under the same GameObject hierarchy** as the bootstrapper; assign explicitly if your layout differs.
- **Scene Manager** auto-finds quiz UI with `GetComponentInChildren` under `labContentRoot` or, if unset, `transform.root`, so it will not bind to the shell’s quiz UI.

## Notes

- Setup/Validate is designed to be idempotent and safe to rerun.
- SceneManager remains clean; content delivery logic is isolated.
- One remote group per lab is the default policy.

## Manual verification (regression)

1. Add a decoy **Scene Manager** (or empty GO with the component) to the **host scene** outside the spawn subtree.
2. Spawn an Addressable lab whose real **Scene Manager** lives only on the spawned prefab.
3. Confirm **Restart** / scenario runs on the lab instance (check logs / step progression), not the decoy.
4. With quiz UI only under the lab prefab, confirm quiz steps use that UI, not a shell Canvas.
