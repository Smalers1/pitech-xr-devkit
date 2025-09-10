namespace Pitech.XR.Core
{
    public interface IXRService { void Initialize(); void Shutdown(); }
    public static class XRServices
    {
        static readonly System.Collections.Generic.Dictionary<System.Type, IXRService> map = new();
        static readonly object mutex = new();

        public static void Register<T>(T impl) where T : class, IXRService
        {
            var key = typeof(T);
            lock (mutex)
            {
                if (map.ContainsKey(key))
                    UnityEngine.Debug.LogWarning($"Replacing existing service of type {key}.");
                map[key] = impl;
            }
        }

        public static void Unregister<T>() where T : class, IXRService
        {
            lock (mutex)
            {
                map.Remove(typeof(T));
            }
        }

        public static T Get<T>() where T : class, IXRService
        {
            lock (mutex)
            {
                return map.TryGetValue(typeof(T), out var s) ? (T)s : null;
            }
        }

        public static bool TryGet<T>(out T svc) where T : class, IXRService
        {
            svc = Get<T>();
            return svc != null;
        }

        public static void InitializeAll()
        {
            lock (mutex)
            {
                foreach (var s in map.Values) s.Initialize();
            }
        }

        public static void ShutdownAll()
        {
            lock (mutex)
            {
                foreach (var s in map.Values) s.Shutdown();
                map.Clear();
            }
        }
    }
}