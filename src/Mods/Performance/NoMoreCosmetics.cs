using System.Collections.Generic;
using GorillaNetworking;
using UnityEngine;

namespace CascadeMenu.Mods.Performance
{
    /// <summary>
    /// Hides the cosmetic renderers on other players' rigs to save performance.
    ///
    /// Efficient by design:
    /// - renderers are collected once per rig and cached;
    /// - the roster is only checked twice a second, and the cache is rebuilt
    ///   only when a rig appears or disappears;
    /// - every renderer that was switched off is switched back on again when
    ///   this mod is disabled.
    ///
    /// The local player's own rig is never touched, so your cosmetics stay
    /// visible to you.
    /// </summary>
    public class NoMoreCosmetics : Mod
    {
        private const float RosterInterval = 0.5f;

        private sealed class RigEntry
        {
            public VRRig Rig;
            public readonly List<Renderer> Hidden = new List<Renderer>();
        }

        private readonly List<RigEntry> _entries = new List<RigEntry>();
        private readonly Dictionary<VRRig, RigEntry> _byRig = new Dictionary<VRRig, RigEntry>();

        private float _timer;

        public NoMoreCosmetics()
        {
            Name = "No More Cosmetics";
            isToggle = true;
        }

        public override void OnEnable()
        {
            Refresh(true);
            Plugin.Log?.LogInfo("Cascade mod: " + Name + " enabled.");
        }

        public override void OnDisable()
        {
            RestoreAll();
            Plugin.Log?.LogInfo("Cascade mod: " + Name + " disabled - renderers restored.");
        }

        public override void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < RosterInterval) return;
            _timer = 0f;
            Refresh(false);
        }

        /// <summary>
        /// Rebuilds the cache only when the set of rigs changed. When
        /// <paramref name="force"/> is true it always rebuilds.
        /// </summary>
        private void Refresh(bool force)
        {
            VRRig[] rigs = Object.FindObjectsByType<VRRig>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            if (!force && SameRigs(rigs)) return;

            RestoreAll();

            VRRig local = GorillaTagger.Instance != null ? GorillaTagger.Instance.offlineVRRig : null;

            for (int i = 0; i < rigs.Length; i++)
            {
                VRRig rig = rigs[i];
                if (rig == null || rig == local) continue;

                var entry = new RigEntry { Rig = rig };

                Renderer[] renderers = rig.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < renderers.Length; r++)
                {
                    Renderer renderer = renderers[r];
                    if (renderer == null) continue;
                    if (rig.mainSkin != null && renderer == rig.mainSkin) continue;

                    string name = renderer.gameObject.name;
                    if (name.Contains("Text") || name.Contains("NameTag") ||
                        name.Contains("face") || renderer.GetComponent<TextMesh>() != null)
                        continue;

                    if (!renderer.enabled) continue;

                    renderer.enabled = false;
                    entry.Hidden.Add(renderer);
                }

                if (entry.Hidden.Count == 0) continue;

                _entries.Add(entry);
                _byRig[rig] = entry;
            }

            Plugin.Log?.LogInfo("Cascade mod: " + Name + " hiding cosmetics on " +
                                _entries.Count + " rig(s).");
        }

        private bool SameRigs(VRRig[] rigs)
        {
            if (_byRig.Count != rigs.Length) return false;

            int matched = 0;
            for (int i = 0; i < rigs.Length; i++)
            {
                if (rigs[i] != null && _byRig.ContainsKey(rigs[i])) matched++;
            }
            return matched == _byRig.Count;
        }

        private void RestoreAll()
        {
            for (int e = 0; e < _entries.Count; e++)
            {
                List<Renderer> hidden = _entries[e].Hidden;
                for (int r = 0; r < hidden.Count; r++)
                {
                    if (hidden[r] != null) hidden[r].enabled = true;
                }
            }

            _entries.Clear();
            _byRig.Clear();
        }
    }
}
