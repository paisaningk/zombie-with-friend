# 0004 — PlayerWeapon (hitscan + projectile framework)

> Task 5 (P1) · ตัดสิน 2026-07-31 (ผ่าน grill session) · สถานะ: **hitscan verified · projectile = untested scaffolding**

บันทึกว่า "ทำไมโค้ดถึงเป็นรูปนี้" เพื่อกันรื้อผิดทีหลัง — ถ้าจะเปลี่ยนโครง อ่านนี่ก่อน

## บริบท

แปลง prototype `NetworkShooter` (projectile + legacy input + top-down `muzzle.forward`) → hitscan first-person + data-driven `WeaponData`. **scope โตกว่า MVP เดิม** (MVP = "มีแค่ hitscan") เป็น weapon framework เต็ม (hitscan + projectile) ตามที่ user เลือก

## การตัดสินใจ

| # | ประเด็น | เลือก | เหตุผลย่อ |
|---|---|---|---|
| 1 | authority | **Model B** — client raycast → ส่ง hit(target NetworkObject + point/dir) → server apply damage | pitch ไม่ sync (task 3) → server reconstruct aim ไม่ได้ · co-op PvE → hit-reg ตรงจอสำคัญกว่า security · server เบา ไม่ต้อง lag-comp |
| 1b | trust | client=targeting, **server=damage amount (จาก WeaponData)** | client ส่ง "โดนใคร" ไม่ส่ง "damage เท่าไหร่" → กันส่ง damage มั่ว |
| 2 | fire-rate | **client gate + server validate** (`nextServerFire` ธรรมดา ไม่ SyncVar) | server-auth ตาม spirit convention, ไม่แบก SyncVar (SyncVar cooldown ไว้ให้ ability task 13) |
| 3 | weapon system | **B polymorphic (SO-as-strategy)** — `abstract WeaponData.Fire(owner)` | ปืนต่างชนิด = behavior ต่าง (ลูกซอง=หลาย ray) ไม่ใช่แค่ stat → เพิ่มปืน=เพิ่ม subclass ไม่แตะ PlayerWeapon · (พิจารณา composition/modules แล้ว = over-engineer ก่อนมีปืนจริง) |
| 4 | ammo | magazine + **manual reload (R, เพิ่ม action)** + auto-on-empty · `[SyncVar] currentAmmo` | |
| 5 | friendly fire | **ไม่มี** — `hitMask` exclude Player(3)+IgnoreRaycast(2) (=-13) | co-op |
| 6 | visual | tracer placeholder (LineRenderer, shooter local + ObserversRpc) · enemy hit-flash = task 9 | |
| 7 | component | **`PlayerWeapon`** (namespace `Combat`) แทน NetworkShooter | |

## รายละเอียด implementation

- **`WeaponData` (abstract SO):** damage, fireRate, magazineSize, reloadTime, fullAuto + `abstract Fire(PlayerWeapon)`
  - `HitscanWeaponData` : range → `owner.FireHitscan(range)`
  - `ProjectileWeaponData` : projectilePrefab, projectileSpeed → `owner.SpawnProjectile()`
- **`PlayerWeapon` (NetworkBehaviour):** owns ammo/reload/fire-rate gate + input (Attack/Reload) + primitives:
  - `FireHitscan`: owner raycast (`hitMask`, range) → `ServerReportHit(targetNob, point, dir)` → server `ServerTryConsume()` + `receiver.ReceiveHit(HitInfo(.., weaponData.damage, 0))`
  - `SpawnProjectile`: → `ServerSpawnProjectile` → server spawn `_weaponData as ProjectileWeaponData`.projectilePrefab (prefab **ไม่ส่งผ่าน RPC** — server อ่านจาก WeaponData เอง) + `ServerInit(dir, speed, damage)`
  - `ServerTryConsume` (server): เช็ค reloading + nextServerFire + ammo>0 → ลด ammo, set cooldown, auto-reload ถ้า 0
- prefab: PlayerWeapon(weaponData=DefaultRifle hitscan, hitMask=-13, aimSource=CameraHolder, muzzle=Cube(1)) · ลบ NetworkShooter
- `NetworkProjectile.ServerInit` เปลี่ยนเป็นรับ (dir, speed, damage) — data-driven, ทิศ 3D
- `TestHitTarget` (throwaway, `Combat`) — IHitReceiver จด damage, ใน SampleScene layer Default → verify combat ก่อนมี enemy (ลบตอน task 9)

## ขอบเขต — task นี้ *ไม่* ทำ / ยังไม่ verify

- **input→fire — user-verified ✅** (play-test: hold คลิก=ยิง+tracer+damage log, R=reload) · *(MCP inject input ไม่ได้ → mechanics verified ผ่าน primitive ตรง, input path ผ่าน play-test)*
- **projectile = UNVERIFIED scaffolding** — spawn/init path รัน (ammo ลด, Instantiate + WeaponData stat) แต่ **ยิงโดนไม่ได้**: Bullet prefab collider = non-trigger แต่ `NetworkProjectile` ใช้ `OnTriggerEnter` → ต้องผ่าตัด prototype (trigger + FishNet Reserialize NetworkObjects) → **เลื่อน task 9** (enemy = เป้าจริง) · ไม่ได้ wire ให้ player คนไหน (equipped = HitscanWeaponData)
- spread/ลูกซอง, knockback (task 9), weapon-swap input, muzzle flash/impact/hitmarker, WeaponData→class SO (task 14)

## Runtime verified (host, MCP — hitscan)

- spawn `Ammo=30` (magazineSize) · ยิงเป้า → `TestHitTarget.LastDamage=25` (=WeaponData.damage), HitCount++, Hp ลด · **fire-rate reject** (2 นัดรัว → ammo -1, hit +1) · **manual reload** (27→30 หลัง reloadTime) · auto-reload = by-construction (`ServerTryConsume` เรียก `ServerStartReload` ตอน ammo 0, กลไก reload verified) · compile clean
