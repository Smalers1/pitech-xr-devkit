using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pitech.XR.Stats
{
    public enum StatKey { Money, CO2, Adoption, MCI }

    [CreateAssetMenu(menuName = "Edu/Stats Config")]
    public class StatsConfig : ScriptableObject
    {
        [Serializable] public struct Entry { public StatKey key; public float defaultValue; public float min; public float max; }
        [SerializeField] Entry[] entries;

        Dictionary<StatKey, Entry> table;
        void Ensure()
        {
            if (table != null) return;
            table = new();
            if (entries == null)
            {
                Debug.LogWarning("StatsConfig entries are null");
                return;
            }
            foreach (var e in entries)
            {
                if (!table.TryAdd(e.key, e))
                    Debug.LogWarning($"Duplicate StatKey {e.key} in StatsConfig");
            }
        }

        public float GetDefault(StatKey k)
        {
            Ensure();
            if (table.TryGetValue(k, out var e)) return e.defaultValue;
            Debug.LogWarning($"Default value for {k} not found");
            return 0f;
        }

        public Vector2 GetRange(StatKey k)
        {
            return TryGetRange(k, out var range) ? range : Vector2.zero;
        }

        public bool TryGetRange(StatKey k, out Vector2 range)
        {
            Ensure();
            if (table.TryGetValue(k, out var e))
            {
                range = new Vector2(e.min, e.max);
                return true;
            }
            Debug.LogWarning($"Range for {k} not found");
            range = default;
            return false;
        }
        public IEnumerable<KeyValuePair<StatKey, Entry>> All() { Ensure(); return table; }
    }

    public enum StatOp { Add, Subtract, Multiply, Divide, Set }

    [Serializable]
    public class StatEffect
    {
        public StatKey key;
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
        readonly Dictionary<StatKey, float> v = new();
        public event Action<StatKey, float, float> OnChanged;

        public void Reset(StatsConfig cfg)
        {
            v.Clear();
            foreach (var kv in cfg.All()) v[kv.Key] = kv.Value.defaultValue;
        }

        public float this[StatKey k]
        {
            get
            {
                if (v.TryGetValue(k, out var val)) return val;
                Debug.LogWarning($"Stat '{k}' not found");
                return 0f;
            }
            set
            {
                if (!v.TryGetValue(k, out var old))
                {
                    Debug.LogWarning($"Stat '{k}' not found; initializing");
                    v[k] = value;
                    OnChanged?.Invoke(k, 0f, value);
                    return;
                }
                if (Mathf.Approximately(old, value)) return;
                v[k] = value;
                OnChanged?.Invoke(k, old, value);
            }
        }
    }
}
