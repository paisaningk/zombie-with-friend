# 0015 — In-game UI (Result + HUD + Shop)

> Task 16a (P5) · ตัดสิน 2026-08-02 (ผ่าน grill session) · สถานะ: **นำไปใช้แล้ว · verified (host, MCP)**

บันทึกว่า "ทำไมโค้ดถึงเป็นรูปนี้" เพื่อกันรื้อผิดทีหลัง — ถ้าจะเปลี่ยนโครง อ่านนี่ก่อน

## บริบท

Task 16 เดิม = "Result + IP + art" (bundle). ซอยเอาเฉพาะ **in-game UI cluster** (Result + HUD + Shop) ที่
ทำให้ loop เล่นได้จริง (ตอนนี้ยิงแล้วไม่เห็น HP/gold, ซื้อของไม่ได้, ไม่มีจอจบ). Direct IP + art = task ต่อไป

กลไกพร้อมหมดจาก task ก่อน: `RestartMatch`/`HandleTransportDisconnect` (task 15), `CmdBuy` (task 12b),
`CmdSetReady` (task 12a), event/accessor ครบทุก stat. เหลือแค่วาด UI ต่อสาย

## การตัดสินใจ (grill)

| # | ประเด็น | เลือก | เหตุผลย่อ |
|---|---|---|---|
| 1 | scope | **in-game UI เท่านั้น** (Result + HUD + Shop) — Direct IP + art แยก | 3 ก้อนนี้ coupling แน่น (reuse StagingController pattern) + ทำให้ loop เล่นได้. IP=networking คนละเรื่อง, art=cosmetic |
| 2 | สถาปัตย์ | **แยก controller ต่อ overlay** (HUD/Shop/Result) + คง StagingController | user เลือก B — "1 ไฟล์ = 1 concern" ชัด |
| 3 | HUD tech | **SOAP** (Obvious Soap, เพิ่งลง) สำหรับ HUD display · Shop/Result = code-built uGUI | SOAP เปล่งประกายที่ data-binding (หลอด/ตัวเลข = 0 บรรทัด update). ปุ่ม interactive (Shop/Result) SOAP ช่วยน้อย → code-built onClick ตรงกว่า |
| 4 | SOAP↔network | **owner-only publisher bridge ดัน SyncVar → SO variable** | SOAP network-agnostic (SO เป็น local asset ไม่ sync). data authoritative อยู่ SyncVar → ต้องมี bridge อ่าน event/poll แล้วเขียน SO. เขียนเฉพาะ `IsOwner` (HUD = local player) |
| 5 | ShopOpen client-visibility | **promote `GameManager.ShopOpen` → `SyncVar<bool>` + `OnShopOpenChanged`** | decision 0011 เขียนไว้แล้วว่าถ้า client ต้องรู้ → SyncVar. ตอนนี้ Shop UI (client) ต้องรู้ว่าร้านเปิด → ถึงเวลา |
| 6 | Shop หา local player | **poll `FindObjectsByType<PlayerController>`+IsOwner (mirror StagingController)** — ไม่เพิ่ม static `Local` | consumer เดียว (Result ไม่ต้อง, HUD ใช้ owner publisher) → static Local ไม่คุ้ม YAGNI |
| 7 | HUD elements | 6: HP(หลอด) · Ammo · Gold · Wave · Class · Ability cooldown(Support เท่านั้น) | ชุดมาตรฐาน FPS co-op, ราคาเท่ากันด้วย SOAP |
| 8 | composite text | ammo "27/30" + wave "Wave 3/5" = **StringVariable ที่ publisher format** | `BindTextMeshPro` bind ได้ค่าเดียว (Prefix/Suffix) — 2 ค่ารวมกัน bind ตรงไม่ได้ → publisher format string ก่อนเขียน |
| 9 | health bar max | **Hud_Health = Normalized (0..1)**, BindFillingImage max=1 | เลี่ยง FloatVariable.Max juggling (max HP โตได้จาก upgrade). `PlayerHealth.Normalized` มีอยู่แล้ว |

## รายละเอียด implementation

- **`GameManager`** — `ShopOpen` bool → `SyncVar<bool>` (private) + `OnShopOpenChanged` event (dedupe/initial-fire แบบ GameState) + `SetShopOpen` เขียน SyncVar. `RestartMatch`/reset ไม่แตะ (WaveManager คุมเปิด/ปิด)
- **`PlayerWeapon`** — +`public int MagazineSize` (ammo "x / y" ใน HUD)
- **`HudPublisher`** (ใหม่, `NetworkBehaviour` บน Player.prefab, owner-only) — Update (owner): poll 6 ค่า เขียน SO **เมื่อเปลี่ยน** (guard equality กัน event spam). SO refs serialized. clear/zero SO ตอน `OnStopClient` (owner) กัน HUD ค้างข้าม despawn
- **SO assets** (`Assets/Data/HUD/`): `Hud_Health`(Float 0..1) · `Hud_Gold`(Int) · `Hud_Ammo`(String) · `Hud_Wave`(String) · `Hud_Class`(String) · `Hud_Cooldown`(Float) · `Hud_IsSupport`(Bool, ซ่อน cooldown ถ้าไม่ใช่ Support)
- **HUD canvas** (SampleScene): Image+`BindFillingImage`(Hud_Health,max1) · TMP+`BindTextMeshPro` ต่อ Gold/Ammo/Wave/Class · cooldown Image/TMP+Bind(Hud_Cooldown) — code update UI = 0 บรรทัด
- **`ShopController`** (ใหม่, client-side scene MonoBehaviour, code-built uGUI) — subscribe `GameManager.OnShopOpenChanged` → show/hide · 3 ปุ่ม upgrade (label cost+lv/max, poll owner `Upgrades.LevelOf`) → `CmdBuy` · ปุ่ม Ready → `CmdSetReady` · poll owner แบบ Staging
- **`ResultController`** (ใหม่, client-side scene MonoBehaviour, code-built uGUI) — subscribe `GameManager.OnGameStateChanged` → Won/Lost show · banner WIN/LOSE · host(`IsServerStarted`): Play Again→`RestartMatch` / Exit→`HandleTransportDisconnect` · client: Exit + "waiting for host"

**cursor:** ทั้ง 3 จอโผล่ตอน state ที่ CursorController ปลดเมาส์อยู่แล้ว (Shop = Playing+ShopOpen → เมาส์ lock อยู่! → ต้องปลดตอน ShopOpen — ดูด้านล่าง)

## ปมที่ต้องระวัง — cursor ตอน Shop

Shop เปิดตอน `GameState.Playing` (ShopOpen=true) แต่ CursorController lock เมาส์ตอน Playing → กดปุ่ม shop ไม่ได้.
แก้: **CursorController เพิ่มเงื่อนไข — free เมาส์เมื่อ `ShopOpen`** (derived เพิ่มจาก GameState). rule ใหม่:
เมาส์ lock ⟺ `Playing && !ShopOpen && !paused`. reuse `SetPaused` pattern (single decision point `Apply()`)

## ขอบเขต — task นี้ *ไม่* ทำ

Direct IP connect UI (task 16b) · placeholder art / Synty models (task 16c) · ระบบชื่อผู้เล่น · shop timer countdown UI (มี timer ใน logic แล้ว, ไม่โชว์นับถอยหลัง) · damage-taken feedback / hitmarker · settings menu · SOAP กับ Shop/Result (interactive → code-built)

## เตรียมให้ต่อ

- **task 16b:** Direct IP — `JoinLobby` รับ IP จริง (เลิก hardcode 127.0.0.1), Join panel wire
- **task 16c:** art pass — Synty models แทน capsule/cube
- **pause menu (post-MVP):** `CursorController.SetPaused(true)` (hook พร้อม)

## Runtime verified (host, MCP)

host flow (Tugboat loopback → SampleScene), player parked kinematic y=40:

- **HUD end-to-end (SOAP: SyncVar→publisher→SO→Bind→UI):** อ่าน TMP text จริง — Gold "Gold: 0" · Class "Support" · Wave "Wave 0 / 5" · Ammo "Ammo  30 / 30" · Cooldown "CD 0s" · HealthFill.fillAmount=1 (normalized→max1) ✓
- **HUD reactive:** SetClass Gunner → Class "Gunner" + **cooldown widget ซ่อน** (BindActiveToBool←Hud_IsSupport) · gold +250 → "Gold: 250" · dmg 40 → HealthFill **0.6** ✓
- **ShopOpen→SyncVar:** SetShopOpen(true) → ShopPanel active · **cursor=None** (CursorController free ตอน Playing+ShopOpen) · PlayerLook freeze ✓
- **Shop UI:** 3 ปุ่ม "DAMAGE+/MAXHP+/FIRERATE+  Lv 0/5 — $150" (poll owner + ShopData) · gold display · **ซื้อผ่านปุ่ม** (onClick→CmdBuy): gold 1100→950, maxHpLevel 0→1 · **Ready ปุ่ม** → IsReady=True, label→"Cancel Ready" · HUD gold reactive → "Gold: 950" ✓
- **Result:** force Won → ResultPanel active, banner **"VICTORY"**, PlayAgain visible (host), cursor None · force Lost → banner **"DEFEAT"** · กลับ Playing → ResultPanel hidden ✓
- compile clean (0 error) · console **0 error** (1 warning benign)

**ยัง NOT verified (single-host harness):** multi-peer — SO variable เป็น per-client (แต่ละเครื่องโชว์ player ตัวเอง), SyncVar (ShopOpen/gold/class) propagate ไป client จริง, shop roster "N/N ready" ข้ามเครื่อง · การ render/คลิก uGUI จริงด้วยเมาส์ (verify ผ่าน `onClick.Invoke` + direct call — play-test โดยผู้ใช้)
