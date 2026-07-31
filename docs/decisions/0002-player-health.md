# 0002 — PlayerHealth (HP=0 → Downed)

> Task 2 (P0) · ตัดสิน 2026-07-31 (ผ่าน grill session) · สถานะ: **นำไปใช้แล้ว**

บันทึกว่า "ทำไมโค้ดถึงเป็นรูปนี้" เพื่อกันรื้อผิดทีหลัง — ถ้าจะเปลี่ยนโครง อ่านนี่ก่อน

## บริบท

Task 1 วาง `PlayerState` (Alive/Downed/Dead + `[Server] SetState()`) ไว้แล้ว. Task 2 เพิ่มชั้น HP:
component server-authoritative ที่ **HP ถึง 0 → สั่ง Downed** (ไม่ respawn). ทุกระบบถัดไปอ้างอิง —
bleed-out (6), revive (7), lose condition (8), wave auto-revive (11), shop Max HP (13), Support heal (13).
Combat มี interface `IHitReceiver.ReceiveHit(in HitInfo)` อยู่แล้ว (server ตัดสิน hit).

## การตัดสินใจ

| # | ประเด็น | เลือก | เหตุผลย่อ |
|---|---|---|---|
| 1 | รับ damage ยังไง | **implement `IHitReceiver`** — `ReceiveHit` (guard server) → forward `hit.Damage` เข้า `ApplyDamage`, **ไม่สน knockback** | consistent กับ combat pipeline เดิม · ต่อ enemy (task 9) ได้ทันทีบรรทัดเดียว · knockback ไม่ใช่หน้าที่ health |
| 2 | HP ↔ state | **Store (ไม่ฟัง state)** — init `_currentHp=_maxHp` ใน `OnStartServer`; coupling ทางเดียว PlayerHealth→`SetState(Downed)` | revive/wave-clear เลือกจำนวน HP เอง (partial vs full) · ไม่ผูก 2 ทางกับ PlayerState |
| 3 | ใครกำหนด HP ตอนฟื้น | **ข้างนอกสั่ง (task 7/11)** ไม่ auto-full-on-Alive | MVP: revive กลางเวฟ HP ยังไม่สรุป (มีแนวโน้ม partial), wave-clear = full → ล็อก auto-full ตอนนี้จะรื้อ |
| 4 | consumer รับรู้ยังไง | **`OnHealthChanged(prev,next)` + props `Current/Max/Normalized/IsFull`** (copy pattern PlayerState) | UI/Heal Pulse ทีหลังมี code path เดียว ไม่แตะ FishNet · จัดการ host double-fire ที่เดียว |
| 5 | maxHP | **serialized `_maxHp=100f`** ตอนนี้; SO ตอน task 14; **ไม่มี `SetMaxHp`** (task 13) | SO ฟิลด์เดียวเปล่าๆ = premature · setter semantics (raise max → raise current?) ยังไม่นิ่ง |
| 6 | `ApplyDamage` visibility | **public `[Server]`** | เป็น core entry ให้ enemy melee/DoT (task 9) เรียกตรงได้โดยไม่ต้องผ่าน HitInfo + ใช้เทสต์ |
| 7 | HP type/invariant | **float** · clamp `[0,max]` · รับ damage เฉพาะ `IsAlive` · `amount<=0` ignore · `maxHp>=1` (OnValidate) | float ตรงกับ `HitInfo.Damage` · Downed/Dead ไม่โดน (ไม่มี finish-off-downed ตาม MVP) |

## รายละเอียด implementation ที่ต้องรู้

- **Spawn HP เนียน (first fire = max, ไม่มี 0):** `SyncVar<float>` default = `0f` ≠ maxHP. เซ็ต `_health.Value=_maxHp`
  ใน `OnStartServer` (รันก่อน `OnStartClient` บน host) แล้วปล่อยให้ `.OnChange` วิ่งผ่าน `Notify` (dedupe/initial-fire
  แบบ PlayerState) → announce แรกเป็น `(max, max)` เลย. `OnStartClient` เรียก `Notify(current)` เป็น safety net
  ให้ pure client (Notify dedupe ถ้า spawn-sync ยิง OnChange ไปแล้ว).
  - ⚠️ **verified แค่ host + แค่ outcome** (Current=100, IsFull ตอน spawn). การ **fire ครั้งแรกฝั่ง remote client จริง**
    ยังไม่ทดสอบ — ถ้า FishNet ยัง**ไม่**ได้ apply SyncVar ตอน `OnStartClient` รัน → safety-net `Notify(_health.Value=0)`
    จะยิง `(0,0)` ก่อน แล้วตามด้วย `(0→100)` = flicker ที่ user เลือก "เนียน" ไว้ไม่เอา. โดยทั่วไป FishNet apply
    SyncType ก่อน OnStartClient (น่าจะรอด) แต่ **ยังไม่ยืนยัน** — เช็คตอน multi-peer / health-bar UI (task ทีหลัง)
- **Host double-fire dedupe:** `HandleSyncChange` ignore `asServer`, dedupe ด้วยค่าใน `Notify` (`Mathf.Approximately`)
  → `OnHealthChanged` ยิงครั้งเดียวต่อการเปลี่ยน 1 ครั้ง (verify แล้ว: ApplyDamage 1 ครั้ง = 1 fire)
- **`ReceiveHit` guard `IsServerInitialized`** ก่อน `ApplyDamage` (แม้ NetworkProjectile จะ guard มาแล้ว)
- **`[RequireComponent(typeof(PlayerState))]`** — PlayerHealth หา PlayerState ผ่าน GetComponent ใน Awake

## ขอบเขต — task 2 *ไม่* ทำ

disable movement/combat, bleed-out 30 วิ, layer/tag change (task 6) · revive + refill HP (task 7/11) ·
enemy ที่ยิงจริง (task 9) · health-bar UI · `SetMaxHp` / class SO (task 13/14)

## ความเสี่ยง / ยังไม่ปิด

- **Multi-peer:** เทสต์แค่ host เดียว — HP sync ไปยัง client จริง (SyncVar propagation) + `ReceiveHit` จาก enemy
  จริง ยังไม่ลอง (ReceiveHit เป็น delegation 2 บรรทัดไป ApplyDamage ที่ verify ครบแล้ว)
- **maxHP เปลี่ยน runtime:** ตอน shop เพิ่ม maxHP (task 13) ต้องตัดสิน current HP เพิ่มตามไหม + เพิ่ม setter
