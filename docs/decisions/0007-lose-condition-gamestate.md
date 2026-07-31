# 0007 — Lose condition + GameState

> Task 8 (P2) · ตัดสิน 2026-07-31 (ผ่าน grill session) · สถานะ: **นำไปใช้แล้ว · verified (host)**

บันทึกว่า "ทำไมโค้ดถึงเป็นรูปนี้" เพื่อกันรื้อผิดทีหลัง — ถ้าจะเปลี่ยนโครง อ่านนี่ก่อน

## บริบท

GameManager (task 1) มี registry + `AnyPlayerAlive()` stub. Task 8 = lose condition: event-driven —
state→Downed/Dead แล้วเช็ค ไม่มีใคร Alive → แพ้ทันที (ไม่รอ timer). task 1 flag ไว้ว่า guard ต้องใช้
"GameState==Playing" → task 8 introduce GameState.

## การตัดสินใจ

| # | ประเด็น | เลือก | เหตุผลย่อ |
|---|---|---|---|
| 1 | detection | GameManager **subscribe `player.OnStateChanged`** ตอน RegisterPlayer (unsub ตอน Unregister) → transition → `CheckLose()` (server) | ใช้ registry + PlayerState event ที่มี · event-driven ไม่ poll |
| 2 | GameState | **`GameState.cs` enum {Lobby,Playing,Won,Lost}** · GameManager: `SyncVar<GameState>` + `OnGameStateChanged(prev,next)` (dedupe/initial-fire แบบ PlayerState) + `[Server] SetGameState` · client อ่านได้ | Playing land ที่ task 8 ตามที่ task 1 บอก · Lobby ใส่ค่าไว้กัน enum churn |
| 3 | lose event | **ไม่มี `OnGameOver` แยก** — lose = `SetGameState(Lost)` → `OnGameStateChanged` ยิง | Lost/Won ใช้ event เดียว, result screen (task 16) subscribe อันเดียว |
| 4 | lose guard | **`Count>0 && none-alive`** (ไม่ใช่แค่ `!AnyPlayerAlive()`) | `!AnyPlayerAlive()` เป็น true ตอน Count==0 ด้วย (เกมว่าง/ยังไม่ spawn) = ไม่ใช่ทีมล้ม → กันแพ้ผิด |
| 5 | disconnect | **re-check lose ตอน Unregister** | คน Alive คนสุดท้ายหลุด (ไม่ยิง OnStateChanged) เหลือแต่ downed → ควรแพ้ |

## รายละเอียด implementation

- `OnStartServer` → `SetGameState(Playing)` (SampleScene = กำลังเล่น; task 15 ค่อยขับ Lobby→Playing จริง)
- `RegisterPlayer`: add + `player.OnStateChanged += HandlePlayerStateChanged` · `UnregisterPlayer`: unsub + `CheckLose()`
- `CheckLose` [Server]: `if GameState != Playing return; if Count>0 && !Any(IsAlive) → SetGameState(Lost)` — idempotent (Lost แล้ว guard Playing กันยิงซ้ำ; sticky: revive หลัง Lost ก็คง Lost)
- `SetGameState` = choke point เดียว (no-op ถ้าค่าเดิม) · GameState SyncVar → `Notify` (dedupe host double-fire, initial fire) → `OnGameStateChanged`
- subscribe ทำฝั่ง server (RegisterPlayer [Server]) → handler รันบน server → CheckLose server-only

## ขอบเขต — task นี้ *ไม่* ทำ

win (task 10 → `SetGameState(Won)`) · Lobby state + Lobby→Playing transition + scene/UI (task 15) · result screen / lose UI + กลับ lobby (task 16) · restart

## Runtime verified (host, MCP)

- server-start → **GameState=Playing** · player Alive → Playing (ไม่ lose พร่ำเพรื่อ), AnyPlayerAlive=True
- **ApplyDamage(100) → player Downed (คนเดียว) → GameState=Lost** (event-driven, instant) · AnyPlayerAlive=False
- **sticky:** revive (SetState Alive) หลัง Lost → State=Alive แต่ **GameState คง Lost** (CheckLose guard Playing) ✓
- compile clean

**ยัง NOT verified:** disconnect-triggered lose จริง (by-construction — Unregister เรียก CheckLose เดียวกัน) · empty-guard Count==0 (by-inspection) · multi-peer · OnGameStateChanged sync ไป client จริง
