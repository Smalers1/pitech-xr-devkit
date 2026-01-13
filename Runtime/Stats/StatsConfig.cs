using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pitech.XR.Stats
{
    [CreateAssetMenu(menuName = "Pi tech/Stats Config")]
    public class StatsConfig : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("Stat title / identifier used in code & bindings. Must be unique. Example: \"Money\" or \"CO2\".")]
            public string key;

            [Tooltip("Initial value when a scenario starts / stats are reset.")]
            public float defaultValue;

            [Tooltip("Minimum allowed value (used for UI sliders/clamping).")]
            public float min;

            [Tooltip("Maximum allowed value (used for UI sliders/clamping).")]
            public float max;
        }
        [SerializeField] Entry[] entries;

        Dictionary<string, Entry> table;

        void Ensure()
        {
            if (table != null) return;
            table = new Dictionary<string, Entry>(StringComparer.Ordinal);
            if (entries == null) return;

            foreach (var e in entries)
            {
                var k = NormalizeKey(e.key);
                if (string.IsNullOrEmpty(k)) continue;
                table[k] = e;
            }
        }

        public static string NormalizeKey(string key) => string.IsNullOrWhiteSpace(key) ? "" : key.Trim();

        public bool TryGet(string key, out Entry entry)
        {
            Ensure();
            return table.TryGetValue(NormalizeKey(key), out entry);
        }

        public float GetDefault(string key) => TryGet(key, out var e) ? e.defaultValue : 0f;

        public Vector2 GetRange(string key)
        {
            if (!TryGet(key, out var e)) return new Vector2(0f, 1f);
            return new Vector2(e.min, e.max);
        }

        public IEnumerable<KeyValuePair<string, Entry>> All()
        {
            Ensure();
            return table;
        }
    }

    public enum StatOp { Add, Subtract, Multiply, Divide, Set }

    [Serializable]
    public class StatEffect
    {
        [Tooltip("Stat key to modify (must exist in StatsConfig).")]
        public string key;
        public StatOp op = StatOp.Add;
        public float value = 0;

        public float Apply(float current)
        {
            switch (op)
            {
                case StatOp.Add: return current + value;
                case StatOp.Subtract: return current - value;
                case StatOp.Multiply: return current * value;
                case StatOp.Divide: return Mathf.Approximately(value, 0f) ? current : current / value;
                case StatOp.Set: return value;
                default: return current;
            }
        }
    }

    public class StatsRuntime
    {
        readonly Dictionary<string, float> v = new Dictionary<string, float>(StringComparer.Ordinal);
        public event Action<string, float, float> OnChanged;

        StatsConfig _cfg;

        public void Reset(StatsConfig cfg)
        {
            _cfg = cfg;
            v.Clear();
            if (cfg == null) return;
            foreach (var kv in cfg.All())
                v[kv.Key] = kv.Value.defaultValue;
        }

        public bool TryGetRange(string key, out float min, out float max)
        {
            if (_cfg == null) { min = 0; max = 1; return false; }
            var r = _cfg.GetRange(key);
            min = r.x; max = r.y;
            return true;
        }

        public void EnsureKey(string key, float initial = 0f)
        {
            var k = StatsConfig.NormalizeKey(key);
            if (string.IsNullOrEmpty(k)) return;
            if (!v.ContainsKey(k)) v[k] = initial;
        }

        public bool TryGet(string key, out float value) => v.TryGetValue(StatsConfig.NormalizeKey(key), out value);

        public float this[string key]
        {
            get => v.TryGetValue(StatsConfig.NormalizeKey(key), out var val) ? val : 0f; // no exception
            set
            {
                var k = StatsConfig.NormalizeKey(key);
                if (string.IsNullOrEmpty(k)) return;

                var old = v.TryGetValue(k, out var o) ? o : 0f;
                if (Mathf.Approximately(old, value)) return;
                v[k] = value;
                OnChanged?.Invoke(k, old, value);
            }
        }


    }
}
