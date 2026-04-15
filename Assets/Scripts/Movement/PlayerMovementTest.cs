using FishNet.Object;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class PlayerMovementTest : NetworkBehaviour, IHitReceiver
{
    [Header("Move (Owner Only)")]
    public float maxSpeed = 7f;
    public float accel = 28f;
    public float decel = 40f;
    public float turnAccel = 60f;

    [Header("Aim (Owner Only)")]
    public Camera cam;                    // Owner camera
    public float turnSpeed = 1080f;       // deg/sec
    public float minAngleDelta = 0.35f;   // degrees (กันสั่น)
    public float aimPointSmooth = 0.04f;  // seconds (0.02–0.08)
    public float minAimMove = 0.01f;      // meters (กัน jitter)

    [Header("Knockback")]
    public float knockbackDamping = 18f;  // ยิ่งมากยิ่งหายเร็ว
    public float knockbackMaxSpeed = 20f; // cap

    Rigidbody rb;

    // owner input
    Vector2 moveInput;

    // aim cache (owner)
    bool hasAim;
    Vector3 rawAimPoint;
    Vector3 smoothAimPoint;

    bool hasTargetRot;
    Quaternion targetRot;

    // knockback (owner-sim)
    Vector3 knockbackVel;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (IsOwner && cam == null)
            cam = Camera.main;

        // Non-owner: ให้เป็น kinematic แล้วเลื่อนตาม net state (คุณทำต่อได้ตามระบบ sync ของคุณ)
        if (!IsOwner)
            rb.isKinematic = true;
    }

    void Update()
    {
        if (!IsOwner)
            return;

        moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

        CacheAimPointPlane();
        ComputeTargetRotation();
    }

    void FixedUpdate()
    {
        if (!IsOwner)
            return;

        // decay knockback -> 0
        var t = 1f - Mathf.Exp(-knockbackDamping * Time.fixedDeltaTime);
        knockbackVel = Vector3.Lerp(knockbackVel, Vector3.zero, t);

        DoMovePhysics();
        ApplyAimRotation();
    }

    // ---------------- Hit / Knockback ----------------

    public void ReceiveHit(in HitInfo hit)
    {
        // NOTE: ถ้าคุณทำ server-authoritative จริง ๆ ให้เรียก AddKnockback เฉพาะ owner ผ่าน RPC
        //AddKnockback(hit.Direction, hit.KnockbackImpulse);
    }

    public void AddKnockback(Vector3 dir, float impulse)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        dir.Normalize();

        knockbackVel += dir * impulse;

        var m = knockbackVel.magnitude;
        if (m > knockbackMaxSpeed)
            knockbackVel = (knockbackVel / m) * knockbackMaxSpeed;
    }

    // ---------------- Move ----------------

    void DoMovePhysics()
    {
        var wishDir = new Vector3(moveInput.x, 0f, moveInput.y);
        wishDir = Vector3.ClampMagnitude(wishDir, 1f);

        var v = rb.linearVelocity;
        var vXZ = new Vector3(v.x, 0f, v.z);

        // baseVel = ความเร็วเดินจริง ๆ (ตัดส่วน knockback ออก)
        var baseVel = vXZ - knockbackVel;

        var target = wishDir * maxSpeed;
        var a = (wishDir.sqrMagnitude > 0f) ? accel : decel;

        if (wishDir.sqrMagnitude > 0f && baseVel.sqrMagnitude > 0.01f)
        {
            var dot = Vector3.Dot(baseVel.normalized, wishDir);
            if (dot < 0.0f) a = Mathf.Max(a, turnAccel);
        }

        var maxDelta = a * Time.fixedDeltaTime;
        var newBaseVel = Vector3.MoveTowards(baseVel, target, maxDelta);

        var finalXZ = newBaseVel + knockbackVel;
        rb.linearVelocity = new Vector3(finalXZ.x, v.y, finalXZ.z);
    }

    // ---------------- Aim (Plane-only) ----------------

    bool TryGetMousePointOnPlane(out Vector3 worldPoint)
    {
        worldPoint = default;
        if (!cam) return false;

        var ray = cam.ScreenPointToRay(Input.mousePosition);
        var plane = new Plane(Vector3.up, rb.position);

        if (!plane.Raycast(ray, out var enter))
            return false;

        worldPoint = ray.GetPoint(enter);
        worldPoint.y = rb.position.y;
        return true;
    }

    void CacheAimPointPlane()
    {
        hasAim = false;

        if (!TryGetMousePointOnPlane(out var p))
            return;

        rawAimPoint = p;
        hasAim = true;

        if (smoothAimPoint == default && !hasTargetRot)
        {
            smoothAimPoint = rawAimPoint;
            return;
        }

        if ((rawAimPoint - smoothAimPoint).sqrMagnitude < (minAimMove * minAimMove))
            return;

        var t = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.0001f, aimPointSmooth));
        smoothAimPoint = Vector3.Lerp(smoothAimPoint, rawAimPoint, t);
    }

    void ComputeTargetRotation()
    {
        hasTargetRot = false;
        if (!hasAim) return;

        var dir = smoothAimPoint - rb.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        hasTargetRot = true;
    }

    void ApplyAimRotation()
    {
        if (!hasTargetRot) return;

        var angle = Quaternion.Angle(rb.rotation, targetRot);
        if (angle < minAngleDelta) return;

        var maxDeg = turnSpeed * Time.fixedDeltaTime;
        rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, targetRot, maxDeg));
    }
}