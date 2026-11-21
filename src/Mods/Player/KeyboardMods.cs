using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GorillaNetworking;
using UnityEngine;
using CascadeMenu.UI;

namespace CascadeMenu.Mods
{
    // -------------------------------------------------------------------------
    // 1. Change Name Mod (Player)
    //    Updates local player nickname across NetworkSystem, GorillaComputer,
    //    PlayerPrefs, and offlineVRRig using the integrated Cascade keyboard.
    // -------------------------------------------------------------------------
    [PluginToggleable]
    [ModCategory("Player")]
    public sealed class NameChangerMod : Mod
    {
        private string _targetName = "MONKE";

        public NameChangerMod()
        {
            Name = "Change Name";
            isToggle = false;

            AddSetting(ModSetting.Text(
                "name", "New player name",
                "Enter your new gorilla nickname (up to 12 characters), then press Run",
                () => _targetName,
                value => _targetName = value ?? "MONKE"));
        }

        public override void Execute()
        {
            string newName = (_targetName ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(newName) || newName.Length > 12)
            {
                ModKit.Notify("Change Name", "Open settings (gear) to set a valid name (1-12 chars).");
                return;
            }

            try
            {
                if (NetworkSystem.Instance != null)
                {
                    NetworkSystem.Instance.SetMyNickName(newName);
                }

                GorillaComputer comp = GorillaComputer.instance;
                if (comp != null)
                {
                    comp.currentName = newName;
                    comp.savedName = newName;
                    comp.SetLocalNameTagText(newName);
                }

                if (GorillaTagger.Instance != null && GorillaTagger.Instance.offlineVRRig != null)
                {
                    GorillaTagger.Instance.offlineVRRig.playerNameVisible = newName;
                }

                PlayerPrefs.SetString("playerName", newName);
                PlayerPrefs.Save();

                ModKit.Notify("Change Name", "Name changed to: " + newName);
                Plugin.Log?.LogInfo("Cascade Change Name: set nickname to " + newName);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError("Cascade Change Name failed: " + ex.Message);
                ModKit.Notify("Change Name", "Failed to apply name.");
            }
        }
    }

    // -------------------------------------------------------------------------
    // 2. Join Room Code Mod (Player)
    //    Joins any room code via PhotonNetworkController.AttemptToJoinSpecificRoom
    //    with configurable JoinType (Solo or JoinWithParty).
    // -------------------------------------------------------------------------
    [PluginToggleable]
    [ModCategory("Player")]
    public sealed class JoinRoomByCodeMod : Mod
    {
        private static readonly string[] JoinModeNames = { "Solo", "With Party" };
        private string _roomCode = "CASCADE";
        private int _joinMode = 0;

        public JoinRoomByCodeMod()
        {
            Name = "Join Room Code";
            isToggle = false;

            AddSetting(ModSetting.Text(
                "code", "Room code",
                "Room code to join when you press Run",
                () => _roomCode,
                value => _roomCode = (value ?? string.Empty).ToUpperInvariant()));

            AddSetting(ModSetting.Dropdown(
                "mode", "Join mode",
                "Join solo or bring your current party",
                JoinModeNames,
                () => _joinMode,
                value => _joinMode = Mathf.Clamp(value, 0, JoinModeNames.Length - 1)));
        }

        public override void Execute()
        {
            string code = (_roomCode ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(code))
            {
                ModKit.Notify("Join Room", "Open settings (gear) to enter a room code first.");
                return;
            }

            try
            {
                PhotonNetworkController controller = PhotonNetworkController.Instance;
                if (controller != null)
                {
                    if (GorillaComputer.instance != null)
                    {
                        GorillaComputer.instance.roomToJoin = code;
                    }
                    JoinType type = _joinMode == 1 ? JoinType.JoinWithParty : JoinType.Solo;
                    controller.AttemptToJoinSpecificRoom(code, type);
                    ModKit.Notify("Join Room", "Joining room: " + code);
                    Plugin.Log?.LogInfo("Cascade Join Room Code: joining " + code + " (" + type + ")");
                }
                else
                {
                    ModKit.Notify("Join Room", "PhotonNetworkController is not ready yet.");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError("Cascade Join Room Code failed: " + ex.Message);
                ModKit.Notify("Join Room", "Join failed: " + ex.Message);
            }
        }
    }

    // -------------------------------------------------------------------------
    // 3. Add Custom Property Mod (Player)
    //    Sets or removes custom Photon player properties on your local player
    //    so other clients / mods can read them.
    // -------------------------------------------------------------------------
    [PluginToggleable]
    [ModCategory("Player")]
    public sealed class CustomPropertyMod : Mod
    {
        private static readonly string[] ActionModes = { "Set / Update Property", "Remove Property" };
        private string _propKey = "CascadeMenu";
        private string _propValue = "1.0";
        private int _actionMode = 0;

        public CustomPropertyMod()
        {
            Name = "Custom Property";
            isToggle = false;

            AddSetting(ModSetting.Text(
                "key", "Property key",
                "Photon custom property key name",
                () => _propKey,
                value => _propKey = value ?? "CascadeMenu"));

            AddSetting(ModSetting.Text(
                "value", "Property value",
                "Photon custom property value string",
                () => _propValue,
                value => _propValue = value ?? string.Empty));

            AddSetting(ModSetting.Dropdown(
                "action", "Action",
                "Set/update the property or remove it from your player",
                ActionModes,
                () => _actionMode,
                value => _actionMode = Mathf.Clamp(value, 0, ActionModes.Length - 1)));
        }

        public override void Execute()
        {
            string key = (_propKey ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(key))
            {
                ModKit.Notify("Custom Property", "Property key cannot be empty.");
                return;
            }

            string val = _propValue ?? string.Empty;
            bool remove = _actionMode == 1;

            try
            {
                GameApi.Resolve();
                Type photonNet = GameApi.PhotonNetworkType;
                Type hashtableType = GameApi.FindType("ExitGames.Client.Photon.Hashtable");
                if (photonNet == null || hashtableType == null)
                {
                    ModKit.Notify("Custom Property", "Photon API not available.");
                    return;
                }

                IDictionary table = Activator.CreateInstance(hashtableType) as IDictionary;
                if (table == null) return;
                table[key] = remove ? null : (object)val;

                MethodInfo setProps = photonNet.GetMethod(
                    "SetPlayerCustomProperties",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { hashtableType },
                    null);

                if (setProps != null)
                {
                    setProps.Invoke(null, new object[] { table });
                    ModKit.Notify("Custom Property", remove
                        ? "Removed property '" + key + "'"
                        : "Set '" + key + "' = '" + val + "'");
                    Plugin.Log?.LogInfo("Cascade Custom Property: " + (remove ? "removed " : "set ") + key + "=" + val);
                }
                else
                {
                    ModKit.Notify("Custom Property", "SetPlayerCustomProperties not found.");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError("Cascade Custom Property failed: " + ex.Message);
                ModKit.Notify("Custom Property", "Failed to apply custom property.");
            }
        }
    }

    // -------------------------------------------------------------------------
    // 4. Network Triggers Toggle Mod (Game)
    //    Toggles all GorillaNetworkJoinTrigger, GorillaNetworkDisconnectTrigger,
    //    and GorillaQuitBox triggers ON or OFF so you can walk across map
    //    boundaries without changing rooms, disconnecting, or quitting.
    // -------------------------------------------------------------------------
    [PluginToggleable]
    [ModCategory("Game")]
    public sealed class NetworkTriggersMod : Mod
    {
        private static readonly string[] FilterModes =
        {
            "Join + Disconnect + Quit Triggers",
            "Join & Disconnect Triggers Only",
            "Quit Box Triggers Only"
        };

        private int _filterMode = 0;
        private readonly List<GameObject> _disabledObjects = new List<GameObject>();
        private readonly List<MonoBehaviour> _disabledBehaviours = new List<MonoBehaviour>();

        public NetworkTriggersMod()
        {
            Name = "Network Triggers Off";
            isToggle = true;

            AddSetting(ModSetting.Dropdown(
                "scope", "Triggers to disable",
                "Which network & map boundary triggers to disable while active",
                FilterModes,
                () => _filterMode,
                value =>
                {
                    _filterMode = Mathf.Clamp(value, 0, FilterModes.Length - 1);
                    if (IsActive)
                    {
                        RestoreTriggers();
                        DisableTriggers();
                    }
                }));
        }

        public override void OnEnable()
        {
            DisableTriggers();
            ModKit.Notify("Network Triggers", "Network triggers OFF (" + (_disabledObjects.Count + _disabledBehaviours.Count) + " disabled).");
            Plugin.Log?.LogInfo("Cascade Network Triggers OFF: " + _disabledObjects.Count + " objects, " + _disabledBehaviours.Count + " components.");
        }

        public override void OnDisable()
        {
            int count = _disabledObjects.Count + _disabledBehaviours.Count;
            RestoreTriggers();
            ModKit.Notify("Network Triggers", "Network triggers ON (" + count + " restored).");
            Plugin.Log?.LogInfo("Cascade Network Triggers ON: restored " + count + " triggers.");
        }

        private void DisableTriggers()
        {
            _disabledObjects.Clear();
            _disabledBehaviours.Clear();

            bool includeNet = _filterMode == 0 || _filterMode == 1;
            bool includeQuit = _filterMode == 0 || _filterMode == 2;

            if (includeNet)
            {
                if (PhotonNetworkController.Instance != null)
                {
                    try
                    {
                        FieldInfo f = typeof(PhotonNetworkController).GetField(
                            "allJoinTriggers",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (f != null && f.GetValue(PhotonNetworkController.Instance) is List<GorillaNetworkJoinTrigger> list)
                        {
                            for (int i = 0; i < list.Count; i++)
                            {
                                GorillaNetworkJoinTrigger trig = list[i];
                                if (trig != null && trig.gameObject != null && trig.gameObject.activeSelf)
                                {
                                    trig.gameObject.SetActive(false);
                                    _disabledObjects.Add(trig.gameObject);
                                }
                            }
                        }
                    }
                    catch (Exception) { }
                }

                GorillaNetworkJoinTrigger[] joinTriggers = UnityEngine.Object.FindObjectsByType<GorillaNetworkJoinTrigger>(FindObjectsSortMode.None);
                for (int i = 0; i < joinTriggers.Length; i++)
                {
                    GorillaNetworkJoinTrigger trig = joinTriggers[i];
                    if (trig != null && trig.gameObject != null && trig.gameObject.activeSelf)
                    {
                        trig.gameObject.SetActive(false);
                        _disabledObjects.Add(trig.gameObject);
                    }
                }

                GorillaNetworkDisconnectTrigger[] discTriggers = UnityEngine.Object.FindObjectsByType<GorillaNetworkDisconnectTrigger>(FindObjectsSortMode.None);
                for (int i = 0; i < discTriggers.Length; i++)
                {
                    GorillaNetworkDisconnectTrigger trig = discTriggers[i];
                    if (trig != null && trig.gameObject != null && trig.gameObject.activeSelf)
                    {
                        trig.gameObject.SetActive(false);
                        _disabledObjects.Add(trig.gameObject);
                    }
                }
            }

            if (includeQuit)
            {
                GorillaQuitBox[] quitBoxes = UnityEngine.Object.FindObjectsByType<GorillaQuitBox>(FindObjectsSortMode.None);
                for (int i = 0; i < quitBoxes.Length; i++)
                {
                    GorillaQuitBox qb = quitBoxes[i];
                    if (qb != null && qb.gameObject != null && qb.gameObject.activeSelf)
                    {
                        qb.gameObject.SetActive(false);
                        _disabledObjects.Add(qb.gameObject);
                    }
                }
            }
        }

        private void RestoreTriggers()
        {
            for (int i = 0; i < _disabledObjects.Count; i++)
            {
                if (_disabledObjects[i] != null)
                {
                    _disabledObjects[i].SetActive(true);
                }
            }
            for (int i = 0; i < _disabledBehaviours.Count; i++)
            {
                if (_disabledBehaviours[i] != null)
                {
                    _disabledBehaviours[i].enabled = true;
                }
            }
            _disabledObjects.Clear();
            _disabledBehaviours.Clear();
        }
    }
}
