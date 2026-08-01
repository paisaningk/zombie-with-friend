# PROGRESS — Horde Defense Co-op

บันทึกความคืบหน้าจริง (living doc) — อัปเดตทุกครั้งที่ปิด task
ดูภาพรวม scope/กติกาที่ [CLAUDE.md](../CLAUDE.md) · เหตุผล design แต่ละ task ที่ [docs/decisions/](decisions/)

> อัปเดตล่าสุด: **2026-08-01**

## สถานะรวม

จบ **Phase 0–4** — Task 9 (Enemy) + 10 (Wave) + 11 (Currency) + 12 (Shop+Ready) build+verified
core loop + เศรษฐกิจ + shop ครบ (kill→gold→ซื้อ upgrade→ready→wave ถัดไป). เหลือ support ability (13) → lobby/gamestate/result (14–16)

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
