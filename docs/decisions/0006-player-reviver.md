# 0006 — PlayerReviver (revive downed teammate)

> Task 7 (P2) · ตัดสิน 2026-07-31 (ผ่าน grill session) · สถานะ: **นำไปใช้แล้ว · verified (host, 2 players)**

บันทึกว่า "ทำไมโค้ดถึงเป็นรูปนี้" เพื่อกันรื้อผิดทีหลัง — ถ้าจะเปลี่ยนโครง อ่านนี่ก่อน

## บริบท

Task 6 วาง Downed + bleed-out + PlayerController (re-enable เมื่อ SetState(Alive)). Task 7 = revive:
Alive เข้าใกล้ Downed + กด Interact ค้าง 3 วิ (uninterruptible) → server validate ระยะ → Alive + partial HP.
ต้องเพิ่ม HP setter ที่ task 2 เลื่อนไว้.

## การตัดสินใจ

| # | ประเด็น | เลือก | เหตุผลย่อ |
|---|---|---|---|
| 1 | โครง | **`PlayerReviver`** (component แยก, owner-drive) — ไม่ยัดใน PlayerController (นั่น react "ตัวเอง"; revive ทำ "ให้คนอื่น") · client-detect + server-validate | รวมทีหลังได้ถ้าเยอะ |
| 2 | หา target | `Physics.OverlapSphereNonAlloc` (Player layer, radius 2.5m) → not-self + `IsDowned` → **นับใกล้สุด** · reviver ต้อง Alive+owner · ทุก frame | client ใช้ GameManager.Players (server-only) ไม่ได้ → physics |
| 3 | hold | **manual timer 3 วิ** (`Interact.IsPressed()`, ไม่พึ่ง Hold-interaction event) · progress 0→1 (UI ทีหลัง) · **uninterruptible** = damage ไม่ cancel · reset เมื่อ release / out-of-range / target-not-downed / reviver-non-Alive | Hold-interaction ไม่รู้เรื่อง target/range |
| 4 | HP ฟื้น | **30% ของ max** (`_reviveHpPercent=0.3` serialized, %-based scale ตาม Max HP upgrade) | full สงวนให้ wave-clear (task 11); partial = บาลานซ์ co-op |
| 5 | SetHp | `[Server] PlayerHealth.SetHp(float)` — absolute, clamp `[0,max]`, ยิง OnHealthChanged, **ไม่มี Alive-guard** (set ตอน downed ได้) · store model (ไม่ยุ่ง state) | reviver เรียก SetState(Alive) + SetHp แยกกัน (independent) |

## รายละเอียด implementation

- **`PlayerReviver.Update` (owner):** ถ้า `IsAlive && Interact.IsPressed()` → `FindDownedTarget()` (OverlapSphere) → มี target: `_hold += dt`, ครบ `_reviveHoldSeconds` → `ServerRevive(target.NetworkObject)`, reset · ไม่งั้น reset `_hold`
- **`ServerRevive(NetworkObject)` [ServerRpc]:** re-validate (reviver `IsAlive`, target `IsDowned`, ระยะ `(pos-pos).sqrMagnitude <= (radius+1)²`) → `targetState.SetState(Alive)` + `targetHealth.SetHp(Max * percent)` → PlayerController ของ target re-enable + หยุด bleed-out ให้เอง
- **`[Server] PlayerHealth.SetHp(float)`** — setter ที่ task 2 เลื่อนมา (revive partial, wave-clear เรียก `SetHp(Max)`)
- input: owner สร้าง `InputSystem_Actions` (Interact) ใน OnStartClient, dispose OnStopClient · `_playerMask` = Player layer (prefab)

## ขอบเขต — task นี้ *ไม่* ทำ

revive progress UI/HUD (มี `Progress` property เตรียมไว้) · revive cooldown (ไม่ต้อง) · aim-based (proximity พอ) · downed layer swap (task 9 — downed ยังอยู่ Player layer เลยเจอใน OverlapSphere)

## Runtime verified (host, 2 players via MCP)

- **revive apply:** player2 Downed + ใกล้ → `ServerRevive` → player2 **State=Alive, HP=30 (=Max×0.3), Movement/Weapon re-enabled** ✓
- **distance rejection:** player2 ไกล 10m → `ServerRevive` reject, คง Downed ✓
- **client-detect:** `FindDownedTarget` (OverlapSphere) คืน player2 (exclude self) ✓
- compile clean

**ยัง NOT verified:** hold input 3 วิ จริง (`Interact.IsPressed()` + timer — inject ไม่ได้; logic ตรงไปตรงมา → **user play-test**) · uninterruptible/reset (by-construction) · multi-peer จริง (client 2 เครื่อง)
