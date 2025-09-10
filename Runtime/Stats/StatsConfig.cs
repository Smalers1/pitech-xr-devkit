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
        void Ensure() { if (table != null) return; table = new(); foreach (var e in entries) table[e.key] = e; }

        public float GetDefault(StatKey k) { Ensure(); return table[k].defaultValue; }
        public Vector2 GetRange(StatKey k) { Ensure(); var e = table[k]; return new Vector2(e.min, e.max); }
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
            get => v[k];
            set { var old = v[k]; if (Mathf.Approximately(old, value)) return; v[k] = value; OnChanged?.Invoke(k, old, value); }
        }
    }
}
