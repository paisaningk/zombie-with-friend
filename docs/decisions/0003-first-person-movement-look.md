# 0003 — First-person movement + look (Task 3, merged with Task 4)

> Task 3+4 (P1) · ตัดสิน 2026-07-31 (ผ่าน grill session) · สถานะ: **นำไปใช้แล้ว · feel user-verified**

บันทึกว่า "ทำไมโค้ดถึงเป็นรูปนี้" เพื่อกันรื้อผิดทีหลัง — ถ้าจะเปลี่ยนโครง อ่านนี่ก่อน

## บริบท

แปลง prototype top-down (twin-stick + Cinemachine + legacy input) → **first-person** ตาม MVP.
`PlayerMovementTest` (aim = mouse→plane→หมุน body) และ `PlayerCamera` (Cinemachine owner-only) เป็น prototype.
NetworkTransform บน root เป็น `_clientAuthoritative=True` อยู่แล้ว (owner ขยับเอง, sync ให้คนอื่น).

## การตัดสินใจ

| # | ประเด็น | เลือก | เหตุผลย่อ |
|---|---|---|---|
| 1 | task 3/4 split | **merge งาน 3+4 pass เดียว** แต่ **component แยก** `PlayerMovement`+`PlayerLook` | movement ต้องใช้ yaw (task 4) → dependency ผกผัน · mouse-look (yaw+pitch) เป็น input handler เดียว ผ่าข้าม task มันงง · test ได้จริง (เดิน+หัน) |
| 2 | physics | **Rigidbody** (เดิม, velocity-based, NT config ไว้แล้ว) | reuse + physics-push/knockback เปิดทางไว้ · CharacterController = churn + ถอด RB |
| 3 | input | **Input System** — generate C# wrapper `InputSystem_Actions` (map `Player`), owner อ่าน `Move`/`Look`/`Sprint` | actions มีอยู่แล้วเป๊ะ · เขียน controller ใหม่อยู่แล้ว ต้นทุนน้อย · เลี่ยง legacy ที่ต้องรื้อ |
| 4 | โครง | เขียนใหม่ · **retire** `PlayerMovementTest`+`PlayerCamera`+Cinemachine · manual camera | Cinemachine เป็น top-down prototype · manual yaw/pitch คุมง่าย + ตรง MVP |
| 5 | knockback | **ตัด** (ไม่ port) | MVP ไม่เน้น · enemy (task 9) ยังไม่มี · RB เปิดทางเพิ่มทีหลัง · PlayerMovement ไม่ต้อง implement IHitReceiver |
| 6 | camera/owner | per-player **Camera+AudioListener บน CameraHolder, owner-only** · **ลบ Main Camera ในซีน** · non-owner kinematic · Cursor Locked | per-player cam = มาตรฐาน networked FPS · ลบ Main Camera = ไม่ต้อง hack ปิด runtime + กัน AudioListener ชน |
| 7 | sprint | **hold `Sprint` → maxSpeed × 1.6, no stamina, ทุกทิศ** | user ขอ · Input มี action แล้ว · stamina ไว้ทีหลัง |

**yaw/pitch seam (network boundary):** **yaw หมุน root** → sync ผ่าน NT (body หัน คนอื่นเห็น) · **pitch หมุน CameraHolder local, clamp ±80°** → camera-only ไม่ sync (คนอื่นไม่เห็นว่าเงยหน้า)

## รายละเอียด implementation ที่ต้องรู้

- **`PlayerMovement`** (owner-only, `RequireComponent(Rigidbody)`): `Move.ReadValue<Vector2>()` → wishDir อ้าง `transform.forward/right` (yaw ของ body), velocity-based (accel/decel), รักษา y velocity (gravity), sprint คูณ speed · non-owner `rb.isKinematic=true`
- **`PlayerLook`** (owner-only): `Look.ReadValue` → **yaw = `transform.Rotate(up, ...)` ตรงๆ (Update)** — เลือก direct transform (ไม่พึ่ง RB, FreezeRotation กัน physics ตี) แทน `MoveRotation` เพื่อ smooth + decouple; **pitch = `cameraHolder.localRotation` clamp ±80°** · owner enable Camera+AudioListener + Cursor.Locked
  - ⚠️ ถ้าเจอ yaw jitter ตอนชนของ → ค่อยเปลี่ยน yaw เป็น `Rigidbody.MoveRotation` (FixedUpdate)
- **Input lifecycle:** owner สร้าง `new InputSystem_Actions()` + `.Player.Enable()` ใน `OnStartClient`, `Dispose()` ใน `OnStopClient` · 2 component สร้าง instance แยกกัน (harmless)
- **Prefab:** CameraHolder localPos `(0, 0.6, 0)` = eye height (ปรับได้), Camera+AudioListener **disabled by default** (non-owner มืด)

## ขอบเขต — task นี้ *ไม่* ทำ

jump/crouch (ไม่อยู่ MVP) · knockback (ตัด) · แปลง `NetworkShooter` → hitscan จาก camera.forward (task 5 — ตอนนี้ยังยิง `muzzle.forward` top-down) · disable movement/look ตอน Downed (task 6 — "PlayerLook ไม่ disable ตอน downed") · stamina

## ความเสี่ยง / ยังไม่ verify

- **Feel (input-driven) — user-verified ✅** WASD เดินอ้าง facing, sprint ×1.6, mouse yaw/pitch + clamp ±80°, cursor lock ผ่าน (`activeInputHandler=2` Both) · *(MCP inject input ไม่ได้ → verify ด้วยการกดเทสต์จริง)*
- **Verified แล้ว (MCP host):** spawn + owner Camera/AudioListener enabled + Cursor Locked + rb non-kinematic + input instance สร้าง · **activeCameras=1 / activeAudioListeners=1** (handoff หลังลบ Main Camera สะอาด ไม่ชน) · errorCount=0 · compile clean
- **ยังไม่:** non-owner จริง (single host), multi-peer, yaw sync ไป client จริง
