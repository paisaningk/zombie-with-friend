using Obvious.Soap;
using UnityEngine;

namespace GameUI
{
    /// <summary>
    /// Tiny SOAP-style binding SOAP itself doesn't ship: drives a GameObject's active state from a
    /// <see cref="BoolVariable"/>. Used by the HUD to show the ability-cooldown widget only for the
    /// Support class (bound to <c>Hud_IsSupport</c>) — decision 0015.
    /// </summary>
    public class BindActiveToBool : MonoBehaviour
    {
        [SerializeField] private BoolVariable _variable;
        [Tooltip("Invert: active when the variable is FALSE.")]
        [SerializeField] private bool _invert = false;
        [Tooltip("GameObject to toggle. Defaults to this one if left empty.")]
        [SerializeField] private GameObject _target;

        private void Awake()
        {
            if (_target == null) _target = gameObject;
        }

        private void OnEnable()
        {
            if (_variable == null) return;
            _variable.OnValueChanged += Apply;
            Apply(_variable.Value);
        }

        private void OnDisable()
        {
            if (_variable != null) _variable.OnValueChanged -= Apply;
        }

        private void Apply(bool value)
        {
            if (_target != null) _target.SetActive(_invert ? !value : value);
        }
    }
}
