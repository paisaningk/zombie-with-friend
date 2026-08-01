using Game;
using UnityEngine;

namespace GameUI
{
    /// <summary>
    /// The single owner of the hardware cursor (decision 0014). Client-side plain scene object in the
    /// game scene — the cursor is a per-client global, so exactly one of these decides its state and
    /// is the ONLY code that writes <see cref="Cursor"/>. Everyone else (PlayerLook, StagingController)
    /// stops touching it, which kills the last-writer-wins fights that appear once pause/menus arrive.
    ///
    /// Cursor state is DERIVED from a single truth, not commanded: locked/hidden only while the match
    /// is actively played (<see cref="GameState.Playing"/>) and not paused; free otherwise (Lobby
    /// staging, Won/Lost result screen). A future pause menu flips <see cref="SetPaused"/> — it sets
    /// the truth and lets <see cref="Apply"/> decide, rather than poking the cursor directly.
    /// </summary>
    public class CursorController : MonoBehaviour
    {
        private GameManager _gm;
        private bool _subscribed;
        private bool _paused; // reserved for a future pause menu (SetPaused)

        private void Update()
        {
            // GameManager (a scene NetworkObject) may initialize a frame or two after us — poll until
            // it exists, subscribe once, then apply the current state (its initial fire may be missed).
            // Same pattern as StagingController.
            if (_subscribed) return;
            if (GameManager.Instance == null) return;

            _gm = GameManager.Instance;
            _gm.OnGameStateChanged += HandleGameState;
            _subscribed = true;
            Apply();
        }

        private void OnDestroy()
        {
            if (_subscribed && _gm != null)
                _gm.OnGameStateChanged -= HandleGameState;
        }

        private void HandleGameState(GameState prev, GameState next) => Apply();

        /// <summary>A future pause menu calls this — it sets the truth; Apply decides the cursor.</summary>
        public void SetPaused(bool paused)
        {
            _paused = paused;
            Apply();
        }

        // ---- the single decision + the single writer ----

        private void Apply()
        {
            bool gameplay = _gm != null && _gm.State == GameState.Playing && !_paused;
            if (gameplay) Hide(); else Show();
        }

        private static void Show()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private static void Hide()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
