using UnityEngine;

namespace Bullet
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class KnockbackReceiver : MonoBehaviour, IHitReceiver
    {
        [SerializeField] private float _maxUp = 0.5f; // ยกขึ้นนิดหน่อย (0 = ไม่เด้งขึ้น)
        [SerializeField] private float _massCompensation = 1f; // ถ้าตัวหนัก/เบาอยากชดเชย

        private Rigidbody _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        public void ReceiveHit(in HitInfo hit)
        {
            // ทิศผลัก: ไปตามทิศกระสุน (หรือจะใช้ (transform.position - shooterPos) ก็ได้)
            Vector3 dir = hit.Direction;
            dir.y = 0f;
            dir.Normalize();

            // เพิ่มแรงขึ้นเล็กน้อยถ้าต้องการ
            Vector3 impulse = dir * hit.KnockbackImpulse + Vector3.up * _maxUp;
            impulse *= _massCompensation;

            // ล้างความเร็วบางส่วนเพื่อให้ผลเด้งชัดขึ้น (เลือกทำ/ไม่ทำ)
            _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);

            // ใส่แรงแบบ impulse
            _rb.AddForce(impulse, ForceMode.Impulse);
        }
    }
}
