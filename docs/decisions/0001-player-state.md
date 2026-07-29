# 0001 — PlayerState / PlayerLifeState / GameManager

> Task 1 (P0) · ตัดสินใจ 2026-07-30 (ผ่าน grill session) · สถานะ: **นำไปใช้แล้ว**

บันทึกว่า "ทำไมโค้ดถึงเป็นรูปนี้" เพื่อกันรื้อผิดทีหลัง — ถ้าจะเปลี่ยนโครง อ่านนี่ก่อน

## บริบท

ต้องมี per-player state (Alive/Downed/Dead) เป็น **foundation** ที่ทุกระบบถัดไปอ้างอิง:
PlayerHealth (2), movement/combat toggle (3/5/6), revive (7), lose condition (8), wave auto-revive (10).
โปรเจกต์ใช้ FishNet modern `SyncVar<T>` (readonly field + `.OnChange` + `.Value`), แยก component เล็ก ๆ (ไม่มี god-object `Player`).

## การตัดสินใจ

| # | ประเด็น | เลือก | เหตุผลย่อ |
|---|---|---|---|
| 1 | state อยู่ที่ไหน | **component `PlayerState` แยกเดี่ยว** | build order ให้มาก่อน PlayerHealth · coupling ต่ำ · เข้ากับ pattern component เล็ก |
| 2 | ไฟล์ enum | **แยกไฟล์** `PlayerLifeState.cs` | — |
| 3 | namespace | **`namespace Player`** (ใหม่) · GameManager อยู่ `namespace Game` | เริ่ม convention ใหม่ ของเดิม global ค่อยย้าย pass เดียว |
| 4 | ค่า enum / default | `Alive=0, Downed=1, Dead=2` → **default = Alive** | spawn มาพร้อมเล่น ไม่ต้องมี pre-init state · ไม่มี None/Spectating (นั่นเป็นเรื่อง GameState) |
| 5 | ใครเขียน state | **private SyncVar + `[Server] SetState()` จุดเดียว** | choke point เดียว · guard server · ใส่ invariant/log ทีหลังง่าย · ซ่อน FishNet |
| 6 | consumer รับรู้ยังไง | **wrap เป็น C# event `OnStateChanged(prev,next)`** + props `IsAlive/IsDowned/IsDead/Current` | consumer ไม่แตะ FishNet · จัดการ host double-fire ที่เดียว · รู้ transition (prev→next) |
| 7 | init contract | **ยิง `OnStateChanged` 1 ครั้งตอนเริ่ม** (prev==next==current) | consumer มี code path เดียว (subscribe ใน Awake) ไม่ต้องเขียน init แยก |
| 8 | "player ทุกคน" | **networked `GameManager` (minimal)** ถือ registry ฝั่ง server + `AnyPlayerAlive()` stub | บ้านเดียวให้ task 8/10/15 ใช้ร่วม · เป็น NetworkBehaviour รองรับ GameState SyncVar (task 15) |
| 8b | register ยังไง | **PlayerState self-register** `GameManager.Instance.RegisterPlayer(this)` ใน OnStartServer/OnStopServer | lifecycle ตรงเป๊ะ ไม่มีทาง desync · null-guarded |
| 8c | `GameManager.Instance` | **set ทั้ง server + client** (registry ยัง guard `IsServerInitialized`) | เผื่อ task 15 client อ่าน GameState · ปลอดภัยเท่าเดิม |

## รายละเอียด implementation ที่ต้องรู้

- **Host double-fire:** `SyncVar.OnChange` ยิง 2 ครั้ง (asServer true+false) บน host → PlayerState **ignore `asServer` แล้ว dedupe ด้วยค่า** (`_notified`) → OnStateChanged ยิงครั้งเดียวต่อการเปลี่ยน 1 ครั้ง
- **SetState เป็น no-op ถ้าค่าเดิม** → ไม่ยิง event ซ้ำ
- **consumer ต้อง subscribe ใน `Awake()`** (Awake รันก่อน OnStartNetwork เสมอ) → การันตีได้รับ initial fire
- **GameManager ไม่ใช่ DontDestroyOnLoad** (ต่างจาก LobbyManager) — เป็น scene object เกิด/ตายพร้อม match, จัดการ static Instance เองใน OnStartNetwork/OnStopNetwork

## ขอบเขต — task 1 *ไม่* ทำ

ไม่แตะ HP (task 2) · ไม่ disable movement/combat · ไม่มี timer · ไม่เปลี่ยน layer (task 6) ·
ยังไม่มี `GameState` SyncVar (task 15) · `AnyPlayerAlive()` เป็น stub ยังไม่ต่อ lose logic (task 8)

## ความเสี่ยง / ยังไม่ปิด

- **Ordering:** GameManager (scene NetworkObject) ต้อง network-initialize ก่อน player spawn ไม่งั้น register ไม่ติด + warning. ปกติ scene object initialize ตอน server start ก่อน connection — แต่ต้องเข้า scene ผ่าน FishNet flow ไม่ใช่กด Play ตรง ๆ. **ยัง verify runtime ไม่ได้** จะชัดตอน task 2 / scene flow
- ถ้า `AnyPlayerAlive()` ถูกเรียกตอน 0 player → คืน `false` โดยตั้งใจ (มี guard `Count > 0`) กัน lone-host trigger Lost
