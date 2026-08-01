# 0010 — Currency (split-on-kill + survival bonus)

> Task 11 (P4) · ออกแบบ + build + **verified 2026-08-01** ✅

## บริบท

Gold ต่อผู้เล่นสำหรับ shop (task 12). enemy ตาย → แบ่งเท่ากันทุกคน connected (ไม่ track kill credit)
+ survival bonus จบ wave. ต่อจาก task 10 (`Enemy.OnDied`, `WaveManager`, `GameManager.Players`).

## การตัดสินใจ (grill)

| # | ประเด็น | เลือก |
|---|---|---|
| 1 | gold เก็บที่ไหน | **`PlayerWallet` component** (mirror PlayerHealth) — ไม่ยัดใน PlayerState (pure state holder) |
| 2 | reward source | **`goldReward` ใน EnemyData** (ต่อ type) + `Enemy.GoldReward` · Runner 10/Tank 30/Ranger 20 |
| 3 | ใครแบ่ง | **`GameManager.AwardGoldForKill(reward)`** (เจ้าของ registry) · WaveManager forward |
| 4 | แบ่งไม่ลงตัว | **floor ทิ้งเศษ** (เท่ากันเป๊ะ) · guard N>0 |
| 5 | sync visibility | **`SyncVar<int>` `ReadPermission.OwnerOnly`** — gold ส่วนตัว, เข้ากับ future kill-credit |
| 6 | lifecycle | เริ่ม **0** (OnStartServer) · persist ข้าม wave · fresh ต่อแมตช์ (per-scene teardown) |
| 7 | survival bonus | จบ wave คน **Alive** ได้ **+200** (tunable) · **ก่อน `ReviveAll`** (คนล้มไม่ได้) · `GameManager.AwardSurvivalBonus(amount)` |
| 8 | เข้าถึง component | **`PlayerController` เป็น hub/facade** — expose `State/Health/Wallet/...` · `GameManager.Players` เก็บ **`PlayerController`** |

## PlayerController → hub (Q8)
component เพิ่มเรื่อยๆ (State/Health/Movement/Look/Weapon/Reviver/Wallet) → เข้าถึงด้วย GetComponent กระจาย = เลอะ.
PlayerController เป็น de-facto hub อยู่แล้ว (cache ref + react ต่อ state) → **ขยายให้ expose ref เป็น property + register เป็นตัวแทน player**
- behavior เดิมคงไว้ (gate Movement/Weapon ตาม state, bleed-out)
- **`GameManager.Players`: `List<PlayerState>` → `List<PlayerController>`**
- register ย้าย `PlayerState.OnStartServer` → `PlayerController.OnStartServer` · GameManager subscribe `pc.State.OnStateChanged`
- consumer แก้ 3 จุด: GameManager (lose `p.State.IsAlive`), WaveManager (revive `p.State/Health`), **Enemy.FindNearestAliveTarget** (`p.State.IsAlive`, `p.transform.position`)

## PlayerWallet
`SyncVar<int> gold` OwnerOnly + `OnGoldChanged` + `Gold` prop (initial-fire owner-only) + `[Server] Add(int)` / `[Server] TrySpend(int)` (เตรียม shop) · `gold=0` OnStartServer

## Flow
```
enemy ตาย → WaveManager.HandleEnemyDied(e) → GameManager.AwardGoldForKill(e.GoldReward)
          → วน Players: share = reward / N (floor), Wallet.Add(share) ทุกคน
wave clear → GameManager.AwardSurvivalBonus(_surviveBonus=200)  // เฉพาะ IsAlive
          → ReviveAll() → inter-wave delay → wave ถัดไป
```

## ไฟล์
**ใหม่:** `Player/PlayerWallet.cs` · doc นี้
**แก้:** `EnemyData.cs`(+goldReward) · `Enemy.cs`(+GoldReward, target ใช้ p.State) · `PlayerController.cs`(hub+register) · `PlayerState.cs`(ถอด self-register) · `GameManager.cs`(registry PlayerController + 2 award) · `WaveManager.cs`(hook+survival bonus+`_surviveBonus`) · Player.prefab(+Wallet) · 3 EnemyData(goldReward) · PROGRESS

## ตัด/เลื่อน
spend/shop UI (task 12, `TrySpend` พร้อม) · kill-credit (ตัด MVP, owner-only เผื่อไว้) · gold HUD (task 16, `OnGoldChanged` พร้อม) · per-wave survival bonus scaling (global ก่อน)

## Runtime verified (host, MCP)
- **wallet:** spawn `gold=0` (owner-only SyncVar) · registry `Players` = **PlayerController** (count=1)
- **kill-split (1 player → share=reward):** kill Runner → **+10** · kill Tank → **+30** (per-type `EnemyData.goldReward`)
- **survival bonus:** wave 1 clear (player Alive) → **+200** ก่อน revive (50 kills + 200 = **250**)
- **registry refactor (PlayerController hub) ไม่พัง:** register ✅ · **lose** (force player Dead → `Lost`, `AnyPlayerAlive=False`) ✅ · wave loop/revive/enemy-target ทำงานครบ

## ยัง NOT verified
- **N>1 floor-split** (share=reward/N, ทิ้งเศษ) — เทสต์ N=1 (share=reward); N>1 = integer math by-construction, ต้อง 2 peer
- **survival bonus ตัด Downed** — `AwardSurvivalBonus` filter `IsAlive` (code); down คนเดียว=Lost ทดสอบไม่ได้ ต้อง 2 peer
- **owner-only sync ไป remote client** · **TrySpend** (task 12, by-construction)
