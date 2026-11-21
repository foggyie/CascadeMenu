using System;
using System.Collections.Generic;
using CascadeMenu.UI;
using GorillaLocomotion;
using GorillaNetworking;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CascadeMenu.Mods
{
    // -------------------------------------------------------------------------
    // World and rig readouts (each with its own unique HUD design & color)
    // -------------------------------------------------------------------------

    /// <summary>Live world position of the local head.</summary>
    [PluginToggleable]
    [ModCategory("Overlays")]
    public sealed class CoordinatesOverlay : OverlayMod
    {
        public CoordinatesOverlay()
        {
            Name = "Coordinates";
            DefaultAnchor = HudAnchor.MidLeft;
            DefaultSlot = 0;
            Design = HudDesign.WindowCard;
            AccentColor = new Color(0.28f, 0.90f, 1f, 1f);
            BadgeIcon = CascadeIcon.Recenter;
            CardWidth = 310f;
            CardHeight = 132f;
            FontSize = 18.5f;
            SortingOrder = 236;
            RefreshInterval = 0.1f;
        }

        protected override string Build()
        {
            Transform head = ModKit.Head;
            if (head == null) return "<color=#47E5FF><b>GPS TELEMETRY</b></color>\nwaiting for local rig";

            Vector3 point = head.position;
            return "<color=#47E5FF><b>GPS TELEMETRY</b></color>\n" +
                   "<color=#8E9BAE>X</color>   " + ModKit.F(point.x) + " m\n" +
                   "<color=#8E9BAE>Y</color>   " + ModKit.F(point.y) + " m\n" +
                   "<color=#8E9BAE>Z</color>   " + ModKit.F(point.z) + " m";
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
            DefaultAnchor = HudAnchor.MidLeft;
            DefaultSlot = 1;
            Design = HudDesign.GaugeWindow;
            AccentColor = new Color(0.32f, 1f, 0.62f, 1f);
            BadgeIcon = CascadeIcon.ArrowUp;
            CardWidth = 315f;
            CardHeight = 148f;
            FontSize = 18.5f;
            SortingOrder = 236;
            RefreshInterval = 0.2f;
        }

        protected override string Build()
        {
            Transform head = ModKit.Head;
            if (head == null) return "<color=#52FF9E><b>ALTIMETER</b></color>\nwaiting for local rig";

            if (Time.unscaledTime >= _nextCheck)
            {
                _nextCheck = Time.unscaledTime + 0.25f;
                _groundDistance = MeasureGround(head.position);
            }

            float altitude = head.position.y;
            GaugeValue = Mathf.Clamp01((_groundDistance >= 0f ? _groundDistance : Mathf.Max(0f, altitude)) / 40f);

            return "<color=#52FF9E><b>ALTIMETER</b></color>\n" +
                   "<color=#8E9BAE>Clearance</color>  " + (_groundDistance >= 0f ? ModKit.F(_groundDistance) + " m" : "-") + "\n" +
                   "<color=#8E9BAE>Elevation</color>  " + ModKit.F(altitude) + " m";
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
            DefaultAnchor = HudAnchor.MidLeft;
            DefaultSlot = 2;
            Design = HudDesign.WindowCard;
            AccentColor = new Color(1f, 0.72f, 0.24f, 1f);
            BadgeIcon = CascadeIcon.Recenter;
            CardWidth = 310f;
            CardHeight = 132f;
            FontSize = 18.5f;
            SortingOrder = 236;
            RefreshInterval = 0.1f;
        }

        protected override string Build()
        {
            Transform head = ModKit.Head;
            if (head == null) return "<color=#FFB83D><b>GYRO HORIZON</b></color>\nwaiting for local rig";

            Vector3 euler = head.eulerAngles;
            float heading = Mathf.Repeat(euler.y, 360f);
            float pitch = Mathf.DeltaAngle(0f, euler.x);
            float roll = Mathf.DeltaAngle(0f, euler.z);

            return "<color=#FFB83D><b>GYRO HORIZON</b></color>\n" +
                   "<color=#8E9BAE>Yaw</color>    " + ModKit.F(heading, "0") + "°\n" +
                   "<color=#8E9BAE>Pitch</color>  " + ModKit.F(pitch, "0") + "°   <color=#8E9BAE>Roll</color> " + ModKit.F(roll, "0") + "°";
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
            DefaultAnchor = HudAnchor.MidRight;
            DefaultSlot = 0;
            Design = HudDesign.GaugeWindow;
            AccentColor = new Color(0.25f, 0.85f, 1f, 1f);
            BadgeIcon = CascadeIcon.Bolt;
            CardWidth = 320f;
            CardHeight = 148f;
            FontSize = 18.5f;
            SortingOrder = 236;
        }

        protected override string Build()
        {
            GTPlayer player = ModKit.Player;
            if (player == null) return "<color=#40D9FF><b>KINETIC VECTOR</b></color>\nwaiting for local player";

            Vector3 velocity = player.InstantaneousVelocity;
            float horizontal = new Vector2(velocity.x, velocity.z).magnitude;
            float speed = velocity.magnitude;
            GaugeValue = Mathf.Clamp01(speed / 16f);

            return "<color=#40D9FF><b>KINETIC VECTOR</b></color>  <b>" + ModKit.F(speed, "0.0") + " m/s</b>\n" +
                   "<color=#8E9BAE>Horizontal</color>  " + ModKit.F(horizontal, "0.00") + " m/s\n" +
                   "<color=#8E9BAE>Vertical</color>    " + ModKit.F(velocity.y, "0.00") + " m/s";
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
            DefaultAnchor = HudAnchor.MidRight;
            DefaultSlot = 1;
            Design = HudDesign.Capsule;
            AccentColor = new Color(0.95f, 0.42f, 0.88f, 1f);
            BadgeIcon = CascadeIcon.Bolt;
            CardWidth = 320f;
            CardHeight = 104f;
            FontSize = 18f;
            SortingOrder = 236;
        }

        protected override string Build()
        {
            GTPlayer player = ModKit.Player;
            if (player == null) return "<color=#F26BE0><b>SWING METER</b></color>\nwaiting for player";

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

            return "<color=#F26BE0><b>SWING METER</b></color>\n" +
                   "L <b>" + ModKit.F(_leftSpeed) + "</b> m/s  ·  R <b>" + ModKit.F(_rightSpeed) + "</b> m/s";
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
            DefaultAnchor = HudAnchor.MidRight;
            DefaultSlot = 2;
            Design = HudDesign.Capsule;
            AccentColor = new Color(0.38f, 0.96f, 0.65f, 1f);
            BadgeIcon = CascadeIcon.Person;
            CardWidth = 330f;
            CardHeight = 104f;
            FontSize = 18f;
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
            if (rig == null) return "<color=#61F5A6><b>ODOMETER</b></color>\nwaiting for local rig";

            Vector3 point = rig.transform.position;
            if (_hasLast)
            {
                float step = Vector3.Distance(point, _last);
                if (step < 5f) _distance += step;
            }

            _last = point;
            _hasLast = true;

            float elapsed = Mathf.Max(1f, Time.realtimeSinceStartup - _started);
            return "<color=#61F5A6><b>ODOMETER</b></color>   <b>" + ModKit.F(_distance) + " m</b>\n" +
                   "<color=#8E9BAE>Avg pace</color>  " + ModKit.F(_distance / elapsed, "0.00") + " m/s";
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
            DefaultAnchor = HudAnchor.BottomRight;
            DefaultSlot = 0;
            Design = HudDesign.Capsule;
            AccentColor = new Color(1f, 0.64f, 0.25f, 1f);
            BadgeIcon = CascadeIcon.ArrowUp;
            CardWidth = 330f;
            CardHeight = 112f;
            FontSize = 18f;
            SortingOrder = 236;
        }

        protected override string Build()
        {
            GTPlayer player = ModKit.Player;
            if (player == null) return "<color=#FFA340><b>APEX TRACKER</b></color>\nwaiting for local player";

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

            return "<color=#FFA340><b>APEX TRACKER</b></color>  #" + _jumps + "\n" +
                   "Peak <b>" + ModKit.F(_lastHeight, "0.00") + " m</b> · " +
                   (grounded ? "<color=#58E08A>GROUND</color>" : "<color=#47E5FF>AIRBORNE</color>");
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
            DefaultAnchor = HudAnchor.TopCenter;
            DefaultSlot = 1;
            Design = HudDesign.TerminalWindow;
            BadgeTitle = "LOBBY TELEMETRY";
            AccentColor = new Color(0.35f, 0.85f, 1f, 1f);
            CardWidth = 330f;
            CardHeight = 196f;
            FontSize = 17.5f;
            SortingOrder = 236;
            AutoSize = true;
            RefreshInterval = 0.5f;
        }

        protected override string Build()
        {
            NetworkSystem system = NetworkSystem.Instance;
            if (system == null) return "network not ready";
            if (!system.InRoom) return "<color=#8E9BAE>status:</color> offline / solo";

            string room = string.IsNullOrEmpty(system.RoomName) ? "-" : system.RoomName;
            string mode = string.IsNullOrEmpty(system.GameModeString) ? "-" : system.GameModeString;
            string region = string.IsNullOrEmpty(system.CurrentRegion) ? "-" : system.CurrentRegion;
            string master = system.MasterClient != null
                ? PlayerActions.SafeName(system.MasterClient)
                : (system.IsMasterClient ? "You" : "-");

            return "<color=#8E9BAE>Code</color>     <b>" + room + "</b>  (" + region + ")\n" +
                   "<color=#8E9BAE>Mode</color>     " + mode + "\n" +
                   "<color=#8E9BAE>Players</color>  " + (PlayerActions.GetPlayers().Count + 1) + "\n" +
                   "<color=#8E9BAE>Host</color>     " + master;
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
            DefaultAnchor = HudAnchor.BottomLeft;
            DefaultSlot = 1;
            Design = HudDesign.KeyCap;
            AccentColor = new Color(0.55f, 0.78f, 1f, 1f);
            CardWidth = 315f;
            CardHeight = 112f;
            FontSize = 18.5f;
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

            return "<color=#8CC7FF><b>CHRONO</b></color>  " + now.ToString("HH:mm:ss") + "\n" +
                   "<color=#8E9BAE>Uptime</color> " + ModKit.Clock(elapsed.TotalSeconds) + " · " + now.ToString("ddd dd MMM");
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
            DefaultAnchor = HudAnchor.BottomRight;
            DefaultSlot = 1;
            Design = HudDesign.GaugeWindow;
            AccentColor = new Color(0.35f, 1f, 0.58f, 1f);
            CardWidth = 300f;
            CardHeight = 128f;
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
            GaugeValue = Mathf.Clamp01(_fps / 120f);

            return "<color=#59FF94><b>FRAME PACING</b></color>\n" +
                   "<b>" + Mathf.RoundToInt(_fps) + " FPS</b>   ·   " + ModKit.F(milliseconds, "0.0") + " ms";
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
            DefaultAnchor = HudAnchor.BottomRight;
            DefaultSlot = 2;
            Design = HudDesign.GaugeWindow;
            AccentColor = new Color(0.75f, 0.52f, 1f, 1f);
            CardWidth = 310f;
            CardHeight = 148f;
            FontSize = 18.5f;
            SortingOrder = 236;
            RefreshInterval = 0.5f;
        }

        protected override string Build()
        {
            float megabytes = GC.GetTotalMemory(false) / 1048576f;
            GaugeValue = Mathf.Clamp01(megabytes / 1024f);

            return "<color=#BF85FF><b>HEAP MONITOR</b></color>  <b>" + ModKit.F(megabytes, "0.0") + " MB</b>\n" +
                   "<color=#8E9BAE>Gen 0</color>  " + GC.CollectionCount(0) +
                   "   <color=#8E9BAE>Gen 1/2</color>  " + GC.CollectionCount(1) + "/" + GC.CollectionCount(2);
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
            DefaultAnchor = HudAnchor.TopRight;
            DefaultSlot = 2;
            Design = HudDesign.TerminalWindow;
            BadgeTitle = "ACTIVE ARRAYLIST";
            AccentColor = new Color(0.36f, 0.92f, 0.68f, 1f);
            CardWidth = 315f;
            CardHeight = 200f;
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
                if (_names.Count < 7) _names.Add("• " + mod.Name);
            }

            string text = "<color=#8E9BAE>Running</color> <b>" + active + "</b>  <color=#8E9BAE>Armed</color> " + enabled + "/" + all.Count;
            if (_names.Count == 0) return text + "\n<color=#647082>no active modules</color>";
            return text + "\n" + string.Join("\n", _names);
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
            DefaultAnchor = HudAnchor.TopRight;
            DefaultSlot = 3;
            Design = HudDesign.TerminalWindow;
            BadgeTitle = "HOTKEY MATRIX";
            AccentColor = new Color(1f, 0.78f, 0.32f, 1f);
            CardWidth = 325f;
            CardHeight = 215f;
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
                _lines.Add("<color=#FFC752>[" + Keybinds.Name(mod.Key) + "]</color>  " + mod.Name);
            }

            if (_lines.Count == 0) return "<color=#647082>no armed mod has a hotkey</color>";
            return string.Join("\n", _lines);
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
            DefaultAnchor = HudAnchor.TopCenter;
            DefaultSlot = 2;
            Design = HudDesign.WindowCard;
            AccentColor = new Color(1f, 0.36f, 0.36f, 1f);
            CardWidth = 325f;
            CardHeight = 168f;
            FontSize = 17.5f;
            SortingOrder = 236;
            AutoSize = true;
            RefreshInterval = 0.2f;
        }

        protected override string Build()
        {
            string id = PlayerActions.SelectedUserId;
            if (string.IsNullOrEmpty(id)) return "<color=#FF5C5C><b>TARGET LOCK</b></color>\nno target locked";

            NetPlayer player = PlayerActions.FindPlayer(id);
            if (player == null) return "<color=#FF5C5C><b>TARGET LOCK</b></color>\ntarget left the room";

            VRRig rig = PlayerActions.FindRigObject(player);
            float distance = rig != null
                ? Vector3.Distance(ModKit.HeadPosition, rig.transform.position)
                : -1f;

            bool infected = PlayerActions.IsInfected(rig, player);
            Color color = PlayerActions.GetPlayerColor(player);
            string hex = ColorUtility.ToHtmlStringRGB(color);

            return "<color=#FF5C5C><b>TARGET LOCK</b></color>  " +
                   (infected ? "<color=#FF3B1F>[INFECTED]</color>" : "<color=#58E08A>[SURVIVOR]</color>") + "\n" +
                   "<b>" + PlayerActions.SafeName(player) + "</b>\n" +
                   "<color=#8E9BAE>Range</color> " + (distance >= 0f ? ModKit.F(distance) + " m" : "-") +
                   " · " + PlayerActions.SafePlatform(player) + "\n" +
                   "<color=#" + hex + ">■■■</color> #" + hex;
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
            DefaultAnchor = HudAnchor.TopLeft;
            DefaultSlot = 3;
            Design = HudDesign.TerminalWindow;
            BadgeTitle = "PHOTON ROOM PROPS";
            AccentColor = new Color(0.45f, 0.85f, 1f, 1f);
            CardWidth = 340f;
            CardHeight = 205f;
            FontSize = 16.5f;
            SortingOrder = 236;
            AutoSize = true;
            RefreshInterval = 1f;
        }

        protected override string Build()
        {
            List<string> props = PlayerActions.RoomCustomProps();
            if (props == null || props.Count == 0) return "<color=#647082>no custom properties</color>";

            string text = "<color=#8E9BAE>Count:</color> " + props.Count;
            for (int i = 0; i < props.Count && i < 10; i++)
            {
                text += "\n" + CascadeUI.Truncate(props[i], 34);
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
            DefaultAnchor = HudAnchor.BottomLeft;
            DefaultSlot = 2;
            Design = HudDesign.Capsule;
            AccentColor = new Color(0.25f, 0.78f, 1f, 1f);
            BadgeIcon = CascadeIcon.Sparkle;
            CardWidth = 320f;
            CardHeight = 112f;
            FontSize = 18f;
            SortingOrder = 236;
        }

        protected override string Build()
        {
            GTPlayer player = ModKit.Player;
            if (player == null) return "<color=#40C7FF><b>HYDRO SENSOR</b></color>\nwaiting for local player";

            return "<color=#40C7FF><b>HYDRO SENSOR</b></color>\n" +
                   "Body: <b>" + (player.InWater ? "Submerged" : "Dry") + "</b> · Head: <b>" +
                   (player.HeadInWater ? "Underwater" : "Clear") + "</b>";
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
            DefaultAnchor = HudAnchor.TopCenter;
            DefaultSlot = 3;
            Design = HudDesign.WindowCard;
            AccentColor = new Color(0.38f, 0.95f, 0.58f, 1f);
            CardWidth = 315f;
            CardHeight = 166f;
            FontSize = 17.5f;
            SortingOrder = 236;
            AutoSize = true;
        }

        protected override string Build()
        {
            GTPlayer player = ModKit.Player;
            if (player == null) return "<color=#61F294><b>SURFACE CONTACT</b></color>\nwaiting for local player";

            return "<color=#61F294><b>SURFACE CONTACT</b></color>\n" +
                   "Body     " + OnOff(player.BodyOnGround) + "   Hands  " + OnOff(player.HandContactingSurface) + "\n" +
                   "HandPad  " + OnOff(player.IsGroundedHand) + "   Sliding " + OnOff(player.IsBodySliding);
        }

        private static string OnOff(bool value)
        {
            return value ? "<color=#58E08A><b>ON</b></color>" : "<color=#647082>OFF</color>";
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
            DefaultAnchor = HudAnchor.BottomCenter;
            DefaultSlot = 1;
            Design = HudDesign.KeyCap;
            AccentColor = new Color(0.35f, 0.85f, 1f, 1f);
            CardWidth = 330f;
            CardHeight = 126f;
            FontSize = 17.5f;
            SortingOrder = 236;
        }

        protected override string Build()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return "<color=#59D9FF><b>KEY STREAM</b></color>\nno keyboard detected";

            string keys = Chip(keyboard.wKey.isPressed, "W") + " " +
                          Chip(keyboard.aKey.isPressed, "A") + " " +
                          Chip(keyboard.sKey.isPressed, "S") + " " +
                          Chip(keyboard.dKey.isPressed, "D");

            string actions = Chip(keyboard.spaceKey.isPressed, "SPC") + " " +
                             Chip(keyboard.leftCtrlKey.isPressed, "CTL") + " " +
                             Chip(keyboard.leftShiftKey.isPressed, "SHF");

            Mouse mouse = Mouse.current;
            string clicks = mouse == null
                ? "-"
                : Chip(mouse.leftButton.isPressed, "LMB") + " " +
                  Chip(mouse.rightButton.isPressed, "RMB") + " " +
                  Chip(mouse.middleButton.isPressed, "MMB");

            return "<color=#59D9FF><b>KEY STREAM</b></color>   " + keys + "\n" +
                   actions + "  ·  " + clicks;
        }

        private static string Chip(bool pressed, string label)
        {
            return pressed
                ? "<color=#4DB2FF><b>[" + label + "]</b></color>"
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
            DefaultAnchor = HudAnchor.BottomCenter;
            DefaultSlot = 2;
            Design = HudDesign.KeyCap;
            AccentColor = new Color(1f, 0.65f, 0.32f, 1f);
            CardWidth = 315f;
            CardHeight = 112f;
            FontSize = 18.5f;
            SortingOrder = 236;
        }

        protected override string Build()
        {
            return "<color=#FFA652><b>ENGINE CLOCK</b></color>  <b>" + ModKit.F(Time.timeScale, "0.00") + "x</b>\n" +
                   "<color=#8E9BAE>Fixed dt</color> " + ModKit.F(Time.fixedDeltaTime, "0.000") + "s · <color=#8E9BAE>Frame</color> " + Time.frameCount;
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
            DefaultAnchor = HudAnchor.BottomCenter;
            DefaultSlot = 3;
            Design = HudDesign.KeyCap;
            AccentColor = new Color(0.45f, 0.95f, 0.78f, 1f);
            CardWidth = 345f;
            CardHeight = 128f;
            FontSize = 17f;
            SortingOrder = 236;
            AutoSize = true;
            RefreshInterval = 2f;
        }

        protected override string Build()
        {
            return "<color=#73F2C7><b>CASCADE v" + PluginInfo.VERSION + "</b></color>  ·  " + ModRegistry.DiscoveredMods.Count + " mods\n" +
                   "GT " + Application.version + " · Unity " + Application.unityVersion;
        }
    }
}
