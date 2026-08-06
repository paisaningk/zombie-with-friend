# PROGRESS — Horde Defense Co-op

บันทึกความคืบหน้าจริง (living doc) — อัปเดตทุกครั้งที่ปิด task
ดูภาพรวม scope/กติกาที่ [CLAUDE.md](../CLAUDE.md) · เหตุผล design แต่ละ task ที่ [docs/decisions/](decisions/)

> อัปเดตล่าสุด: **2026-08-03**

## สถานะรวม

จบ **Phase 0–4** + **Task 14–15 + 16a + 16b** — core loop + เศรษฐกิจ + shop + staging + gamestate lifecycle + **in-game UI** + **Direct IP** ครบ → **เกมเล่นได้ครบ loop จริง + multi-peer LAN verified ครั้งแรก**
**Task 16b (Direct IP): ✅ G-a ผ่าน** (2 peer ต่อ LAN IP `192.168.1.71` จริง, เล่นได้ปกติ) — เจอ+แก้ **บั๊ก multi-peer ค้างเดิม** (FishNet SceneCondition, ดูล่าง)
**Task 16c (art): build เสร็จ** — Synty pawn แทน cube + แต่งฉาก, รอ play-review → **MVP build ครบทุก task (1–16c) เหลือ verify play + G-b**
**Phase 6 (post-MVP):** ออกแบบระบบ Weapon เสร็จแล้ว (W1–W5, decision 0016) — build หลังปิด MVP + LAN verify

## Task board

| # | Task | Phase | สถานะ |
|---|---|---|---|
| 1 | PlayerState enum + SyncVar | P0 | ✅ **เสร็จ (2026-07-30)** |
| 2 | PlayerHealth — HP=0 → Downed | P0 | ✅ **เสร็จ (2026-07-31)** |
| 3 | First-person movement | P1 | ✅ **เสร็จ (2026-07-31)** *(รวมกับ 4)* |
| 4 | PlayerLook — yaw/pitch ±80° | P1 | ✅ **เสร็จ (2026-07-31)** *(merge กับ 3; "ไม่ disable ตอน downed" = task 6)* |
| 5 | PlayerCombat hitscan + WeaponData | P1 | ✅ **เสร็จ (2026-07-31)** *(hitscan verified; projectile = scaffolding รอ task 9)* |
| 6 | Downed / bleed-out 30 วิ | P2 | ✅ **เสร็จ (2026-07-31)** |
| 7 | Revive (hold 3 วิ) | P2 | ✅ **เสร็จ (2026-07-31)** |
| 8 | Lose condition — AnyPlayerAlive() | P2 | ✅ **เสร็จ (2026-07-31)** |
| 9 | Enemy AI 3 types *(งานใหม่)* | P3 | ✅ **เสร็จ (2026-08-01)** |
| 10 | Wave spawner + auto-revive | P3 | ✅ **เสร็จ (2026-08-01)** |
| 11 | Currency split-on-kill | P4 | ✅ **เสร็จ (2026-08-01)** |
| 12 | Shop + ready-check | P4 | ✅ **เสร็จ (2026-08-01)** *(12a ready + 12b shop)* |
| 13 | Support Heal Pulse | P4 | ✅ **เสร็จ (2026-08-01)** |
| 14 | Lobby class select + ready | P5 | ✅ **เสร็จ (2026-08-01)** *(in-game staging)* |
| 15 | GameState management | P5 | ✅ **เสร็จ (2026-08-02)** |
| 16a | In-game UI (Result + HUD + Shop) | P5 | ✅ **เสร็จ (2026-08-02)** |
| 16b | Direct IP connect UI | P5 | ✅ **เสร็จ (2026-08-03)** — G-a ผ่าน (LAN 2 peer จริง) + แก้บั๊ก SceneCondition |
| 16c | Placeholder art pass | P5 | 🟡 **build+static verified (2026-08-03)** — รอ play-review (visual/nav/2-peer) |
| W1 | Arsenal foundation (2-slot + swap) | P6 | 🟡 **build+static verified** — รอ runtime play-test |
| W2 | Projectile weapon (ปิดหนี้ task 5) | P6 | 🟡 **build+static verified** — PlayerProjectile wired |
| W3 | Attachment system (3 mod slots) | P6 | 🟡 **build+static verified** — resolve fold ผ่าน smoke test |
| W4 | Shop integration (ปืน/attachment) | P6 | 🟡 **build+wired** — ปุ่มซื้อปืน/mod ในร้าน รอ play-test |
| W5 | Effect-hook layer + 3 ตัวอย่าง | P6 | 🟡 **build+static verified** — 3 effect assets พร้อม |

> **Phase 6 (Weapon system) = post-MVP** — design ครบผ่าน grill ([decision 0016](decisions/0016-weapon-system.md)), build **หลังปิด MVP** (16b + LAN verify + 16c). ต่อด้วย Skill system + Ping (ยังไม่ออกแบบ)

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

### 2026-07-30 — Task 1: runtime verification (host, single instance) ✅ (partial)

รัน dev flow จริงใน play mode (host ผ่าน `LobbyManager.OnCreateLobby` → Tugboat loopback → plain `LoadScene(SampleScene)`) แล้ว spawn Player prefab ผ่าน `ServerManager.Spawn` เพื่อ exercise code path ของ task 1 (วัดจำนวน `OnStateChanged` ยิงด้วย counter บน handler จริง)

**Verified (host, single instance):**
- `GameManager` (scene NetworkObject) network-initialize ผ่าน plain `LoadScene` ได้ — `Instance` set + `IsServerInitialized=True` + `IsSpawned=True` → **ordering หลักที่ doc flag ไว้ผ่าน** (ไม่มี warning `GameManager.Instance is null`)
- `PlayerState` register เข้า GameManager ที่ init แล้ว → `Players.Count=1`, spawn มา default `Alive`
- transition Downed→Alive→Dead→Alive: **host double-fire ยุบเหลือ `OnStateChanged` ครั้งเดียวต่อ 1 change** (counter idle=0, 1 ต่อ transition จริง, 0 ต่อ no-op)
- `SetState` no-op guard (ค่าเดิม → ไม่ยิง event)
- `AnyPlayerAlive()` track Alive↔Dead แบบ dynamic (True→False ตอน Dead, กลับ True ตอน revive)

**ยัง NOT verified (ขอบเขตของ harness นี้ ไม่ใช่บั๊ก):**
- cross-peer SyncVar propagation — ทั้งหมดเป็น server+client instance เดียว ยังไม่มี peer ที่ 2 จริง
- failure ordering (player register ตอน GameManager ยัง**ไม่**ขึ้น) — test ได้แค่ happy path (GameManager มาก่อน); branch warning ยัง code-read เฉยๆ
- `AnyPlayerAlive()` branch `Count==0` (lone-host guard)

**เจอระหว่างทาง (นอกขอบเขต task 1 — เรื่อง spawn/scene-flow):**
- component บน `PlayerSpawn` คือ **FishNet built-in `FishNet.Component.Spawning.PlayerSpawner`** (MonoBehaviour, `_playerPrefab=Player` assign แล้ว) — **ไม่ใช่** custom `Networking.PlayerSpawner` และ **ไม่ต้องมี NetworkObject**
- **Root cause ที่ไม่มีใคร spawn:** FishNet PlayerSpawner spawn ตอน event `OnClientLoadedStartScenes` ซึ่งยิง**ครั้งเดียวตอน connection โหลด start scenes เสร็จ (ตอน connect)** — ไม่ยิงซ้ำตอน `LoadGlobalScenes` ทีหลัง. flow จริง (menu) ทุกคน connect ในลอบบี้ก่อน แล้วค่อยโหลด game scene → event ยิงไปแล้วตอนอยู่เมนู → spawner ในซีนเกม subscribe ไม่ทัน → spawn ไม่เกิดทั้ง host+client (architectural mismatch กับ lobby-first flow)
- custom `Networking.PlayerSpawner` (NetworkBehaviour) ไม่ได้ถูกใช้ในซีนไหน
- มี scene-load 2 path: `LobbyPanel.StartGame` ใช้ `LoadGlobalScenes` (FishNet, ถูก) แต่ dev `InitScene.IsSkipMenu` ใช้ plain `SceneManager.LoadScene` (FishNet track ไม่ได้)

### 2026-07-30 — Lobby/join flow review (grill) + player-spawn fix ✅

Review flow เข้าห้อง (MenuFlowController / MainMenuPanel / JoinPanel / LobbyPanel / LobbyManager / transport providers) ผ่าน grill session แล้วแก้ตามที่ตกลง + fix ปัญหา player ไม่ spawn (verify runtime ใน real flow แล้ว)

**Decisions (grill):**
1. **localhost-only ก่อน** — Direct IP ไว้ทีหลัง (`JoinLobby` ยัง ignore code/IP ที่พิมพ์, `ConnectionAddress` hardcode `127.0.0.1`); ช่อง code หน้า Join = cosmetic ชั่วคราว
2. **Tugboat `SupportsLobby=false`** — direct-IP transport ไม่มี lobby service จริง ให้ player list ใช้ connection events; `true` สงวนให้ Steam
3. โหลด game scene: `LoadGlobalScenes(GameScene, ReplaceScenes = ReplaceOption.All)` (unload เมนู offline-scene ทุก peer อัตโนมัติ) แทน `UnloadGlobalScenes` ที่เป็น no-op
4. join = **optimistic** (เข้าห้องก่อน) + โชว์ error ตอน disconnect ไม่ให้เด้งกลับเงียบ; await-connection จริงไว้ทำตอน Direct IP
5. dev `IsSkipMenu` ใช้ networked load ตัวเดียวกับ flow จริง (host-only `LoadGlobalScenes`, client รับผ่าน network)
6. roster ในห้อง = host-only stopgap; synced state (LobbyPlayer + SyncVar isReady/class) เลื่อนไป **task 14**

**Changes:**
- `GamePlayerSpawner.cs` (ใหม่, MonoBehaviour) — presence-based: subscribe `SceneManager.OnClientPresenceChangeEnd` + drain connection ที่ present อยู่แล้วตอน init (host), idempotent ด้วย HashSet, spawn ผ่าน `GetPooledInstantiated` + `ServerManager.Spawn(nob, conn, scene)` — แก้ root cause ที่ built-in spawner (start-scenes-based) พลาด lobby-first flow
- SampleScene: เอา FishNet built-in `PlayerSpawner` ออกจาก `PlayerSpawn` → ใส่ `GamePlayerSpawner` (playerPrefab=Player, spawns=[1,2]) แทน
- ลบ custom `Networking/PlayerSpawner.cs` (dead — ไม่มี code/scene อ้าง ยืนยันด้วย GUID)
- `TugboatTransprotProvider` → `SupportsLobby=false`
- `LobbyPanel.StartGame` → `LoadGlobalScenes` + `ReplaceOption.All`, ตัด `UnloadGlobalScenes`
- `InitScene.IsSkipMenu` → host-only `LoadGlobalScenes` + `ReplaceOption.All` (เลิก plain load); fully-qualify `UnityEngine.SceneManagement.SceneManager` กัน ambiguous กับ FishNet
- `LobbyManager`: เรียก `OnErrorLog("การเชื่อมต่อหลุด")` ตอนหลุดไม่ตั้งใจ → ได้ `Debug.LogError` จริง + ยิง `OnError` ให้ UI (ยังไม่มี element แสดง — defer) ไม่ให้ disconnect เงียบสนิท
- `LobbyManager.OnCreateLobby`: แทน magic `WaitForSeconds(0.2f)` ด้วย `await UniTask.WaitUntil(() => IsHostStarted).Timeout(5s)` — รอ host ขึ้นจริงก่อน return (caller เปิด LobbyPanel ต่อ ซึ่ง gate ปุ่ม Start ด้วย IsHost()); timeout → `OnErrorLog` + `StopConnection` teardown + return false (ไม่ค้าง/ไม่ทิ้ง half-started host)
- `JoinPanel`: ส่ง `LobbyInputField.text` แทนสตริงลิเทอรัล `"LobbyInputField"`

**Runtime verified — full menu flow ผ่าน UI จริง (กด Host → Start เอง, ไม่ใช่ reflection):** อ่านจาก `[FLOW]` log ที่ใส่ชั่วคราว (ถอดออกแล้ว):
- `Host: started OK (IsHostStarted=True)` — **WaitUntil host-start ทำงาน ไม่ timeout** (แทน magic 0.2s ได้จริง)
- `Lobby.Setup: StartButton visible=True` — **Start button gating ถูก** (ปุ่มโผล่หลัง host ขึ้นจริง)
- `StartGame → LoadGlobalScenes(ReplaceOption.All)` → เมนู unload (`sceneCount=2`)
- **spawn ผ่าน EVENT path:** `presence ADDED conn 0` → `SPAWNED player for conn 0 at (-8.03,2.42,0)` — ในเมนู flow จริง host เข้าซีน**หลัง**ซีนโหลด (drain ตอน init เจอ 0) → เข้า `OnClientPresenceChangeEnd` = **code path เดียวกับ client-join** → ยืนยัน event path ทำงานจริง (ไม่ใช่แค่ construction)
- `GameManager registered (total=1)`, `PlayerState=1`, `Player(Clone)` ใน SampleScene
- แยก (MCP test ก่อนหน้า): **drain path** (host present ตั้งแต่ init) ก็ spawn ได้ → **ครบทั้ง 2 path (drain + event)**
- **error-on-disconnect เห็นจริง** (`การเชื่อมต่อหลุด` ตอน single-instance `IsSkipMenu=true` → client branch → join fail) → decision #4 ทำงาน
- compile clean (errorCount=0)

**ยัง NOT verified:** multi-peer จริง (2 connection แยก — client ที่ 2 ผ่าน MPM/คนละเครื่อง) ยังไม่ลอง — แต่กลไกของ client-join (event path) verify ผ่าน host แล้ว เหลือแค่ยืนยัน 2 คนพร้อมกัน

**เปิดค้าง (แยก):** `OnError` มี `Debug.LogError` แล้วแต่ยังไม่มี UI element แสดงบนจอ (ค่อยต่อ); `LobbyPanel.MenuScene` เป็น dead field หลังตัด `UnloadGlobalScenes`; `IsSkipMenu=true`+single instance → client branch → join fail (by design — ต้อง MPM tag "Host" ถึง host, เทสต์เมนูใช้ `IsSkipMenu=false`); Direct IP + synced roster = งานทีหลัง (ข้อ 1/6)

### 2026-07-31 — Task 2: PlayerHealth (HP=0 → Downed) ✅

วางชั้น HP server-authoritative ต่อจาก PlayerState — HP ถึง 0 → สั่ง Downed (ไม่ respawn). ออกแบบผ่าน grill session

**ไฟล์ใหม่:**
- `Assets/Scripts/Player/PlayerHealth.cs` — `NetworkBehaviour, IHitReceiver`, `[RequireComponent(PlayerState)]`; `SyncVar<float>` HP + serialized `_maxHp=100`; `[Server] ApplyDamage(float)` (public core) + `ReceiveHit(in HitInfo)` (forward `hit.Damage`, ไม่สน knockback); `OnHealthChanged` + props (`Current/Max/Normalized/IsFull`); HP≤0 → `PlayerState.SetState(Downed)`
- `docs/decisions/0002-player-health.md`

**Wiring:** เพิ่ม `PlayerHealth` ลง `Assets/Prefabs/Player.prefab` (root มี NetworkObject + PlayerState แล้ว), `_maxHp=100`

**Decisions (grill):** IHitReceiver (ไม่ใช่แค่ ApplyDamage) · **Store model** (init=max ใน OnStartServer, ไม่ฟัง state, revive/wave-clear สั่ง HP เอง — ยืดหยุ่น partial-revive) · `OnHealthChanged` ทำเลย (copy PlayerState) · serialized maxHP (SO ทีหลัง task 14, ไม่มี SetMaxHp) · ApplyDamage public · **spawn HP เนียน** (first fire = max ไม่มี 0)

**Runtime verified (host, MCP):** spawn `Current=Max=100, IsFull, Normalized=1` (ไม่มี 0) · `ApplyDamage(30)→70` ยิง `OnHealthChanged` ครั้งเดียว (host double-fire dedupe) · `ApplyDamage(0/-10)` no-op ไม่ยิง · `ApplyDamage(100)→0` → `PlayerState=Downed` + `AnyPlayerAlive=false` · damage ตอน Downed ถูก block (Alive-guard, ไม่มี finish-off-downed) · `IHitReceiver` + `ApplyDamage` public · compile clean (errorCount=0)

**ยัง NOT verified:** multi-peer จริง (HP SyncVar propagation ไป client, `ReceiveHit` จาก enemy จริง) — เทสต์แค่ host; ReceiveHit เป็น delegation ไป ApplyDamage ที่ verify ครบ · **first-fire "เนียน" verified แค่ outcome บน host** (Current=max) — การ fire ครั้งแรกฝั่ง client จริงยังไม่ทดสอบ (OnStartClient safety-net อาจยิง `(0,0)` ถ้า FishNet ยังไม่ apply SyncVar ก่อน OnStartClient) → เช็คตอน multi-peer/health-bar (ดู 0002 ความเสี่ยง)

**ค้างให้ task ถัดไป:** task 6 (bleed-out/disable) · task 7/11 (revive/wave-clear สั่ง HP ผ่าน setter ที่จะเพิ่ม) · task 9 (enemy เรียก ApplyDamage/ReceiveHit) · task 13 (SetMaxHp + semantics) · task 14 (maxHP → class SO)

### 2026-07-31 — Task 3+4: First-person movement + look ✅

แปลง prototype top-down → first-person. merge งาน 3+4 (movement ต้องใช้ yaw), component แยก. ออกแบบผ่าน grill

**ไฟล์ใหม่:**
- `Assets/Scripts/Player/PlayerMovement.cs` — owner Rigidbody WASD อ้าง `transform.forward/right` (Input System `Move`) + sprint (`Sprint` hold → ×1.6) · non-owner kinematic
- `Assets/Scripts/Player/PlayerLook.cs` — owner `Look` → **yaw หมุน root (sync ผ่าน NT)** + **pitch CameraHolder local clamp ±80° (ไม่ sync)** · enable owner Camera+AudioListener + Cursor.Locked
- `Assets/InputSystem_Actions.cs` (generated — เปิด `generateWrapperCode` ใน .meta)
- `docs/decisions/0003-first-person-movement-look.md`

**ลบ:** `PlayerMovementTest.cs`, `PlayerCamera.cs` (prototype, ยืนยันไม่มี ref เหลือ)

**Prefab (Player.prefab):** ถอด `PlayerMovementTest`+`PlayerCamera`+`CinemachineCamera`+`CinemachineFollow` · ใส่ `Camera`+`AudioListener` (disabled) บน CameraHolder (eye `(0,0.6,0)`) · ใส่ `PlayerMovement`+`PlayerLook` + wire refs

**Scene:** ลบ `Main Camera` ใน SampleScene (per-player cam แทน)

**Decisions (grill):** merge 3+4/component แยก · Rigidbody (เดิม) · Input System (generated class) · ทิ้ง Cinemachine เขียน manual · ตัด knockback · per-player cam owner-only + ลบ Main Camera · sprint ×1.6 no-stamina · yaw=root(synced)/pitch=CameraHolder(local ไม่ sync)

**Runtime verified (host, MCP):** spawn owner player · `IsOwner=True` · `rb.isKinematic=False` · **Camera+AudioListener enabled** · **Cursor Locked** · input instance สร้าง · **activeCameras=1 / activeAudioListeners=1** (handoff หลังลบ Main Camera สะอาด ไม่ชน) · errorCount=0 · compile clean

**Feel — user-verified ✅ (2026-07-31):** WASD เดินอ้าง facing, sprint, mouse yaw/pitch + clamp ±80°, cursor lock — ผ่าน (`activeInputHandler=2` Both → input ส่งถึงจริง)

**ยัง NOT verified:** non-owner จริง (single host), multi-peer + yaw sync ไป client

**ค้าง:** yaw ใช้ `transform.Rotate` ตรงๆ — ถ้าเจอ jitter ตอนชนของค่อยเปลี่ยนเป็น `Rigidbody.MoveRotation` · `NetworkShooter` ยัง `muzzle.forward` top-down (task 5 hitscan) · eye height 0.6 ปรับได้

### 2026-07-31 — Task 5: PlayerWeapon (hitscan + projectile framework) ✅ (hitscan)

แปลง projectile prototype → hitscan first-person + data-driven WeaponData (polymorphic). scope โตกว่า MVP เดิม (เป็น weapon framework เต็ม hitscan+projectile) ตามที่ user เลือก. ออกแบบผ่าน grill. เหตุผลเต็มดู [decisions/0004](decisions/0004-player-weapon.md)

**ไฟล์ใหม่:** `Combat/WeaponData.cs`(abstract) + `HitscanWeaponData.cs` + `ProjectileWeaponData.cs` + `PlayerWeapon.cs` + `TestHitTarget.cs`(throwaway) · `InputSystem_Actions`(+action Reload, regen) · asset `Data/Weapons/DefaultRifle.asset`(hitscan) + `DefaultLauncher.asset`(projectile) · decision 0004
**ลบ:** `ProjectileShooter.cs` (NetworkShooter)
**แก้:** `NetworkProjectile.ServerInit(dir, speed, damage)` data-driven + ทิศ 3D
**Prefab:** ถอด NetworkShooter → ใส่ PlayerWeapon (weaponData=DefaultRifle, hitMask=-13 exclude Player+IgnoreRaycast, aimSource=CameraHolder, muzzle=Cube(1)) · **Scene:** เพิ่ม TestHitTarget (cube+NetworkObject, layer Default) ใน SampleScene

**Decisions (grill):** Model B (client raycast→ส่ง hit→server damage จาก WeaponData) · fire-rate client-gate+server-validate · **B polymorphic SO-as-strategy** (พิจารณา composition แล้ว over-engineer) · ammo magazine+manual R+auto-on-empty (SyncVar) · no friendly fire · tracer placeholder · PlayerWeapon (namespace Combat)

**Runtime verified (host, MCP — hitscan):** spawn Ammo=30 · ยิงเป้า `LastDamage=25`(=WeaponData.damage, server ใส่) HitCount++ Hp ลด · **fire-rate reject** (2 นัดรัว→ติด 1) · **manual reload** (27→30) · auto-reload by-construction · compile clean

**Feel — user-verified ✅ (2026-07-31):** input→fire ผ่าน play-test — hold คลิก = ยิง (tracer + `[TestHitTarget] took 25 dmg` log + ammo ลด), R = reload · *(MCP inject input ไม่ได้ → verify ด้วยการกดจริง)*

**⚠️ projectile = UNVERIFIED scaffolding:** spawn/init รัน (ammo ลด + WeaponData stat) แต่ **ยิงโดนไม่ได้** — Bullet collider non-trigger vs `OnTriggerEnter` → ต้องผ่าตัด prototype (trigger + FishNet Reserialize) → **เลื่อน task 9** (มี enemy = เป้าจริง) · mark `// UNVERIFIED` ใน ProjectileWeaponData/SpawnProjectile แล้ว · Bullet.prefab revert กลับ (edit ผมทำ serialization เสีย)

### 2026-07-31 — Task 6: PlayerController (Downed / bleed-out) ✅

Downed behavior + coordinator กลาง (ผู้ใช้ถามหา "ตัวกลางของ player" → ทำ thin lifecycle facade). ออกแบบผ่าน grill. เหตุผลเต็ม [decisions/0005](decisions/0005-player-controller-downed.md)

**ไฟล์ใหม่:** `Player/PlayerController.cs` — thin coordinator: subscribe `PlayerState.OnStateChanged` → `Movement/Weapon.enabled = IsAlive` (คง Look), zero velocity ตอน non-Alive, server bleed-out timer (`_bleedOutSeconds=30`) → `SetState(Dead)` · decision 0005
**Prefab:** ใส่ `PlayerController` ลง Player.prefab (GetComponent หา ref เอง)

**Decisions (grill):** PlayerController = coordinator บางๆ (ไม่ god-object, 0001 แยก component ยังคงอยู่) · disable Movement+Weapon (คง Look) ทั้ง Downed+Dead · zero velocity · **layer swap เลื่อน task 9** (ยังไม่มี enemy, friendly-fire ครอบคลุมแล้ว) · bleed-out server-only 30 วิ (ไม่ sync countdown) · Dead ใช้ disable เดียวกับ Downed (spectate เลื่อน 11/15)

**Runtime verified (host, MCP):** Alive → Movement/Weapon/Look enabled · **Downed** (ApplyDamage 100) → Movement/Weapon disabled, Look on, velocityXZ=0 · **bleed-out 30 วิ → Dead** (จริงตามเวลา) disabled คงเดิม, AnyPlayerAlive=False · **SetState(Alive) → re-enable** (revive/wave-clear hook), AnyPlayerAlive=True · compile clean

**ยัง NOT verified:** owner กด move/shoot ตอน downed จริง (`.enabled=false`→Update ไม่รัน, by-construction — play-test ได้) · revive-cancel-before-30วิ (by-construction) · multi-peer

**เตรียมให้ task ถัดไป:** task 7 (revive) + task 11 (wave-clear) แค่เรียก `PlayerState.SetState(Alive)` → controller re-enable + bleed-out หยุดให้เอง · task 8 (lose) ใช้ `AnyPlayerAlive()`

### 2026-07-31 — Task 7: PlayerReviver (revive downed teammate) ✅

revive: Alive เข้าใกล้ downed + hold Interact 3 วิ → server validate → Alive + partial HP. ออกแบบผ่าน grill. เหตุผลเต็ม [decisions/0006](decisions/0006-player-reviver.md)

**ไฟล์:** ใหม่ `Player/PlayerReviver.cs` (OverlapSphere หา downed + manual hold 3 วิ + `ServerRpc` validate/apply) · แก้ `PlayerHealth.cs` เพิ่ม **`[Server] SetHp(float)`** (setter ที่ task 2 เลื่อนมา) · decision 0006 · wire `PlayerReviver` ลง Player.prefab (playerMask=Player, radius2.5/hold3/hp30%)

**Decisions (grill):** component แยก (owner-drive, รวมทีหลังได้) · OverlapSphere proximity (not-self+IsDowned, nearest, 2.5m) · manual hold 3 วิ (Interact.IsPressed, uninterruptible=damage ไม่ cancel) · **revive 30% max** (full สงวน wave-clear) · `SetHp(float)` absolute ไม่มี Alive-guard (store model)

**Runtime verified (host, 2 players MCP):** revive apply → target **Alive + HP=30 (Max×0.3) + Movement/Weapon re-enabled** · **distance rejection** (ไกล 10m → reject, คง Downed) · **client-detect** (FindDownedTarget OverlapSphere เจอ downed, exclude self) · compile clean

**ยัง NOT verified:** hold input 3 วิ จริง (inject ไม่ได้ → user play-test) · uninterruptible/reset (by-construction) · multi-peer จริง

**เตรียมให้ต่อ:** task 11 (wave-clear) ใช้ `SetHp(Max)` + `SetState(Alive)` ทุกคน (setter พร้อมแล้ว)

### 2026-07-31 — Task 8: Lose condition + GameState ✅

event-driven lose + introduce GameState. ออกแบบผ่าน grill. เหตุผลเต็ม [decisions/0007](decisions/0007-lose-condition-gamestate.md)

**ไฟล์:** ใหม่ `Game/GameState.cs` (enum {Lobby,Playing,Won,Lost}) · แก้ `GameManager.cs` — `SyncVar<GameState>` + `OnGameStateChanged` event (dedupe/initial-fire แบบ PlayerState) + `[Server] SetGameState` + subscribe `player.OnStateChanged` (Register/Unregister) + `CheckLose()` · decision 0007

**Decisions (grill):** subscribe OnStateChanged (event-driven) · minimal GameState enum + SyncVar + event เดียว (ไม่มี OnGameOver แยก) · lose guard **`Count>0 && none-alive`** (กัน empty-game แพ้ผิด) · re-check lose ตอน disconnect (Unregister) · Playing ตอน server-start

**Runtime verified (host, MCP):** server-start → **GameState=Playing** · player Alive → ไม่ lose · **down player คนเดียว → Lost** (event-driven instant) · **sticky** (revive หลัง Lost → คง Lost, CheckLose guard Playing) · compile clean

**ยัง NOT verified:** disconnect-lose (by-construction) · Count==0 guard (by-inspection) · multi-peer + client GameState sync

**เตรียมให้ต่อ:** task 10 win → `SetGameState(Won)` · task 15 Lobby/scene lifecycle · task 16 result screen subscribe `OnGameStateChanged`

### 2026-07-31 — Task 9: Enemy AI — ออกแบบแล้ว (grill) ยังไม่ build 🔵

grill ออกแบบ enemy ทั้งก้อนเสร็จ — **บันทึกดีไซน์ไว้ที่ [decisions/0008](decisions/0008-enemy-ai.md)** (ยังไม่เขียนโค้ด). สรุปย่อ:
- `Enemy` component เดียว + `EnemyData` SO (stat + attackType) · 3 prefab per type (Runner/Tank/Ranger), visual placeholder
- health: SyncVar HP + `IHitReceiver` + hit-flash (HP OnChange) + OnDied→despawn
- movement: FollowerEntity **server-only** + NetworkTransform sync · chase via destination
- target: **registry-based** (GameManager.Players filter IsAlive, nearest) → **ตัด downed layer swap ทิ้ง**
- attack: distance-check server-side · melee = ApplyDamage ตรงๆ · **Ranger = projectile** (ปิดหนี้ projectile)
- build notes: layer Enemy ใหม่ · A* graph scan · แก้/สร้าง projectile prefab (reserialize ให้ถูก) · cleanup TestHitTarget+prototype cubes
- **wave spawner = task 10 (แยก)** · gold = task 12

**สถานะ: design เท่านั้น ยังไม่ build/verify** — เริ่มเขียนจาก 0008

### 2026-08-01 — Task 9: Enemy AI (3 types) build + verified ✅

build ตามดีไซน์ 0008 ครบ (ไม่มี decision เปลี่ยน) — server-auth enemy 3 types + ปิดหนี้ projectile. เหตุผล+ผลเต็ม [decisions/0008](decisions/0008-enemy-ai.md)

**ไฟล์ใหม่:** `Enemies/EnemyData.cs` (SO) · `Enemies/Enemy.cs` (`NetworkBehaviour, IHitReceiver` — SyncVar HP, registry-based nearest-alive target 0.5s, FollowerEntity server-only chase, melee=ApplyDamage ตรง/ranged=projectile, hit-flash, OnDied→Despawn)
**แก้:** `Combat/Projectile.cs` → `damageMask`/`blockMask` (layer-based) แทน hardcode tag exclude — ปิดหนี้ projectile จาก task 5 + prefab เดียวใช้ได้ 2 ทิศ
**ลบ:** `Combat/TestHitTarget.cs` (throwaway)
**wiring (MCP):** layer Enemy=9 · 3 SO `Data/Enemies/*` · `Prefabs/Enemies/EnemyProjectile` + 3 enemy prefab (auto-register DefaultPrefabObjects) · cleanup SampleScene (ลบ TestHitTarget+Cube+Cube(1)+Cube(2))

**2 บั๊กเจอตอน verify + แก้:** (1) FollowerEntity `groundMask=-1` ชน collider ตัวเอง → บินขึ้นฟ้า → แก้ groundMask=Ground เท่านั้น (2) Ranger aim สูงเกิน (y1.8 > cube top y1.5) บินทะลุ → แก้ aim กลางตัว + projectile CCD

**Runtime verified (host, MCP):** chase (A* + grounded) · **melee → player HP→0→Downed→Dead→lose** · enemy `ApplyDamage` 30→15→0 → hit-flash + Die→Despawn · **Ranger projectile → player HP 100→28** · target filter IsAlive nearest (ตัด downed-layer-swap ทิ้ง)

**ยัง NOT verified:** multi-peer จริง (SyncVar propagation/flash ฝั่ง client, FollowerEntity disabled บน pure-client) · player กดยิง hitscan→enemy จริง (verify ผ่าน ReceiveHit path แล้ว, เหลือ play-test input)

**เตรียมให้ต่อ:** task 10 (wave) ใช้ `Enemy.OnDied` นับ + spawn จาก fixed points + `WaveData` list · task 12 (gold) hook `OnDied` แบ่ง gold

### 2026-08-01 — Task 11: Currency (split-on-kill + survival bonus) build + verified ✅

gold ต่อผู้เล่น + **PlayerController → hub refactor**. ออกแบบผ่าน grill. เหตุผลเต็ม [decisions/0010](decisions/0010-currency.md)

**ไฟล์ใหม่:** `Player/PlayerWallet.cs` (`SyncVar<int>` gold **OwnerOnly** + `OnGoldChanged` + `Add`/`TrySpend`)
**แก้:** `EnemyData`(+goldReward Runner10/Tank30/Ranger20) · `Enemy`(+`GoldReward`, target→PlayerController) · **`PlayerController`→hub** (expose `State/Health/Wallet/...` + register เป็นตัวแทน player) · `PlayerState`(ถอด self-register) · `GameManager`(registry `PlayerState`→**`PlayerController`** + `AwardGoldForKill` แบ่ง floor + `AwardSurvivalBonus`) · `WaveManager`(hook kill + survival bonus ก่อน revive + `_surviveBonus=200`) · Player.prefab(+PlayerWallet, NB ตัวที่ 11)

**Q8 facade:** component player เยอะ → `PlayerController` เป็น hub (accessor + coordinator), `GameManager.Players` เก็บ PlayerController — consumer แก้ 3 จุด (GameManager lose, WaveManager revive, Enemy target)

**กติกา:** kill → `reward/N` floor แบ่งทุกคน connected (ไม่สน state, ไม่ track killer) · survival bonus +200 เฉพาะ Alive **ก่อน** auto-revive · gold owner-only เริ่ม 0

**Runtime verified (host, MCP):** wallet gold=0 · **Runner kill +10 / Tank kill +30** (per-type) · **survival bonus +200** (5 kills+bonus=250) · registry refactor ไม่พัง — register + **lose** (player Dead→Lost) + wave/revive/target ครบ

**ยัง NOT verified:** N>1 floor-split (เทสต์ N=1) · survival bonus ตัด Downed · owner-only sync remote · TrySpend (ทั้งหมดต้อง 2 peer / task 12)

**เตรียมให้ต่อ:** task 12 (shop) ใช้ `PlayerWallet.TrySpend` + เสียบ ready-gate ที่ seam ก่อน `StartNextWave` · task 16 (HUD) subscribe `OnGoldChanged`

### 2026-08-01 — Task 12b: Shop upgrades build + verified ✅

ปิด economy loop — 3 stat upgrade (Damage% / MaxHP / FireRate) per-player ซื้อระหว่าง wave. เหตุผล+ผลเต็ม [decisions/0011](decisions/0011-shop-ready.md)

**ไฟล์ใหม่:** `Shop/UpgradeType.cs` (enum) · `Shop/ShopData.cs` (SO: ต่อ upgrade {cost/effectPerLevel/maxLevel}) · `Player/PlayerUpgrades.cs` (owner-only SyncVar 3 level + `DamageMultiplier`/`FireRateMultiplier`/`BonusMaxHp` + `[ServerRpc] CmdBuy`→`[Server] TryBuy`) · asset `Data/Shop/DefaultShop` (150 / +20%,+25,+15% / cap5)
**แก้:** `Game/GameManager.cs` (+`ShopOpen` server bool + `SetShopOpen`) · `Game/WaveManager.cs` (`ShopWindow` เปิด/ปิด ShopOpen ใน try/finally) · `Player/PlayerHealth.cs` (**`Max` = base + BonusMaxHp**, SetHp/Normalized/IsFull ใช้ Max) · `Combat/PlayerWeapon.cs` (**damage × DamageMultiplier** ใน ServerReportHit+projectile, **cooldown ÷ FireRateMultiplier** client-gate+server-validate ผ่าน `EffectiveCooldown`) · `Player/PlayerController.cs` (+`Upgrades` accessor) · Player.prefab (+PlayerUpgrades NB13, wire DefaultShop)

**กลไก (key):** ไม่แตะ WeaponData/`_maxHp` (shared) — เก็บ level ต่อผู้เล่น (owner-only) คำนวณ stat สดตอนใช้ · owner-only พอ (damage=server, fireRate client-gate=owner, maxHP server+owner)

**Runtime verified (host, MCP):** baseline mult=1/Max=100 · **shop-closed → reject** (gold ไม่หัก) · Damage→mult1.2 · MaxHp→Max125 + heal +25 delta (68→93) · FireRate→mult1.15 (แต่ละครั้ง −150) · **weapon เห็น upgrade** (EffectiveCooldown 0.1087, DamageMult 1.2) · **cap lv5** · **insufficient gold reject** · compile clean

**เก็บกวาด:** `PlayerMovement.FixedUpdate` +guard `_rb.isKinematic` (แก้ warning "set linearVelocity of kinematic body" ตอน body kinematic — ผู้ใช้เจอตอน test/park)

**ยัง NOT verified:** multi-peer (isReady propagate, owner-only level ไป client) · ยิงจริงหลังซื้อ (read-site value verified; hit path = task9) · owner กดปุ่มจริง (verify ผ่าน CmdBuy→TryBuy)

**เตรียมต่อ:** task 13 (Support Heal Pulse) · task 16 (shop/ready/gold HUD — subscribe `OnUpgradesChanged`/`OnReadyChanged`/`OnGoldChanged`; `ShopOpen`→SyncVar ถ้า client ต้องรู้) · **post-MVP backlog: weapon swap** (ต้องเคลียร์ projectile weapon ที่ค้าง task5 ก่อน)

### 2026-08-01 — Task 12 grill + 12a Ready-check build + verified ✅

grill ทั้ง task 12 (shop+ready) → **ซอยเป็น 12a (ready-check) + 12b (shop)**. เหตุผลเต็ม [decisions/0011](decisions/0011-shop-ready.md). 12a build+verify แล้ว

**ไฟล์ใหม่:** `Player/PlayerReady.cs` (NetworkBehaviour, SyncVar `isReady` **all-read** + `[ServerRpc] CmdSetReady` + `[Server] ServerSetReady` + `OnReadyChanged` — mirror Wallet/Health) · decision 0011
**แก้:** `Game/WaveManager.cs` — แทน `_interWaveDelay` ด้วย **shop window**: หลัง survival+`ReviveAll` → `ResetAllReady` → `await ShopWindow` = `WhenAny(ทุกคน ready, Delay(_shopTimer))` (default 60วิ, linked-CTS ยกเลิก branch แพ้) · `PlayerController.cs` (+`Ready` accessor hub) · Player.prefab (+PlayerReady, NB ตัวที่ 12) · CLAUDE.md (wave rule มี timer แล้ว)

**Decisions (grill):** ready-gate ระหว่าง wave (Wave1 คง prepDelay) · **WhenAny(ready, timer)** — user ขอ timer 1 นาที (ปรับได้) กัน AFK deadlock (**ขัด MVP เดิม "ไม่มี timer" → อัปเดต doc แล้ว**) · `PlayerReady` component แยก (reuse lobby task14) all-read (ทีมเห็น) · reset ทุก shop window · ซื้อได้เฉพาะร้านเปิด (a) → 12b เพิ่ม `GameManager.ShopOpen` · **weapon swap = post-MVP** (ตัด MVP, ต้องเคลียร์ projectile weapon ก่อน)

**Runtime verified (host, MCP):** spawn `IsReady=False` · `ServerSetReady(true)`→`AllReady=True` · `ResetAllReady`→คืน False · **shop window timeout path** (wave 1→2, player ไม่ ready, 5วิ timer → advance = AFK guard) · **shop window ready path** (`CmdSetReady(true)` → wave 2→3 ก่อน timer 120วิ) · player parked (kinematic y40) + bulk-kill กัน Lost · compile clean

**ยัง NOT verified:** multi-peer (isReady propagation ไป client, "2/N ready") · owner กดปุ่มจริง (inject ไม่ได้ → verify ผ่าน CmdSetReady path)

**เตรียมให้ 12b:** `WaveManager` เปิด/ปิด `GameManager.ShopOpen` รอบ `ShopWindow` seam · purchase RPC เช็ก ShopOpen

### 2026-08-01 — Task 10: Wave spawner + auto-revive build + verified ✅

wave progression ครบ loop (spawn→clear→revive→advance→Won/Lost). ออกแบบผ่าน grill. เหตุผล+ผลเต็ม [decisions/0009](decisions/0009-wave-spawner.md)

**ไฟล์ใหม่:** `Enemies/WaveData.cs` (SO: `Wave[]` → `WeightedEnemy[]{prefab,weight,minCount}` + totalCount/maxAlive/spawnInterval) · `Game/WaveManager.cs` (NetworkBehaviour, UniTask loop, `SyncVar<int> currentWave` + `OnWaveChanged`) · asset `Data/Waves/Campaign` (5 wave)
**แก้:** `Enemies/Enemy.cs` — `OnDied` `Action` → **`Action<Enemy>`** + `[Server] DespawnByManager()` (cleanup เงียบตอน Lost) · SampleScene (+4 `EnemySpawn` + `WaveManager` NetworkObject wire Campaign+points)

**โมเดล wave (grill, Option B):** weighted pool + งบรวม `totalCount` + **minimum การันตี** + **maxAlive (เติมทดแทนตอนตาย)** · Σmin>total → min ชนะ · spawn สลับสุ่ม 4 จุด

**loop (server, UniTask):** รอ `Playing` → prep → per wave: resolve queue (min+weighted+shuffle) → spawn คุม maxAlive/interval → รอ clear (`List<Enemy>` ว่าง) → **auto-revive ทุกคน Alive+full HP** → delay → wave ถัดไป → **Won** · Lost → cancel + despawn enemy เหลือ

**Runtime verified (host, MCP):** currentWave **1→2→3→4→5→Won** · W1=Runner×5, W2 **Tank โผล่** (guaranteed min+refill), **maxAlive cap** 6/total10 · wave clear → **revive heal 60→100** · **Lost** (AFK player ตาย) → loop cancel + enemy despawn หมด

**ยัง NOT verified:** multi-peer (currentWave/pose/revive ฝั่ง client) · revive จาก Downed จริง (ต้อง 2 player — คนเดียว down = Lost) · ผู้เล่นยิงจริง (bulk-kill = path เดียวกับ hitscan)

**เตรียมให้ต่อ:** task 11/12 (gold) hook `WaveManager.HandleEnemyDied` (มี comment ชี้จุดแบ่ง gold) · task 12/13 (shop+ready) เสียบที่ seam ก่อน `StartNextWave` (แทน auto inter-wave delay) · task 16 (result/HUD) subscribe `OnWaveChanged` + `OnGameStateChanged`

### 2026-08-01 — Task 13: Support Heal Pulse (+ PlayerClass foundation) build + verified ✅

ability ตัวแรก + วางรากฐาน class. ออกแบบผ่าน grill. เหตุผล+ผลเต็ม [decisions/0012](decisions/0012-support-heal-pulse.md)

**ไฟล์ใหม่:** `Player/PlayerClassType.cs` (enum `{Gunner,Support}`) · `Player/PlayerClass.cs` (NB, `SyncVar` all-read default **Support** + `[Server] SetClass` + event — mirror PlayerState) · `Player/HealPulseAbility.cs` (NB — owner input Q → client-gate → `[ServerRpc] CmdHealPulse` → `ServerCast` validate → apply → `[ObserversRpc] RpcPlayEffect`)
**แก้:** `Player/PlayerController.cs` (+accessor `Class`/`Ability` + **`IsAlive`** canonical check delegate PlayerState ไม่ใช่ hp>0) · `InputSystem_Actions.inputactions` (+action `Ability`=Q, regenerate wrapper)
**wiring (MCP):** +`PlayerClass`+`HealPulseAbility` ลง Player.prefab (NB 14–15)

**กลไก (C-1):** cast (Q) → server: Support+Alive+cooldown → เดิน `GameManager.Players` radius 6m: Alive→heal **+40** (รวมตัวเอง, clamp) / Downed→ปลุก **Alive+15%Max** (<manual 30%) / Dead→ข้าม · cooldown = `SyncVar<double>` **network time** (owner-only) 15วิ · effect = ทรงกลมขยาย placeholder + log

**Decisions (grill):** PlayerClass enum ตอนนี้ (gate = หัวใจ, task14 เสียบ UI) · C-1 pulse heal+ปลุก(HP ต่ำ) · registry ไม่ใช่ physics (server-auth) · SyncVar network-time cooldown (ตาม action pattern) · Ability=Q · component แยก (class ≠ ability) · self-guard IsAlive (เหมือน Reviver) · class all-read/cooldown owner-only · ค่า → ClassData SO task14

**Runtime verified (host, 2 players MCP):** self+ally heal (60→100 clamp / 50→90) · cooldown 15 set + re-cast reject · downed ally → Alive+15 · Dead skip · Gunner reject · Downed caster reject · ObserversRpc effect ยิงผ่าน RPC pipeline จริง (log ×10) · compile clean

**ยัง NOT verified:** multi-peer (class/cooldown SyncVar ไป client, network-time ข้ามเครื่อง) · owner กด Q จริง (verify ผ่าน ServerCast path)

**เตรียมให้ต่อ:** task 14 (lobby `SetClass` + ย้าย heal/maxHP/weapon → `ClassData` SO) · task 16 (cooldown bar อ่าน `CooldownRemaining` · class HUD จาก `OnClassChanged`)

### 2026-08-01 — Task 14: Class select + Ready (in-game staging) build + verified ✅

class-select + ready ก่อนเริ่มแมตช์. ออกแบบผ่าน grill — **พลิกจาก menu-lobby → in-game staging** (Option C). เหตุผล+ผลเต็ม [decisions/0013](decisions/0013-lobby-class-ready-staging.md)

**ปมที่แก้:** class เก็บบน `PlayerClass` (บน Player.prefab → มีตัวตนหลัง spawn ในเกม) แต่ menu-lobby เลือก**ก่อน** spawn → ต้องมี networking channel ในลอบบี้ + snapshot ข้าม scene. **Option C หลบปม** — เลื่อนจังหวะเลือกไป staging บนด่านหลัง spawn → `PlayerClass`/`PlayerReady` (per-player synced ที่ task 11/12/13 ทำไว้) ใช้ได้ทันที. ตรงกับ KF (เข้าห้อง→โหลดด่าน→เมนูบนด่าน) + reuse pattern shop-window (trader-time) ตอนเปิดแมตช์

**flow:** เข้า scene → `GameState.Lobby` (staging) → จอเลือก Gunner/Support + Ready (freeze คุมตัว + ปลดเมาส์) → host กด Start (เปิดเมื่อทุกคน ready) → `Playing` → prep → wave 1

**ไฟล์ใหม่:** `GameUI/Staging/StagingController.cs` (client-side, build UI ใน code แบบ placeholder — panel/class btn/ready/host-Start/roster; subscribe GameState; owner cursor ตอน Lobby) · decision 0013
**แก้:** `PlayerClass`(+`[ServerRpc] CmdSetClass` guard `State==Lobby`) · `GameManager.OnStartServer`(→`Lobby` แทน `Playing`) · `WaveManager`(+`static Instance` +`[Server] TryStartMatch` = guard Lobby+AllReady→Playing) · `PlayerController`(gate `Movement/Weapon = IsAlive && Playing`, subscribe `OnGameStateChanged`, +`Look` facade) · `PlayerLook`(ไม่ล็อคเมาส์ใน OnStartClient, `Update` freeze look + ล็อคเมาส์เมื่อ Playing) · SampleScene(+`StagingController` GO)

**Decisions (grill):** Full networked single-coordinator → **พลิกเป็น in-game staging** (reuse ตรงเจตนา) · reuse `Lobby` state + WaveManager owns gate · **host Start button** (เลี่ยง trickle-in) · freeze+ปลดเมาส์ · **flow อย่างเดียว** (Gunner/Support ปืนเหมือนกันไปก่อน — `ClassData` stat = follow-up) · Start เปิดเมื่อทุกคน ready · class ล็อกหลังเริ่ม

**Runtime verified (host, MCP, parked player):** `Lobby` ตอนเข้า scene (ไม่ auto-Playing) + wave gated (currentWave=0) · freeze (Movement/Weapon off, Look on) · **`OnStartClient` fire แม้ disabled** (`_input != null` → ยิง/เดินได้หลังเริ่ม) · `CmdSetClass` ตอน Lobby (Support→Gunner) · ready-gate (`TryStartMatch` reject→ready→accept→`Playing`) · **gating flip บน Playing** (Movement/Weapon on) · panel hide + cursor lock · **class ล็อกหลังเริ่ม** (CmdSetClass no-op ตอน Playing) · loop เดินต่อ (currentWave=1, enemy spawn) · compile+console clean

**ยัง NOT verified:** multi-peer (roster/SyncVar เพื่อนข้ามเครื่อง, "N/N ready") · owner กดปุ่ม UI จริง + render uGUI (verify ผ่าน Cmd path — play-test โดยผู้ใช้)

**เตรียมให้ต่อ:** **follow-up** `ClassData` SO → per-class stat (Gunner fire rate สูง, เสียบผ่าน multiplier แบบ PlayerUpgrades) · task 15 (GameState เต็มระบบ — staging ดึง `Lobby→Playing` มาแล้วบางส่วน · กลับ Lobby หลังจบ) · task 16 (result screen · class HUD `OnClassChanged` · roster ต่อยอด)

### 2026-08-02 — Task 15: GameState management (end-of-match lifecycle) build + verified ✅

ปิดปลายทาง Won/Lost ที่เคยเป็นทางตัน — **Play Again (replay in-place)** + **Exit to MainMenu** + รวมเจ้าของ cursor. ออกแบบผ่าน grill. เหตุผล+ผลเต็ม [decisions/0014](decisions/0014-gamestate-management.md)

**Ownership (ตัดสิน):** GameManager = state authority (`RestartMatch`) · WaveManager = reactor ต่อ GameState ล้วน (Playing=go / Lost=cancel / **Lobby(จาก terminal)=re-arm**) · PlayerController (hub) = `ResetForReplay()` coordinate component ตัวเอง · **CursorController (ใหม่)** = คนเดียวที่เขียน `Cursor.*`

**ไฟล์ใหม่:** `GameUI/CursorController.cs` (MonoBehaviour, subscribe GameState → `Apply()` Playing→Hide/อื่น→Show, `SetPaused()` เผื่อ pause) · decision 0014
**แก้:** `GameManager` (+`RestartMatch()` guard Won/Lost → loop `ResetForReplay` → Lobby) · `PlayerController` (+`ResetForReplay()` hub) · `PlayerWallet`/`PlayerUpgrades` (+`ResetForReplay()`→0) · `WaveManager` (subscribe ครั้งเดียวใน `RunArmed`, แยก `RunCampaign` re-invoke ได้, `RestartCampaign` re-arm ตอน Lobby, **skip ShopWindow wave สุดท้าย**) · `PlayerLook`/`StagingController` (ถอด `Cursor.*` ทิ้ง) · `GameState.cs` (legal-transition graph comment) · `LobbyManager` (+`SceneReference _mainMenuScene` + `ReturnToMainMenuIfInGame` ทั้ง 2 disconnect path — แก้หนี้ disconnect-ค้าง)
**wiring (MCP):** CursorController GO ใน SampleScene · LobbyManager `_mainMenuScene`=MainMenu (Init scene)

**Decisions (grill):** C (replay in-place) เป็นหลัก + ปุ่ม Exit (teardown) · Play Again host-only / Exit host-all|client-self · reset gold+upgrades→0 (campaign fresh) · re-invoke RunCampaign fresh CTS (ไม่ใช่ outer-loop) · **3-ชั้น co-location reset** (GameManager→PlayerController→component) · CursorController single writer derive จาก GameState (กัน last-writer-wins ตอน pause) MonoBehaviour ไม่ใช่ static · skip shop ท้าย · per-site guard + ยาม RestartMatch Won/Lost · return-to-MainMenu ที่ LobbyManager

**Runtime verified (host, MCP, parked player):** Cursor ทุก state (Lobby/Won/Lost=None, Playing=Locked) · **Won ถึง** (wave 5 เคลียร์) · **Play Again จาก Won reset ครบ** (gold 2940→0, dmgLv 3→0, hp→100, Lobby, wave 0) · **re-arm × 2 replays** (round2 หลัง Won + round3 หลัง Lost → wave 1 spawn 5, loop เดียวไม่ซ้อน) · **Lost + Play Again** (down→Lost, enemies despawn, Downed→Alive+full) · **guard RestartMatch** ตอน Playing no-op · **Exit teardown → MainMenu** + connection down · 0 error

**ยัง NOT verified:** multi-peer (GameState/reset/cursor propagate, host-teardown เตะ client, client-self-leave) · owner กดปุ่ม result UI (=task 16) · skip-final-shop (by-construction, play-test)

**เตรียมให้ต่อ (task 16):** result screen subscribe `OnGameStateChanged` (Won/Lose) · ปุ่ม Play Again → `GameManager.Instance.RestartMatch()` (host) · ปุ่ม Exit → `LobbyManager.Instance.HandleTransportDisconnect()` · HUD (`OnWaveChanged`/`OnGoldChanged`/`OnClassChanged`) · **pause menu (post-MVP):** `CursorController.SetPaused(true)`

### 2026-08-02 — Task 16a: In-game UI (Result + HUD + Shop) build + verified ✅

จอในเกมครบ → **เกมเล่นได้ครบ loop จริง** (เห็น HP/gold/wave, ซื้อของได้, เห็นจอจบ). ออกแบบผ่าน grill. เหตุผล+ผลเต็ม [decisions/0015](decisions/0015-ingame-ui.md)

**สถาปัตย์:** แยก controller ต่อ overlay (user เลือก B). **HUD = SOAP** (Obvious Soap เพิ่งลง — ลองใช้ตรงจุด data-binding), **Shop/Result = code-built uGUI** (interactive → onClick ตรง). กลไกพร้อมจาก task ก่อนหมด

**ไฟล์ใหม่:** `GameUI/HudPublisher.cs` (owner-only NB บน player — ดัน SyncVar→SO variable) · `GameUI/ShopController.cs` (code-built, poll owner, 3 ปุ่มซื้อ+ready) · `GameUI/ResultController.cs` (code-built, Won/Lose banner+ปุ่ม, statics ล้วน) · `GameUI/BindActiveToBool.cs` (SOAP-style bind: ซ่อน cooldown เมื่อไม่ใช่ Support) · 7 SO assets `Data/HUD/*` (Float/Int/String/Bool) · decision 0015
**แก้:** `GameManager` (`ShopOpen` bool→**`SyncVar<bool>`**+`OnShopOpenChanged` — client เปิดจอ Shop ได้) · `PlayerWeapon` (+`MagazineSize` accessor) · `CursorController` (free เมาส์เมื่อ `ShopOpen` ด้วย — Shop เปิดตอน Playing) · `PlayerLook` (freeze look เมื่อ ShopOpen)
**wiring (MCP):** HudPublisher+7 SO ref บน Player.prefab · SampleScene: HUDCanvas (BindFillingImage→Hud_Health max1 · 5×BindTextMeshPro→Gold/Ammo/Wave/Class/Cooldown · BindActiveToBool cooldown) + ShopController(_shop=DefaultShop) + ResultController GO

**SOAP↔network (key):** SO variable เป็น local asset ไม่ sync → **owner publisher** อ่าน SyncVar event/poll แล้วเขียน SO (IsOwner เท่านั้น) · composite text (ammo "27/30", wave "3/5") publisher format เป็น StringVariable · health = Normalized 0..1 เลี่ยง Max juggling

**Runtime verified (host, MCP, parked player):** HUD end-to-end อ่าน TMP text จริง (Gold/Class/Wave "Wave 0/5"/Ammo "30/30"/Cooldown/HealthFill=1) · reactive (Gunner→cooldown ซ่อน, gold 0→250→950, dmg→fill 0.6) · ShopOpen→SyncVar→ShopPanel+cursor free · **ซื้อผ่านปุ่ม** (onClick→CmdBuy gold 1100→950, maxHpLv→1) · Ready toggle · Result Won"VICTORY"/Lost"DEFEAT"+PlayAgain(host) · 0 error

**ยัง NOT verified:** multi-peer (SO per-client, SyncVar propagate, roster ข้ามเครื่อง) · คลิกเมาส์จริง (verify ผ่าน onClick.Invoke — play-test)

**เตรียมให้ต่อ:** **16b** Direct IP (`JoinLobby` รับ IP จริง เลิก hardcode 127.0.0.1) · **16c** art (Synty models) · pause menu (post-MVP) `CursorController.SetPaused(true)`

### 2026-08-03 — Phase 6: Weapon system — ออกแบบแล้ว (grill) ยังไม่ build 🔵

grill ออกแบบระบบ Weapon ("โมปืน") ทั้งก้อนเสร็จ — **บันทึกดีไซน์ที่ [decision 0016](decisions/0016-weapon-system.md)** + รูป flow ([assets/0016-weapon-system-flow.html](decisions/assets/0016-weapon-system-flow.html)). สรุปย่อ:
- **ขอบเขต (C):** arsenal 2 ช่อง (Primary/Secondary) + swap + attachment 3 ช่อง/กระบอก · **per-run** (reset ทุกแมตช์) · ซื้อจากร้าน · universal
- **สถาปัตย์ 3 ชั้น:** `WeaponData` SO (template stats-only) → `SyncList<WeaponSlot>` (ตัวเลข id+ammo, owner-only, ข้ามเน็ต) → `WeaponInstance[]` (runtime cache: template+mods+effects+profile, rebuild ตอน sync เปลี่ยน)
- **mod:** ตัวเลข + behavior · stack = รวม% คูณครั้งเดียว × upgrade เดิม · shot behavior → `WeaponProfile` (fixed vocabulary, server-auth+predict)
- **effect layer (ของแปลก):** `WeaponEffect` SO รันฝั่ง server ล้วน (OnHitDealt/OnKill) — ปลอดภัยเพราะไม่มี prediction · 3 ตัวอย่าง: Explosive AoE / Chain lightning / Heal-Ammo on kill · local attribution (`ReceiveHit` คืน bool killed) · recursion guard (effect damage → `ApplyDamage` ตรง)
- **ปิดหนี้:** player-projectile weapon (task 5) ใน W2 (เครื่องยนต์ verify แล้วผ่าน Ranger)
- **sub-task:** W1 arsenal · W2 projectile · W3 attachment · W4 shop · W5 effect layer

**สถานะ: design เท่านั้น** — build หลังปิด MVP (16b + LAN verify + 16c). **ถัดไป:** Skill system (generalize ability, ยืมโครงนี้) + Ping (ยังไม่ออกแบบ)

### 2026-08-03 — Task 16b: Direct IP connect — build + static verify ✅ (รอ G-a)

ทำให้เข้าห้องด้วย Direct IP (LAN) ได้จริง — ปิดหนี้ hardcode `127.0.0.1` + await-connection + error UI. ออกแบบผ่าน grill 12 decisions + advisor. เหตุผลเต็ม [decisions/0017](decisions/0017-direct-ip.md)

**แก้ (4 ไฟล์):**
- `Networking/TransportProvider/TugboatTransprotProvider.cs` — `_joinAddress` field (default 127.0.0.1) · `ConnectionAddress` คืน field · `JoinLobby(addr)` เขียน field (A1 — typed IP เป็น source-of-truth เดียว, host self-connect คง loopback)
- `Networking/LobbyManager.cs` — **await-real join** (`AwaitClientJoin`: subscribe `OnAuthenticated`/`OnClientConnectionState` **ก่อน** `StartConnection` กัน success-race + `UniTaskCompletionSource` + `.Timeout(10s)` backstop) · `_isJoining` flag กัน Stopped ตอน join-fail ชน mid-game-drop handler · `GetHostDisplayAddress()` + LAN IPv4 resolver · host-side `Debug.Log` ตอน client connect/disconnect
- `GameUI/MainMenu/JoinPanel.cs` — validate `IPAddress.TryParse` IPv4-only ก่อน connect · disable ปุ่ม+"Connecting..." · **error label subscribe `OnError`** (เดิมไม่มีใคร subscribe → error มองไม่เห็น)
- `GameUI/MainMenu/LobbyPanel.cs` — host: seed LAN IP ลง `IpInputField` (แก้ได้) · client: read-only host IP · copy จาก field แทน 127.0.0.1

**Decisions (grill, 12 ข้อ):** A1 IP-through-transport · B3 best-guess LAN IP แก้ได้+copy · C1 await-real · T1 พึ่ง LiteNetLib ~5.5วิ + backstop 10 · F1 connecting-feedback · D1 TryParse IPv4 · E1 label-only rename · **E1a error-label in-scope** (advisor จับ: `OnError` ไม่มี UI subscriber) · **I1 force IPv4-only** (advisor: dual-stack trap) · H1 host เห็น client ตอน staging + log · G-a/G-b verify ladder

**บั๊กเจอตอน static verify + แก้:** LAN IP resolver เดิม (score by range+type) **เดาผิดบนเครื่อง dev** — Hyper-V `vEthernet (Internal Switch)` (`192.168.217.1`) รายงานเป็น Ethernet+192.168 → tie กับ LAN จริง (`192.168.1.71`) → คืน virtual → **แก้: เพิ่ม default-gateway เป็นสัญญาณหลัก (+10)** เพราะ virtual switch ไม่มี gateway → LAN จริงชนะ (score 14 vs 4). ยืนยัน ranking ถูกผ่าน MCP

**Wiring (MCP):** Tugboat `_enableIpv6=false` (Init scene, saved) · MainMenu: +ErrorText (JoinPanel) +IpInputField (LobbyPanel) + label "Copy IP"/placeholder (saved)

**Static verified (MCP):** compile errors=0 · resolver ranking LAN จริงชนะ virtual · code path await-flow ถูก · host loopback ไม่ regression

**ยัง NOT verified — G-a (ผู้ใช้ทำ ปิด task):** 2 instance (MPM/2 build) client พิมพ์ **LAN IP จริง** (ไม่ใช่ 127.0.0.1) → ต่อ+เล่นได้ · error path (IP ผิด → เห็น error บนจอ ปุ่มคืนสภาพ) · **ถ้าต่อไม่ติดทั้งที่ IP ถูก → สงสัย IPv6 dual-stack ก่อน** · G-b (2 เครื่องจริง) ปิด MVP

**เตรียมต่อ:** 16c (art) ปิด MVP · Phase 6 weapon (build หลัง LAN verify)

### 2026-08-03 — Task 16b: G-a LAN test ผ่าน + แก้บั๊ก multi-peer (FishNet SceneCondition) ✅

**G-a (LAN 2 peer จริง):** client ต่อ LAN IP `192.168.1.71` (ไม่ใช่ loopback) สำเร็จ + เล่นได้ปกติ → **Direct IP ทำงานสมบูรณ์ + multi-peer verified ครั้งแรกของโปรเจกต์**

**บั๊กที่เจอตอน G-a (multi-peer ค้างเดิม ไม่เกี่ยว 16b):** client เข้าเกมแล้ว console ขึ้น `SceneId of ... not found in SceneObjects` (GameManager + WaveManager) → **manager ไม่ spawn บน client = เกมพัง**

**Root cause:** NetworkManager ไม่มี `ObserverManager` ใน scene → FishNet auto-add ตอน runtime ด้วย `_defaultConditions` ว่าง → ตามโค้ด `ObserverManager.AddDefaultConditions`: condition ว่าง = **ทุก object ถูก observe โดยทุก client ไม่ว่าอยู่ scene ไหน** → client ที่ยังอยู่ lobby (ยังไม่โหลด SampleScene) ได้ spawn ของ scene object ใน SampleScene → หา SceneId ไม่เจอ → drop → manager ไม่เกิด. โผล่ครั้งแรกเพราะทุก task ก่อนหน้า verify แค่ host เดี่ยว (object local อยู่แล้ว ไม่ส่งข้ามเน็ต)

**Fix (scene/asset เท่านั้น ไม่แตะโค้ด):**
- สร้าง `Assets/Data/DefaultSceneCondition.asset` (FishNet `SceneCondition`)
- เพิ่ม `ObserverManager` component ลง NetworkManager (Init scene) + set `_defaultConditions = [DefaultSceneCondition]`
- SceneCondition = `connection.Scenes.Contains(object.scene)` → object sync เฉพาะ client ที่โหลด scene เดียวกัน → client ในลอบบี้ไม่ได้รับ spawn ของ SampleScene, พอโหลด scene แล้วค่อยได้ = ถูกต้อง (แถม late-join ไม่พัง)

**Red herrings ที่ลองก่อน (ไม่ใช่สาเหตุ):** SceneId reserialize (ไม่ช่วย — id ปกติ, เป็นเรื่อง observation ไม่ใช่ id · SampleScene SceneId เปลี่ยนไปตอน reserialize แต่ไม่กระทบ), DontDestroyOnLoad, inactive object, duplicate instance, SampleScene pre-open in editor

**บทเรียน:** multi-peer scene game บน FishNet **ต้องมี SceneCondition** เสมอ ไม่งั้น object ข้าม scene รั่วไปหา client ที่ไม่ได้อยู่ scene นั้น → เก็บเป็น convention สำหรับ scene object ใหม่ทุกตัว

**ยัง NOT verified:** G-b (2 เครื่องจริง + firewall) — ปิด MVP ภายหลัง

### 2026-08-03 — Task 16c: Placeholder art pass — build + static verify ✅ (รอ play-review)

เลิกเป็น cube → Synty static pawn + แต่งฉาก. ออกแบบผ่าน grill 9 decisions. เหตุผลเต็ม [decisions/0018](decisions/0018-placeholder-art.md)

**สลับ visual (prefab, ไม่แตะ collider/logic ตาม Q5):**
- **Player** cube → `SM_Pawn_Weapon_Male_01` (ถือปืน) เป็น child · cube renderer ปิด · muzzle `Cube (1)` transform คงอยู่
- **Runner/Tank/Ranger** cube → `SM_Pawn_Run/Idle/Weapon_Male_01` · root scale เดิม (0.8/1.6/1.0) แยกขนาดให้ฟรี · material `Texture_04/07/02` แยกสี · cube renderer ปิด

**โค้ด (2 จุด, visual-only):**
- `Enemies/Enemy.cs` — `CacheFlashColor` fallback: ถ้า `_flashRenderer` null/disabled → `FindVisibleRenderer()` (enabled renderer แรก = pawn) → hit-flash เด้งบนโมเดลที่เห็น ไม่ใช่ cube ที่ซ่อน
- `Player/PlayerLook.cs` — owner-hide (P2): serialized `bodyModel` + OnStartClient (IsOwner) ปิด renderer ทั้งหมดของ body → เจ้าของไม่เห็นตัวเอง (FP), คนอื่นเห็น · **ไม่มี viewmodel** (ตัด MVP)

**Arena (SampleScene):** floor grid มีอยู่แล้ว (`Global_Grid_09`) · +4 กำแพงขอบ cube (±20, grid mat, BoxCollider) · +6 cover prop (crate/barrel/barrier, ±12) · **A* rescan** (`AstarPath.active.Scan()`) · saved

**เลือกตัวละคร (build-time correction):** grill Q4 เลือก rigged Character (Male_Face/Dummy) แต่เจอตอน build ว่า **pack ไม่มี animation/controller** → rigged จะ T-pose → สลับเป็น **static SM_Pawn** (ท่าเดียว ไม่มี rig = ไม่มี T-pose, ตรง S1) · pose บอก role (ถือปืน/วิ่ง/ยืน)

**MCP note:** interpreter รอบนี้ flaky หนัก (null/hang เยอะ, instance component access + prefab-ref wiring พัง) — pattern ที่ reliable: `LoadPrefabContents`+`SaveAsPrefabAsset(out ok)` (structural + ref set), single-purpose `ApplyPrefabInstance` (add/property เดี่ยว, combined นัก null). verify ทุกอย่างด้วย asset-read

**Static verified (asset-read):** ทั้ง 4 prefab มี SyntyModel + cube renderer ปิด + material ถูก · bodyModel=SyntyModel wired · compile errors=0 · arena saved + A* scanned

**ยัง NOT verified — play-review (Q9, ผู้ใช้):** pawn โผล่แทน cube · สี 04/07/02 ดูโอเค · เท้าจม/ลอย (pivot pawn vs collider) · **owner-hide 2-peer** (เจ้าของซ่อน/คนอื่นเห็น) · **enemy nav หลังแต่งฉาก** (จุดเสี่ยงสุด — A* rescan + cover ไม่บล็อค) · hit-flash บนโมเดล

**เตรียมต่อ:** ปรับสี/scale/pose ตาม review · G-b (2 เครื่อง) ปิด MVP · Phase 6 Weapon build

### 2026-08-03 — Hygiene: ลบ dead test code ✅

เก็บกวาด prototype/test code ที่ CLAUDE.md ระบุ "ไม่ใช่ production" (ถามยืนยัน scope กับ user ก่อนลบ)

**ลบ:** `Networking/Test/` (PlayerCubeCreator/SyncMaterialColor/DespawnAfterTime — 3 NetworkBehaviour ทดลอง) + `poop.prefab` (test ore) + `Bullet.prefab` (old projectile) · component `PlayerCubeCreator` ถอดจาก Player.prefab

**เคลียร์ ref (เก็บของจริงไว้):** `SpawnOre.Ore`→null (เก็บ SpawnOre, ore feature อยู่นอก MVP แต่ไม่ลบ) · `DefaultLauncher.projectilePrefab`→null (เก็บ SO, Phase 6 W2 สร้าง projectile ใหม่)

**ไม่แตะ:** SpawnOre, DefaultLauncher SO, `EmptyNetworkBehaviour` (FishNet built-in ไม่ใช่ test)

**วิธี (MCP flaky):** ref clearing = แก้ YAML ตรงๆ (reflection/SerializedObject ผ่าน interpreter คืน null เชื่อไม่ได้) · deletion = `AssetDatabase.DeleteAsset` (auto-update DefaultPrefabObjects) · verify compile errors=0 ไม่มี missing ref

### 2026-08-03 — Phase 6 W1: Arsenal foundation — build + static verify ✅ (รอ runtime)

refactor weapon เดี่ยว → arsenal 2 ช่อง + swap ตามสถาปัตย์ 3 ชั้น (decision 0016). ปรึกษา advisor ก่อน build (จับ 5 จุด: SyncList idiom, guard read, timer array, OnChange batch, คง HUD signature)

**ไฟล์ใหม่ (Combat/):** `WeaponSlot.cs` (struct value-type + IEquatable: weaponId+ammo+mod×3, per-slot ammo) · `WeaponCatalog.cs` (SO, id=index, resolve id→WeaponData ทุกเครื่องเหมือนกัน) · `WeaponProfile.cs` (resolved stats) · `WeaponInstance.cs` (runtime cache + `Resolve(template, upgrades)` fold multiplier)
**refactor:** `PlayerWeapon.cs` — `SyncList<WeaponSlot>` + `SyncVar<int> activeSlot` + `_reloading` (owner-only) · `WeaponInstance[2]` cache · timer per-slot (`_nextClientFire[2]`/`_nextServerFire[2]`, ไม่ sync) · OnChange handler (Add/Clear/Complete→rebuild all, Set→identity-skip ammo-only) · rebuild on OnUpgradesChanged · fire path ขับด้วย `ActiveInstance.profile` · swap key 1/2 → `CmdSwap` (cancel reload 7a) · **คง `Ammo`/`MagazineSize`/`IsReloading` signature → active slot (HudPublisher ไม่พัง)**

**FishNet facts (verify):** SyncList indexer `set` = force=true = dirty เสมอ (`_slots[i]=copy` ยิง OnChange แน่) · delegate `(op,index,old,new,asServer)` · `SyncList(SyncTypeSettings)` รับ owner-only

**Wiring:** `WeaponCatalog.asset` [DefaultRifle=id0, DefaultLauncher=id1] · Player.prefab PlayerWeapon `_catalog`+`_startingLoadout=[0,1]` (LoadPrefabContents pattern)

**Static verified:** compile errors=0 · `Resolve` ถูก (rifle dmg25/cd0.125/mag30/hitscan/range100 · launcher dmg40/cd0.33/mag10/projectile) · wiring persisted

**ยัง NOT verified — runtime (play-test):** spawn → 2 slot · swap 1↔2 เปลี่ยน activeSlot · ammo/reload/cooldown แยกช่อง · swap cancel reload · damage server-auth · hitscan (slot0=rifle) ยิงโดน · **launcher (slot1) projPrefab=null → ยิงกินกระสุนแต่ไม่เกิด projectile จนกว่า W2** (progression ตั้งใจ)

**เตรียม W2:** `PlayerProjectile` prefab (clone EnemyProjectile, damageMask=Enemy) → เข้า catalog id1 (DefaultLauncher.projectilePrefab) → launcher ยิงโดนจริง (ปิดหนี้ task 5)

### 2026-08-07 — Phase 6 W2–W5: projectile + attachment + shop + effect layer — build + static verified ✅

build ต่อจาก W1 จนครบทั้ง Phase 6 (decision 0016). compile errors=0 · resolve chain verified ผ่าน editor smoke test

**W2 — Projectile (ปิดหนี้ task 5):** `Prefabs/PlayerProjectile.prefab` (clone EnemyProjectile, `damageMask=Enemy(512)`) → wire เข้า `DefaultLauncher.projectilePrefab` · fire path `profile.isProjectile` ของ W1 ขับให้อยู่แล้ว (ไม่มีโค้ดใหม่)

**W3 — Attachment:** `AttachmentData.cs` (flat/pct damage, pctFireRate, flatMagazine + behavior forceFullAuto/addPellets/addSpread/addPierce + `WeaponEffect[]`) · `AttachmentCatalog.cs` (id=index) · `WeaponInstance.Resolve` ขยายเป็น `(template, slot, attachmentCatalog, upgrades)` fold Q10b `(base+Σflat)×(1+Σpct)×upgradeMult` · `WeaponProfile` +pelletCount/spread/pierceCount/effects · **fire path multi-pellet + pierce** (`FireHitscanShot`: RaycastAll เรียงระยะ, cone scatter, ส่ง target array ครั้งเดียว → server consume 1 นัด)

**W4 — Shop:** `WeaponData`/`AttachmentData` +displayName/cost · `PlayerWeapon.CmdBuyWeapon`/`TryBuyWeapon` (เขียนทับ WeaponSlot ทั้ง entry = ammo reset atomic) + `CmdEquipAttachment`/`TryEquipAttachment` (validate ShopOpen + gold ผ่าน `PlayerWallet.TrySpend`) · `ShopController` +หมวด WEAPONS/ATTACHMENTS (reuse MakeButton/LocalPlayer เดิม)

**W5 — Effect layer:** `WeaponEffect.cs` (abstract SO + `EffectContext` struct) hook `OnHitDealt`/`OnKill` server-only · **`IHitReceiver.ReceiveHit` → คืน `bool killed`** (local attribution) แก้ implementer ครบ 3 (Enemy=HP→0, PlayerHealth=Alive→Downed, KnockbackReceiver=false) · `PlayerWeapon.EffectDamage` (ApplyDamage ตรง = **recursion guard**) + `EffectHealShooter`/`EffectRefillAmmo` + `RpcEffectBurst`/`RpcEffectBeam` · **3 effect:** ExplosiveEffect (OverlapSphere AoE) · ChainLightningEffect (jump หา nearest, damage fraction) · HealAmmoOnKillEffect

**ตัด legacy:** `WeaponData.Fire(PlayerWeapon)` abstract strategy hook (fire path ใช้ fixed vocabulary + profile แทน ตาม Q11) — subclass ไม่ต้อง override อีก

**Assets:** `WeaponCatalog` [DefaultRifle=0, DefaultLauncher=1] · `AttachmentCatalog` [ShotgunChoke=0 (+5 pellets/spread6/−40%dmg), ExplosiveRounds=1 (−20% fireRate + AoE), VampireRounds=2 (+5 dmg + heal/ammo on kill)] · 3 effect assets (damageMask=Enemy) · Player.prefab `_catalog`+`_attachments`+loadout[0,1] · ShopController catalogs wired

**Static verified — `Tools/Horde/Weapon Resolve Smoke Test`** (editor menu, ทำเพราะ MCP interpreter เรียก method ที่มี `in` param ไม่ได้):
```
rifle (plain):              dmg=25  cd=0.125 pellets=1 effects=0
rifle + ShotgunChoke:       dmg=15  cd=0.125 pellets=6 spread=6      ← stat+behavior fold
rifle + Explosive+Vampire:  dmg=30  cd=0.156 effects=2                ← Q10b stack + effect layer
launcher:                   dmg=40  projectile=True projPrefab=PlayerProjectile ← W2
```

**ยัง NOT verified — runtime play-test (ทำทีเดียวตอนท้าย ตามที่ user เลือก):** swap 1/2 + ammo/reload แยกช่อง · ยิง rifle โดน enemy · **launcher ยิงโดนจริง** (ปิดหนี้ task 5) · ซื้อปืน/mod ในร้าน (gold หัก, slot เขียนทับ, ammo reset) · shotgun 6 นัดกระจาย · **explosive AoE / chain / heal-on-kill ทำงาน + ไม่ recurse** · multi-peer (SyncList owner-only propagate)
