# PROGRESS — Horde Defense Co-op

บันทึกความคืบหน้าจริง (living doc) — อัปเดตทุกครั้งที่ปิด task
ดูภาพรวม scope/กติกาที่ [CLAUDE.md](../CLAUDE.md) · เหตุผล design แต่ละ task ที่ [docs/decisions/](decisions/)

> อัปเดตล่าสุด: **2026-07-30**

## สถานะรวม

กำลังอยู่ **Phase 0 (foundation)** → ต่อด้วย Phase 1 (first-person conversion)
โค้ดฐานเดิมยังเป็น prototype (top-down + projectile) — ดูตาราง "สถานะจริง vs MVP" ใน CLAUDE.md

## Task board

| # | Task | Phase | สถานะ |
|---|---|---|---|
| 1 | PlayerState enum + SyncVar | P0 | ✅ **เสร็จ (2026-07-30)** |
| 2 | PlayerHealth — HP=0 → Downed | P0 | ⏭️ ถัดไป |
| 3 | First-person movement | P1 | ⬜ |
| 4 | PlayerLook — yaw/pitch ±80° | P1 | ⬜ |
| 5 | PlayerCombat hitscan + WeaponData | P1 | ⬜ |
| 6 | Downed / bleed-out 30 วิ | P2 | ⬜ |
| 7 | Revive (hold 3 วิ) | P2 | ⬜ |
| 8 | Lose condition — AnyPlayerAlive() | P2 | ⬜ |
| 9 | Enemy AI 3 types *(งานใหม่)* | P3 | ⬜ |
| 10 | Wave spawner + auto-revive | P3 | ⬜ |
| 11 | Currency split-on-kill | P4 | ⬜ |
| 12 | Shop + ready-check | P4 | ⬜ |
| 13 | Support Heal Pulse | P4 | ⬜ |
| 14 | Lobby class select + ready | P5 | ⬜ |
| 15 | GameState management | P5 | ⬜ |
| 16 | Result screen + IP UI + art | P5 | ⬜ |

---

## Log

### 2026-07-30 — Task 1: PlayerState enum + SyncVar ✅

**ทำอะไร:** วางรากฐาน per-player lifecycle state (Alive/Downed/Dead) แบบ server-authoritative + coordinator ขั้นต่ำ

**ไฟล์ใหม่:**
- `Assets/Scripts/Player/PlayerLifeState.cs` — enum (Alive=0 default)
- `Assets/Scripts/Player/PlayerState.cs` — `NetworkBehaviour` data holder (private SyncVar + `[Server] SetState()` + `OnStateChanged` event + props)
- `Assets/Scripts/Game/GameManager.cs` — `NetworkBehaviour` minimal (server-side player registry + `AnyPlayerAlive()` stub)

**Wiring ใน Unity (ทำผ่าน MCP แล้ว):**
- เพิ่ม `PlayerState` ลง `Assets/Prefabs/Player.prefab` (root มี NetworkObject)
- วาง GameObject `GameManager` (+NetworkObject) ใน `Assets/Scenes/SampleScene.unity`

**Verify:** compile ผ่าน 3 types, Console 0 error / 0 warning, prefab+scene เซฟลง disk แล้ว

**เหตุผล design:** ดู [decisions/0001-player-state.md](decisions/0001-player-state.md)

**⚠️ ยัง verify runtime ไม่ได้ (static ผ่านหมด):** ordering ว่า `GameManager` (scene NetworkObject) initialize ก่อน player spawn — จะไม่มี warning `GameManager.Instance is null` ก็ต่อเมื่อเข้า SampleScene ผ่าน networking flow ปกติ (ไม่ใช่กด Play ใส่ scene ตรงๆ). จะถูก exercise จริงตอน task 2 เรียก `SetState(Downed)` และตอนต่อ scene flow (task 14/15)

**ค้างไว้ให้ task ถัดไป:**
- task 8 จะเอา `AnyPlayerAlive()` stub ไปต่อ lose logic (เพิ่มเงื่อนไข GameState==Playing)
- task 15 จะหย่อน `GameState` SyncVar ลง `GameManager` (Instance เปิดไว้ทั้ง server+client แล้ว รองรับ)
- convention ใหม่ `namespace Player` / `namespace Game` — ของเดิม (PlayerMovementTest ฯลฯ อยู่ global) ค่อยย้ายเป็น pass เดียวทีหลัง
