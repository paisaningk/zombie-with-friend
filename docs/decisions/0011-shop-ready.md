# 0011 — Shop + Ready-check

> Task 12 (P4) · ออกแบบผ่าน grill 2026-08-01 · **ซอยเป็น 12a (ready) + 12b (shop)**

## บริบท

ปิด core-economy loop: ระหว่าง wave เปิดร้านให้ผู้เล่นเอา gold (task 11) ซื้อ 3 upgrade
(Damage% / MaxHP / FireRate) แล้วกด Ready เพื่อขึ้น wave ถัดไป. ต่อจาก task 10 (`WaveManager`
loop, `_interWaveDelay` seam) + task 11 (`PlayerWallet.TrySpend`, `PlayerController` hub).

## การตัดสินใจ (grill)

| # | ประเด็น | เลือก |
|---|---|---|
| 1 | ตำแหน่ง ready-gate | **ระหว่าง wave เท่านั้น** (หลัง clear+revive) · Wave 1 คง `_prepDelay` auto-start |
| 2 | รอ Ready แบบไหน | **`WhenAny(ทุกคน Ready, timer)`** — ครบก่อน/หมดเวลาก่อน อันไหนก็เริ่ม (กัน AFK deadlock). `_shopTimer` serialized (default 60วิ) แทน `_interWaveDelay` |
| 3 | Ready flag เก็บที่ไหน | **component ใหม่ `PlayerReady`** — reuse กับ lobby (task 14) · pattern เดิม (concern แยก component) |
| 4 | ใครเห็น isReady | **all-read (default)** — readiness เป็นข้อมูลทีม (UI "2/4 ready" task 16) ต่างจาก gold (owner-only) |
| 5 | reset ready | server เคลียร์ทุกคนตอน**เปิด shop window** (หลัง `ReviveAll`) → กดใหม่ทุก wave |
| 6 | upgrade ติดตัวยังไง | **`PlayerUpgrades` เก็บ "level" ต่อ upgrade** (owner-only SyncVar) → **ไม่แตะ SO/prefab ที่ shared** · อ่าน stat ตอนใช้แล้วคูณ level |
| 7 | ของในร้าน | **3 stat upgrade** (Damage% / MaxHP / FireRate) · **weapon swap = post-MVP** (ตัดตาม MVP, จด backlog) |
| 8 | ตัวเลข/ราคา/cap | เก็บใน **`ShopData` SO** ต่อ upgrade: `cost` / `effectPerLevel` / `maxLevel` · default 150 / (20%,+25,15%) / cap 5 |
| 9 | ซื้อได้ตอนไหน | **เฉพาะร้านเปิด** (server บังคับ) — `GameManager.ShopOpen` (server bool) เซ็ตโดย WaveManager · purchase RPC เช็กก่อน |
| 10 | ซื้อ MaxHP | **heal current ทันที** (`SetHp(Current + delta)`) — ซื้อตอนเลือดเต็มไม่งั้นรู้สึกเปล่าประโยชน์ |
| 11 | purchase flow | `[ServerRpc] CmdBuy` → `[Server] TryBuy(type)` แยก (MCP เรียก TryBuy verify ตรงได้) |
| 12 | UI ร้าน | **เลื่อน task 16** (เหมือน gold HUD / ready UI) · verify 12b ด้วย MCP |

## กลไก upgrade (Q6) — key insight

`WeaponData`/`_maxHp` เป็น **shared** (SO ไฟล์เดียว / serialized เท่ากันทุก instance) → แก้ตรงๆ = แก้ทุกคน.
แก้โดย **ไม่แตะค่าฐาน** เก็บ level ต่อผู้เล่น แล้วคำนวณสดตอนใช้:
```
damage จริง   = weaponData.damage × (1 + damageLevel × effectPerLevel)
fireRate จริง = weaponData.Cooldown ÷ (1 + fireRateLevel × effectPerLevel)
maxHP จริง    = _maxHp + maxHpLevel × effectPerLevel
```
- SyncVar level = **owner-only** พอ: damage คิดฝั่ง server (มีค่า), fireRate client-gate = owner (มีค่า), maxHP อ่าน server+owner — ทุกจุดที่ใช้ = server หรือ owner ครบ, peer อื่นไม่ต้องรู้ (เหมือน gold)

## Flow
```
Wave clear → survival bonus → ReviveAll
  → ResetAllReady + GameManager.SetShopOpen(true)
  → 🛒 await WhenAny(ทุกคน PlayerReady.IsReady, Delay(_shopTimer))
  → GameManager.SetShopOpen(false) → Wave ถัดไป

ซื้อ: owner CmdBuy(type) → [Server] TryBuy:
  ShopOpen? → level < maxLevel? → Wallet.TrySpend(cost)? → level++ → (MaxHp: Health.SetHp(Current+delta))
```

## ไฟล์
**12a:** `Player/PlayerReady.cs` (ใหม่) · `Game/WaveManager.cs` (shop window แทน interWaveDelay) · `Player/PlayerController.cs` (+`Ready` accessor) · Player.prefab (+PlayerReady)
**12b:** `Player/PlayerUpgrades.cs` + `Shop/ShopData.cs` + `Shop/UpgradeType.cs` (ใหม่) · `Game/GameManager.cs` (+ShopOpen) · `Player/PlayerHealth.cs` (Max +BonusMaxHp) · `Combat/PlayerWeapon.cs` (damage×/fireRate÷ mult) · `Player/PlayerController.cs` (+`Upgrades`) · Player.prefab (+PlayerUpgrades) · `Data/Shop/DefaultShop.asset`
**doc:** CLAUDE.md (wave timer + weapon-swap backlog) · PROGRESS

## ตัด/เลื่อน
weapon swap (**post-MVP** — ต้องเคลียร์ projectile weapon ที่ค้าง task 5 ก่อน) · shop/ready UI (task 16) · ShopOpen → SyncVar (task 16 ถ้า client ต้องรู้) · cost-scaling (ตัด MVP, cap แทน) · per-player timer setting (task 16 UI)

## สถานะ
12a: ⬜ build · 12b: ⬜ build
