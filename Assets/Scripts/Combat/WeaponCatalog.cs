using UnityEngine;

namespace Combat
{
    /// <summary>
    /// The shared weapon-template registry (decision 0016, Q12). A <see cref="WeaponSlot"/> syncs a small
    /// int <c>weaponId</c>; every machine resolves it to the same <see cref="WeaponData"/> through this
    /// catalog asset — <c>id = array index</c>. ScriptableObject instances can't cross the network, so we
    /// sync the index and look it up locally. The same catalog asset must be referenced on every peer
    /// (it is: one shared asset on the Player prefab), so ids line up (mirrors FishNet's prefab-id model).
    /// </summary>
    [CreateAssetMenu(menuName = "Weapons/Weapon Catalog", fileName = "WeaponCatalog")]
    public class WeaponCatalog : ScriptableObject
    {
        [Tooltip("Weapons by id. id = index in this array. Order is the network contract — insert new " +
                 "weapons at the END so existing ids don't shift.")]
        [SerializeField] private WeaponData[] _weapons;

        public int Count => _weapons != null ? _weapons.Length : 0;

        /// <summary>Resolve an id to its template, or null if out of range (-1 / empty slot).</summary>
        public WeaponData Get(int id) =>
            (_weapons != null && id >= 0 && id < _weapons.Length) ? _weapons[id] : null;
    }
}
