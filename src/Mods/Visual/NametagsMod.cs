using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using CascadeMenu.Mods.Core;
using CascadeMenu.Mods.Player;

namespace CascadeMenu.Mods.Visual
{
    /// <summary>
    /// All-in-One overhead visual nametag system ("NameBadges") combining crisp
    /// player names, fur-color matching, through-wall visibility, platform tags,
    /// speaking indicator, distance, infected status, live FPS, Actor #, 0-9 color
    /// code, current map zone, and detected mods / custom properties in one zero-overlap tag.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Visual")]
    public sealed class NametagsMod : Mod
    {
        private bool _throughWalls = true;
        private bool _matchFurColor = true;
        private bool _bold = true;
        private bool _showPlatform = true;
        private bool _showSpeaking = true;
        private bool _showInfected = true;
        private bool _showDistance = false;
        private bool _showFps = true;
        private bool _showActor = true;
        private bool _showColorCode = false;
        private bool _showZone = false;
        private bool _showCustomProps = false;
        private bool _showSelf = false;
        private float _heightOffset = 0.46f;
        private float _scale = 1.0f;

        private readonly Dictionary<VRRig, TextMeshPro> _tags = new Dictionary<VRRig, TextMeshPro>();
        private readonly List<VRRig> _stale = new List<VRRig>();
        private float _nextRefresh;
        private bool _depthDirty;

        public NametagsMod()
        {
            Name = "NameBadges";
            isToggle = true;

            AddSetting(ModSetting.Toggle("throughWalls", "See through walls",
                "Keep nametags visible through walls and map geometry",
                () => _throughWalls, v => { _throughWalls = v; _depthDirty = true; }));

            AddSetting(ModSetting.Toggle("furColor", "Match fur color",
                "Tint each player's name to match their Gorilla fur color",
                () => _matchFurColor, v => _matchFurColor = v));

            AddSetting(ModSetting.Toggle("bold", "Bold text",
                "Render player names in bold for easier reading at distance",
                () => _bold, v => _bold = v));

            AddSetting(ModSetting.Toggle("platform", "Show platform",
                "Display [Steam], [PCVR], or [Quest] next to each name",
                () => _showPlatform, v => _showPlatform = v));

            AddSetting(ModSetting.Toggle("speaking", "Speaking indicator",
                "Show a live green dot when a player is talking",
                () => _showSpeaking, v => _showSpeaking = v));

            AddSetting(ModSetting.Toggle("infected", "Show infected tag",
                "Show a red [TAGGED] badge when a player is infected",
                () => _showInfected, v => _showInfected = v));

            AddSetting(ModSetting.Toggle("distance", "Show distance",
                "Show distance in metres next to the nametag",
                () => _showDistance, v => _showDistance = v));

            AddSetting(ModSetting.Toggle("fps", "Show live FPS",
                "Display color-coded FPS under each player's name",
                () => _showFps, v => _showFps = v));

            AddSetting(ModSetting.Toggle("actor", "Show Actor #",
                "Display each player's Photon Actor Number",
                () => _showActor, v => _showActor = v));

            AddSetting(ModSetting.Toggle("colorCode", "Show color code (0-9)",
                "Display the player's 0-9 Gorilla color code",
                () => _showColorCode, v => _showColorCode = v));

            AddSetting(ModSetting.Toggle("zone", "Show map zone",
                "Display which Gorilla Tag map zone the player is in",
                () => _showZone, v => _showZone = v));

            AddSetting(ModSetting.Toggle("props", "Show mods & custom props",
                "Display detected mods / Photon custom properties below the tag",
                () => _showCustomProps, v => _showCustomProps = v));

            AddSetting(ModSetting.Toggle("self", "Show above yourself",
                "Also render your own badge above your local rig",
                () => _showSelf, v => _showSelf = v));

            AddSetting(ModSetting.Slider("height", "Height offset",
                "Vertical height above each player's head in metres",
                0.25f, 1.2f, () => _heightOffset, v => _heightOffset = v, v => v.ToString("0.00") + " m"));

            AddSetting(ModSetting.Slider("scale", "Nametag scale",
                "Size multiplier for overhead nametags",
                0.5f, 2.0f, () => _scale, v => _scale = v, v => v.ToString("0.00") + "x"));
        }

        public override void OnDisable()
        {
            foreach (var kv in _tags)
            {
                if (kv.Value != null) UnityEngine.Object.Destroy(kv.Value.gameObject);
            }
            _tags.Clear();
        }

        public override void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            bool refreshText = Time.unscaledTime >= _nextRefresh;
            if (refreshText) _nextRefresh = Time.unscaledTime + 0.15f;

            var activeSet = new HashSet<VRRig>();
            List<VRRig> rigs = PlayerActions.GetRemoteRigs();
            for (int i = 0; i < rigs.Count; i++)
            {
                VRRig r = rigs[i];
                if (r != null && r.gameObject != null && r.gameObject.activeInHierarchy)
                    activeSet.Add(r);
            }
            if (_showSelf && VRRig.LocalRig != null && VRRig.LocalRig.gameObject.activeInHierarchy)
                activeSet.Add(VRRig.LocalRig);

            _stale.Clear();
            foreach (var kv in _tags)
            {
                if (kv.Key == null || !kv.Key.gameObject.activeInHierarchy || !activeSet.Contains(kv.Key))
                {
                    if (kv.Value != null) UnityEngine.Object.Destroy(kv.Value.gameObject);
                    _stale.Add(kv.Key);
                }
            }
            for (int i = 0; i < _stale.Count; i++) _tags.Remove(_stale[i]);

            foreach (VRRig rig in activeSet)
            {
                bool created = false;
                if (!_tags.TryGetValue(rig, out TextMeshPro tmp) || tmp == null)
                {
                    var go = new GameObject("CascadeNameBadge");
                    if (Plugin.Root != null) go.transform.SetParent(Plugin.Root.transform, false);
                    tmp = go.AddComponent<TextMeshPro>();
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.fontSize = 2.9f;
                    tmp.richText = true;
                    tmp.textWrappingMode = TextWrappingModes.NoWrap;
                    tmp.outlineWidth = 0.22f;
                    tmp.outlineColor = new Color32(10, 12, 16, 235);
                    _tags[rig] = tmp;
                    created = true;
                }

                if (created || _depthDirty)
                {
                    ApplyDepthMode(tmp, _throughWalls);
                }

                Transform head = rig.headMesh != null ? rig.headMesh.transform : rig.transform;
                float rigScale = Mathf.Max(0.5f, rig.scaleFactor);
                Vector3 pos = head.position + Vector3.up * (_heightOffset * rigScale);
                tmp.transform.position = pos;
                tmp.transform.localScale = Vector3.one * (0.20f * _scale * rigScale);
                Vector3 lookDir = pos - cam.transform.position;
                if (lookDir.sqrMagnitude > 0.0001f)
                    tmp.transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);

                if (refreshText || created)
                {
                    tmp.text = BuildNametag(rig, cam.transform.position);
                }
            }

            _depthDirty = false;
        }

        private static void ApplyDepthMode(TextMeshPro tmp, bool throughWalls)
        {
            if (tmp == null) return;
            try
            {
                Material mat = tmp.fontMaterial;
                if (mat != null)
                {
                    mat.renderQueue = throughWalls ? 4500 : 3000;
                    if (mat.HasProperty("_ZTest"))
                        mat.SetFloat("_ZTest", (float)(throughWalls ? CompareFunction.Always : CompareFunction.LessEqual));
                    if (mat.HasProperty("_ZWrite"))
                        mat.SetFloat("_ZWrite", 0f);
                }
                if (tmp.renderer != null)
                {
                    tmp.renderer.sortingOrder = throughWalls ? 4500 : 0;
                    tmp.renderer.shadowCastingMode = ShadowCastingMode.Off;
                    tmp.renderer.receiveShadows = false;
                }
            }
            catch { }
        }

        private string BuildNametag(VRRig rig, Vector3 camPos)
        {
            NetPlayer player = rig.isLocal ? PlayerActions.LocalPlayer() : rig.Creator;
            string name = player != null && !string.IsNullOrEmpty(player.NickName) ? player.NickName : rig.playerNameVisible;
            if (string.IsNullOrEmpty(name)) name = "Gorilla";

            bool infected = PlayerActions.IsInfected(rig, player);
            Color nameCol = Color.white;
            if (_showInfected && infected)
            {
                nameCol = new Color(1f, 0.36f, 0.24f);
            }
            else if (_matchFurColor)
            {
                Color c = rig.playerColor;
                Color.RGBToHSV(c, out float h, out float s, out float v);
                if (v < 0.45f) v = 0.45f;
                nameCol = Color.Lerp(Color.HSVToRGB(h, s, v), Color.white, 0.18f);
            }
            string hex = ColorUtility.ToHtmlStringRGB(nameCol);

            string speakingPrefix = (_showSpeaking && PlayerActions.IsRigSpeaking(rig))
                ? "<color=#34D399>● </color>"
                : string.Empty;

            string line1 = speakingPrefix + (_bold
                ? "<b><color=#" + hex + ">" + name + "</color></b>"
                : "<color=#" + hex + ">" + name + "</color>");

            if (_showPlatform)
            {
                string plat = DetectPlatform(rig, player);
                if (!string.IsNullOrEmpty(plat))
                {
                    string platHex = plat == "Steam" ? "#60A5FA" : (plat == "PCVR" ? "#38BDF8" : "#A78BFA");
                    line1 += " <size=70%><color=" + platHex + ">[" + plat + "]</color></size>";
                }
            }

            if (_showInfected && infected)
            {
                line1 += " <size=68%><color=#FF5533>[TAGGED]</color></size>";
            }

            if (_showDistance && !rig.isLocal)
            {
                float dist = Vector3.Distance(camPos, rig.transform.position);
                line1 += " <size=68%><color=#CBD5E1>" + Mathf.RoundToInt(dist) + "m</color></size>";
            }

            // Line 2: compact telemetry (Actor #, FPS, Color Code, Zone)
            var subParts = new List<string>(4);
            if (_showActor && player != null && player.ActorNumber > 0)
                subParts.Add("<color=#88CCFF>#" + player.ActorNumber + "</color>");

            if (_showFps)
            {
                int fps = PlayerActions.GetRigFps(rig);
                if (fps >= 0)
                {
                    string fpsCol = fps < 30 ? "#FF5555" : (fps < 60 ? "#FFCC44" : "#55FF88");
                    subParts.Add("<color=" + fpsCol + ">" + fps + " FPS</color>");
                }
            }

            if (_showColorCode)
                subParts.Add("<color=#CBD5E1>Col " + PlayerActions.ColorCode9(rig.playerColor) + "</color>");

            if (_showZone)
            {
                string zone = PlayerActions.GetRigZone(rig);
                if (!string.IsNullOrEmpty(zone) && zone != "Unknown")
                    subParts.Add("<color=#FDE68A>" + zone + "</color>");
            }

            string result = line1;
            if (subParts.Count > 0)
            {
                result += "\n<size=72%>" + string.Join(" · ", subParts) + "</size>";
            }

            // Line 3: Custom Properties / Mods
            if (_showCustomProps && player != null)
            {
                string props = PlayerActions.PlayerCustomPropsSummary(player, 2);
                if (!string.IsNullOrEmpty(props) && props != "None")
                {
                    result += "\n<size=64%><color=#93C5FD>" + props + "</color></size>";
                }
            }

            return result;
        }

        private static System.Reflection.FieldInfo _cosmeticStrField;
        private static bool _cosmeticFieldChecked;

        private static string DetectPlatform(VRRig rig, NetPlayer player)
        {
            try
            {
                if (!_cosmeticFieldChecked)
                {
                    _cosmeticFieldChecked = true;
                    const System.Reflection.BindingFlags flags =
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
                    _cosmeticStrField = typeof(VRRig).GetField("rawCosmeticString", flags)
                        ?? typeof(VRRig).GetField("concatStringOfCosmeticsAllowed", flags);
                }

                string cosmetics = _cosmeticStrField != null ? _cosmeticStrField.GetValue(rig) as string : null;
                if (!string.IsNullOrEmpty(cosmetics))
                {
                    if (cosmetics.IndexOf("S. FIRST LOGIN", StringComparison.OrdinalIgnoreCase) >= 0)
                        return "Steam";
                    if (cosmetics.IndexOf("FIRST LOGIN", StringComparison.OrdinalIgnoreCase) >= 0)
                        return "PCVR";
                }
            }
            catch { }

            try
            {
                if (player != null && PlayerActions.PlayerCustomPropsSummary(player, 1) != "None")
                {
                    return "Steam";
                }
            }
            catch { }

            return "Quest";
        }
    }
}
