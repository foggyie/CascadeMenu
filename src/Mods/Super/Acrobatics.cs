using UnityEngine;

namespace CascadeMenu.Mods
{
    /// <summary>
    /// One-switch wrapper around the existing body-rotation controller. The mod
    /// also exposes the controller's smoothing value.
    /// </summary>
    [PluginToggleable]
    [ModCategory("Super")]
    public sealed class Acrobatics : Mod
    {
        private float _smoothing = 5f;

        public Acrobatics()
        {
            Name = "Acrobatics";
            isToggle = true;

            AddSetting(ModSetting.Slider("smoothing", "Smoothing",
                "How tightly the body follows your head",
                1f, 15f,
                () => _smoothing,
                value =>
                {
                    _smoothing = value;
                    ApplySmoothing();
                },
                value => value.ToString("0.0")));
        }

        public override void OnEnable()
        {
            CascadeMenuBehaviour menu = CascadeMenuBehaviour.Instance;
            if (menu != null && menu.Config != null)
            {
                _smoothing = menu.Config.AcrobaticsSmoothing.Value;
            }
            AcrobaticsController.SetActive(true);
            Plugin.Log?.LogInfo("Cascade Acrobatics active.");
        }

        public override void OnDisable()
        {
            AcrobaticsController.SetActive(false);
            Plugin.Log?.LogInfo("Cascade Acrobatics stopped.");
        }

        private void ApplySmoothing()
        {
            CascadeMenuBehaviour menu = CascadeMenuBehaviour.Instance;
            if (menu == null || menu.Config == null) return;
            menu.Config.AcrobaticsSmoothing.Value = Mathf.Max(1f, _smoothing);
        }
    }
}
