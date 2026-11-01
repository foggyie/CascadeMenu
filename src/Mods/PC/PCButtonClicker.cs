using System;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CascadeMenu.Mods.PC
{
    /// <summary>
    /// PC button clicker: a left-mouse click in first or third person presses
    /// whatever in-game button the cursor is aimed at.
    ///
    /// The cursor is only unlocked while this mod is on, and the previous
    /// lock/visibility state is restored on disable. Click detection reads both
    /// the raw key state and the Input System, and any failure is logged instead
    /// of throwing (a throw would make the mod host switch it back off).
    /// </summary>
    public class PCButtonClicker : Mod
    {
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private bool _wasDown;
        private CursorLockMode _savedLock;
        private bool _cursorWasVisible;

        public PCButtonClicker()
        {
            Name = "PC Button Clicker";
            isToggle = true;
        }

        public override void OnEnable()
        {
            _wasDown = false;
            _savedLock = Cursor.lockState;
            _cursorWasVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Plugin.Log?.LogInfo("Cascade mod: " + Name + " enabled.");
        }

        public override void OnDisable()
        {
            Cursor.lockState = _savedLock;
            Cursor.visible = _cursorWasVisible;
            _wasDown = false;
            Plugin.Log?.LogInfo("Cascade mod: " + Name + " disabled.");
        }

        public override void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            bool isDown = (GetAsyncKeyState(0x01) & 0x8000) != 0 || mouse.leftButton.isPressed;
            bool clicked = isDown && !_wasDown;
            _wasDown = isDown;
            if (!clicked) return;

            try
            {
                HandleClick(mouse);
            }
            catch (Exception exception)
            {
                Plugin.Log?.LogError("Cascade PC Button Clicker failed: " + exception);
            }
        }

        private void HandleClick(Mouse mouse)
        {
            Camera cam = FindCamera();
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            ray.origin += ray.direction * 0.1f;

            RaycastHit[] hits = Physics.RaycastAll(ray, 1000f, Physics.AllLayers, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null) continue;

                MonoBehaviour[] scripts = hit.collider.GetComponentsInParent<MonoBehaviour>();
                bool isButton = false;
                for (int s = 0; s < scripts.Length; s++)
                {
                    MonoBehaviour script = scripts[s];
                    if (script == null) continue;
                    string typeName = script.GetType().Name;
                    if (typeName.Contains("Button") || typeName.Contains("Pressable"))
                    {
                        isButton = true;
                        break;
                    }
                }

                if (!isButton) continue;

                Press(hit, scripts);
                break;
            }
        }

        private static void Press(RaycastHit hit, MonoBehaviour[] scripts)
        {
            GameObject probe = new GameObject("CascadeClickProbe");
            probe.transform.position = hit.point;

            Collider col = probe.AddComponent<SphereCollider>();
            col.isTrigger = true;
            probe.AddComponent<Rigidbody>().isKinematic = true;

            // Gorilla Tag buttons only act on a collider that carries the hand
            // indicator. Add it when the type is present (resolved by name so
            // this keeps compiling if it moves).
            AddHandIndicator(probe);

            try
            {
                hit.collider.SendMessageUpwards("OnTriggerEnter", col, SendMessageOptions.DontRequireReceiver);

                for (int s = 0; s < scripts.Length; s++)
                {
                    MonoBehaviour script = scripts[s];
                    if (script == null) continue;

                    MethodInfo method = script.GetType().GetMethod(
                        "OnTriggerEnter", BindingFlags.Public | BindingFlags.Instance);
                    if (method != null) method.Invoke(script, new object[] { col });
                }
            }
            catch (Exception exception)
            {
                Plugin.Log?.LogError("Cascade PC Button Clicker press failed: " + exception);
            }
            finally
            {
                UnityEngine.Object.Destroy(probe, 0.1f);
            }
        }

        private static void AddHandIndicator(GameObject probe)
        {
            try
            {
                Type type = FindType("GorillaTriggerColliderHandIndicator");
                if (type != null && typeof(Component).IsAssignableFrom(type))
                    probe.AddComponent(type);
            }
            catch (Exception exception)
            {
                Plugin.Log?.LogWarning("Cascade PC Button Clicker hand indicator failed: " + exception.Message);
            }
        }

        private static Type FindType(string name)
        {
            System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                try
                {
                    Type[] types = assemblies[i].GetTypes();
                    for (int t = 0; t < types.Length; t++)
                    {
                        if (types[t] != null && types[t].Name == name) return types[t];
                    }
                }
                catch
                {
                    // Some assemblies cannot be enumerated; skip them.
                }
            }
            return null;
        }

        /// <summary>
        /// Prefers a non-stereo camera without a render target. Third-person
        /// mods usually expose a camera whose name marks it as such; otherwise
        /// the main camera is used.
        /// </summary>
        private static Camera FindCamera()
        {
            Camera[] cameras = Camera.allCameras;

            for (int i = 0; i < cameras.Length; i++)
            {
                Camera c = cameras[i];
                if (c == null || !c.isActiveAndEnabled) continue;
                if (c.stereoTargetEye != StereoTargetEyeMask.None) continue;
                if (c.targetTexture != null) continue;

                string name = c.name.ToLowerInvariant();
                if (name.Contains("third") || name.Contains("tp") || name.Contains("spectator"))
                    return c;
            }

            for (int i = 0; i < cameras.Length; i++)
            {
                Camera c = cameras[i];
                if (c == null || !c.isActiveAndEnabled) continue;
                if (c.stereoTargetEye != StereoTargetEyeMask.None) continue;
                if (c.targetTexture != null) continue;
                return c;
            }

            return Camera.main;
        }
    }
}
