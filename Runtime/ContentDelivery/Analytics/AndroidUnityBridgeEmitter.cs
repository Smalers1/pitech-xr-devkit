using UnityEngine;

namespace Pitech.XR.ContentDelivery
{
    internal static class AndroidUnityBridgeEmitter
    {
        public static void EmitLifecycleJson(string payloadJson)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Emit("emitLifecycleJson", payloadJson);
#endif
        }

        public static void EmitTelemetryBatchJson(string payloadJson)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Emit("emitLifecycleJson", payloadJson);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaClass bridgeClass;
        private static bool bridgeLookupAttempted;

        private static void Emit(string methodName, string payloadJson)
        {
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return;
            }

            try
            {
                AndroidJavaClass target = GetBridgeClass();
                target?.CallStatic(methodName, payloadJson);
            }
            catch (System.Exception error)
            {
                Debug.LogWarning($"[ContentDelivery] Failed to emit payload to Android bridge: {error.Message}");
            }
        }

        private static AndroidJavaClass GetBridgeClass()
        {
            if (bridgeClass != null)
            {
                return bridgeClass;
            }

            if (bridgeLookupAttempted)
            {
                return null;
            }

            bridgeLookupAttempted = true;
            string packageName = Application.identifier;
            if (string.IsNullOrWhiteSpace(packageName))
            {
                return null;
            }

            string className = $"{packageName}.unity.UnityBridgeEvents";
            try
            {
                bridgeClass = new AndroidJavaClass(className);
            }
            catch (System.Exception error)
            {
                Debug.LogWarning($"[ContentDelivery] Android bridge class not found ({className}): {error.Message}");
            }

            return bridgeClass;
        }
#endif
    }
}
