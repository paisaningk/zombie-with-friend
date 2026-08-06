using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Shared attachment-template registry (decision 0016, W3) — same id → same <see cref="AttachmentData"/>
    /// on every peer, mirroring <see cref="WeaponCatalog"/>. A <see cref="WeaponSlot"/>'s mod ids index
    /// this catalog (-1 = empty mod slot). Append new attachments at the END so ids don't shift.
    /// </summary>
    [CreateAssetMenu(menuName = "Weapons/Attachment Catalog", fileName = "AttachmentCatalog")]
    public class AttachmentCatalog : ScriptableObject
    {
        [SerializeField] private AttachmentData[] _attachments;

        public int Count => _attachments != null ? _attachments.Length : 0;

        public AttachmentData Get(int id) =>
            (_attachments != null && id >= 0 && id < _attachments.Length) ? _attachments[id] : null;
    }
}
