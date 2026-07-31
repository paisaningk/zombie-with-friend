# 0008 — Enemy AI (3 types)

> Task 9 (P3) · ออกแบบ 2026-07-31 (ผ่าน grill session) · สถานะ: **ออกแบบแล้ว — ยังไม่ build** ⚠️

บันทึก **การตัดสินใจจาก grill** ไว้ก่อน (กันดีไซน์หาย) — โค้ดยังไม่ได้เขียน. เริ่ม build จากเอกสารนี้.

## บริบท

Task 9 = enemy AI (3 types: Runner/Tank/Ranger, server-auth, A*). **Wave spawner = task 10 (แยก)**.
A* พร้อมในซีน: `AstarPath` (บน "Path") + prototype agent Cube(1) (FollowerEntity). ไม่มี enemy/wave script.
ปิดหนี้ที่ค้าง: projectile มีเป้าจริง (enemy=IHitReceiver), enemy เรียก ApplyDamage, hit-flash.

## การตัดสินใจ (grill)

| # | ประเด็น | เลือก | เหตุผลย่อ |
|---|---|---|---|
| 1 | โครง | **`Enemy` component เดียว + `EnemyData` SO** (stat + `attackType {Melee,Ranged}`) | enemy variety MVP น้อย (2 melee + 1 ranged) · polymorphic ไว้ทีหลังถ้า behavior เยอะ |
| 2 | health | **`SyncVar` HP** (จาก EnemyData) · `IHitReceiver.ReceiveHit` (server ลด HP) · **hit-flash** piggyback บน HP OnChange (client เห็น HP ลด→กระพริบ) · `OnDied` event → despawn | SyncVar เผื่อ health-bar + client react · hit-flash = ปิดหนี้ที่ user อยากได้ · OnDied hook ให้ task 10 (wave)/12 (gold) |
| 3 | movement | **FollowerEntity server-only** (disable บน client) · **NetworkTransform** sync (server-auth) · chase via `destination` | enemy = server-spawned ไม่มี owner → server คุม pathfinding, client เห็นตำแหน่งผ่าน NT · ใช้ FollowerEntity (ทิ้ง AIPath prototype) |
| 4 | target | **registry-based** — `GameManager.Players` filter `IsAlive` → **ใกล้สุด** · re-select ~0.5s | server อ่าน registry ได้ตรงๆ · horde ไล่ nearest เสมอ (ไม่ต้อง aggro range) · **→ ตัด downed layer swap ทิ้ง** (filter IsAlive ตรงๆ, hitMask ก็ exclude Player แล้ว) |
| 5 | attack | **melee** (Runner/Tank) · **Ranger = projectile** (หลบได้) | projectile = ดีต่อ horde shooter + ปิดหนี้ projectile · hitscan ranged = หลบไม่ได้ unfair |
| 6 | types | **3 prefab (base+variant per type)** แต่ละอัน EnemyData ฝัง · **visual = placeholder ก่อน** (ไม่ต้อง distinct scale/สี) | idiomatic FishNet (spawn registered prefab) · เลี่ยง sync SO ref |

**EnemyData (SO):** `maxHp, moveSpeed, damage, attackRange, attackRate, attackType {Melee,Ranged}` + (Ranged: `projectilePrefab, projectileSpeed`) · *(goldReward เลื่อน task 12)*
→ 3 asset: RunnerData (เร็ว/hp น้อย) · TankData (ช้า/hp เยอะ) · RangerData (ranged)

## attack flow (ทั้งหมด server-side — enemy ไม่มี owner)

```
ทุก tick (server):
  1. chase: FollowerEntity.destination = target.position
  2. distance(enemy, target) <= attackRange ?
  3. ใช่ + Time >= nextAttackTime → ATTACK; nextAttackTime = Time + 1/attackRate
  4. ไกล → เดินต่อ (ไม่ตี)
```
- **detection = distance check** (ไม่ใช่ collision trigger) — เชื่อถือได้ + ไม่พึ่ง collider event
- **melee:** ถึง range (~1.5m) → server เรียก `target.PlayerHealth.ApplyDamage(EnemyData.damage)` **ตรงๆ** (server เป็นเจ้าของทั้งคู่ ไม่ต้อง RPC)
- **ranged (Ranger):** ถึง range (~10m) → หยุด → server spawn `projectilePrefab` เล็ง target → ลูกวิ่ง → โดน player (`IHitReceiver`→ApplyDamage) · kiting = polish ทีหลัง
- **guards:** ตีเฉพาะ target ยัง Alive (ล้ม→re-select) · enemy ยังไม่ตาย

## implementation notes (ตอน build)

- **layer `Enemy` ใหม่** — player `hitMask` (=-13, exclude Player+IgnoreRaycast) รวม Enemy → player ยิงโดน enemy ได้
- **A* graph:** ตั้ง/scan graph บน `Path` (AstarPath) คลุม arena (Ground) ให้ FollowerEntity เดินได้
- **projectile prefab (Ranger):** แก้ Bullet ให้ถูก (trigger collider + **reserialize ผ่าน Fish-Networking editor menu** — ไม่ใช่ SaveAsPrefabAsset ที่เคยทำ serialization เสีย) หรือสร้าง enemy-projectile ใหม่ → ปิดหนี้ projectile (player projectile-weapon ใช้ได้ตาม)
- **cleanup SampleScene:** เอา `TestHitTarget` (throwaway) + prototype Cube/Cube(1)/Cube(2) ออก (enemy = เป้า/agent จริงแล้ว) · เก็บ `Path` (AstarPath)

## ไฟล์ที่จะสร้าง (ตอน build)

`Enemies/Enemy.cs` + `Enemies/EnemyData.cs` · 3 EnemyData asset + 3 enemy prefab (base+variant) · projectile prefab (Ranger) · layer Enemy · แก้ SampleScene (cleanup) · อัปเดต decision นี้เป็น "verified" + PROGRESS

## ตัด/เลื่อน

wave spawner (task 10) · gold/goldReward (task 12) · enemy health-bar UI · distinct visual per type · kiting · knockback enemy · enemy attack anim/effect (placeholder ทีหลัง)

## ยังไม่ verified (ยังไม่ build)

ทั้งหมด — เอกสารนี้เป็น **design เท่านั้น**. เทสต์ตอน build: spawn enemy ใกล้ player → A* ไล่ → ถึง range → attack (player HP ลด) → player ยิง → HP ลด + hit-flash → ตาย/despawn · Ranger → projectile ใส่ player
