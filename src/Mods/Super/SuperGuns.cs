using CascadeMenu.Mods;
using UnityEngine;

namespace CascadeMenu.Mods.Super
{
    /// <summary>
    /// Aim anywhere and shoot to spawn Super Infection resources at the hit point (authority required).
    /// </summary>
    [PluginPreferenced]
    [PluginToggleable]
    public class SpawnResourceGun : Mod
    {
        private int _prefabIndex;
        private float _cooldown = 0.3f;

        public SpawnResourceGun()
        {
            Name = "Spawn Resource (Gun)";
            isToggle = true;

            AddSetting(ModSetting.Slider(
                "cooldown",
                "Fire cooldown",
                "Seconds between shots, so a held trigger does not spam objects",
                0.05f,
                2f,
                () => _cooldown,
                value => _cooldown = value,
                value => value.ToString("0.00") + " s"));
        }

        public override void Update()
        {
            GunLib.Update((hit, rig, player) =>
            {
                if (hit.collider == null) return;

                var si = Object.FindFirstObjectByType<SuperInfection>();
                if (si == null || si.siManager == null || !si.IsAuthorityAndActive)
                {
                    Plugin.Log?.LogWarning("[Super] Spawn Resource Gun: Super Infection authority required.");
                    return;
                }

                var prefabs = si.ResourcePrefabs;
                if (prefabs == null || prefabs.Count == 0) return;

                _prefabIndex = (_prefabIndex + 1) % prefabs.Count;
                var prefab = prefabs[_prefabIndex];
                if (prefab == null) return;

                Vector3 pos = hit.point + hit.normal * 0.2f;
                Quaternion rot = Quaternion.LookRotation(Vector3.ProjectOnPlane(Vector3.forward, hit.normal), hit.normal);
                var mgr = si.siManager.gameEntityManager;
                var id = mgr.RequestCreateItem(prefab.gameObject.name.GetStaticHash(), pos, rot, 0L);
                var entity = mgr.GetGameEntity(id);
                if (entity != null)
                    Plugin.Log?.LogInfo($"[Super] Spawned {prefab.gameObject.name} at {pos}");
                else
                    Plugin.Log?.LogWarning($"[Super] Failed to spawn {prefab.gameObject.name}");
            }, cooldown: _cooldown, needsRaycast: true);
        }

        public override void OnDisable()
        {
            GunLib.Cleanup();
        }
    }
}

