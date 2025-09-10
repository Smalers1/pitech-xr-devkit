namespace Pitech.XR.Core
{
    public interface IXRService { void Initialize(); void Shutdown(); }
    public static class XRServices
    {
        static readonly System.Collections.Generic.Dictionary<System.Type, IXRService> map = new();
        public static void Register<T>(T impl) where T : class, IXRService => map[typeof(T)] = impl;
        public static T Get<T>() where T : class, IXRService => map.TryGetValue(typeof(T), out var s) ? (T)s : null;
        public static bool TryGet<T>(out T svc) where T: class, IXRService { svc = Get<T>(); return svc != null; }
        public static void InitializeAll(){ foreach (var s in map.Values) s.Initialize(); }
        public static void ShutdownAll(){ foreach (var s in map.Values) s.Shutdown(); map.Clear(); }
    }
}
