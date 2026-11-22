using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using CascadeMenu.Mods.Core;
using CascadeMenu.UI;

namespace CascadeMenu.Mods.Fun
{
    /// <summary>
    /// Floating glowing crystal shards orbiting smoothly around your Gorilla.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Fun")]
    public sealed class HaloOrbitMod : Mod
    {
        private int _shardCount = 8;
        private float _radius = 0.65f;
        private float _speed = 1.6f;
        private bool _rainbow = true;

        private GameObject _root;
        private readonly List<Transform> _shards = new List<Transform>();
        private readonly List<Material> _mats = new List<Material>();

        public HaloOrbitMod()
        {
            Name = "Orbiting Crystal Halo";
            isToggle = true;

            AddSetting(ModSetting.Stepper("count", "Crystal count",
                "Number of glowing shards orbiting around you",
                3, 16, () => _shardCount, v => { _shardCount = v; RebuildShards(); }));

            AddSetting(ModSetting.Slider("radius", "Orbit radius",
                "Distance of the orbiting ring in metres",
                0.35f, 1.6f, () => _radius, v => _radius = v, v => v.ToString("0.00") + " m"));

            AddSetting(ModSetting.Slider("speed", "Orbit speed",
                "Rotation speed in radians/sec",
                0.3f, 5.0f, () => _speed, v => _speed = v, v => v.ToString("0.0") + "x"));

            AddSetting(ModSetting.Toggle("rainbow", "Rainbow glow",
                "Cycle rainbow hues across the crystals (OFF uses your Cascade theme accent)",
                () => _rainbow, v => _rainbow = v));
        }

        public override void OnEnable()
        {
            RebuildShards();
        }

        public override void OnDisable()
        {
            ClearShards();
        }

        public override void LateUpdate()
        {
            GorillaTagger tagger = GorillaTagger.Instance;
            if (tagger == null || tagger.headCollider == null) return;
            if (_root == null) RebuildShards();

            Vector3 center = tagger.headCollider.transform.position;
            float t = Time.time * _speed;

            for (int i = 0; i < _shards.Count; i++)
            {
                Transform tr = _shards[i];
                if (tr == null) continue;

                float angle = t + (i / (float)_shards.Count) * Mathf.PI * 2f;
                float bob = Mathf.Sin(Time.time * 2.4f + i * 0.9f) * 0.08f;
                tr.position = center + new Vector3(Mathf.Cos(angle) * _radius, bob, Mathf.Sin(angle) * _radius);
                tr.Rotate(45f * Time.deltaTime, 90f * Time.deltaTime, 25f * Time.deltaTime, Space.Self);

                if (i < _mats.Count && _mats[i] != null)
                {
                    Color c = _rainbow
                        ? Color.HSVToRGB(Mathf.Repeat(Time.time * 0.22f + i / (float)_shards.Count, 1f), 0.85f, 1f)
                        : (CascadeTheme.Active != null ? (Color)CascadeTheme.Active.Accent : new Color(0.3f, 0.7f, 1f));
                    _mats[i].color = c;
                }
            }
        }

        private void RebuildShards()
        {
            ClearShards();
            _root = new GameObject("CascadeOrbitHaloRoot");
            if (Plugin.Root != null) _root.transform.SetParent(Plugin.Root.transform, false);

            Shader sh = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Standard");

            for (int i = 0; i < _shardCount; i++)
            {
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "Shard_" + i;
                cube.transform.SetParent(_root.transform, false);
                cube.transform.localScale = new Vector3(0.055f, 0.11f, 0.055f);

                Collider col = cube.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);

                Renderer r = cube.GetComponent<Renderer>();
                if (r != null)
                {
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    if (sh != null)
                    {
                        var m = new Material(sh) { color = Color.cyan };
                        r.material = m;
                        _mats.Add(m);
                    }
                }
                _shards.Add(cube.transform);
            }
        }

        private void ClearShards()
        {
            for (int i = 0; i < _mats.Count; i++)
                if (_mats[i] != null) Object.Destroy(_mats[i]);
            _mats.Clear();
            _shards.Clear();
            if (_root != null) { Object.Destroy(_root); _root = null; }
        }
    }
}
