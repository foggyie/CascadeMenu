using System.Collections.Generic;

namespace CascadeMenu
{
    /// <summary>
    /// Small session store for the demo controls. Replace these values with your own
    /// mod settings when wiring Cascade pages to real features.
    /// </summary>
    internal sealed class DemoState
    {
        private readonly Dictionary<string, bool> _bools = new Dictionary<string, bool>();
        private readonly Dictionary<string, float> _floats = new Dictionary<string, float>();
        private readonly Dictionary<string, int> _ints = new Dictionary<string, int>();

        public bool GetBool(string key, bool fallback = false)
        {
            bool value;
            return _bools.TryGetValue(key, out value) ? value : fallback;
        }

        public void SetBool(string key, bool value)
        {
            _bools[key] = value;
        }

        public float GetFloat(string key, float fallback = 0f)
        {
            float value;
            return _floats.TryGetValue(key, out value) ? value : fallback;
        }

        public void SetFloat(string key, float value)
        {
            _floats[key] = value;
        }

        public int GetInt(string key, int fallback = 0)
        {
            int value;
            return _ints.TryGetValue(key, out value) ? value : fallback;
        }

        public void SetInt(string key, int value)
        {
            _ints[key] = value;
        }
    }
}
