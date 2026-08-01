# 0014 — GameState management (end-of-match lifecycle)

> Task 15 (P5) · ตัดสิน 2026-08-02 (ผ่าน grill session) · สถานะ: **นำไปใช้แล้ว · verified (host, MCP)**

บันทึกว่า "ทำไมโค้ดถึงเป็นรูปนี้" เพื่อกันรื้อผิดทีหลัง — ถ้าจะเปลี่ยนโครง อ่านนี่ก่อน

## บริบท

แกน `GameState` สร้างมาแล้วกระจายหลาย task: enum `{Lobby,Playing,Won,Lost}` + `SyncVar` + `OnGameStateChanged`
(task 8) · `Lobby` ตอน server-start + `Lobby→Playing` staging (task 14) · `Playing→Won` (task 10) ·
`Playing→Lost` (task 8). **สิ่งที่ยังเป็นทางตัน = ปลายทาง Won/Lost** — พอ flip แล้ว: `RefreshGating` ปิด
Movement/Weapon (freeze), **แต่เมาส์ยัง Locked** (PlayerLook ปลดแค่ `OnStopClient`, StagingController ปลดแค่
`Lobby`) → ไม่มี cursor กด, ไม่มี UI, ไปต่อไม่ได้. `RunAsync` เป็น for-loop รอบเดียว จบแล้วจบเลย

Task 15 ปิดปลายทาง: **Play Again (replay in-place)** + **Exit to MainMenu** + รวมเจ้าของ cursor เป็นตัวเดียว

## การตัดสินใจ (grill)

| # | ประเด็น | เลือก | เหตุผลย่อ |
|---|---|---|---|
| 1 | ปลายทางหลังจบ | **C (replay in-place → กลับ `Lobby` staging) เป็นหลัก + ปุ่ม Exit to MainMenu (teardown)** | C อยู่ในซีนเกมล้วน ไม่ต้องยุ่ง scene juggling/networking teardown → ขับ state machine ตรง ("enum คุม scene/UI"). menu-lobby แบบคงห้อง (B) ต้อง networked-menu + synced-roster ที่เลื่อนไว้ = งานเยอะทับหนี้เดิม |
| 2 | ใครกดได้ | **Play Again = host-only** (mirror ปุ่ม Start staging) · **Exit: host = teardown ทั้ง session / client = ออกคนเดียว** | สอดคล้อง pattern เดิม (host คุมจังหวะ, client จัดการ connection ตัวเอง). MVP ไม่ต้อง vote |
| 3 | Play Again reset อะไร | life→Alive · HP→full · **gold→0 · upgrades→0** · ready→false · class คงไว้ · enemies เกลี้ยง · currentWave→0 | campaign 5-wave จบในตัว → เล่นซ้ำ = เริ่มใหม่สะอาด, shop กลับมามีความหมาย, เลี่ยง power-creep (MVP ตัด endless/cost-scaling) |
| 4 | กลไก restart loop | **re-invoke `RunCampaign` ด้วย fresh CTS** (ไม่ใช่ outer `while(true)`) | งานเก่าตายแล้วทั้ง 2 path ตอน re-arm (Won=จบเอง, Lost=cancel แล้ว). cancellation คุมง่ายกว่า outer-loop |
| 5 | เจ้าของ lifecycle | **GameManager = state authority + `RestartMatch()` · WaveManager = reactor ต่อ GameState ล้วน** (Playing=go / Lost=cancel / **Lobby(จาก terminal)=re-arm**) | ownership ชัด: GameManager ถือ state+registry / WaveManager ถือ wave execution. ไม่มีใครเอื้อมเข้า loop จากข้างนอก |
| 6 | reset อยู่ชั้นไหน | **3 ชั้น: `GameManager.RestartMatch` → loop เรียก `PlayerController.ResetForReplay()` (hub) → component `ResetForReplay()`** | co-location: เจ้าของ state รู้ค่า reset เอง. เพิ่ม component ใหม่วันหลัง แก้แค่ `PlayerController.ResetForReplay` (จุดเดียวที่ wire component อยู่แล้ว) ไม่แตะ GameManager → กันลืม |
| 7 | cursor ownership | **`CursorController` (MonoBehaviour) = คนเดียวที่เขียน `Cursor.*`** subscribe GameState → `Apply()` (Playing→Hide/อื่น→Show), มี `SetPaused(flag)` เผื่อ pause. ถอด `Cursor.*` ออกจาก PlayerLook + StagingController | scattered writer = last-writer-wins (ปมที่จะเจอตอน ESC/pause). single decider ที่ derive จาก "ความจริงเดียว" (GameState+pause) กันชน. MonoBehaviour ไม่ใช่ static เพราะมี subscription lifecycle (OnDestroy unsub ฟรี, static ค้างข้าม domain-reload) |
| 8 | ShopWindow ท้าย wave สุดท้าย | **ข้าม** → Won ทันที (คง revive) | เดิมรอ ready/timer 60วิ ก่อนขึ้นจอ Win + ซื้อ upgrade ที่ไม่ได้ใช้ (งง). ทุกคน Alive ตอน Won ดูดี |
| 9 | formalize transition | **per-site guard** (ตามของเดิม) + เพิ่มยาม `RestartMatch` เฉพาะ `Won/Lost` · legal-transition graph = comment ใน `GameState.cs` | 4-state ไม่คุ้มทำ matrix กลาง. ยาม `RestartMatch` กัน Play Again ผิดจังหวะ (กลางแมตช์) |
| 10 | Exit return-path | **`LobbyManager` โหลด `MainMenu` แบบ local ตอน disconnect ในเกม** (guard: ไม่โหลดถ้าอยู่ MainMenu แล้ว) | LobbyManager persist (DontDestroyOnLoad) รอด scene unload + คุม connection อยู่แล้ว. teardown (`HandleTransportDisconnect` host-all/client-self) มีอยู่แล้ว. bonus: แก้หนี้ "disconnect ในเกมค้าง" |
| 11 | ตอน Won/Lost ค้าง | **ไม่มี auto-timer** — รอ host กด. คน Dead ตอน Lost → Play Again เซ็ต Alive+full (ไม่ persist ใครตาย) | prototype ไม่ต้อง auto-return; host คุมจังหวะ |

## เส้นแบ่ง task 15 / 16

- **task 15 = กลไกทั้งหมด** — `RestartMatch`, `ResetForReplay`, re-arm loop, CursorController, exit teardown + return-to-MainMenu, skip-final-shop, guards
- **task 16 = UI ล้วน** — จอ Win/Lose + ปุ่ม Play Again/Exit (เรียกกลไก task 15) + HUD (wave/gold/class)

## รายละเอียด implementation

- **`GameManager.RestartMatch()` [Server]** — guard `State==Won||Lost` → loop `_players[i].ResetForReplay()` → `SetGameState(Lobby)`. (reset players ก่อน SetGameState → CheckLose guard `Playing` ไม่ยิงผิด)
- **`PlayerController.ResetForReplay()` [Server]** — State→Alive · Health.SetHp(Max) · `Wallet.ResetForReplay()` · `Upgrades.ResetForReplay()` · Ready.ServerSetReady(false)
- **`PlayerWallet.ResetForReplay()` [Server]** → `_gold.Value = 0` · **`PlayerUpgrades.ResetForReplay()` [Server]** → 3 level → 0
- **`WaveManager`** — subscribe `OnGameStateChanged` ครั้งเดียว (ย้ายจากใน `RunAsync` ออกมา กัน double-subscribe ตอน re-invoke):
  - `OnStartServer` → `_cts=new` → `RunArmed` (รอ GameManager → subscribe ครั้งเดียว → `RunCampaign`)
  - `RunCampaign(ct)` — รอ `Playing` → prep → per wave { SetCurrentWave · RunWave · AwardSurvivalBonus · ReviveAll · **`if i<last` ShopWindow** } → `SetGameState(Won)`
  - `HandleGameState(prev,next)`: `Lost` → cancel `_cts` + despawn (เดิม) · **`Lobby && prev∈{Won,Lost}` → RestartCampaign** (re-arm)
  - `RestartCampaign()` — `_cts.Cancel/Dispose` → `_cts=new` → `SetCurrentWave(0)` → `_alive.Clear` → `RunCampaign(_cts.Token)`
- **`CursorController`** (ใหม่, client-side MonoBehaviour บน scene object) — poll หา `GameManager.Instance` แล้ว subscribe (มิเรอร์ StagingController) → `Apply()`; `Show`/`Hide` เป็น funnel ภายใน; `SetPaused(bool)`
- **`PlayerLook`** — ลบทุกบรรทัดแตะ `Cursor.*` (lock ตอน Playing + unlock `OnStopClient`). เหลือ: อ่าน look input เฉพาะ Playing
- **`StagingController`** — ลบ block force `Cursor.lockState=None`
- **`LobbyManager`** — `[SerializeField] SceneReference _mainMenuScene`; ทั้ง 2 disconnect path (intentional `HandleTransportDisconnect` + unintentional `OnClientConnectionState`) → `ReturnToMainMenuIfInGame()` = ถ้า active scene ≠ MainMenu → `SceneManager.LoadScene(MainMenu)`

## Legal transition graph (documentation, per-site enforced)

```
Lobby   → Playing   (WaveManager.TryStartMatch: host + AllReady)
Playing → Won       (RunCampaign: last wave cleared)
Playing → Lost      (GameManager.CheckLose: none Alive)
Won/Lost→ Lobby     (GameManager.RestartMatch: host Play Again)   ← task 15
(any    → teardown  = ออกจาก scene ทั้งหมด ไม่ใช่ transition ใน enum)
```

## ขอบเขต — task นี้ *ไม่* ทำ

result screen UI + ปุ่ม (task 16) · HUD wave/gold/class (task 16) · menu-lobby แบบคงห้อง (B, ต้อง networked-menu) ·
pause menu (CursorController มี hook `SetPaused` รอไว้ แต่ไม่ทำ) · auto-return timer · vote-to-restart ·
per-class stat (`ClassData` follow-up) · Direct IP (task 16)

## เตรียมให้ต่อ

- **task 16:** result screen subscribe `OnGameStateChanged` (Won/Lose banner) · ปุ่ม Play Again → `GameManager.Instance.RestartMatch()` (host) · ปุ่ม Exit → `LobbyManager.Instance.HandleTransportDisconnect()` · HUD subscribe `OnWaveChanged`/`OnGoldChanged`/`OnClassChanged`
- **pause menu (post-MVP):** เรียก `CursorController.SetPaused(true)` — ไม่แตะ `Cursor.*` เอง

## Runtime verified (host, MCP)

host flow (Tugboat loopback → `LoadGlobalScenes(SampleScene)`), player parked kinematic y=40, bulk-kill enemies:

- **Cursor (single owner ทุก state):** Lobby=`None` · Playing=`Locked` · Won=`None` · Lost=`None` — CursorController derive จาก GameState ถูกทุก transition ✓
- **Won ถึงจริง:** วิ่ง wave 1→5 (bulk-kill) → wave 5 เคลียร์ → `Won` (board ว่าง killed=0) ✓
- **Play Again จาก Won (`RestartMatch`) — reset ครบทุกช่อง:** ก่อนกด (gold=2940, dmgLv=3, hp=60) → หลังกด `Lobby` · cursor `None` · **gold=0 · dmgLevel=0** · hp=100/100 · life=Alive · ready=False · currentWave=0 ✓
- **re-arm × 2 replays (V3):** round2 (หลัง Won) → Playing → wave 1 spawn 5 (`sceneEnemies==internal_alive==5` = loop เดียว ไม่ซ้อน) · round3 (หลัง Lost) → Playing → wave 1 spawn 5 เท่ากัน → **ยืนยันไม่มี double-subscribe/double-loop ข้าม replay** (subscription อยู่ใน RunArmed ครั้งเดียว, RestartCampaign ไม่ re-sub) ✓
- **Lost + Play Again จาก Lost (V4):** down player (คนเดียว) → `Lost` (event-driven) · enemies **despawn เกลี้ยง** · Play Again → `Lobby` · **Downed→Alive+full 100/100** (ไม่ persist ใครตาย) · wave=0 ✓
- **guard `RestartMatch` (V5):** เรียกตอน `Playing` → no-op คง `Playing` ✓
- **Exit teardown (V8):** host `HandleTransportDisconnect` → server/host stop + **activeScene → `MainMenu`** (return-path ทำงาน, แก้หนี้ disconnect-ค้าง) ✓
- compile clean (0 error) · console **0 error** ตลอด run (1 warning benign)

**ยัง NOT verified (single-host harness):** multi-peer — GameState/reset/cursor SyncVar propagate ไป client จริง, host-teardown เตะ remote client, client-self-leave (`ClientManager.StopConnection`) · owner กดปุ่ม result UI จริง (= task 16) · **skip-final-shop** พิสูจน์ by-construction (`if i<WaveCount-1`) + Won มาทันทีหลังเคลียร์ wave 5 (shopTimer 0.5s ตอนเทสต์ยังไม่ตัดขาดจาก timer เป๊ะ — play-test ยืนยันอีกที)
