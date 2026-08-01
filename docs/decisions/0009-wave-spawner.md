# 0009 — Wave Spawner + Auto-revive

> Task 10 (P3) · ออกแบบ + build + **verified 2026-08-01** ✅

## บริบท

Task 10 = wave progression: spawn enemy เป็น wave, ตรวจเคลียร์, auto-revive ระหว่าง wave, ชนะเมื่อครบ 5 wave.
ต่อยอดจาก task 9 (enemy 3 types + `Enemy.OnDied` hook) และ task 8 (`GameManager` + `GameState`).

## การตัดสินใจ (grill)

| # | ประเด็น | เลือก |
|---|---|---|
| 1 | โครง | **`WaveManager` แยก** (NetworkBehaviour, scene NetworkObject, server-auth) เรียก `GameManager.SetGameState(Won)` ตอนจบ · bundle กับ GameManager เป็น prefab `GameManagers` |
| 2 | data | **`WaveData` (SO) เดียวถือครบ 5 wave** (`Wave[] waves`) · อ้าง enemy ด้วย **prefab** |
| 3 | โมเดล wave | **weighted pool + งบรวม + minimum การันตี + max alive (เติมทดแทน)** — Option B |
| 4 | spawn point | **4 empty GameObject ใน scene** (ขอบ arena, ในเขต A*) · เลือกจุด **สุ่มต่อตัว** |
| 5 | นับ clear | **`List<Enemy> _alive`** + spawn queue · ตาย→เอาออก · clear = queue ว่าง && list ว่าง · **`Enemy.OnDied` → `Action<Enemy>`** |
| 6 | ระหว่าง wave | **auto-advance** (revive → delay → wave ถัดไป) · seam `StartNextWave()` ให้ task 12 เสียบ shop/ready · อยู่ใน `Playing` ตลอด |
| 7 | auto-revive | **ทุกคนรวม Dead** → `SetState(Alive)`+`SetHp(Max)` · guard `GameState==Playing` |
| 8 | เริ่ม wave 1 | รอ `GameState==Playing` (ผูก GameManager) + prep delay |
| 9 | sync | **NetworkBehaviour + `SyncVar<int> currentWave`** + `OnWaveChanged` (dedupe/initial-fire แบบ GameManager) → HUD/result |
| 10 | async | **UniTask** (loop เป็นลำดับ) · cancel ผ่าน CTS ใน OnStopServer |
| 11 | แพ้กลางคลื่น | state→`Lost` → **cancel loop + despawn enemy ที่เหลือ** · Won ไม่ต้องจัดการพิเศษ |

## โครงสร้าง data

```csharp
[Serializable] class WeightedEnemy { NetworkObject prefab; float weight; int minCount; }
[Serializable] class Wave { WeightedEnemy[] pool; int totalCount; int maxAlive; float spawnInterval; }
class WaveData : ScriptableObject { Wave[] waves; }   // 1 asset = ทั้ง campaign (5 wave)
```

**แก้ budget ตอนเริ่ม wave:**
1. วาง `minCount` ทุกชนิด → `guaranteedSum`
2. `remaining = totalCount - guaranteedSum` → ถ้า >0 เติมด้วย weighted random จาก pool
3. Σmin > totalCount → **minimum ชนะ** (spawn Σmin) · weight 0 + min>0 = ชนิดการันตีอย่างเดียว
4. ได้ multiset → **shuffle** เป็น spawn queue

## Loop (server-only, UniTask, ct จาก CTS)

```
await WaitUntil(GameManager.Instance != null); subscribe OnGameStateChanged (Lost)
await WaitUntil(State == Playing)
await prepDelay
for i in 1..waves.Length:
    currentWave = i
    queue = ResolveQueue(waves[i])          // weighted + min + shuffle
    while queue ไม่ว่าง || _alive ไม่ว่าง:
        while _alive.Count < maxAlive && queue ไม่ว่าง:
            spawn(queue.Dequeue()) ที่จุดสุ่ม → _alive.Add + subscribe OnDied
            await Delay(spawnInterval)
        if queue ไม่ว่าง: await WaitUntil(_alive.Count < maxAlive)   // รอมีที่ว่าง
        else:            await WaitUntil(_alive.Count == 0)          // รอเคลียร์
    if State != Playing: break
    ReviveAll()                              // ทุกคน Alive + full HP
    await interWaveDelay
SetGameState(Won)
```

- **OnDied handler** (server): `_alive.Remove(e)` (+ gold hook task 12)
- **Lost handler:** `_cts.Cancel()` + despawn `_alive` ที่เหลือ (เงียบ ไม่ยิง OnDied)
- guard `IsServerInitialized` ทุก handler (event ยิงทุก peer)

## ไฟล์

**ใหม่:** `Enemies/WaveData.cs` · `Game/WaveManager.cs` · `Data/Waves/Campaign.asset` · prefab `GameManagers` · doc นี้
**แก้:** `Enemies/Enemy.cs` (`OnDied`→`Action<Enemy>`, +`[Server] DespawnByManager()`) · SampleScene (+4 spawn point, +WaveManager) · PROGRESS

## Tunables (default)
prepDelay 5s · interWaveDelay 3s · per-wave: pool/totalCount/maxAlive/spawnInterval (ยาก↑ ด้วยจำนวน+ส่วนผสม, stat share ที่ EnemyData)

## ตัด/เลื่อน
ready-gate + shop (task 12/13, seam พร้อม) · per-wave stat buff (scale ด้วยจำนวน/ส่วนผสมพอ) · wave HUD UI (task 16, SyncVar พร้อม) · gold on kill (task 12, hook `OnDied`)

## Runtime verified (host, MCP)

Campaign 5 wave: W1 `Runner×5` · W2 `total10/maxAlive6 [Runner min6 w3, Tank min1 w1]` · W3 `16/7 [+Ranger]` · W4 `24/8` · W5 `36/9`

- **wave loop:** Playing → prep(5s) → wave 1 spawn (log A* pathfinding เพียบ) · currentWave SyncVar เดิน **1→2→3→4→5** ตาม clear
- **composition:** W1 = Runner×5 เป๊ะ · W2 เห็น **Tank โผล่** (guaranteed `minCount` + weighted refill)
- **maxAlive cap:** W2 มี 6 alive แม้ total=10 (cap ทำงาน) · ตาย→**เติมทดแทน**จาก budget ที่เหลือ
- **wave clear → auto-revive:** ตั้ง player HP=60 ก่อน clear → หลัง clear **HP=100** (heal-to-full) · currentWave→2
- **Won:** เคลียร์ W5 → `GameManager.SetGameState(Won)` ✅
- **Lost handling:** ปล่อย enemy ฆ่า AFK host player → Lost → **WaveManager cancel loop + despawn enemy ที่เหลือหมด** (0 ตัว) ✅

## ยัง NOT verified
- **multi-peer จริง** (currentWave/enemy pose ไป client, revive ฝั่ง client)
- **revive จาก Downed/Dead จริง** (เทสต์ heal-to-full ด้วยการ down HP ไม่ถึง 0; SetState(Alive) path verify แล้ว task 6/7 — down จริงคนเดียว = Lost sticky, ต้อง 2 player)
- ผู้เล่นยิงจริง (bulk-kill ผ่าน `ApplyDamage` = path เดียวกับ hitscan `ReceiveHit`)
