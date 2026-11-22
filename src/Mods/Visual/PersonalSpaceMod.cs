using System.Collections.Generic;
using UnityEngine;
using CascadeMenu.Mods.Core;
using CascadeMenu.Mods.Player;

namespace CascadeMenu.Mods.Visual
{
    /// <summary>
    /// Client-side personal space & view-clearing mod.
    /// When other players crowd right in front of your face, smoothly hides or
    /// shrinks their visual rig locally until they step back out of your bubble.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Visual")]
    public sealed class PersonalSpaceMod : Mod
    {
        private static readonly string[] Modes = { "Hide Renderers", "Mini Shrink" };

        private float _radius = 1.35f;
        private int _modeIndex = 0;

        private readonly Dictionary<VRRig, List<Renderer>> _hiddenRenderers = new Dictionary<VRRig, List<Renderer>>();
        private readonly Dictionary<VRRig, Vector3> _savedScales = new Dictionary<VRRig, Vector3>();
        private readonly List<VRRig> _stale = new List<VRRig>();

        public PersonalSpaceMod()
        {
            Name = "Personal Space";
            isToggle = true;

            AddSetting(ModSetting.Dropdown("mode", "Clear style",
                "Hide nearby players completely or shrink them while inside your personal bubble",
                Modes, () => _modeIndex, v =>
                {
                    RestoreAll();
                    _modeIndex = Mathf.Clamp(v, 0, Modes.Length - 1);
                }));

            AddSetting(ModSetting.Slider("radius", "Bubble radius",
                "Distance in metres around your head to keep clear",
                0.5f, 3.5f, () => _radius, v => _radius = v, v => v.ToString("0.00") + " m"));
        }

        public override void OnDisable()
        {
            RestoreAll();
        }

        public override void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 camPos = cam.transform.position;
            List<VRRig> rigs = PlayerActions.GetRemoteRigs();
            var inBubble = new HashSet<VRRig>();

            for (int i = 0; i < rigs.Count; i++)
            {
                VRRig rig = rigs[i];
                if (rig == null || rig.isLocal) continue;

                Transform head = rig.headMesh != null ? rig.headMesh.transform : rig.transform;
                float dist = Vector3.Distance(camPos, head.position);
                if (dist <= _radius)
                {
                    inBubble.Add(rig);
                    ApplyClear(rig);
                }
            }

            // Restore anyone who stepped back out of the bubble
            _stale.Clear();
            foreach (var kv in _hiddenRenderers)
            {
                if (kv.Key == null || !inBubble.Contains(kv.Key)) _stale.Add(kv.Key);
            }
            for (int i = 0; i < _stale.Count; i++) RestoreRenderers(_stale[i]);

            _stale.Clear();
            foreach (var kv in _savedScales)
            {
                if (kv.Key == null || !inBubble.Contains(kv.Key)) _stale.Add(kv.Key);
            }
            for (int i = 0; i < _stale.Count; i++) RestoreScale(_stale[i]);
        }

        private void ApplyClear(VRRig rig)
        {
            if (_modeIndex == 0)
            {
                if (!_hiddenRenderers.TryGetValue(rig, out List<Renderer> list))
                {
                    list = new List<Renderer>();
                    Renderer[] renderers = rig.GetComponentsInChildren<Renderer>(true);
                    for (int i = 0; i < renderers.Length; i++)
                    {
                        if (renderers[i] != null && renderers[i].enabled)
                        {
                            renderers[i].enabled = false;
                            list.Add(renderers[i]);
                        }
                    }
                    _hiddenRenderers[rig] = list;
                }
                else
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (list[i] != null && list[i].enabled) list[i].enabled = false;
                    }
                }
            }
            else
            {
                if (!_savedScales.ContainsKey(rig))
                {
                    _savedScales[rig] = rig.transform.localScale;
                }
                rig.transform.localScale = Vector3.one * 0.08f;
            }
        }

        private void RestoreRenderers(VRRig rig)
        {
            if (rig != null && _hiddenRenderers.TryGetValue(rig, out List<Renderer> list))
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] != null) list[i].enabled = true;
                }
            }
            _hiddenRenderers.Remove(rig);
        }

        private void RestoreScale(VRRig rig)
        {
            if (rig != null && _savedScales.TryGetValue(rig, out Vector3 orig))
            {
                rig.transform.localScale = orig;
            }
            _savedScales.Remove(rig);
        }

        private void RestoreAll()
        {
            var keys = new List<VRRig>(_hiddenRenderers.Keys);
            for (int i = 0; i < keys.Count; i++) RestoreRenderers(keys[i]);
            _hiddenRenderers.Clear();

            var scaleKeys = new List<VRRig>(_savedScales.Keys);
            for (int i = 0; i < scaleKeys.Count; i++) RestoreScale(scaleKeys[i]);
            _savedScales.Clear();
        }
    }
}
