# 0005 — PlayerController (Downed / bleed-out)

> Task 6 (P2) · ตัดสิน 2026-07-31 (ผ่าน grill session) · สถานะ: **นำไปใช้แล้ว · verified (host)**

บันทึกว่า "ทำไมโค้ดถึงเป็นรูปนี้" เพื่อกันรื้อผิดทีหลัง — ถ้าจะเปลี่ยนโครง อ่านนี่ก่อน

## บริบท

PlayerHealth (task 2) ตั้ง Downed ตอน HP=0 แล้ว แต่ยังไม่มีอะไร react. Task 6 = พฤติกรรม Downed:
ปิด Movement+Combat (คง Look), freeze, bleed-out 30 วิ → Dead. ต้องเตรียม hook ให้ revive (task 7).

## การตัดสินใจ

| # | ประเด็น | เลือก | เหตุผลย่อ |
|---|---|---|---|
| 1 | โครง | **`PlayerController`** (thin lifecycle coordinator, ไม่ใช่ god-object) — react `PlayerState.OnStateChanged`, ถือ ref via GetComponent | ผู้ใช้ถามว่าทำไมไม่มีตัวกลาง → คำตอบ: 0001 ตั้งใจแยก component (FishNet SyncVar/RPC แยก, disable แยกได้, SRP) · แต่ coordinator บางๆ ที่ประสาน (ไม่เก็บ logic) เป็นสิ่งที่ควรมี · รวม disable+bleed-out+revive-hook ที่เดียว |
| 2 | disable | `Movement.enabled = Weapon.enabled = IsAlive` (ปิดทั้ง Downed+Dead) · **คง Look** · เข้า non-Alive → zero horizontal velocity · คง collider | Look ต้อง active ตาม MVP (มองได้ตอน downed) · Downed/Dead disable เหมือนกัน → logic เดียว |
| 3 | layer swap | **เลื่อน task 9** | enemy ยังไม่มี → swap ตอนนี้ verify ไม่ได้ + friendly-fire ครอบคลุมด้วย Player-exclude ใน hitMask แล้ว → prep ล้วน (เดา boundary ผิดได้) |
| 4 | bleed-out | **server-only** `_bleedOutSeconds=30` (serialized) — เข้า Downed→set `_bleedOutEnd`; `Update`(server): `IsDowned && Time>=end → SetState(Dead)` · cancel ผ่าน state transition · ไม่ sync countdown | YAGNI: ยังไม่มี HUD; teammate รู้ downed จาก PlayerState (synced) แล้ว |
| 5 | Dead | disable ชุดเดียวกับ Downed · full spectate (free cam/follow/respawn) เลื่อน task 11/15 | task 6 = แค่ผลของ bleed-out; spectate เป็นระบบทีหลัง |

## รายละเอียด implementation

- subscribe `PlayerState.OnStateChanged` ใน **Awake** (contract 0001 — รับ initial fire) · `HandleStateChanged(prev,next)`: `Movement/Weapon.enabled = (next==Alive)` (Look ไม่แตะ) · non-Alive → zero rb velocity XZ (คง y = gravity) · server + next==Downed → `_bleedOutEnd = Time+_bleedOutSeconds`
- `Update` (guard `IsServerInitialized`): `IsDowned && Time>=_bleedOutEnd → SetState(Dead)` — ออกจาก Downed (revive→Alive หรือ →Dead) guard หยุดเอง **ไม่ต้อง cancel explicit**
- disable ทำงานทุก peer (OnStateChanged synced) — ที่มีผลคือ owner (คนอ่าน input); non-owner harmless (kinematic + Update early-return อยู่แล้ว)
- **revive/wave-clear hook:** task 7/11 แค่เรียก `PlayerState.SetState(Alive)` → controller re-enable ทุกอย่าง + bleed-out หยุด (verified)
- ไม่มี serialized ref (GetComponent เอง), ไม่มี SyncVar/RPC (bleed-out server-only, SetState อยู่ PlayerState)

## ขอบเขต — task นี้ *ไม่* ทำ

layer swap (task 9) · synced bleed-out countdown + downed HUD (ทีหลัง) · full spectate/respawn (task 11/15) · revive เอง (task 7) · visual downed (เอียง/สีจาง = polish) · lose condition event (task 8 ใช้ AnyPlayerAlive)

## Runtime verified (host, MCP)

- Alive: `Movement/Weapon/Look enabled` · **ApplyDamage(100)→Downed:** Movement/Weapon `enabled=False`, Look `True`, `rb.velocityXZ=(0,0)` · **bleed-out 30 วิ→Dead** (จริงตามเวลา): state=Dead, disabled คงเดิม, `AnyPlayerAlive=False` · **SetState(Alive)→re-enable:** Movement/Weapon `True`, `AnyPlayerAlive=True` · compile clean

**ยัง NOT verified:** owner กด move/shoot ตอน downed จริง (mechanism = `.enabled=false` → Update ไม่รัน; verified flag toggle, input-stop by-construction — user play-test ได้) · revive-cancel-before-30วิ (by-construction) · multi-peer
