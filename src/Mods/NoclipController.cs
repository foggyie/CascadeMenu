using System;
using GorillaLocomotion;
using UnityEngine;

namespace CascadeMenu.Mods
{
    /// <summary>
    /// Persistent noclip. While enabled the local body and head colliders are
    /// disabled and the player rigidbody stops detecting collisions, so walls,
    /// props and other players can be walked through.
    ///
    /// The controller re-applies the state every frame because respawns and map
    /// loads restore the colliders. Turning the config off restores whatever
    /// state the colliders had before noclip was enabled.
    /// </summary>
    internal sealed class NoclipController : MonoBehaviour
    {
        private static NoclipController _instance;

        private MenuConfig _config;

        private bool _applied;
        private Collider _body;
        private Collider _head;
        private Rigidbody _rigidbody;
        private bool _bodyWasEnabled;
        private bool _headWasEnabled;
        private bool _detectWasEnabled;

        internal static bool IsEnabled
        {
            get { return _instance != null && _instance._config != null && _instance._config.Noclip.Value; }
        }

        internal void Initialize(MenuConfig config)
        {
            _config = config;
            _instance = this;

            // Noclip is dangerous if it leaks across sessions; start disabled.
            if (config != null) config.Noclip.Value = false;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) Destroy(gameObject);
            else _instance = this;
        }

        private void Update()
        {
            if (_config == null) return;

            if (_config.Noclip.Value) Apply();
            else Restore();
        }

        /// <summary>Forces noclip on or off (used by the Movement toggle).</summary>
        internal static bool SetActive(bool active)
        {
            if (_instance == null || _instance._config == null)
                return false;

            _instance._config.Noclip.Value = active;
            if (active) _instance.Apply();
            else _instance.Restore();
            return _instance._config.Noclip.Value;
        }

        internal static bool Toggle()
        {
            if (_instance == null || _instance._config == null)
                return false;
            return SetActive(!_instance._config.Noclip.Value);
        }

        private void Apply()
        {
            GTPlayer player = GTPlayer.Instance;
            if (player == null) return;

            Collider body = player.bodyCollider;
            Collider head = player.headCollider;
            Rigidbody rigidbody = player.playerRigidBody;

            // A respawn replaces the player objects. Restore the old ones before
            // caching the new references so we never leave stale colliders off.
            if (_applied && (body != _body || head != _head || rigidbody != _rigidbody))
                Restore();

            if (!_applied)
            {
                _body = body;
                _head = head;
                _rigidbody = rigidbody;
                _bodyWasEnabled = body != null && body.enabled;
                _headWasEnabled = head != null && head.enabled;
                _detectWasEnabled = rigidbody != null && rigidbody.detectCollisions;
                _applied = true;
            }

            if (_body != null) _body.enabled = false;
            if (_head != null) _head.enabled = false;
            if (_rigidbody != null) _rigidbody.detectCollisions = false;
        }

        private void Restore()
        {
            if (!_applied) return;

            try
            {
                if (_body != null) _body.enabled = _bodyWasEnabled;
                if (_head != null) _head.enabled = _headWasEnabled;
                if (_rigidbody != null) _rigidbody.detectCollisions = _detectWasEnabled;
            }
            catch (Exception)
            {
                // A respawn can destroy the cached objects before this runs.
            }

            _body = null;
            _head = null;
            _rigidbody = null;
            _applied = false;
        }

        private void OnDisable()
        {
            Restore();
            if (_instance == this) _instance = null;
        }

        private void OnDestroy()
        {
            Restore();
            if (_instance == this) _instance = null;
        }
    }
}
