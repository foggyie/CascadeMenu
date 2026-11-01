using System;
using System.Collections.Generic;
using System.Reflection;
using GorillaLocomotion;
using GorillaTagScripts;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace CascadeMenu.Mods.Halloween
{
    /// <summary>
    /// Core helper utilities for Halloween ghost and haunted object mechanics.
    /// Adapted for CascadeMenu.
    /// </summary>
    public static class HalloweenHelper
    {
        public static readonly BepInEx.Logging.ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("Halloween");
        private static WanderingGhost _wg;
        private static LurkerGhost _lg;

        // HauntedObject.TriggerEffects(GameObject) is private in Assembly-CSharp, so
        // it has to be called through reflection. Cached once to keep it cheap.
        private static readonly MethodInfo TriggerEffectsMethod =
            typeof(HauntedObject).GetMethod(
                "TriggerEffects",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(GameObject) },
                null);

        /// <summary>Runs a haunted object's effects (rattle, sound, light, spawn, anim).</summary>
        public static bool TriggerHaunted(HauntedObject haunted)
        {
            if (haunted == null || TriggerEffectsMethod == null) return false;
            TriggerEffectsMethod.Invoke(haunted, new object[] { haunted.gameObject });
            return true;
        }

        /// <summary>
        /// Checks if the local player has Master Client authority over the room.
        /// </summary>
        public static bool IsMasterClient => NetworkSystem.Instance != null && NetworkSystem.Instance.IsMasterClient;

        /// <summary>
        /// Retrieves the active WanderingGhost instance in the map.
        /// </summary>
        public static WanderingGhost GetWanderingGhost()
        {
            if (_wg != null) return _wg;
            var ghost = Object.FindAnyObjectByType<WanderingGhost>();
            if (ghost == null)
            {
                var go = GameObject.FindGameObjectWithTag("WanderingGhost");
                if (go != null) ghost = go.GetComponent<WanderingGhost>();
            }
            _wg = ghost;
            return ghost;
        }

        /// <summary>
        /// Retrieves the active LurkerGhost instance in the map.
        /// </summary>
        public static LurkerGhost GetLurkerGhost()
        {
            if (_lg != null) return _lg;
            var lurker = Object.FindAnyObjectByType<LurkerGhost>();
            if (lurker == null)
            {
                var go = GameObject.FindGameObjectWithTag("LurkerGhost");
                if (go != null) lurker = go.GetComponent<LurkerGhost>();
            }
            _lg = lurker;
            return lurker;
        }

        public static void ChangeWanderingGhostState(WanderingGhost ghost, string stateName)
        {
            if (ghost == null) return;
            try
            {
                Type stateType = typeof(WanderingGhost).GetNestedType("ghostState", BindingFlags.Public | BindingFlags.NonPublic);
                if (stateType != null)
                {
                    object stateVal = Enum.Parse(stateType, stateName, true);
                    Traverse.Create(ghost).Method("ChangeState", stateVal).GetValue();
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning("[Halloween] Failed to change wandering ghost state to " + stateName + ": " + ex.Message);
            }
        }

        public static string GetWanderingGhostStateName(WanderingGhost ghost)
        {
            if (ghost == null) return string.Empty;
            try
            {
                object state = Traverse.Create(ghost).Field("currentState").GetValue();
                return state != null ? state.ToString() : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public static void ChangeLurkerGhostState(LurkerGhost lurker, string stateName)
        {
            if (lurker == null) return;
            try
            {
                Type stateType = typeof(LurkerGhost).GetNestedType("ghostState", BindingFlags.Public | BindingFlags.NonPublic);
                if (stateType != null)
                {
                    object stateVal = Enum.Parse(stateType, stateName, true);
                    Traverse.Create(lurker).Method("ChangeState", stateVal).GetValue();
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning("[Halloween] Failed to change lurker ghost state to " + stateName + ": " + ex.Message);
            }
        }

        public static string GetLurkerGhostStateName(LurkerGhost lurker)
        {
            if (lurker == null) return string.Empty;
            try
            {
                object state = Traverse.Create(lurker).Field("currentState").GetValue();
                return state != null ? state.ToString() : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Overrides ghost materials to visibleMaterial so they are visible without a Scrying Glass.
        /// </summary>
        private static WanderingGhost _cachedWandering;
        private static LurkerGhost _cachedLurker;

        public static void SetGhostsVisible(bool visible)
        {
            if (_cachedWandering == null) _cachedWandering = GetWanderingGhost();
            if (_cachedLurker == null) _cachedLurker = GetLurkerGhost();

            var wandering = _cachedWandering;
            if (wandering != null && wandering.mrenderer != null)
            {
                Material targetMat = visible ? wandering.visibleMaterial : wandering.scryableMaterial;
                foreach (var r in wandering.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is ParticleSystemRenderer) continue;
                    r.enabled = true;
                    if (visible && targetMat != null && r == wandering.mrenderer)
                        r.sharedMaterial = targetMat;
                }
                if (!visible && targetMat != null) wandering.mrenderer.sharedMaterial = targetMat;

                // Billboard the ghost toward the player (same as scrying orb version)
                var face = wandering.mrenderer.GetComponent<FacePlayer>();
                if (visible && face == null) wandering.mrenderer.gameObject.AddComponent<FacePlayer>();
                else if (!visible && face != null) Object.Destroy(face);
            }

            var lurker = _cachedLurker;
            if (lurker != null)
            {
                if (lurker.meshRenderer != null)
                {
                    lurker.meshRenderer.enabled = true;
                    Material bodyMat = visible ? lurker.visibleMaterial : lurker.scryableMaterial;
                    if (bodyMat != null) lurker.meshRenderer.sharedMaterial = bodyMat;
                }

                if (lurker.bonesMeshRenderer != null)
                {
                    lurker.bonesMeshRenderer.enabled = true;
                    Material bonesMat = visible ? lurker.visibleMaterialBones : lurker.scryableMaterialBones;
                    if (bonesMat != null) lurker.bonesMeshRenderer.sharedMaterial = bonesMat;
                }
            }
        }

        /// <summary>
        /// Teleports the Wandering Ghost to the player and forces idle scare state.
        /// Master Client authoritative.
        /// </summary>
        public static void SpawnWanderingGhost(Vector3? targetPosition = null)
        {
            if (!IsMasterClient)
            {
                Log.LogWarning("[Halloween] Cannot spawn Wandering Ghost: Master Client required.");
                return;
            }

            var ghost = GetWanderingGhost();
            if (ghost == null)
            {
                Log.LogWarning("[Halloween] Wandering Ghost not found in scene.");
                return;
            }

            Vector3 spawnPos = targetPosition ?? (GorillaTagger.Instance != null && GorillaTagger.Instance.headCollider != null
                ? GorillaTagger.Instance.headCollider.transform.position + GorillaTagger.Instance.headCollider.transform.forward * 2.5f
                : ghost.transform.position);

            ghost.transform.position = spawnPos;
            ChangeWanderingGhostState(ghost, "idle");
        }

        /// <summary>
        /// Teleports unheld flowers to the destination position.
        /// Master Client authoritative.
        /// </summary>
        public static void SpawnFlowers(Vector3? targetPos = null, bool spawnAll = false)
        {
            if (!IsMasterClient)
            {
                Log.LogWarning("[Halloween] Cannot spawn flowers: Master Client required.");
                return;
            }

            var ghost = GetWanderingGhost();
            var flowers = (ghost != null && ghost.allFlowers != null && ghost.allFlowers.Length > 0)
                ? ghost.allFlowers
                : Object.FindObjectsByType<ThrowableSetDressing>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (flowers == null || flowers.Length == 0) return;

            Vector3 spawnLocation = targetPos ?? (GorillaTagger.Instance != null && GorillaTagger.Instance.headCollider != null
                ? GorillaTagger.Instance.headCollider.transform.position + GorillaTagger.Instance.headCollider.transform.forward * 1.0f
                : Vector3.zero);

            float duration = (ghost != null && ghost.flowerSpawnDuration > 1f) ? ghost.flowerSpawnDuration : 30f;

            var free = new List<ThrowableSetDressing>();
            foreach (var f in flowers)
                if (f != null && !f.InHand()) free.Add(f);
            if (free.Count == 0) return;

            if (!spawnAll)
            {
                var one = free[Random.Range(0, free.Count)];
                free.Clear();
                free.Add(one);
            }

            foreach (var flower in free)
            {
                try
                {
                    if (!flower.gameObject.activeSelf) flower.gameObject.SetActive(true);
                    if (!flower.IsLocalOwnedWorldShareable) flower.WorldShareableRequestOwnership();

                    flower.SetWillTeleport();
                    flower.transform.position = spawnLocation + Random.insideUnitSphere * 0.15f;
                    flower.StartRespawnTimer(duration);
                }
                catch (Exception e)
                {
                    Log.LogWarning("[Halloween] Flower spawn failed: " + e.Message);
                }
            }
        }

        /// <summary>
        /// Teleports all unheld flowers into the player's right hand.
        /// Master Client authoritative.
        /// </summary>
        public static void BringFlowersToHand(bool rightHand = true)
        {
            if (!IsMasterClient || GorillaTagger.Instance == null) return;
            Transform hand = rightHand ? GorillaTagger.Instance.rightHandTransform : GorillaTagger.Instance.leftHandTransform;
            SpawnFlowers(hand != null ? hand.position : Vector3.zero, spawnAll: true);
        }

        /// <summary>
        /// Triggers all HauntedObjects across the entire map.
        /// </summary>
        private static HauntedObject[] _hauntCache = new HauntedObject[0];
        private static float _hauntCacheTime = -100f;

        public static HauntedObject[] GetHauntedObjects()
        {
            if (Time.time - _hauntCacheTime > 5f)
            {
                _hauntCacheTime = Time.time;
                _hauntCache = Object.FindObjectsByType<HauntedObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                Log.LogInfo($"Found {_hauntCache.Length} HauntedObject components");
            }
            return _hauntCache;
        }

        public static void TriggerAllHauntedObjects()
        {
            _hauntCacheTime = -100f; // force refresh on manual press
            var objects = GetHauntedObjects();
            int ok = 0, inactive = 0;
            foreach (var obj in objects)
            {
                if (obj == null) continue;
                if (!obj.gameObject.activeInHierarchy) { inactive++; continue; }
                try { HalloweenHelper.TriggerHaunted(obj); ok++; }
                catch (Exception e) { Log.LogWarning("Haunt failed on " + obj.name + ": " + e); }
            }
            Log.LogInfo($"Haunted {ok}/{objects.Length} objects ({inactive} inactive/out of zone)");
        }

        // ---------------- Lurker targeting ----------------

        private static int _targetIndex;
        private static int _allIndex;

        public static List<NetPlayer> GetPlayers()
        {
            var list = new List<NetPlayer>();
            foreach (var c in VRRigCache.ActiveRigContainers)
                if (c != null && c.Creator != null) list.Add(c.Creator);
            var local = NetworkSystem.Instance != null ? NetworkSystem.Instance.LocalPlayer : null;
            if (local != null && !list.Exists(p => p.ActorNumber == local.ActorNumber)) list.Add(local);
            return list;
        }

        /// <summary>Makes the Lurker hunt the given player (Master Client).</summary>
        public static bool LurkerAttack(NetPlayer player)
        {
            if (!IsMasterClient || player == null) return false;
            var lurker = GetLurkerGhost();
            if (lurker == null) return false;
            try
            {
                lurker.Allowed(false);
                Traverse.Create(lurker).Method("PickPlayer", player).GetValue();
                Transform tt = Traverse.Create(lurker).Field("targetTransform").GetValue<Transform>();
                if (tt == null) return false;
                Traverse.Create(lurker).Field("cooldownTimeRemaining").SetValue(0f);
                ChangeLurkerGhostState(lurker, "seek");
                return true;
            }
            catch (Exception e)
            {
                Log.LogWarning("[Halloween] Lurker attack failed: " + e.Message);
                return false;
            }
        }

        public static void LurkerAttackMe()
        {
            LurkerAttack(NetworkSystem.Instance != null ? NetworkSystem.Instance.LocalPlayer : null);
        }

        public static void CycleTarget()
        {
            var players = GetPlayers();
            if (players.Count == 0) return;
            _targetIndex = (_targetIndex + 1) % players.Count;
            Log.LogInfo("[Halloween] Lurker target: " + (players[_targetIndex]?.NickName ?? "Target"));
        }

        public static void LurkerAttackTarget()
        {
            var players = GetPlayers();
            if (players.Count == 0) return;
            _targetIndex %= players.Count;
            LurkerAttack(players[_targetIndex]);
        }

        /// <summary>Call every frame: whenever the Lurker is back on patrol, send it after the next player.</summary>
        public static void LurkerAttackAllTick()
        {
            if (!IsMasterClient) return;
            var lurker = GetLurkerGhost();
            if (lurker == null || GetLurkerGhostStateName(lurker) != "patrol") return;
            var players = GetPlayers();
            if (players.Count == 0) return;
            _allIndex = (_allIndex + 1) % players.Count;
            LurkerAttack(players[_allIndex]);
        }
    }

    /// <summary>
    /// Keeps the Wandering Ghost in its visible "idle" state so every player sees it.
    /// </summary>
    public static class GhostHold
    {
        private static float _savedRadius = -1f;

        public static void Hold(WanderingGhost g)
        {
            if (g == null) return;
            if (_savedRadius < 0f) _savedRadius = g.sphereColliderRadius;
            g.sphereColliderRadius = 0.01f;   // stops "player touched ghost -> hide" check
            Traverse.Create(g).Field("idlePassedTime").SetValue(0f); // stops idle timeout -> patrol
            if (HalloweenHelper.GetWanderingGhostStateName(g) != "idle")
                HalloweenHelper.ChangeWanderingGhostState(g, "idle");
        }

        public static void Release(WanderingGhost g)
        {
            if (g != null && _savedRadius >= 0f) g.sphereColliderRadius = _savedRadius;
            _savedRadius = -1f;
        }
    }

    // =========================================================================
    // Mod Implementations
    // =========================================================================

    [PluginPreferenced]
    [PluginToggleable]
    [DefaultExecutionOrder(32000)]
    public class SeeGhosts : Mod
    {
        public SeeGhosts()
        {
            Name = "See Ghosts (Self)";
            isToggle = true;
        }

        public override void LateUpdate()
        {
            HalloweenHelper.SetGhostsVisible(true);
        }

        public override void OnDisable()
        {
            HalloweenHelper.SetGhostsVisible(false);
        }

        public override void Execute()
        {
            HalloweenHelper.SetGhostsVisible(true);
        }
    }

    public class SpawnWanderingGhost : Mod
    {
        public SpawnWanderingGhost()
        {
            Name = "Spawn Ghost (Self)";
            isToggle = false;
        }

        public override void Execute()
        {
            HalloweenHelper.SpawnWanderingGhost();
        }
    }

    public class SendGhostToPatrol : Mod
    {
        public SendGhostToPatrol()
        {
            Name = "Send Ghost To Patrol (Self)";
            isToggle = false;
        }

        public override void Execute()
        {
            if (!HalloweenHelper.IsMasterClient) return;
            var ghost = HalloweenHelper.GetWanderingGhost();
            if (ghost != null)
            {
                HalloweenHelper.ChangeWanderingGhostState(ghost, "patrol");
            }
        }
    }

    public class SpawnFlowers : Mod
    {
        public SpawnFlowers()
        {
            Name = "Spawn Flower (Self)";
            isToggle = false;
        }

        public override void Execute()
        {
            HalloweenHelper.SpawnFlowers(spawnAll: false);
        }
    }

    public class FlowersToHand : Mod
    {
        public FlowersToHand()
        {
            Name = "Flowers To Hand (Self)";
            isToggle = false;
        }

        public override void Execute()
        {
            HalloweenHelper.BringFlowersToHand(rightHand: true);
        }
    }

    public class HauntAllObjects : Mod
    {
        public HauntAllObjects()
        {
            Name = "Haunt All (Self)";
            isToggle = false;
        }

        public override void Execute()
        {
            HalloweenHelper.TriggerAllHauntedObjects();
        }
    }

    [PluginPreferenced]
    [PluginToggleable]
    public class AuraHauntNearby : Mod
    {
        private float _nextHauntTime;

        public AuraHauntNearby()
        {
            Name = "Haunt Aura (Self)";
            isToggle = true;
        }

        public override void Update()
        {
            if (Time.time < _nextHauntTime || GorillaTagger.Instance == null) return;
            _nextHauntTime = Time.time + 1.0f;

            Vector3 playerPos = GorillaTagger.Instance.transform.position;
            var objects = HalloweenHelper.GetHauntedObjects();
            foreach (var obj in objects)
            {
                if (obj != null && obj.gameObject.activeInHierarchy && Vector3.Distance(obj.transform.position, playerPos) <= 12f)
                {
                    HalloweenHelper.TriggerHaunted(obj);
                }
            }
        }
    }

    public class LurkerAttackMe : Mod
    {
        public LurkerAttackMe() { Name = "Lurker Attack (Self)"; isToggle = false; }
        public override void Execute() { HalloweenHelper.LurkerAttackMe(); }
    }

    public class LurkerNextTarget : Mod
    {
        public LurkerNextTarget() { Name = "Lurker Next Target (Self)"; isToggle = false; }
        public override void Execute() { HalloweenHelper.CycleTarget(); }
    }

    public class LurkerAttackTarget : Mod
    {
        public LurkerAttackTarget() { Name = "Lurker Attack Target (Self)"; isToggle = false; }
        public override void Execute() { HalloweenHelper.LurkerAttackTarget(); }
    }

    [PluginToggleable]
    public class LurkerAttackAll : Mod
    {
        public LurkerAttackAll() { Name = "Lurker Attack All (Self)"; isToggle = true; }
        public override void Update() { HalloweenHelper.LurkerAttackAllTick(); }
    }

    [PluginToggleable]
    [DefaultExecutionOrder(32000)]
    public class OrbitWanderingGhost : Mod
    {
        private float _angle;
        public float radius = 2.5f;
        public float speed = 90f;
        public float height = 0.2f;

        public OrbitWanderingGhost() { Name = "Orbit Ghost (Self)"; isToggle = true; }

        public override void LateUpdate()
        {
            if (!HalloweenHelper.IsMasterClient || GorillaTagger.Instance == null) return;
            var ghost = HalloweenHelper.GetWanderingGhost();
            var head = GorillaTagger.Instance.headCollider;
            if (ghost == null || head == null) return;

            GhostHold.Hold(ghost);
            _angle += speed * Time.deltaTime;
            float rad = _angle * Mathf.Deg2Rad;
            Vector3 center = head.transform.position;
            Vector3 pos = center + new Vector3(Mathf.Cos(rad) * radius, height, Mathf.Sin(rad) * radius);

            ghost.transform.position = pos;
            Vector3 tangent = new Vector3(-Mathf.Sin(rad), 0f, Mathf.Cos(rad));
            ghost.transform.rotation = Quaternion.LookRotation(tangent);
        }

        public override void OnEnable()
        {
            if (HalloweenHelper.IsMasterClient)
            {
                var ghost = HalloweenHelper.GetWanderingGhost();
                if (ghost != null) HalloweenHelper.ChangeWanderingGhostState(ghost, "idle");
            }
        }

        public override void OnDisable()
        {
            if (HalloweenHelper.IsMasterClient)
            {
                var ghost = HalloweenHelper.GetWanderingGhost();
                if (ghost != null)
                {
                    GhostHold.Release(ghost);
                    HalloweenHelper.ChangeWanderingGhostState(ghost, "patrol");
                }
            }
        }
    }

    [PluginToggleable]
    [DefaultExecutionOrder(32000)]
    public class ShowWanderingGhostToAll : Mod
    {
        public ShowWanderingGhostToAll() { Name = "Show Ghost (Self)"; isToggle = true; }

        public override void LateUpdate()
        {
            if (!HalloweenHelper.IsMasterClient) return;
            GhostHold.Hold(HalloweenHelper.GetWanderingGhost());
        }

        public override void OnDisable()
        {
            if (!HalloweenHelper.IsMasterClient) return;
            var g = HalloweenHelper.GetWanderingGhost();
            if (g == null) return;
            GhostHold.Release(g);
            HalloweenHelper.ChangeWanderingGhostState(g, "patrol");
        }
    }
}

