using System;
using System.Collections.Generic;
using GorillaLocomotion;
using GorillaNetworking;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CascadeMenu.Mods
{
    // -------------------------------------------------------------------------
    // World and rig readouts
    // -------------------------------------------------------------------------

    /// <summary>Live world position of the local head.</summary>
    [PluginToggleable]
    [ModCategory("Overlays")]
    public sealed class CoordinatesOverlay : OverlayMod
    {
        public CoordinatesOverlay()
        {
            Name = "Coordinates";
            CardHeight = 140f;
            FontSize = 19f;
            SortingOrder = 236;
            RefreshInterval = 0.1f;
        }

        protected override string Build()
        {
            Transform head = ModKit.Head;
            if (head == null) return "<b>POSITION</b>\nwaiting for the local rig";

            Vector3 point = head.position;
            return "<b>POSITION</b>\n" +
                   "X   " + ModKit.F(point.x) + "\n" +
                   "Y   " + ModKit.F(point.y) + "\n" +
                   "Z   " + ModKit.F(point.z);
        }
    }

    /// <summary>Height above the floor under the player, plus world altitude.</summary>
    [PluginToggleable]
    [ModCategory("Overlays")]
    public sealed class AltitudeOverlay : OverlayMod
    {
        private float _groundDistance = -1f;
        private float _nextCheck;

        public AltitudeOverlay()
        {
            Name = "Altitude";
            CardHeight = 150f;
            FontSize = 19f;
            SortingOrder = 236;
            RefreshInterval = 0.2f;
        }

        protected override string Build()
        {
            Transform head = ModKit.Head;
            if (head == null) return "<b>ALTITUDE</b>\nwaiting for the local rig";

            if (Time.unscaledTime >= _nextCheck)
            {
                _nextCheck = Time.unscaledTime + 0.25f;
                _groundDistance = MeasureGround(head.position);
            }

            float altitude = head.position.y;
            return "<b>ALTITUDE</b>\n" +
                   "Ground   " + (_groundDistance >= 0f ? ModKit.F(_groundDistance) + " m" : "-") + "\n" +
                   "World Y  " + ModKit.F(altitude) + "\n" +
                   "Above 0  " + ModKit.F(Mathf.Max(0f, altitude)) + " m";
        }

        private static float MeasureGround(Vector3 origin)
        {
            GTPlayer player = ModKit.Player;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 300f, ~0, QueryTriggerInteraction.Ignore);
            float best = -1f;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider collider = hits[i].collider;
                if (collider == null) continue;
                if (player != null && (collider == player.bodyCollider || collider == player.headCollider)) continue;
                if (best < 0f || hits[i].distance < best) best = hits[i].distance;
            }

            return best;
        }
    }

    /// <summary>Heading, pitch and roll of the view.</summary>
    [PluginToggleable]
    [ModCategory("Overlays")]
    public sealed class AttitudeOverlay : OverlayMod
    {
        public AttitudeOverlay()
        {
            Name = "Attitude";
            CardHeight = 132f;
            FontSize = 19f;
            SortingOrder = 236;
            RefreshInterval = 0.1f;
        }

        protected override string Build()
        {
            Transform head = ModKit.Head;
            if (head == null) return "<b>ATTITUDE</b>\nwaiting for the local rig";

            Vector3 euler = head.eulerAngles;
            float heading = Mathf.Repeat(euler.y, 360f);
            float pitch = Mathf.DeltaAngle(0f, euler.x);
            float roll = Mathf.DeltaAngle(0f, euler.z);

            return "<b>ATTITUDE</b>\n" +
                   "Heading  " + ModKit.F(heading, "0") + "°\n" +
                   "Pitch    " + ModKit.F(pitch, "0") + "°\n" +
                   "Roll     " + ModKit.F(roll, "0") + "°";
        }
    }

    /// <summary>World speed split into horizontal and vertical parts.</summary>
    [PluginToggleable]
    [ModCategory("Overlays")]
    public sealed class VelocityOverlay : OverlayMod
    {
        public VelocityOverlay()
        {
            Name = "Velocity";
            CardHeight = 140f;
            FontSize = 19f;
            SortingOrder = 236;
        }

        protected override string Build()
        {
            GTPlayer player = ModKit.Player;
            if (player == null) return "<b>VELOCITY</b>\nwaiting for the local player";

            Vector3 velocity = player.InstantaneousVelocity;
            float horizontal = new Vector2(velocity.x, velocity.z).magnitude;
            float speed = velocity.magnitude;

            return "<b>VELOCITY</b>\n" +
                   "Speed      " + ModKit.F(speed, "0.00") + " m/s\n" +
                   "Horizontal " + ModKit.F(horizontal, "0.00") + "\n" +
                   "Vertical   " + ModKit.F(velocity.y, "0.00");
        }
    }

    /// <summary>Speed of both hands, useful for swinging practice.</summary>
    [PluginToggleable]
    [ModCategory("Overlays")]
    public sealed class HandSpeedOverlay : OverlayMod
    {
        private Vector3 _lastLeft;
        private Vector3 _lastRight;
        private bool _hasLast;
        private float _leftSpeed;
        private float _rightSpeed;

        public HandSpeedOverlay()
        {
            Name = "Hand Speed";
            CardHeight = 116f;
            FontSize = 19f;
            SortingOrder = 236;
        }

        protected override string Build()
        {
            GTPlayer player = ModKit.Player;
            if (player == null) return "<b>HAND SPEED</b>\nwaiting for the local player";

            Vector3 left = player.LeftHand.GetCurrentHandPosition();
            Vector3 right = player.RightHand.GetCurrentHandPosition();
            float dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);

            if (_hasLast)
            {
                float leftInstant = Vector3.Distance(left, _lastLeft) / dt;
                float rightInstant = Vector3.Distance(right, _lastRight) / dt;
                if (leftInstant < 60f) _leftSpeed = Mathf.Lerp(_leftSpeed, leftInstant, 0.3f);
                if (rightInstant < 60f) _rightSpeed = Mathf.Lerp(_rightSpeed, rightInstant, 0.3f);
            }

            _lastLeft = left;
            _lastRight = right;
            _hasLast = true;

            return "<b>HAND SPEED</b>\n" +
                   "Left    " + ModKit.F(_leftSpeed) + " m/s\n" +
                   "Right   " + ModKit.F(_rightSpeed) + " m/s";
        }
    }

    /// <summary>Distance covered on foot since the overlay was switched on.</summary>
    [PluginToggleable]
    [ModCategory("Overlays")]
    public sealed class DistanceWalkedOverlay : OverlayMod
    {
        private Vector3 _last;
        private bool _hasLast;
        private float _distance;
        private float _started;

        public DistanceWalkedOverlay()
        {
            Name = "Distance Walked";
            CardHeight = 116f;
            FontSize = 19f;
            SortingOrder = 236;
            RefreshInterval = 0.1f;
        }

        protected override void OnOverlayStarted()
        {
            _distance = 0f;
            _hasLast = false;
            _started = Time.realtimeSinceStartup;
        }

        protected override string Build()
        {
            VRRig rig = ModKit.Rig;
            if (rig == null) return "<b>DISTANCE</b>\nwaiting for the local rig";

            Vector3 point = rig.transform.position;
            if (_hasLast)
            {
                float step = Vector3.Distance(point, _last);
                // Ignore respawns and room teleports.
                if (step < 5f) _distance += step;
            }

            _last = point;
            _hasLast = true;

            float elapsed = Mathf.Max(1f, Time.realtimeSinceStartup - _started);
            return "<b>DISTANCE</b>\n" +
                   "Walked   " + ModKit.F(_distance) + " m\n" +
                   "Average  " + ModKit.F(_distance / elapsed, "0.00") + " m/s";
        }
    }

    /// <summary>Jump count alongside the height of the last jump.</summary>
    [PluginToggleable]
    [ModCategory("Overlays")]
    public sealed class JumpCounterOverlay : OverlayMod
    {
        private bool _wasGrounded;
        private bool _armed;
        private float _takeoff;
        private float _peak;
        private int _jumps;
        private float _lastHeight;

        public JumpCounterOverlay()
        {
            Name = "Jump Counter";
            CardHeight = 132f;
            FontSize = 19f;
            SortingOrder = 236;
        }

        protected override string Build()
        {
            GTPlayer player = ModKit.Player;
            if (player == null) return "<b>JUMPS</b>\nwaiting for the local player";

            bool grounded = player.BodyOnGround;
            float height = ModKit.HeadPosition.y;

            if (grounded && !_wasGrounded && _armed)
            {
                if (_peak > 0.35f)
                {
                    _jumps++;
                    _lastHeight = _peak;
                }
                _armed = false;
            }

            if (!grounded && _wasGrounded && !_armed)
            {
                _armed = true;
                _takeoff = height;
                _peak = 0f;
            }

            if (!grounded && _armed)
            {
                _peak = Mathf.Max(_peak, height - _takeoff);
            }

            _wasGrounded = grounded;

            return "<b>JUMPS</b>\n" +
                   "Count    " + _jumps + "\n" +
                   "Last     " + ModKit.F(_lastHeight, "0.00") + " m\n" +
                   "State    " + (grounded ? "grounded" : "airborne");
        }
    }

    // -------------------------------------------------------------------------
    // Session and room readouts
    // -------------------------------------------------------------------------

    /// <summary>Room name, mode, region, population and master client.</summary>
    [PluginToggleable]
    [ModCategory("Overlays")]
    public sealed class RoomInfoOverlay : OverlayMod
    {
        public RoomInfoOverlay()
        {
            Name = "Room Info";
            CardHeight = 186f;
            FontSize = 18f;
            SortingOrder = 236;
            AutoSize = true;
            RefreshInterval = 0.5f;
        }

        protected override string Build()
        {
            NetworkSystem system = NetworkSystem.Instance;
            if (system == null) return "<b>ROOM</b>\nnetwork not ready";
            if (!system.InRoom) return "<b>ROOM</b>\nnot in a room";

            string room = string.IsNullOrEmpty(system.RoomName) ? "-" : system.RoomName;
            string mode = string.IsNullOrEmpty(system.GameModeString) ? "-" : system.GameModeString;
            string region = string.IsNullOrEmpty(system.CurrentRegion) ? "-" : system.CurrentRegion;
            string master = system.MasterClient != null
                ? PlayerActions.SafeName(system.MasterClient)
                : (system.IsMasterClient ? "You" : "-");

            return "<b>ROOM</b>\n" +
                   "Name     " + room + "\n" +
                   "Mode     " + mode + "\n" +
                   "Region   " + region + "\n" +
                   "Players  " + PlayerActions.GetPlayers().Count + "\n" +
                   "Master   " + master;
        }
    }

    /// <summary>Elapsed session time and the local wall clock.</summary>
    [PluginToggleable]
    [ModCategory("Overlays")]
    public sealed class SessionClockOverlay : OverlayMod
    {
        private float _started;

        public SessionClockOverlay()
        {
            Name = "Session Clock";
            CardHeight = 128f;
            FontSize = 19f;
            SortingOrder = 236;
            RefreshInterval = 0.25f;
        }

        protected override void OnOverlayStarted()
        {
            _started = Time.realtimeSinceStartup;
        }

        protected override string Build()
        {
            TimeSpan elapsed = TimeSpan.FromSeconds(Time.realtimeSinceStartup - _started);
            DateTime now = DateTime.Now;

            return "<b>SESSION</b>\n" +
                   "Elapsed  " + ModKit.Clock(elapsed.TotalSeconds) + "\n" +
                   "Local    " + now.ToString("HH:mm:ss") + "\n" +
                   "Date     " + now.ToString("ddd dd MMM");
        }
    }

    /// <summary>Smoothed frame rate and frame time.</summary>
    [PluginToggleable]
    [ModCategory("Overlays")]
    public sealed class FrameTimeOverlay : OverlayMod
    {
        private float _accum;
        private int _frames;
        private float _fps;

        public FrameTimeOverlay()
        {
            Name = "Frame Time";
            CardHeight = 116f;
            FontSize = 19f;
            SortingOrder = 236;
        }

        protected override string Build()
        {
            _accum += Time.unscaledDeltaTime;
            _frames++;
            if (_accum >= 0.25f)
            {
                _fps = _frames / _accum;
                _accum = 0f;
                _frames = 0;
            }

            float milliseconds = _fps > 0.01f ? 1000f / _fps : 0f;
            return "<b>FRAME</b>\n" +
                   "FPS     " + Mathf.RoundToInt(_fps) + "\n" +
                   "Time    " + ModKit.F(milliseconds, "0.0") + " ms";
        }
    }

    /// <summary>Managed memory and garbage collection counts.</summary>
    [PluginToggleable]
    [ModCategory("Overlays")]
    public sealed class MemoryOverlay : OverlayMod
    {
        public MemoryOverlay()
        {
            Name = "Memory";

            CardHeight = 140f;
            FontSize = 19f;
            SortingOrder = 236;
            RefreshInterval = 0.5f;
        }

        protected override string Build()
        {
            float megabytes = GC.GetTotalMemory(false) / 1048576f;
            return "<b>MEMORY</b>\n" +
                   "Managed  " + ModKit.F(megabytes, "0.0") + " MB\n" +
                   "GC 0     " + GC.CollectionCount(0) + "\n" +
                   "GC 1/2   " + GC.CollectionCount(1) + " / " + GC.CollectionCount(2);
        }
    }

    /// <summary>Mod counts with a short list of what is running.</summary>
    [PluginToggleable]
    [ModCategory("Overlays")]
    public sealed class ActiveModsOverlay : OverlayMod
    {
        private readonly List<string> _names = new List<string>(6);

        public ActiveModsOverlay()
        {
            Name = "Active Mods";
            CardHeight = 190f;
            FontSize = 17f;
            SortingOrder = 236;
            AutoSize = true;
            RefreshInterval = 0.3f;
        }

        protected override string Build()
        {
            IReadOnlyList<Mod> all = ModRegistry.DiscoveredMods;
            int enabled = 0;
            int active = 0;
            _names.Clear();

            for (int i = 0; i < all.Count; i++)
            {
                Mod mod = all[i];
                if (mod == null) continue;
                if (mod.Enabled) enabled++;
                if (!mod.IsActive) continue;
                active++;
                if (_names.Count < 7) _names.Add(mod.Name);
            }

            string text = "<b>ACTIVE MODS</b>\n" +
                          "Enabled  " + enabled + " / " + all.Count + "\n" +
                          "Running  " + active;

            if (_names.Count == 0) return text + "\nnothing running";
            return text + "\n" + string.Join("\n", _names.ToArray());
        }
    }

    /// <summary>Every armed mod that has a keyboard bind.</summary>
    [PluginToggleable]
    [ModCategory("Overlays")]
    public sealed class KeybindOverlay : OverlayMod
    {
        private readonly List<string> _lines = new List<string>(8);

        public KeybindOverlay()
        {
            Name = "Keybinds";
            CardWidth = 320f;
            CardHeight = 210f;
            FontSize = 17f;
            SortingOrder = 236;
            AutoSize = true;
            RefreshInterval = 0.5f;
        }

        protected override string Build()
        {
            IReadOnlyList<Mod> all = ModRegistry.DiscoveredMods;
            _lines.Clear();

            for (int i = 0; i < all.Count; i++)
            {
                Mod mod = all[i];
                if (mod == null || !mod.Enabled || mod.Key == Key.None) continue;
                if (_lines.Count >= 9) break;
                _lines.Add(Keybinds.Name(mod.Key) + "   " + mod.Name);
            }

            if (_lines.Count == 0) return "<b>KEYBINDS</b>\nno armed mod has a key";
            return "<b>KEYBINDS</b>\n" + string.Join("\n", _lines.ToArray());
        }
    }

    /// <summary>Whoever is highlighted in the player list or by the gun.</summary>
    [PluginToggleable]
    [ModCategory("Overlays")]
    public sealed class TargetOverlay : OverlayMod
    {
        public TargetOverlay()
        {
            Name = "Target Info";
            CardHeight = 166f;
            FontSize = 18f;
            SortingOrder = 236;
            AutoSize = true;
            RefreshInterval = 0.2f;
        }

        protected override string Build()
        {
            string id = PlayerActions.SelectedUserId;
            if (string.IsNullOrEmpty(id)) return "<b>TARGET</b>\nno player selected";

            NetPlayer player = PlayerActions.FindPlayer(id);
            if (player == null) return "<b>TARGET</b>\nthat player left the room";

            VRRig rig = PlayerActions.FindRigObject(player);
            float distance = rig != null
                ? Vector3.Distance(ModKit.HeadPosition, rig.transform.position)
                : -1f;

            Color color = PlayerActions.GetPlayerColor(player);
            string hex = ColorUtility.ToHtmlStringRGB(color);

            return "<b>TARGET</b>\n" +
                   "Name    " + PlayerActions.SafeName(player) + "\n" +
                   "Distance " + (distance >= 0f ? ModKit.F(distance) + " m" : "-") + "\n" +
                   "Platform " + PlayerActions.SafePlatform(player) + "\n" +
                   "Color   <color=#" + hex + ">########</color> " + hex;
        }
    }

    /// <summary>Count and contents of this room's custom properties.</summary>
    [PluginToggleable]
    [ModCategory("Overlays")]
    public sealed class CustomPropsOverlay : OverlayMod
    {
        public CustomPropsOverlay()
        {
            Name = "Room Props";
            CardWidth = 340f;
            CardHeight = 200f;
            FontSize = 17f;
            SortingOrder = 236;
            AutoSize = true;
            RefreshInterval = 1f;
        }

        protected override string Build()
        {
            List<string> props = PlayerActions.RoomCustomProps();
            if (props == null || props.Count == 0) return "<b>ROOM PROPS</b>\nnone set";

            string text = "<b>ROOM PROPS</b> (" + props.Count + ")";
            for (int i = 0; i < props.Count && i < 10; i++)
            {
                text += "\n" + CascadeMenu.UI.CascadeUI.Truncate(props[i], 34);
            }
            return text;
        }
    }

    // -------------------------------------------------------------------------
    // Player state readouts
    // -------------------------------------------------------------------------

    /// <summary>Water and ground state for the local player.</summary>
    [PluginToggleable]
    [ModCategory("Overlays")]
    public sealed class WaterStateOverlay : OverlayMod
    {
        public WaterStateOverlay()
        {
            Name = "Water State";
            CardHeight = 140f;
            FontSize = 19f;
            SortingOrder = 236;
        }

        protected override string Build()
        {
            GTPlayer player = ModKit.Player;
            if (player == null) return "<b>WATER</b>\nwaiting for the local player";

            return "<b>WATER</b>\n" +
                   "Body      " + (player.InWater ? "in water" : "dry") + "\n" +
                   "Head      " + (player.HeadInWater ? "underwater" : "above") + "\n" +
                   "Vertical  " + ModKit.F(player.InstantaneousVelocity.y, "0.00");
        }
    }

    /// <summary>Ground contact, hand contact and sliding flags.</summary>
    [PluginToggleable]
    [ModCategory("Overlays")]
    public sealed class GroundedOverlay : OverlayMod
    {
        public GroundedOverlay()
        {
            Name = "Ground State";
            CardHeight = 176f;
            FontSize = 18f;
            SortingOrder = 236;
            AutoSize = true;
        }

        protected override string Build()
        {
            GTPlayer player = ModKit.Player;
            if (player == null) return "<b>GROUND</b>\nwaiting for the local player";

            return "<b>GROUND</b>\n" +
                   "Body        " + OnOff(player.BodyOnGround) + "\n" +
                   "Hands       " + OnOff(player.HandContactingSurface) + "\n" +
                   "Hand touch  " + OnOff(player.IsGroundedHand) + "\n" +
                   "Butt touch  " + OnOff(player.IsGroundedButt) + "\n" +
                   "Sliding     " + OnOff(player.IsBodySliding);
        }

        private static string OnOff(bool value)
        {
            return value ? "<color=#58E08A>yes</color>" : "<color=#8A94A6>no</color>";
        }
    }

    // -------------------------------------------------------------------------
    // Technical readouts
    // -------------------------------------------------------------------------

    /// <summary>Desktop keyboard and mouse state.</summary>
    [PluginToggleable]
    [ModCategory("Overlays")]
    public sealed class InputMonitorOverlay : OverlayMod
    {
        public InputMonitorOverlay()
        {
            Name = "Input Monitor";
            CardWidth = 320f;
            CardHeight = 132f;
            FontSize = 18f;
            SortingOrder = 236;
            ShowPanel = false;
        }

        protected override string Build()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return "<b>INPUT</b>\nno keyboard detected";

            string keys = Chip(keyboard.wKey.isPressed, "W") + " " +
                          Chip(keyboard.aKey.isPressed, "A") + " " +
                          Chip(keyboard.sKey.isPressed, "S") + " " +
                          Chip(keyboard.dKey.isPressed, "D");

            string actions = Chip(keyboard.spaceKey.isPressed, "Space") + " " +
                             Chip(keyboard.leftCtrlKey.isPressed, "Ctrl") + " " +
                             Chip(keyboard.leftShiftKey.isPressed, "Shift");

            Mouse mouse = Mouse.current;
            string clicks = mouse == null
                ? "no mouse"
                : Chip(mouse.leftButton.isPressed, "L") + " " +
                  Chip(mouse.rightButton.isPressed, "R") + " " +
                  Chip(mouse.middleButton.isPressed, "M");

            return "<b>INPUT</b>\nMove   " + keys + "\nAction " + actions + "\nMouse  " + clicks;
        }

        private static string Chip(bool pressed, string label)
        {
            return pressed
                ? "<color=#4DB2FF><b>" + label + "</b></color>"
                : "<color=#5A6372>" + label + "</color>";
        }
    }

    /// <summary>Engine time scale and step size.</summary>
    [PluginToggleable]
    [ModCategory("Overlays")]
    public sealed class TimeScaleOverlay : OverlayMod
    {
        public TimeScaleOverlay()
        {
            Name = "Time Scale";
            CardHeight = 140f;
            FontSize = 19f;
            SortingOrder = 236;
        }

        protected override string Build()
        {
            return "<b>TIME</b>\n" +
                   "Scale    " + ModKit.F(Time.timeScale, "0.00") + "\n" +
                   "Fixed dt " + ModKit.F(Time.fixedDeltaTime, "0.000") + "\n" +
                   "Frames   " + Time.frameCount;
        }
    }

    /// <summary>Mod, game, engine and platform versions.</summary>
    [PluginToggleable]
    [ModCategory("Overlays")]
    public sealed class BuildStampOverlay : OverlayMod
    {
        public BuildStampOverlay()
        {
            Name = "Build Stamp";
            CardWidth = 340f;
            CardHeight = 168f;
            FontSize = 17f;
            SortingOrder = 236;
            AutoSize = true;
            RefreshInterval = 2f;
        }

        protected override string Build()
        {
            return "<b>CASCADE</b>\n" +
                   "Mod      v" + PluginInfo.VERSION + "\n" +
                   "Game     " + Application.version + "\n" +
                   "Unity    " + Application.unityVersion + "\n" +
                   "Platform " + Application.platform + "\n" +
                   "Mods     " + ModRegistry.DiscoveredMods.Count;
        }
    }
}
