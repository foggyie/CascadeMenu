using System.Collections.Generic;
using UnityEngine;
using CascadeMenu.UI;

namespace CascadeMenu.Mods
{
    /// <summary>Changes player name using virtual keyboard.</summary>
    [PluginToggleable]
    [ModCategory("Player")]
    public sealed class NameChangerMod : Mod
    {
        public NameChangerMod()
        {
            Name = "Change Name";
            isToggle = false;
        }

        public override void Execute()
        {
            VRRig localRig = VRRig.LocalRig;
            string currentName = localRig != null && !string.IsNullOrEmpty(localRig.playerNameVisible) 
                ? localRig.playerNameVisible : "MONKE";
            
            VirtualKeyboard.Show(currentName, "Enter new name:", (newName) =>
            {
                if (!string.IsNullOrEmpty(newName) && newName.Length <= 12)
                {
                    if (GorillaTagger.Instance != null && GorillaTagger.Instance.offlineVRRig != null)
                    {
                        GorillaTagger.Instance.offlineVRRig.playerNameVisible = newName;
                    }
                    ModKit.Notify("Name", $"Changed to: {newName}");
                    Plugin.Log?.LogInfo($"Name changed to: {newName}");
                }
                else
                {
                    ModKit.Notify("Name", "Invalid name (max 12 chars)");
                }
            });
        }
    }

    /// <summary>Join room by code using virtual keyboard.</summary>
    [PluginToggleable]
    [ModCategory("Player")]
    public sealed class JoinRoomByCodeMod : Mod
    {
        public JoinRoomByCodeMod()
        {
            Name = "Join Room Code";
            isToggle = false;
        }

        public override void Execute()
        {
            VirtualKeyboard.Show("", "Enter room code:", (code) =>
            {
                if (!string.IsNullOrEmpty(code))
                {
                    code = code.ToUpper();
                    ModKit.Notify("Room", $"Joining: {code}");
                    
                    // Use reflection to call PhotonNetworkController
                    try
                    {
                        var controllerType = System.Type.GetType("PhotonNetworkController");
                        if (controllerType != null)
                        {
                            var instanceProp = controllerType.GetProperty("Instance", 
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                            if (instanceProp != null)
                            {
                                var controller = instanceProp.GetValue(null);
                                var joinMethod = controllerType.GetMethod("AttemptToJoinSpecificRoom");
                                if (joinMethod != null && controller != null)
                                {
                                    joinMethod.Invoke(controller, new object[] { code });
                                    Plugin.Log?.LogInfo($"Attempting to join room: {code}");
                                    return;
                                }
                            }
                        }
                        ModKit.Notify("Room", "Network controller not available");
                    }
                    catch (System.Exception ex)
                    {
                        Plugin.Log?.LogError($"Failed to join room: {ex.Message}");
                        ModKit.Notify("Room", "Join failed - check console");
                    }
                }
            });
        }
    }

    /// <summary>Set custom room properties using keyboard (simplified).</summary>
    [PluginToggleable]
    [ModCategory("Player")]
    public sealed class CustomPropertyMod : Mod
    {
        private string _lastKey = "color";
        private string _lastValue = "red";

        public CustomPropertyMod()
        {
            Name = "Set Custom Property";
            isToggle = false;
        }

        public override void Execute()
        {
            VirtualKeyboard.Show(_lastKey, "Enter property key:", (key) =>
            {
                _lastKey = key;
                if (!string.IsNullOrEmpty(key))
                {
                    VirtualKeyboard.Show(_lastValue, $"Enter value for '{key}':", (value) =>
                    {
                        _lastValue = value;
                        ModKit.Notify("Property", $"Would set {key} = {value}");
                        Plugin.Log?.LogInfo($"Custom property: {key} = {value}");
                        Plugin.Log?.LogInfo("(Feature requires Photon integration)");
                    });
                }
            });
        }
    }

    /// <summary>Search and display text using keyboard.</summary>
    [PluginToggleable]
    [ModCategory("Tools")]
    public sealed class ModSearchMod : Mod
    {
        public ModSearchMod()
        {
            Name = "Search Mods";
            isToggle = false;
        }

        public override void Execute()
        {
            VirtualKeyboard.Show("", "Search mods:", (query) =>
            {
                if (!string.IsNullOrEmpty(query))
                {
                    SearchMods(query.ToLower());
                }
            });
        }

        private void SearchMods(string query)
        {
            Plugin.Log?.LogInfo($"=== MOD SEARCH: {query} ===");
            Plugin.Log?.LogInfo("Search functionality would list matching mods here");
            ModKit.Notify("Search", $"Searched for: {query}");
        }
    }

    /// <summary>Quick text input demo using keyboard.</summary>
    [PluginToggleable]
    [ModCategory("Player")]
    public sealed class QuickChatMod : Mod
    {
        public QuickChatMod()
        {
            Name = "Quick Chat";
            isToggle = false;
        }

        public override void Execute()
        {
            VirtualKeyboard.Show("", "Type message:", (message) =>
            {
                if (!string.IsNullOrEmpty(message))
                {
                    ModKit.Notify("Chat", $"Sent: {message}");
                    Plugin.Log?.LogInfo($"Chat message: {message}");
                }
            });
        }
    }
}
