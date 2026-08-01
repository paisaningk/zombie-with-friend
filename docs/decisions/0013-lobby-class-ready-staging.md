# 0013 — Class Select + Ready (in-game staging)

> Task 14 (P5) · ตัดสิน 2026-08-01 (ผ่าน grill session) · สถานะ: **นำไปใช้แล้ว · verified (host, MCP)**

บันทึกว่า "ทำไมโค้ดถึงเป็นรูปนี้" เพื่อกันรื้อผิดทีหลัง — ถ้าจะเปลี่ยนโครง อ่านนี่ก่อน

## บริบท

MVP ต้องให้ผู้เล่นเลือก class (Gunner / Support) + กด Ready ก่อนเริ่มแมตช์ (CLAUDE.md แนว **Killing Floor**).
task ก่อนเตรียมของไว้ให้พอดี: `PlayerClass` (SyncVar all-read) + `PlayerReady` (SyncVar all-read) เป็น
per-player synced component, `GameState` enum มีค่า `Lobby` ที่ยังไม่ถูกใช้, `WaveManager` มี ready-check
pattern (`AllReady`/`ResetAllReady`/`ShopWindow`) และ **รอ `Playing`** อยู่แล้วก่อนเริ่ม wave.

**ปมสถาปัตยกรรม:** class เก็บบน `PlayerClass` ซึ่งอยู่บน `Player.prefab` → **มีตัวตนหลัง spawn ในเกม
scene เท่านั้น**. แต่ CLAUDE.md เดิมเขียน "class เลือกที่ Lobby" (menu scene, **ก่อน** spawn) → ตอนอยู่
menu-lobby ยังไม่มี `PlayerClass` ให้เซ็ต + ค่าที่เลือกต้องเดินทางข้าม scene-load (ที่ `ReplaceOption.All`
unload menu ทิ้งหมด) มาถึงจังหวะ spawn. การแก้ปมนี้ตรงๆ (menu-lobby) ต้องสร้าง networking channel ใหม่
ในลอบบี้ (spawned coordinator / FishNet Broadcast) + snapshot ข้าม scene → งานเยอะ + ทดสอบยาก + เจอ
ปัญหา offline-scene/observer.

## การตัดสินใจ (grill)

| # | ประเด็น | เลือก | เหตุผลย่อ |
|---|---|---|---|
| 1 | ระดับลอบบี้ | **Full networked** (เห็น roster/ready เพื่อน + gate Start) single coordinator | ชื่อ task = "ready" → หัวใจคือ gate การเริ่ม + เห็น class เพื่อน |
| 2 | **สถาปัตย์ (พลิกเกม)** | **staging ในเกม scene หลัง spawn** (Option C) แทน menu-lobby | **หลบปม** — พอ spawn แล้ว `PlayerClass`/`PlayerReady` มีตัวตน + sync ทันที ไม่มีช่องว่างข้าม scene. reuse ของที่ task 11/12/13 ทำไว้ให้ตรงเจตนา ("reuse ที่ task 14"). ตรงกับ KF (เข้าห้อง → โหลดด่าน → เมนูบนด่านเลือก perk + ready) มากกว่าบรรทัดเดิม |
| 3 | state ของ staging | reuse `GameState.Lobby` (ไม่เพิ่ม `Staging`) | ค่ามีอยู่แล้ว, decision 0007 คาดไว้ว่า task 15 จะ drive `Lobby → Playing` |
| 4 | เริ่มแมตช์ยังไง | **host มีปุ่ม Start Match** | ผู้เล่นทยอยโหลดเข้าไม่พร้อมกัน (trickle-in) → host เป็นคนกดเมื่อเห็นว่าครบ, เลี่ยง "รอครบจำนวน" ที่เปราะ. host = server (FishNet host) → กดแล้วเรียก server ตรงๆ ไม่ต้อง RPC |
| 5 | ตอน staging ทำอะไรได้ | **freeze movement/look/weapon + ปลดล็อคเมาส์** (โหมดเมนู) | จอ staging มีปุ่มให้กด แต่ FPS ล็อคเมาส์ → ต้องปลดเมาส์. freeze เต็มตัว = น้อยงาน ไม่ต้อง toggle เดิน-เมนู (KF เดินได้ = เกินจำเป็น prototype) |
| 6 | Gunner vs Support | **flow อย่างเดียว** — เลือก class ได้จริง + Support ได้ heal / Gunner ไม่ได้ · **ปืนเหมือนกันไปก่อน** | ส่วนยาก/มีค่า = flow. per-class stat (`ClassData` SO) เลื่อนเป็น follow-up (โครง `PlayerUpgrades` FireRate/Damage multiplier รองรับแล้ว) |
| 7 | Ready ทำอะไร | **ปุ่ม Start ของ host เปิดใช้ได้เมื่อทุกคน ready** | ให้ ready มีความหมายจริง + host ยังคุมจังหวะ |
| 8 | UI | uGUI placeholder เรียบๆ (reuse `ButtonFx`/`UIPanel`) | art จริง = task 18. roster ใช้ชื่อ "Player {ClientId}" ไปก่อน (ยังไม่มีระบบชื่อ) |
| 9 | class ล็อกเมื่อไหร่ | **ล็อกหลังเริ่ม** — `CmdSetClass` apply เฉพาะ `GameState.Lobby` | เปลี่ยน class กลางแมตช์ไม่ควรได้ |

## รายละเอียด implementation

- **`PlayerClass`** — เพิ่ม `[ServerRpc] CmdSetClass(PlayerClassType)` (owner → server) → server guard `GameManager.State == Lobby` แล้วเรียก `SetClass` เดิม. `[Server] SetClass` เดิมคงไว้ (wave/test เรียกได้)
- **`GameManager.OnStartServer`** — เปลี่ยน `SetGameState(Playing)` → `SetGameState(Lobby)`. `CheckLose` guard `Playing` อยู่แล้ว → staging (Alive ทุกคน) ปลอดภัย ไม่ lose ผิด
- **`WaveManager`** — เป็นเจ้าของ staging-gate (มี `AllReady()` อยู่แล้ว):
  - เพิ่ม `static Instance` (set/clear ใน OnStartNetwork/OnStopNetwork แบบ GameManager) ให้ host UI เรียกได้
  - `[Server] TryStartMatch()` — guard `State==Lobby` + `AllReady()` → `_gm.SetGameState(Playing)`
  - `RunAsync` เดิม **ไม่ต้องแก้ logic รอ** — มันรอ `Playing` อยู่แล้ว, `TryStartMatch` แค่เป็นตัวพลิก Lobby→Playing
- **`PlayerController`** — gate เพิ่มมิติ GameState: `Movement/Weapon.enabled = IsAlive && Playing` · **Look ใหม่: enabled = Playing** (staging = ไม่หันกล้อง; ตอน Playing คง rule เดิม "look on ตอน Downed"). subscribe `GameManager.OnGameStateChanged` (นอกจาก PlayerState) → `RefreshGating()` จุดเดียว. เพิ่ม facade `Look`
- **`PlayerLook`** — ไม่ล็อคเมาส์ใน `OnStartClient` แล้ว (defer). `Update` (owner): ถ้า `GameManager.State != Playing` → return (freeze look, ไม่แตะเมาส์); ถ้า Playing → ล็อคเมาส์ (idempotent) + อ่าน look. camera/listener ยัง enable ตอน spawn (เห็นด่านระหว่าง staging). `OnStopClient` ปลดเมาส์เหมือนเดิม
- **`StagingController`** (ใหม่, client-side MonoBehaviour บน StagingCanvas ใน scene) — ขับ UI:
  - subscribe `GameManager.OnGameStateChanged` · `Lobby` → show panel + **ปลดล็อคเมาส์** · `Playing`/อื่น → hide (PlayerLook ล็อคเมาส์เอง)
  - ปุ่ม Gunner/Support → หา local owner `PlayerController` → `PlayerClass.CmdSetClass`
  - ปุ่ม Ready (toggle) → `PlayerReady.CmdSetReady`
  - ปุ่ม Start (โชว์เฉพาะ host, เปิดเมื่อทุกคน ready) → `WaveManager.Instance.TryStartMatch()` (host=server)
  - roster: re-scan `FindObjectsByType<PlayerController>` ทุก ~0.5วิ ระหว่าง staging (placeholder, กัน player ที่ spawn ทีหลัง) → โชว์ "Player {id}" + class + จุด ready

**cursor ownership:** StagingController เป็นเจ้าของตอน `Lobby` (unlock), PlayerLook เป็นเจ้าของตอน `Playing` (lock) — เขียนคนละ state ไม่ชนกัน

## ขอบเขต — task นี้ *ไม่* ทำ

`ClassData` SO / per-class stat (Gunner ยิงต่าง) — follow-up · room browser / server list (post-MVP, ต้อง Steam) ·
ระบบชื่อผู้เล่นจริง · เดินได้ตอน staging · late-join กลางแมตช์ · art/animation (task 18) ·
เปลี่ยน class กลางแมตช์

## เตรียมให้ต่อ

- **follow-up:** `ClassData` SO → per-class weapon/maxHP/heal (Gunner fire rate สูง) — เสียบผ่าน multiplier แบบ `PlayerUpgrades`
- **task 15:** GameState เต็มระบบ (staging นี้ดึง `Lobby→Playing` มาแล้วบางส่วน) · scene lifecycle · กลับ Lobby หลังจบ
- **task 16:** result screen · class HUD (`PlayerClass.OnClassChanged`) · cooldown/gold/wave HUD

## Runtime verified (host, MCP)

host flow (Tugboat loopback → `LoadGlobalScenes(SampleScene)`), player parked kinematic y=40 กัน AFK-death:

- **staging เริ่มถูก:** เข้า scene → `GameState = Lobby` (ไม่ auto-Playing), `currentWave = 0` (wave ถูก gate), StagingPanel active, `Cursor.lockState = None` ✓
- **freeze gating:** ตอน Lobby → `Movement.enabled = False`, `Weapon.enabled = False`, `Look.enabled = True` (self-gate ภายใน) ✓
- **OnStartClient fire แม้ component disabled:** `PlayerMovement._input != null` ทั้งที่ spawn มาตอน disabled → ยืนยัน FishNet เรียก network callback ไม่สน `enabled` → ยิง/เดินได้หลังเริ่มแมตช์ (ความเสี่ยงหลักที่ flag ไว้ = ปิดจบ) ✓
- **CmdSetClass ตอน Lobby:** Support → `CmdSetClass(Gunner)` → หลัง tick = **Gunner** ✓
- **ready-gate:** player not-ready → `TryStartMatch = False`, คง Lobby ✓ · `CmdSetReady(true)` → ready → `TryStartMatch = True` → **Playing** ✓
- **gating flip บน Playing (parked+Alive):** `Movement.enabled = True`, `Weapon.enabled = True` ✓ — RefreshGating รับ event GameState ครบ (subscription race ที่กังวล = ไม่เกิดจริง)
- **หลัง Playing:** StagingPanel hidden, `Cursor.lockState = Locked` (PlayerLook คืน cursor) ✓
- **class ล็อกหลังเริ่ม:** `CmdSetClass(Gunner)` ตอน Playing → คง **Support** (guard `State==Lobby`) ✓
- **loop เดินต่อ:** prep delay → `currentWave = 1`, enemies spawn (5) — staging→playing→wave ต่อกันครบ ✓
- compile clean (0 error) · console 0 error ตลอด run

**ยัง NOT verified (single-host harness):** multi-peer — roster เห็น class/ready เพื่อนข้ามเครื่อง, `PlayerClass`/`PlayerReady` SyncVar propagation ไป client, "N/N ready" · owner กดปุ่มจริงบน UI (verify ผ่าน `CmdSetClass`/`CmdSetReady`/`TryStartMatch` path — client-gate เป็น UX ล้วน) · การ render/คลิก uGUI จริง (ต้อง play-test โดยผู้ใช้)
