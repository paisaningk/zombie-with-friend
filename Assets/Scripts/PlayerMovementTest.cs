using FishNet.Object;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class PlayerMovementTest : NetworkBehaviour
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

    Rigidbody rb;

    // owner input
    Vector2 moveInput;

    // aim cache (owner)
    bool hasAim;
    Vector3 rawAimPoint;
    Vector3 smoothAimPoint;

    bool hasTargetRot;
    Quaternion targetRot;

    // remote state
    Vector3 netPos;
    Quaternion netRot;
    bool netInitialized;

    float nextSendTime;

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

        // Non-owner: ให้เป็น kinematic แล้วเลื่อนตาม net state (ลดฟิสิกส์ตีกัน)
        if (!IsOwner)
            rb.isKinematic = true;
    }

    void Update()
    {
        if (IsOwner)
        {
            // input
            moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

            // aim
            CacheAimPointPlane();
            ComputeTargetRotation();
        }
    }

    void FixedUpdate()
    {
        if (!IsOwner)
            return;

        DoMovePhysics();
        ApplyAimRotation();
    }

    // ---------------- Move ----------------

    void DoMovePhysics()
    {
        Vector3 wishDir = new Vector3(moveInput.x, 0f, moveInput.y);
        wishDir = Vector3.ClampMagnitude(wishDir, 1f);

        Vector3 v = rb.linearVelocity;
        Vector3 vXZ = new Vector3(v.x, 0f, v.z);

        Vector3 target = wishDir * maxSpeed;
        float a = (wishDir.sqrMagnitude > 0f) ? accel : decel;

        if (wishDir.sqrMagnitude > 0f && vXZ.sqrMagnitude > 0.01f)
        {
            float dot = Vector3.Dot(vXZ.normalized, wishDir);
            if (dot < 0.0f) a = Mathf.Max(a, turnAccel);
        }

        float maxDelta = a * Time.fixedDeltaTime;
        Vector3 newVXZ = Vector3.MoveTowards(vXZ, target, maxDelta);

        rb.linearVelocity = new Vector3(newVXZ.x, v.y, newVXZ.z);
    }

    // ---------------- Aim (Plane-only) ----------------

    bool TryGetMousePointOnPlane(out Vector3 worldPoint)
    {
        worldPoint = default;
        if (!cam) return false;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        // plane ผ่านตำแหน่งตัวละคร: เสถียรสุดสำหรับ top-down
        Plane plane = new Plane(Vector3.up, rb.position);

        if (!plane.Raycast(ray, out float enter))
            return false;

        worldPoint = ray.GetPoint(enter);
        worldPoint.y = rb.position.y; // lock y ให้ตรงตัวละคร
        return true;
    }

    void CacheAimPointPlane()
    {
        hasAim = false;

        if (!TryGetMousePointOnPlane(out Vector3 p))
            return;

        rawAimPoint = p;
        hasAim = true;

        // smooth aimpoint (กันสั่น)
        if (smoothAimPoint == default && !hasTargetRot) // ครั้งแรก
        {
            smoothAimPoint = rawAimPoint;
            return;
        }

        if ((rawAimPoint - smoothAimPoint).sqrMagnitude < (minAimMove * minAimMove))
            return;

        float t = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.0001f, aimPointSmooth));
        smoothAimPoint = Vector3.Lerp(smoothAimPoint, rawAimPoint, t);
    }

    void ComputeTargetRotation()
    {
        hasTargetRot = false;
        if (!hasAim) return;

        Vector3 dir = smoothAimPoint - rb.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        hasTargetRot = true;
    }

    void ApplyAimRotation()
    {
        if (!hasTargetRot) return;

        float angle = Quaternion.Angle(rb.rotation, targetRot);
        if (angle < minAngleDelta) return;

        float maxDeg = turnSpeed * Time.fixedDeltaTime;
        rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, targetRot, maxDeg));
    }
}