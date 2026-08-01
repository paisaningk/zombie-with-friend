# 0012 — Support Heal Pulse (+ PlayerClass foundation)

> Task 13 (P4) · ตัดสิน 2026-08-01 (ผ่าน grill session) · สถานะ: **นำไปใช้แล้ว · verified (host, MCP)**

บันทึกว่า "ทำไมโค้ดถึงเป็นรูปนี้" เพื่อกันรื้อผิดทีหลัง — ถ้าจะเปลี่ยนโครง อ่านนี่ก่อน

## บริบท

Support class มี ability เดียว: **Heal Pulse รอบตัว, cooldown 15 วิ** (CLAUDE.md). Gunner ไม่มี ability.
ต้องใช้ action pattern มาตรฐาน: `[SyncVar] cooldownEndTime` + `ServerRpc` (trigger/validate) + `ObserversRpc` (effect).

**ปัญหา dependency:** ability เป็น class-based แต่ **class-select เป็น task 14** และ codebase ยัง**ไม่มี** concept ของ class เลย
(ยืนยันด้วย grep — ไม่มี enum/SyncVar/field). และยัง**ไม่มี** input action สำหรับ ability
(`Move/Look/Attack/Reload/Interact/Crouch/Jump/Sprint` เท่านั้น). สองอันนี้ต้องวางใน task นี้.

## การตัดสินใจ

| # | ประเด็น | เลือก | เหตุผลย่อ |
|---|---|---|---|
| 1 | class มายังไง | **เพิ่ม `PlayerClassType` enum + `PlayerClass` component (SyncVar) ตอนนี้** | ability เป็น class-based แท้ๆ — gate คือหัวใจ. enum เล็ก (mirror PlayerState) → task 14 แค่เสียบ UI ไม่ต้องรื้อ. (ไม่ทำ generic-แล้ว-gate-ทีหลัง / ไม่ใช้ placeholder field) |
| 2 | heal ให้ใคร (**C-1**) | **Alive ใน radius (รวมตัวเอง) → heal +40** · **Downed → ปลุก (Alive + 15% Max)** · **Dead → ข้าม** | รวมตัวเอง = support ไม่ต้องพึ่งคนอื่น. ปลุกได้แต่ HP ต่ำ (<manual 30%) → `PlayerReviver` ยังมีค่า ไม่ทับกัน. Dead = spectate จน wave จบ (MVP) |
| 3 | server หาเป้า | **เดิน `GameManager.Players` (registry)** filter ระยะ+state | heal เป็น server-auth ล้วน — registry มี PlayerController + State/Health ตรงๆ ไม่ต้อง physics. (ต่างจาก Reviver ที่ **client** หาเป้า เลยต้อง OverlapSphere) |
| 4 | cooldown | **`SyncVar<double> _cooldownEndTime` ด้วย FishNet network time** (`TimeManager.TicksToTime(TickType.Tick)`) | ตาม action pattern ใน CLAUDE.md — วาง template ให้ ability ตัวหลัง. network time (ไม่ใช่ `Time.time` เฉพาะเครื่อง) → UI cooldown bar (task 16) อ่านได้ถูกข้าม peer. cd 15 วิ ผู้เล่นอยากเห็นเวลาเหลือจริง (ต่างจาก fire-rate 0.1 วิ) |
| 5 | input | **action ใหม่ `Ability` = `Q`, `WasPressedThisFrame`** | Q มาตรฐาน shooter/MOBA, ไม่ชน E(Interact)/R(Reload)/Shift/Space. ตั้งชื่อตามความหมาย ไม่ repurpose Crouch/Jump. instant cast = pressed-this-frame ไม่ใช่ hold |
| 6 | ค่า | heal **40** · revive **15%** Max · radius **6m** · cooldown **15s** — serialized บน component | ปรับ balance ง่าย. heal flat เดาง่าย / revive %Max scale ตาม MaxHP upgrade (เหมือน 0006). → ย้าย `ClassData` SO ตอน task 14 |
| 7 | effect | **A+B: ObserversRpc spawn ทรงกลมขยาย placeholder (code-only) + `Debug.Log`** | เห็น radius จริงตอน play-test + log ช่วย verify ผ่าน MCP. code-only ไม่ต้องเตรียม asset (เหมือน tracer ของ PlayerWeapon). particle จริง = task 18 |
| 8 | โครง | **แยก `PlayerClass` + `HealPulseAbility` (namespace `Player`) + เสียบ hub** | class เป็นข้อมูลระดับ player (task 14 lobby / maxHP / weapon จะอ่าน) ไม่ใช่ของ ability คนเดียว → แยกแล้วแก้/ต่อยอดง่าย |
| 9 | gate ตอน Downed/Dead | **ability self-guard `IsAlive`** (เหมือน `PlayerReviver`) + เพิ่ม `PlayerController.IsAlive` (delegate `PlayerState`, **ไม่ใช่ hp>0**) | Reviver precedent = owner-driven ability self-guard, ไม่อยู่ในชุด disable ของ controller → คง rule 0005 สะอาด. `hp>0` **ผิด** (Downed=hp0≠Dead; C-1 ปลุกด้วย SetHp ก่อน SetState) → source of truth คือ enum เสมอ |
| 10 | SyncVar visibility | **`PlayerClass` = all-read, default `Support`** · **`_cooldownEndTime` = owner-only** | class = team info (โชว์ class เพื่อน task 16) เหมือน PlayerState/Ready. default Support = ยิงเทสต์ได้ทันที. cooldown = ส่วนตัว เจ้าของเห็นพอ (เหมือน Wallet/Upgrades) |

## รายละเอียด implementation

- **`PlayerClassType`** — enum `{ Gunner = 0, Support = 1 }` (ไฟล์แยก, mirror `PlayerLifeState`)
- **`PlayerClass`** — NetworkBehaviour, `SyncVar<PlayerClassType>` (all-read), default `Support` ใน `OnStartServer`, `[Server] SetClass()` (no-op guard), `OnClassChanged` event (init-fire + dedupe แบบ PlayerState), props `Current`/`IsSupport`/`IsGunner`
- **`HealPulseAbility`** — NetworkBehaviour:
  - refs: `PlayerState`/`PlayerClass` (GetComponent ใน Awake), serialized `_healAmount=40`/`_reviveHpPercent=0.15`/`_radius=6`/`_cooldownSeconds=15`/`_effectDuration`
  - `SyncVar<double> _cooldownEndTime` (owner-only) + `CooldownRemaining` prop (สำหรับ UI task 16)
  - owner input (`InputSystem_Actions`, สร้าง `OnStartClient` ถ้า owner, dispose `OnStopClient`) — เหมือน Reviver/Weapon
  - `Update` (owner): `Ability.WasPressedThisFrame()` + `CanCastLocally()` (IsAlive + IsSupport + cooldown หมด) → `CmdHealPulse()`
  - `[ServerRpc] CmdHealPulse()` → `ServerCast()` (re-validate ทุกอย่าง: Support + Alive + cooldown) → set `_cooldownEndTime = Now + cd` → เดิน `GameManager.Instance.Players` filter `sqrMagnitude <= radius²`: Alive → `Health.SetHp(Current+heal)` · Downed → `State.SetState(Alive)` + `Health.SetHp(Max*percent)` · Dead → ข้าม → `RpcPlayEffect(pos)`
  - network time: `base.TimeManager.TicksToTime(FishNet.Managing.Timing.TickType.Tick)`
  - `[ObserversRpc] RpcPlayEffect(Vector3)` → spawn primitive sphere scale 0→2·radius over `_effectDuration` แล้ว Destroy (local visual ล้วน) + `Debug.Log`
- **`PlayerController`** — +accessor `Class`/`Ability` (cache Awake) + `public bool IsAlive => State != null && State.IsAlive`
- **input** — เพิ่ม action `Ability` (Button) + binding `<Keyboard>/q` ใน `.inputactions` → regenerate wrapper (`InputSystem_Actions.cs`)
- **wiring (MCP):** เพิ่ม `PlayerClass` + `HealPulseAbility` ลง `Player.prefab` (NB ตัวที่ 14–15)

## ขอบเขต — task นี้ *ไม่* ทำ

UI cooldown bar / class-select UI (task 14/16) · ability ตัว 2 · `ClassData` SO (task 14) · particle/asset จริง (task 18) ·
`SetClass` จาก lobby (task 14) · self-revive ผ่าน pulse (Downed caster กดไม่ได้ — component disabled) ·
sync cooldown ให้ทั้งทีม (owner-only พอ)

## เตรียมให้ต่อ

- **task 14:** lobby class-select เรียก `PlayerClass.SetClass()` · ย้าย heal/maxHP/weapon values → `ClassData` SO (จุดเดียวกับ 0002 note)
- **task 16:** cooldown bar อ่าน `HealPulseAbility.CooldownRemaining` · แสดง class เพื่อน (`PlayerClass.OnClassChanged`, all-read พร้อม)

## Runtime verified (host, 2 players via MCP)

ทดสอบด้วย host + spawn player 2 (server-owned) park ที่ y=40 (กัน wave enemy), cancel wave loop (`_cts`) กัน interference:

- **spawn baseline:** class = **Support** (default), `CooldownRemaining=0`, HP=Max=100 ✓
- **self + ally heal (Q1/Q2/Q3):** caster casts → ตัวเอง 60→**100 (clamp Max)** + ally 50→**90** (+40 flat, registry radius 6m) ✓
- **cooldown (Q4):** cast แรก set `CooldownRemaining=15.0` เป๊ะ · cast ซ้ำทันที (on cooldown) → **rejected** (HP คงเดิม) ✓ · ally `CooldownRemaining=0` (cooldown per-caster owner-only) ✓
- **revive downed (C-1):** ally **Downed** (hp0) → หลัง pulse = **Alive + hp 15 (=Max×15%)** ✓ — ต่ำกว่า manual revive 30% ตามดีไซน์
- **Dead skip:** ally **Dead** (hp20) → หลัง pulse = **คง Dead, hp20** (ข้าม ไม่ heal/ไม่ปลุก) ✓
- **non-Support reject:** `SetClass(Gunner)` → cast → **rejected** (hp คง 60) ✓ (server re-validate class)
- **Downed caster reject (Q9):** caster **Downed** → cast → **rejected** (คง Downed hp0, ไม่ self-revive) — Alive-guard ทำงาน ✓
- **effect (Q7):** `RpcPlayEffect` ObserversRpc ยิงผ่าน FishNet RPC pipeline จริง (`RpcReader___Observers_RpcPlayEffect`) → log `[HealPulse] pulse at (0,40,0) (r=6)` เห็น 10 ครั้ง ✓
- compile clean (Assembly-CSharp + input wrapper regenerate `Ability` action) ✓

**ยัง NOT verified (single-host harness):** multi-peer จริง — class SyncVar (all-read) propagation ไป client, `_cooldownEndTime` (owner-only) บน remote owner, `CooldownRemaining` ที่ใช้ network time ข้ามเครื่อง · owner กด **Q** จริง (inject input ไม่ได้ → verify ผ่าน `ServerCast` path; client-gate เป็น UX ล้วน server เป็น authority) · `PulseVisual` ทรงกลมขยาย (spawn/destroy by-construction — log ยืนยัน RPC ถึงแล้ว)
