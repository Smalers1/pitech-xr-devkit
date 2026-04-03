using System.Reflection;
using Pitech.XR.Core;
using UnityEngine;

namespace Pitech.XR.ContentDelivery
{
    [AddComponentMenu("Pi tech XR/Content Delivery/Addressables Bootstrapper")]
    public sealed class AddressablesBootstrapper : MonoBehaviour
    {
        [Header("Config")]
        public AddressablesModuleConfig config;

        [Header("Launch Defaults (Unity menu / direct mode)")]
        public LaunchSource defaultLaunchSource = LaunchSource.Direct;
        public string internalLabId = "default";
        public string internalResolvedVersionId = string.Empty;
        public string internalRuntimeUrl = string.Empty;

        [Header("Scene Manager Integration (optional)")]
        [Tooltip(
            "Optional SceneManager reference. If null, auto-detect searches only under this GameObject (not the whole loaded world). " +
            "For shell + Addressable lab, leave null or assign the lab instance explicitly.")]
        public MonoBehaviour sceneManager;

        [Tooltip("Disable SceneManager autoStart until launch context resolves.")]
        public bool deferSceneManagerAutoStart = true;

        [Tooltip("Call Restart() on SceneManager after context resolution.")]
        public bool restartSceneManagerAfterContext = true;

        private IContentDeliveryService service;

        private void Awake()
        {
            service = XRServices.Get<IContentDeliveryService>();
            if (service == null)
            {
                service = new ContentDeliveryRuntimeService(config);
                XRServices.Register(service);
            }

            service.Initialize();

            if (sceneManager == null)
            {
                sceneManager = FindSceneManagerUnderThisHierarchy();
            }

            if (deferSceneManagerAutoStart && sceneManager != null)
            {
                TrySetAutoStart(sceneManager, false);
            }
        }

        private void Start()
        {
            if (service == null)
            {
                Debug.LogWarning("[Bootstrapper] Service is null in Start — skipping.");
                return;
            }

            LaunchContext context = ResolveLaunchContext();
            Debug.Log($"[Bootstrapper] Resolved context — source={context.source}, labId={context.labId}, addressKey={(string.IsNullOrWhiteSpace(context.addressKey) ? "EMPTY" : context.addressKey)}");
            service.SetLaunchContext(context);

            if (sceneManager != null && restartSceneManagerAfterContext)
            {
                TryRestart(sceneManager);
            }
        }

        private LaunchContext ResolveLaunchContext()
        {
            if (LaunchContextRegistry.TryConsumeExternalContext(out LaunchContext external))
            {
                return external;
            }

            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ILaunchContextProvider provider &&
                    provider.TryBuildLaunchContext(config, out LaunchContext provided) &&
                    provided != null)
                {
                    return provided;
                }
            }

            if (defaultLaunchSource == LaunchSource.UnityMenu)
            {
                return LaunchContextFactory.CreateUnityMenuContext(
                    internalLabId,
                    internalResolvedVersionId,
                    internalRuntimeUrl,
                    config);
            }

            return LaunchContextFactory.CreateDirectContext(config);
        }

        private MonoBehaviour FindSceneManagerUnderThisHierarchy()
        {
            MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null)
                {
                    continue;
                }

                if (behaviour.GetType().FullName == "Pitech.XR.Scenario.SceneManager")
                {
                    return behaviour;
                }
            }

            return null;
        }

        private static void TrySetAutoStart(MonoBehaviour target, bool value)
        {
            if (target == null)
            {
                return;
            }

            var type = target.GetType();
            FieldInfo field = type.GetField("autoStart", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && field.FieldType == typeof(bool))
            {
                field.SetValue(target, value);
                return;
            }

            PropertyInfo prop = type.GetProperty("autoStart", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null && prop.PropertyType == typeof(bool) && prop.CanWrite)
            {
                prop.SetValue(target, value);
            }
        }

        private static void TryRestart(MonoBehaviour target)
        {
            if (target == null)
            {
                return;
            }

            MethodInfo restart = target.GetType().GetMethod(
                "Restart",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            restart?.Invoke(target, null);
        }
    }
}
