# 0008 — Enemy AI (3 types)

> Task 9 (P3) · ออกแบบ 2026-07-31 (grill) · **build + verified 2026-08-01** ✅

บันทึก **การตัดสินใจจาก grill** + ผล build. ดีไซน์ทำตามที่ตกลงครบ (ไม่มี decision เปลี่ยน) — เจอ 2 บั๊กตอน verify แก้แล้ว (ดูท้ายเอกสาร).

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

## BUILD (2026-08-01) — ทำตามดีไซน์ครบ

**ไฟล์ใหม่:** `Enemies/EnemyData.cs` (SO: displayName, maxHp, moveSpeed, attackType{Melee,Ranged}, damage, attackRange, attackRate, projectilePrefab, projectileSpeed) · `Enemies/Enemy.cs` (`NetworkBehaviour, IHitReceiver` — SyncVar HP, registry-based nearest-alive target re-select 0.5s, FollowerEntity server-only chase, distance-check melee=ApplyDamage ตรง / ranged=spawn projectile, hit-flash บน HP OnChange, OnDied→Despawn)
**แก้:** `Combat/Projectile.cs` (`NetworkProjectile`) — เปลี่ยนจาก hardcode `CompareTag("Player") return` เป็น **`damageMask`/`blockMask`** (layer-based) → ปิดหนี้ projectile + ให้ enemy projectile โดน player ได้ (prefab เดียวใช้ได้ 2 ทิศ: enemy→Player, player→Enemy)
**assets/wiring (ผ่าน MCP):** layer **Enemy=9** · 3 SO `Data/Enemies/{Runner,Tank,Ranger}Data` · `Prefabs/Enemies/EnemyProjectile` (sphere+trigger+kinematic RB+NetworkObject+NetworkTransform+NetworkProjectile, damageMask=Player blockMask=Ground, layer IgnoreRaycast) · 3 enemy prefab `{Runner,Tank,Ranger}Enemy` (capsule+CapsuleCollider layer Enemy+NetworkObject+NetworkTransform+FollowerEntity+Enemy) auto-register ใน DefaultPrefabObjects · cleanup SampleScene (ลบ TestHitTarget+Cube+Cube(1)+Cube(2), ลบ `Combat/TestHitTarget.cs`)

**stat ที่ตั้ง:** Runner(hp30/spd7/dmg8/range1.6/rate1.5, melee) · Tank(hp150/spd2.5/dmg20/range2/rate0.7, melee) · Ranger(hp50/spd3.5/dmg12/range12/rate0.8, ranged/projSpeed20)

## 2 บั๊กที่เจอตอน verify + แก้

1. **FollowerEntity บินขึ้นฟ้า (y พุ่ง 70→228, vel +Y คงที่)** — `groundMask` default = `-1` (ทุก layer **รวมตัวเอง**) → ground-raycast ชน capsule ตัวเอง → ดันขึ้นสะสมทุกเฟรม. **แก้:** ตั้ง `movement.groundMask = Ground(1<<6)` เท่านั้น (exclude self/Enemy) bake เข้าทั้ง 3 prefab → y=0.5 นิ่งบนพื้น
2. **Ranger projectile บินทะลุ player ไม่โดน** — aim `target.pos + up*0.8` = y1.8 แต่ player cube collider top = y1.5 → บินเหนือหัว. **แก้:** aim กลางตัว (`target.transform.position`) + origin `+up*0.5` → ยิงผ่าน center collider · เพิ่ม projectile RB `collisionDetectionMode=ContinuousSpeculative` กัน fast-trigger tunneling

## Runtime verified (host, single instance, MCP)

- **chase:** FollowerEntity + A* (GridGraph 100×100 auto-scan) — destination=player, ไล่ x เข้าหา, grounded y=0.5 (หลังแก้บั๊ก 1)
- **melee (Runner):** ไล่ถึง (dist 1.03 < range 1.6) → ApplyDamage → **player HP→0 → Downed → bleed-out → Dead → lose trigger** (ครบ chain)
- **enemy damage/death:** `ApplyDamage(15)` 30→15 (hit-flash fire บน HP OnChange), อีก 15 →0 → **Die→OnDied→Despawn** (enemyCount 1→0)
- **ranged (Ranger):** นิ่งที่ range → spawn projectile เล็ง player → **projectile โดน → player HP 100→28** (6 hits × 12, หลังแก้บั๊ก 2)
- **target registry:** filter IsAlive nearest (downed player ไม่โดน target, ตัด layer-swap ทิ้งตามดีไซน์)

## ยัง NOT verified

- **multi-peer จริง** (2 connection แยก) — HP/pose SyncVar propagation ไป client, hit-flash ฝั่ง client, FollowerEntity disabled บน pure-client (เทสต์ได้แค่ host = server+client รวม)
- **player hitscan → enemy path จริง** (กด input ยิง) — verify ผ่าน `ReceiveHit`/`ApplyDamage` (path เดียวกับ `ServerReportHit`) แต่ยังไม่กดยิงจริง (MCP inject input ไม่ได้ → play-test)
- ranger **kiting** (ตัดออก MVP), obstacle avoidance (arena โล่ง), enemy ชน player แล้วดัน rigidbody (collision push — cosmetic, cap ทีหลังถ้ากวน)
