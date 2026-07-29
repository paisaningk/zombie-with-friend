# CLAUDE.md

คู่มือสำหรับ Claude Code ในโปรเจกต์นี้ — อ่านก่อนเริ่มงานทุกครั้ง

> ⚠️ **โค้ดปัจจุบันยังเป็น prototype/test** และต่างจากเป้าหมาย MVP หลายจุด (movement เป็น top-down, combat เป็น projectile, ยังไม่มี health/enemy/wave). ดูส่วน "สถานะจริงตอนนี้" ก่อนสรุปว่าอะไรมีอยู่แล้ว — **อย่าเชื่อว่าไฟล์มีอยู่โดยไม่เช็ก**

> 📋 **เริ่มงานทุกครั้ง เปิด [docs/PROGRESS.md](docs/PROGRESS.md)** — บันทึกความคืบหน้าจริง (ทำถึง task ไหน, ค้างอะไร). เหตุผล design แต่ละ task อยู่ที่ [docs/decisions/](docs/decisions/). อัปเดต PROGRESS.md ทุกครั้งที่ปิด task

## โปรเจกต์คืออะไร

**Horde Defense Co-op** — เกม co-op 4 คน แนว Killing Floor (survive-and-clear waves, ไม่มี base)
ผู้เล่นสู้เอง, มุมมอง first-person, ยิงแบบ hitscan, ผ่าน 5 wave = ชนะ / ทุกคนไม่ Alive = แพ้

## Environment

- **Engine:** Unity `6000.3.17f1`, Render Pipeline: **URP 17.4.0**
- **Platform dev:** Windows, PowerShell เป็น shell หลัก
- **IDE:** Rider / Visual Studio

## Dependencies (จาก `Packages/manifest.json` + `Assets/Plugins/`)

| Package | ใช้ทำอะไร |
|---|---|
| **FishNet** (`Assets/Plugins/FishNet`) | Networking หลัก — NetworkBehaviour, SyncVar, ServerRpc/ObserversRpc, NetworkTransform |
| **A\* Pathfinding Project** (`Packages/com.arongranberg.astar`) | Enemy pathfinding (Follower Entity) |
| **Cinemachine** `3.1.6` | กล้อง (ปัจจุบัน `PlayerCamera` ใช้ CinemachineCamera) |
| **Input System** `1.19.0` | `Assets/InputSystem_Actions.inputactions` — *แต่โค้ดยิงปัจจุบันยังใช้ legacy `Input.GetMouseButton`* |
| **Odin Inspector** (`Sirenix`) | `[Button]`, inspector attributes — ใช้ทั่วโปรเจกต์ |
| **UniTask** (`com.cysharp.unitask`) | Async (`UniTask<bool>` ใน LobbyManager) แทน coroutine |
| **Facepunch.Steamworks** | Steam transport (มี wire ไว้ แต่ MVP โฟกัส Direct IP) |
| **LitMotion**, **Eflatun.SceneReference** | Tweening / scene reference แบบ type-safe |
| Unity AI Navigation `2.0.11` | NavMesh module (มีติดมา — pathfinding หลักใช้ A\*) |

**Art:** `Assets/SyntyStudios/PolygonPrototype/` — Synty low-poly prototype pack (มี art จริง ไม่ใช่แค่ capsule)

## Scenes (`Assets/Scenes/`)

- `Init.unity` — bootstrap (`DevBootstrap` / `InitScene`)
- `MainMenu.unity` — main menu + lobby flow
- `SampleScene.unity` — scene เกม (arena) — *ยังเป็น sample*

## โครงสร้างโค้ด (`Assets/Scripts/`)

- `Networking/` — `LobbyManager` (Singleton, multi-transport, lobby-code), `PlayerSpawner`, `TransportProvider/` (Tugboat / Steam / Offline + `ITransportProvider`)
- `Networking/Test/` — โค้ดทดลอง (`PlayerCubeCreator`, `SyncMaterialColor`, `DespawnAfterTime`) — **ไม่ใช่ production**
- `Combat/` — `ProjectileShooter` (`NetworkShooter`), `Projectile`, `KnockbackReceiver`, `IHitReceiver`
- `Camera/` — `PlayerCamera` (Cinemachine, owner-only)
- `Movement/` — `PlayerMovementTest` (ปัจจุบัน top-down twin-stick aim + knockback)
- `GameUI/MainMenu/` — `MenuFlowController` + `MainMenuPanel` / `JoinPanel` / `LobbyPanel`, `Component/` (`ButtonFx`, `UIPanel`)
- `DevTool/` — `DevBootstrap`, `InitScene` (MPM / dev bootstrap)
- `Utility/` — `Singleton<T>`, `TestHost`, editor helpers
- `World/Resources/` — `SpawnOre`

**Prefabs (`Assets/Prefabs/`):** `Player` (ยังเป็น Cube + `ProjectileShooter`/`PlayerCamera`/`PlayerMovementTest`/`PlayerCubeCreator` + FishNet, มี child `CameraHolder`), `Bullet`, `poop`

## สถานะจริงตอนนี้ vs เป้าหมาย MVP

| ระบบ | ตอนนี้ (จริง) | เป้าหมาย MVP |
|---|---|---|
| **Movement** | Top-down twin-stick, หมุนตัวเข้าหา aim point จากกล้อง (`PlayerMovementTest`) | First-person, WASD + mouse-look |
| **Camera** | Cinemachine owner-only (`PlayerCamera`) | Manual yaw (player transform) / pitch (cameraHolder) split, clamp ±80° |
| **Combat** | **Projectile** spawn จริง, legacy `Input.GetMouseButton` | **Hitscan** raycast จากกล้อง + `WeaponData` |
| **Spawn** | Spawn ทันทีตอน connect ที่ random point ใน PlayerSpawner | Spawn ตอนเริ่มเกม (หลัง lobby ready) |
| **Lobby** | Lobby-**code** system + multi-transport (Yak/Tugboat/Steam/Offline) | Direct IP + class select + ready |
| **Health/State** | ❌ ยังไม่มี | `PlayerHealth`, `PlayerState` enum (Alive/Downed/Dead) |
| **Enemy** | ❌ **ยังไม่ได้ทำ** (ยืนยันแล้ว 2026-07-29 — เอกสาร MVP ที่เขียนว่า "เสร็จแล้ว" ผิด) | 3 types, server-auth, A\* |
| **Wave / Economy / Shop** | ❌ ยังไม่มี | `WaveData`, per-player gold, 3 upgrades |

> **แผนที่ตกลงกัน (2026-07-29):** เริ่มที่ Phase 0 (foundation) + Phase 1 (first-person conversion). Enemy AI เป็นงานใหม่ทั้งหมดใน Phase 3

## Networking Conventions (สำคัญมาก)

- **Game-critical logic ทั้งหมด Server-authoritative:** Enemy AI, Player HP/State, Currency, Purchase validation, Revive validation
- **Movement:** Client Authority ผ่าน `NetworkTransform` (owner ขยับเอง)
- **State ควบคุมด้วย SyncVar enum:** `GameState` (Lobby/Playing/Won/Lost), `PlayerState` (Alive/Downed/Dead)
- **Action pattern:** `[SyncVar] cooldownEndTime` + `ServerRpc` (trigger/validate) + `ObserversRpc` (play effect)
- **Ready-check pattern:** `[SyncVar] isReady` — ใช้ซ้ำทั้ง lobby class-select และ wave shop/ready
- **Data-driven:** stat ผ่าน ScriptableObject — `WeaponData`, `EnemyData`, `WaveData`
- **Async:** ใช้ **UniTask** ไม่ใช่ coroutine (ตาม LobbyManager)
- `LobbyManager` เป็น `Singleton<T>` แบบ `dontDestroyOnLoad`, ยิง event ให้ UI (`OnLobbyCreated`, `OnPlayerListChanged`, ฯลฯ)

## MVP Scope — กฎที่ตัดสินใจแล้ว

**Class (เลือกที่ Lobby):** Gunner (fire rate สูง/dmg กลาง, ไม่มี ability) · Support (fire rate ปกติ + Heal Pulse รอบตัว, cooldown 15 วิ)

**Downed / Revive (แทน respawn):**
- HP = 0 → **Downed** (ไม่ respawn ทันที): ปิด Movement + Combat, **`PlayerLook` ยัง active** (หมุนกล้องได้), enemy **ไม่ target** downed (เปลี่ยน layer/tag ออกจาก detection — ไม่แก้ target logic), bleed-out **30 วิ**
- Revive: เพื่อนเข้าใกล้ + กด Interact **ค้าง 3 วิ**, **uninterruptible**, server validate ระยะก่อน apply
- Bleed-out หมด → **Dead** (spectate จน wave จบ)
- **Wave clear → auto-revive ทุกคนเป็น Alive + Full HP** ก่อนเข้า shop

**Lose:** event-driven — state → Downed/Dead แล้วเรียก `AnyPlayerAlive()`; ไม่มีใคร Alive → แพ้ทันที (ไม่รอ timer)

**Wave:** fixed 5 waves จาก `WaveData` list, ระหว่าง wave ไม่มี timer — รอทุกคนกด Ready

**Economy:** gold ต่อคน, enemy ตาย → **แบ่งเท่ากันทุกคนที่ connected** ไม่สน state, ไม่ track kill credit. Shop: 3 upgrade ราคาคงที่ (Damage %, Max HP, Fire Rate)

**Enemy:** Runner / Tank / Ranger (server-auth, A\*), spawn จาก **fixed spawn point** ใน scene

**Camera:** yaw หมุน player transform (sync ผ่าน NetworkTransform) / pitch หมุน cameraHolder local, clamp ±80°, hitscan origin/dir จาก `playerCamera.transform.forward` ส่งกับ ServerRpc, **ไม่ sync pitch** ให้คนอื่นเห็น

**Art:** placeholder (ใช้ Synty prototype หรือ capsule/cube ได้)

## ตัดออกจาก MVP (อย่าเผลอทำ)

Turret/building, melee/projectile weapon (มีแค่ hitscan), weapon swap, ability ตัว 2, class เพิ่ม, endless mode, multi-lane map, shared currency, kill-credit, upgrade cost scaling, Steam/Relay lobby, stat summary, asset จริง/animation, viewmodel กันทะลุกำแพง, sync pitch, **Base/BaseHealth (ตัดทั้งระบบ)**, enemy finish-off downed, last-stand, revive แบบ interruptible, auto-proximity revive, persist downed state ข้าม wave, centroid-based spawn

## Build Order (เรียงตาม dependency)

**เสร็จแล้ว:** DevBootstrap + MPM · Lobby flow (code + multi-transport) · Player spawn (on-connect) · projectile combat + movement prototype

**ต้องแปลง prototype → MVP:**
- `PlayerMovementTest` (top-down) → first-person movement
- `PlayerCamera` (Cinemachine) → yaw/pitch manual split
- `ProjectileShooter` (projectile + legacy input) → hitscan + Input System

**ถัดไป (เรียงตาม dependency):**
1. `PlayerHealth` — HP=0 → Downed (ไม่ respawn) · ~~BaseHealth ตัดทิ้ง~~
2. `PlayerState` enum + SyncVar (ทำก่อน ทุกระบบอ้างอิง)
3. First-person movement — Client Authority NetworkTransform
4. `PlayerLook` — yaw/pitch split, clamp ±80°, ไม่ disable ตอน downed
5. `PlayerCombat` — hitscan raycast จากกล้อง + `WeaponData`
6. Downed/Bleed-out — 30 วิ, disable Movement+Combat, layer exclusion
7. Revive — Interact + `Physics.OverlapSphere` + hold 3 วิ + ServerRpc (uninterruptible)
8. Lose condition — `AnyPlayerAlive()` event-driven
9. Support Ability — Heal Pulse (cooldown SyncVar + ServerRpc + ObserversRpc)
10. Enemy AI (3 types, server-auth, A\*) + Wave Spawner — fixed spawn points + `WaveData` list (5 waves)
11. Wave-clear auto-revive
12. Currency — per-player gold SyncVar, split-on-kill
13. Shop + Ready-check — purchase RPC (3 upgrades)
14. Lobby scene — class select + ready (ต่อยอดจาก lobby ที่มี)
15. GameState management — enum SyncVar คุม scene/UI
16. Result screen — Win/Lose + กลับ Lobby
17. Direct IP connect UI
18. Placeholder art pass
