using System.Collections.Generic;
using GorillaLocomotion;
using GorillaNetworking;
using UnityEngine;
using CascadeMenu.Menu;
using CascadeMenu.Mods.Core;
using CascadeMenu.Mods.Util;
using CascadeMenu.Mods.Overlays;
using CascadeMenu.Mods.Player;
using CascadeMenu.Mods.Cosmetics;
using CascadeMenu.Mods.Visual;
using CascadeMenu.Mods.Movement;

namespace CascadeMenu.Mods.Tools
{
    // -------------------------------------------------------------------------
    // Measurement and inspection
    // -------------------------------------------------------------------------

    /// <summary>
    /// Draws a line between both hands and reports its length, for measuring
    /// rooms, builds and grab distances.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Tools")]
    public sealed class RulerMod : OverlayMod
    {
        private LineRenderer _line;

        public RulerMod()
        {
            Name = "Ruler";
            DefaultAnchor = HudAnchor.BottomRight;
            DefaultSlot = 3;
            Design = HudDesign.Capsule;
            AccentColor = new Color(0.35f, 0.9f, 1f, 1f);
            BadgeIcon = CascadeMenu.UI.CascadeIcon.Sliders;
            CardWidth = 310f;
            CardHeight = 104f;
            FontSize = 18.5f;
            ShowPanel = true;
            ViewDistance = 0.6f;
        }

        protected override void OnOverlayStarted()
        {
            _line = ModKit.Line("CascadeRuler", null, 240, 0.012f);
            _line.positionCount = 2;
            _line.startColor = new Color(0.35f, 0.9f, 1f, 0.9f);
            _line.endColor = new Color(0.35f, 0.9f, 1f, 0.9f);
        }

        protected override void OnOverlayStopped()
        {
            if (_line != null) Object.Destroy(_line.gameObject);
            _line = null;
        }

        public override void Update()
        {
            if (_line == null) return;

            GTPlayer player = ModKit.Player;
            if (player == null)
            {
                _line.positionCount = 0;
                return;
            }

            Vector3 left = player.LeftHand.GetCurrentHandPosition();
            Vector3 right = player.RightHand.GetCurrentHandPosition();
            _line.positionCount = 2;
            _line.SetPosition(0, left);
            _line.SetPosition(1, right);
        }

        protected override string Build()
        {
            GTPlayer player = ModKit.Player;
            if (player == null) return "<b>RULER</b>\nwaiting for the local player";

            Vector3 left = player.LeftHand.GetCurrentHandPosition();
            Vector3 right = player.RightHand.GetCurrentHandPosition();
            float span = Vector3.Distance(left, right);

            return "<b>RULER</b>\n" +
                   "Hands    " + ModKit.F(span, "0.00") + " m\n" +
                   "Gorilla  " + ModKit.F(span * 3.281f, "0.0") + " ft";
        }
    }

    /// <summary>Distance and surface name of whatever the view is aimed at.</summary>
    [PluginToggleable]
    [ModCategory("Tools")]
    public sealed class LookDistanceMod : OverlayMod
    {
        private RaycastHit _hit;
        private bool _hasHit;

        public LookDistanceMod()
        {
            Name = "Look Distance";
            DefaultAnchor = HudAnchor.MidLeft;
            DefaultSlot = 3;
            Design = HudDesign.WindowCard;
            AccentColor = new Color(0.45f, 0.95f, 0.65f, 1f);
            CardWidth = 330f;
            CardHeight = 144f;
            FontSize = 17.5f;
            AutoSize = true;
        }

        public override void Update()
        {
            Transform head = ModKit.Head;
            _hasHit = false;
            if (head == null) return;

            if (Physics.Raycast(head.position, head.forward, out _hit, 500f, ~0, QueryTriggerInteraction.Ignore))
            {
                _hasHit = true;
            }
        }

        protected override string Build()
        {
            if (!_hasHit) return "<b>LOOK</b>\nno surface in range";

            Collider collider = _hit.collider;
            string target = collider != null ? collider.gameObject.name : "unknown";
            string layer = collider != null ? LayerMask.LayerToName(collider.gameObject.layer) : string.Empty;
            if (string.IsNullOrEmpty(layer)) layer = "-";

            return "<b>LOOK</b>\n" +
                   "Distance " + ModKit.F(_hit.distance, "0.00") + " m\n" +
                   "Target   " + CascadeMenu.UI.CascadeUI.Truncate(target, 22) + "\n" +
                   "Layer    " + layer;
        }
    }

    /// <summary>Copies everything known about the object being looked at.</summary>
    [PluginToggleable]
    [ModCategory("Tools")]
    public sealed class ObjectInfoMod : Mod
    {
        public ObjectInfoMod()
        {
            Name = "Object Info";
            isToggle = false;
        }

        public override void Execute()
        {
            Transform head = ModKit.Head;
            if (head == null)
            {
                ModKit.Notify("Object Info", "No view camera yet.");
                return;
            }

            RaycastHit hit;
            if (!Physics.Raycast(head.position, head.forward, out hit, 500f, ~0, QueryTriggerInteraction.Ignore))
            {
                ModKit.Notify("Object Info", "Nothing in front of the view.");
                return;
            }

            Collider collider = hit.collider;
            GameObject target = collider != null ? collider.gameObject : null;
            string path = target != null ? PathOf(target.transform) : "unknown";

            string report = "Object: " + (target != null ? target.name : "unknown") +
                            "\nPath: " + path +
                            "\nLayer: " + (target != null ? LayerMask.LayerToName(target.layer) : "-") +
                            "\nTag: " + (target != null ? target.tag : "-") +
                            "\nDistance: " + ModKit.F(hit.distance, "0.00") + " m" +
                            "\nPoint: " + ModKit.Vec(hit.point) +
                            "\nNormal: " + ModKit.Vec(hit.normal);

            Renderer renderer = collider != null ? collider.GetComponent<Renderer>() : null;
            if (renderer != null)
            {
                report += "\nMaterial: " + (renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "-");
            }

            PlayerActions.Copy(report);
            Plugin.Log?.LogInfo("Cascade Object Info copied:\n" + report);
            ModKit.Notify("Object Info", "Copied " + (target != null ? target.name : "object") + " to clipboard.");
        }

        private static string PathOf(Transform transform)
        {
            string path = transform.name;
            Transform parent = transform.parent;
            int guard = 0;
            while (parent != null && guard++ < 12)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
    }

    // -------------------------------------------------------------------------
    // Object Enabler & Disabler (Tools)
    // Gun mode: aim and shoot any scene object to disable it, or press Undo /
    // Restore All. Also supports enabling/disabling any GameObject by name.
    // -------------------------------------------------------------------------
    [PluginToggleable]
    [ModCategory("Tools")]
    public sealed class ObjectTogglerMod : Mod
    {
        private static readonly string[] ModeNames =
        {
            "Gun: Shoot to Disable Object",
            "Gun: Undo Last Disabled on Shoot",
            "By Name: Disable Matching Objects",
            "By Name: Enable Matching Objects",
            "Restore All Disabled Objects"
        };

        private int _mode = 0;
        private string _objectQuery = "";
        private float _cooldown = 0.3f;
        private static readonly List<GameObject> DisabledHistory = new List<GameObject>();

        public ObjectTogglerMod()
        {
            Name = "Object Enabler & Disabler";
            isToggle = true;

            AddSetting(ModSetting.Dropdown(
                "mode", "Action mode",
                "Use the Gun to shoot objects off, search by name to enable/disable, or restore all",
                ModeNames,
                () => _mode,
                value =>
                {
                    _mode = Mathf.Clamp(value, 0, ModeNames.Length - 1);
                    if (_mode == 2 && !string.IsNullOrEmpty(_objectQuery))
                    {
                        ToggleByName(_objectQuery, false);
                    }
                    else if (_mode == 3 && !string.IsNullOrEmpty(_objectQuery))
                    {
                        ToggleByName(_objectQuery, true);
                    }
                    else if (_mode == 4)
                    {
                        RestoreAll();
                    }
                }));

            AddSetting(ModSetting.Text(
                "query", "Object name / path",
                "Name or path of GameObject to enable or disable (used in By Name modes)",
                () => _objectQuery,
                value => _objectQuery = value ?? string.Empty));

            AddSetting(ModSetting.Slider(
                "cooldown", "Gun fire cooldown",
                "Seconds between shots when using Gun mode",
                0.1f, 1.5f,
                () => _cooldown,
                value => _cooldown = value,
                value => value.ToString("0.00") + " s"));
        }

        public override void OnEnable()
        {
            if (_mode == 2 && !string.IsNullOrEmpty(_objectQuery))
            {
                ToggleByName(_objectQuery, false);
            }
            else if (_mode == 3 && !string.IsNullOrEmpty(_objectQuery))
            {
                ToggleByName(_objectQuery, true);
            }
            else if (_mode == 4)
            {
                RestoreAll();
            }
            Plugin.Log?.LogInfo("Cascade Object Enabler & Disabler active.");
        }

        public override void Update()
        {
            if (_mode != 0 && _mode != 1) return;

            GunLib.Update((hit, rig, player) =>
            {
                if (_mode == 1)
                {
                    UndoLast();
                    return;
                }

                if (hit.collider == null || hit.collider.gameObject == null) return;
                GameObject target = hit.collider.gameObject;

                // Protect local player, remote players, and the Cascade menu itself
                if (IsProtected(target))
                {
                    ModKit.Notify("Object Toggler", "Protected object: " + target.name);
                    return;
                }

                target.SetActive(false);
                if (!DisabledHistory.Contains(target))
                {
                    DisabledHistory.Add(target);
                }
                ModKit.Notify("Object Disabled", "Disabled: " + target.name + " (" + DisabledHistory.Count + " stored)");
                Plugin.Log?.LogInfo("Cascade Object Disabler: hid " + target.name);
            }, cooldown: _cooldown);
        }

        public override void OnDisable()
        {
            GunLib.Cleanup();
        }

        private static bool IsProtected(GameObject go)
        {
            if (go == null) return true;
            Transform t = go.transform;
            if (Plugin.Root != null && t.IsChildOf(Plugin.Root.transform)) return true;
            if (GorillaTagger.Instance != null && t.IsChildOf(GorillaTagger.Instance.transform)) return true;
            if (VRRig.LocalRig != null && t.IsChildOf(VRRig.LocalRig.transform)) return true;
            if (go.GetComponentInParent<VRRig>() != null) return true;
            if (go.GetComponentInParent<Camera>() != null) return true;
            return false;
        }

        private static void ToggleByName(string query, bool enable)
        {
            string q = (query ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(q))
            {
                ModKit.Notify("Object Toggler", "Enter an object name in settings first.");
                return;
            }

            if (enable)
            {
                int enabledCount = 0;
                // First check our disabled history
                for (int i = DisabledHistory.Count - 1; i >= 0; i--)
                {
                    GameObject obj = DisabledHistory[i];
                    if (obj == null)
                    {
                        DisabledHistory.RemoveAt(i);
                        continue;
                    }
                    if (obj.name.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        obj.SetActive(true);
                        DisabledHistory.RemoveAt(i);
                        enabledCount++;
                    }
                }

                // Also search all loaded transforms (including inactive children)
                Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
                for (int i = 0; i < all.Length; i++)
                {
                    Transform tr = all[i];
                    if (tr == null || tr.gameObject == null) continue;
                    if (!tr.gameObject.scene.IsValid()) continue;
                    if (tr.gameObject.activeSelf) continue;
                    if (tr.name.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        tr.gameObject.SetActive(true);
                        enabledCount++;
                    }
                }

                ModKit.Notify("Object Enabler", "Enabled " + enabledCount + " object(s) matching '" + q + "'.");
            }
            else
            {
                int disabledCount = 0;
                GameObject[] all = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                for (int i = 0; i < all.Length; i++)
                {
                    GameObject obj = all[i];
                    if (obj == null || !obj.activeSelf || IsProtected(obj)) continue;
                    if (obj.name.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        obj.SetActive(false);
                        if (!DisabledHistory.Contains(obj)) DisabledHistory.Add(obj);
                        disabledCount++;
                    }
                }
                ModKit.Notify("Object Disabler", "Disabled " + disabledCount + " object(s) matching '" + q + "'.");
            }
        }

        internal static void UndoLast()
        {
            while (DisabledHistory.Count > 0)
            {
                int last = DisabledHistory.Count - 1;
                GameObject obj = DisabledHistory[last];
                DisabledHistory.RemoveAt(last);
                if (obj != null)
                {
                    obj.SetActive(true);
                    ModKit.Notify("Object Enabler", "Re-enabled: " + obj.name);
                    return;
                }
            }
            ModKit.Notify("Object Enabler", "No disabled objects to undo.");
        }

        internal static void RestoreAll()
        {
            int count = 0;
            for (int i = 0; i < DisabledHistory.Count; i++)
            {
                if (DisabledHistory[i] != null)
                {
                    DisabledHistory[i].SetActive(true);
                    count++;
                }
            }
            DisabledHistory.Clear();
            ModKit.Notify("Object Enabler", "Restored " + count + " disabled object(s).");
        }
    }

    /// <summary>One-click action to re-enable every object disabled by Object Enabler & Disabler.</summary>
    [PluginToggleable]
    [ModCategory("Tools")]
    public sealed class RestoreDisabledObjectsMod : Mod
    {
        public RestoreDisabledObjectsMod()
        {
            Name = "Restore Disabled Objects";
            isToggle = false;
        }

        public override void Execute()
        {
            ObjectTogglerMod.RestoreAll();
        }
    }

    /// <summary>Drops a world marker you can walk back to.</summary>
    [PluginToggleable]
    [ModCategory("Tools")]
    public sealed class BeaconMod : OverlayMod
    {
        private const int Segments = 48;

        private Vector3 _position;
        private LineRenderer _ring;
        private LineRenderer _column;
        private GameObject _marker;

        public BeaconMod()
        {
            Name = "Beacon";
            DefaultAnchor = HudAnchor.BottomLeft;
            DefaultSlot = 3;
            Design = HudDesign.Capsule;
            AccentColor = new Color(1f, 0.78f, 0.3f, 1f);
            BadgeIcon = CascadeMenu.UI.CascadeIcon.Recenter;
            CardWidth = 320f;
            CardHeight = 120f;
            FontSize = 17.5f;
        }

        protected override void OnOverlayStarted()
        {
            _position = ModKit.HeadPosition;

            _ring = ModKit.Line("CascadeBeaconRing", null, 239, 0.03f, true);
            _ring.positionCount = Segments;
            _ring.startColor = new Color(1f, 0.75f, 0.25f, 0.9f);
            _ring.endColor = _ring.startColor;

            for (int i = 0; i < Segments; i++)
            {
                float angle = i / (float)Segments * Mathf.PI * 2f;
                _ring.SetPosition(i, _position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.5f);
            }

            _column = ModKit.Line("CascadeBeaconColumn", null, 239, 0.02f);
            _column.positionCount = 2;
            _column.startColor = new Color(1f, 0.85f, 0.4f, 0.35f);
            _column.endColor = new Color(1f, 0.85f, 0.4f, 0f);
            _column.SetPosition(0, _position);
            _column.SetPosition(1, _position + Vector3.up * 40f);

            _marker = ModKit.Primitive("CascadeBeaconMarker", PrimitiveType.Sphere, null, Vector3.one * 0.25f);
            _marker.transform.position = _position + Vector3.up * 0.25f;
            ModKit.Paint(_marker, new Color(1f, 0.78f, 0.3f, 1f));
        }

        protected override void OnOverlayStopped()
        {
            if (_ring != null) Object.Destroy(_ring.gameObject);
            if (_column != null) Object.Destroy(_column.gameObject);
            if (_marker != null) Object.Destroy(_marker);
            _ring = null;
            _column = null;
            _marker = null;
        }

        protected override string Build()
        {
            Vector3 head = ModKit.HeadPosition;
            float distance = Vector3.Distance(head, _position);
            float ground = Mathf.Max(0f, _position.y);

            return "<b>BEACON</b>\n" +
                   "Distance " + ModKit.F(distance, "0.0") + " m\n" +
                   "Point    " + ModKit.Vec(_position) + "\n" +
                   "Height   " + ModKit.F(ground, "0.0") + " m";
        }
    }

    // -------------------------------------------------------------------------
    // Clipboard helpers
    // -------------------------------------------------------------------------

    /// <summary>Copies the current room code.</summary>
    [PluginToggleable]
    [ModCategory("Tools")]
    public sealed class RoomCodeCopyMod : Mod
    {
        public RoomCodeCopyMod()
        {
            Name = "Copy Room Code";
            isToggle = false;
        }

        public override void Execute()
        {
            NetworkSystem system = NetworkSystem.Instance;
            if (system == null || !system.InRoom)
            {
                ModKit.Notify("Room Code", "Not in a room.");
                return;
            }

            string code = system.RoomName;
            if (string.IsNullOrEmpty(code))
            {
                ModKit.Notify("Room Code", "This room has no code.");
                return;
            }

            PlayerActions.Copy(code);
            ModKit.Notify("Room Code", "Copied " + code + ".");
        }
    }

    /// <summary>Copies the room's player list, one name and id per line.</summary>
    [PluginToggleable]
    [ModCategory("Tools")]
    public sealed class PlayerListCopyMod : Mod
    {
        public PlayerListCopyMod()
        {
            Name = "Copy Player List";
            isToggle = false;
        }

        public override void Execute()
        {
            List<NetPlayer> players = PlayerActions.GetPlayers();
            if (players == null || players.Count == 0)
            {
                ModKit.Notify("Player List", "No players to copy.");
                return;
            }

            var lines = new System.Text.StringBuilder();
            for (int i = 0; i < players.Count; i++)
            {
                NetPlayer player = players[i];
                if (player == null) continue;
                lines.Append(PlayerActions.SafeName(player))
                     .Append(" - ")
                     .Append(PlayerActions.SafePlatform(player))
                     .Append(" - ")
                     .Append(player.UserId ?? "unknown")
                     .Append('\n');
            }

            PlayerActions.Copy(lines.ToString());
            ModKit.Notify("Player List", "Copied " + players.Count + " players.");
        }
    }

    /// <summary>Copies the local account id.</summary>
    [PluginToggleable]
    [ModCategory("Tools")]
    public sealed class LocalIdCopyMod : Mod
    {
        public LocalIdCopyMod()
        {
            Name = "Copy Local ID";
            isToggle = false;
        }

        public override void Execute()
        {
            NetPlayer local = PlayerActions.LocalPlayer();
            string id = local != null ? local.UserId : null;
            if (string.IsNullOrEmpty(id))
            {
                ModKit.Notify("Local ID", "No local player yet.");
                return;
            }

            PlayerActions.Copy(id);
            ModKit.Notify("Local ID", "Copied " + id + ".");
        }
    }

    /// <summary>Copies the local head position as plain numbers.</summary>
    [PluginToggleable]
    [ModCategory("Tools")]
    public sealed class PositionCopyMod : Mod
    {
        public PositionCopyMod()
        {
            Name = "Copy Position";
            isToggle = false;
        }

        public override void Execute()
        {
            Transform head = ModKit.Head;
            if (head == null)
            {
                ModKit.Notify("Position", "No local rig yet.");
                return;
            }

            Vector3 point = head.position;
            string text = ModKit.Vec(point);
            PlayerActions.Copy(text);
            ModKit.Notify("Position", "Copied " + text + ".");
        }
    }

    /// <summary>Copies the room's custom properties.</summary>
    [PluginToggleable]
    [ModCategory("Tools")]
    public sealed class RoomPropsCopyMod : Mod
    {
        public RoomPropsCopyMod()
        {
            Name = "Copy Room Props";
            isToggle = false;
        }

        public override void Execute()
        {
            List<string> props = PlayerActions.RoomCustomProps();
            if (props == null || props.Count == 0)
            {
                ModKit.Notify("Room Props", "This room has no custom properties.");
                return;
            }

            PlayerActions.Copy(string.Join("\n", props.ToArray()));
            ModKit.Notify("Room Props", "Copied " + props.Count + " properties.");
        }
    }

    // -------------------------------------------------------------------------
    // Window and cursor control
    // -------------------------------------------------------------------------

    /// <summary>Unlocks the desktop cursor while it is on.</summary>
    [PluginToggleable]
    [ModCategory("Tools")]
    public sealed class CursorFreeMod : Mod
    {
        private CursorLockMode _previousLock;
        private bool _previousVisible;
        private bool _saved;

        public CursorFreeMod()
        {
            Name = "Free Cursor";
            isToggle = true;
        }

        public override void OnEnable()
        {
            _previousLock = Cursor.lockState;
            _previousVisible = Cursor.visible;
            _saved = true;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public override void OnDisable()
        {
            if (!_saved) return;
            Cursor.lockState = _previousLock;
            Cursor.visible = _previousVisible;
            _saved = false;
        }
    }

    /// <summary>Toggles the game window between fullscreen and windowed.</summary>
    [PluginToggleable]
    [ModCategory("Tools")]
    public sealed class FullscreenToggleMod : Mod
    {
        private bool _previous;
        private bool _saved;

        public FullscreenToggleMod()
        {
            Name = "Fullscreen";
            isToggle = true;
        }

        public override void OnEnable()
        {
            _previous = Screen.fullScreen;
            _saved = true;
            if (!Screen.fullScreen) Screen.fullScreen = true;
        }

        public override void OnDisable()
        {
            if (!_saved) return;
            Screen.fullScreen = _previous;
            _saved = false;
        }
    }

}
