using UnityEngine;
using CascadeMenu.Menu;
using CascadeMenu.Mods.Core;
using CascadeMenu.Mods.Player;
using CascadeMenu.Mods.Tools;
using CascadeMenu.Mods.Cosmetics;
using CascadeMenu.Mods.Visual;
using CascadeMenu.Mods.Movement;
using CascadeMenu.Mods.Overlays;

namespace CascadeMenu.Mods.Util
{
    /// <summary>
    /// Cached shader lookups to avoid repeated Find calls with identical fallback chains.
    /// </summary>
    internal static class ShaderLookup
    {
        private static Shader _uiUnlit;
        private static Shader _spriteUnlit;

        /// <summary>
        /// Finds the first available shader from the given fallback chain.
        /// Results are not cached when using this method directly.
        /// </summary>
        public static Shader Find(params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                Shader shader = Shader.Find(names[i]);
                if (shader != null) return shader;
            }
            return null;
        }

        /// <summary>
        /// Standard UI unlit shader: UI/Default → Sprites/Default → Unlit/Color.
        /// </summary>
        public static Shader UiUnlit()
        {
            if (_uiUnlit == null)
            {
                _uiUnlit = Find("UI/Default", "Sprites/Default", "Unlit/Color");
            }
            return _uiUnlit;
        }

        /// <summary>
        /// Standard sprite unlit shader: Sprites/Default → UI/Default → Unlit/Color.
        /// </summary>
        public static Shader SpriteUnlit()
        {
            if (_spriteUnlit == null)
            {
                _spriteUnlit = Find("Sprites/Default", "UI/Default", "Unlit/Color");
            }
            return _spriteUnlit;
        }
    }
}
