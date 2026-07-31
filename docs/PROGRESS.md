# PROGRESS — Horde Defense Co-op

บันทึกความคืบหน้าจริง (living doc) — อัปเดตทุกครั้งที่ปิด task
ดูภาพรวม scope/กติกาที่ [CLAUDE.md](../CLAUDE.md) · เหตุผล design แต่ละ task ที่ [docs/decisions/](decisions/)

> อัปเดตล่าสุด: **2026-07-31**

## สถานะรวม

กำลังอยู่ **Phase 0 (foundation)** → ต่อด้วย Phase 1 (first-person conversion)
โค้ดฐานเดิมยังเป็น prototype (top-down + projectile) — ดูตาราง "สถานะจริง vs MVP" ใน CLAUDE.md

## Task board

| # | Task | Phase | สถานะ |
|---|---|---|---|
| 1 | PlayerState enum + SyncVar | P0 | ✅ **เสร็จ (2026-07-30)** |
| 2 | PlayerHealth — HP=0 → Downed | P0 | ✅ **เสร็จ (2026-07-31)** |
| 3 | First-person movement | P1 | ⏭️ ถัดไป |
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
