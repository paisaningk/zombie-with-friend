# 0018 — Placeholder Art Pass

> Task 16c (P5, ปิด MVP) · ตัดสิน 2026-08-03 (grill 9 decisions) · สถานะ: **build + static verify ✅ · รอ play-review 🟡**

เลิกเป็น cube/capsule → Synty PolygonPrototype static models + แต่งฉาก ให้ "ดูเป็นเกม" โดยไม่แตะ gameplay ที่ verify มาแล้ว

## บริบท

เกมเล่นได้ครบ loop + multi-peer verified (task 1–16b) แต่ตัวละคร/enemy ยังเป็น cube, ฉากโล่ง. 16c = art pass สุดท้ายปิด MVP. Art pack: `Assets/SyntyStudios/PolygonPrototype/` (Synty prototype, URP, มี prefab พร้อม drop) — เป็น pack เดียวในโปรเจกต์

## การตัดสินใจ (grill)

| # | ประเด็น | เลือก | เหตุผลย่อ |
|---|---|---|---|
| S1 | animation | **static ไม่มี animation** | CLAUDE.md ตัด animation จาก MVP · เป้า = เลิกเป็น cube · animation = post-MVP แยก |
| A1 | scope | **Player + Enemies + Arena (light)** | characters = หัวใจ · arena แค่ dressing ไม่ใช่ level design |
| P2 | player FP | **เต็มตัวคนอื่นเห็น / ซ่อน renderer เมื่อ IsOwner** | เจ้าของได้วิว FP สะอาด ไม่มีตัวบัง · ไม่มี viewmodel (ตัด MVP) |
| Q4 | identity | Player=ถือปืน · Enemy แยก scale+pose+สี | mesh เดียว → แยกด้วยขนาด/ท่า/สี |
| Q5 | safety | **visual-only** — collider/physics/logic คงเดิม · enemy scale ที่ root เดิม | gameplay ที่ verify 16 task ไม่ขยับ · hitbox กล่องเดิม (รับได้) |
| Q6 | arena look | floor grid เทา + prop สี | ตัวละครเด่นตัดพื้น + สื่อ placeholder |
| Q7 | arena extent | floor + กำแพงขอบ + cover เบา · re-scan A* | มีบรรยากาศ ไม่บานปลาย · verify enemy nav |
| C1 | cleanup | art-only ซ่อน visual · ไม่ลบ component | ตรง Q5 · muzzle `Cube (1)` transform ต้องคง (PlayerWeapon อ้าง) |
| Q9 | verify | ผม static+asset-read · พี่ visual+2-peer+nav | ความสวย/owner-hide ข้ามเน็ต inject ไม่ได้ |

## Build-time correction: rigged → static pawn

grill Q4 เลือก rigged Character (Male_Face/Dummy) — แต่ตอน build เจอว่า **pack ไม่มี animation clip/controller เลย** (prototype pack) → rigged character ไม่มี controller = **bind pose (T-pose กางแขน) = น่าเกลียดกว่า cube**. สลับเป็น **static `SM_Pawn_*`** (single-pose MeshRenderer, ไม่มี rig = ไม่มีทาง T-pose, ตรง S1). Bonus: pose บอก role — Player/Ranger ถือปืน, Runner วิ่ง, Tank ยืน

## Mapping (build)

| | model | scale (root เดิม) | material |
|---|---|---|---|
| Player | `SM_Pawn_Weapon_Male_01` | 1.0 | default |
| Runner | `SM_Pawn_Run_Male_01` | 0.8 เล็ก | `Texture_04` |
| Tank | `SM_Pawn_Idle_Male_01` | 1.6 ใหญ่ | `Texture_07` |
| Ranger | `SM_Pawn_Weapon_Male_01` | 1.0 กลาง | `Texture_02` |

Synty model = child ชื่อ "SyntyModel" ใต้ root, เท้าที่ก้น collider (player y=-0.5, enemy y=-1) · cube renderer เดิมปิด (ไม่ลบ) · enemy scale มาจาก root prefab เดิม (ไม่แตะ = ไม่กระทบ collider/A*)

## โค้ด (2 จุด, visual-only)

- `Enemies/Enemy.cs` `CacheFlashColor` — fallback: `_flashRenderer` null/disabled → `FindVisibleRenderer()` (enabled renderer แรก = pawn). hit-flash เด้งบนโมเดลที่เห็น (cube ที่ถือ ref เดิมถูกซ่อน). `_BaseColor` (URP Lit) → Synty material รองรับ
- `Player/PlayerLook.cs` — `[SerializeField] GameObject bodyModel` (=SyntyModel) · OnStartClient เมื่อ IsOwner ปิด renderer ทั้งหมดของ bodyModel. non-owner return ก่อน = ยังเห็น body. **serialized ref (ไม่ใช่ Find magic-string)** ตามที่ user ขอ

## Arena (SampleScene)

floor grid มีอยู่แล้ว (`Global_Grid_09` บน Ground 100×100) · +4 กำแพงขอบ (cube ±20, grid mat, BoxCollider) · +6 cover (crate/barrel/barrier ใน ±12, เลี่ยง spawn corner ±15 + center) · **`AstarPath.active.Scan()`** re-scan · save. EnemySpawn ที่ ±15, ไม่โดนกำแพงทับ

## MCP note (สำหรับงาน MCP prefab ครั้งหน้า)

Synaptic interpreter รอบนี้ flaky หนัก. Pattern ที่ **reliable**:
- อ่าน/แก้ prefab structural + serialized ref → `LoadPrefabContents` + `SaveAsPrefabAsset(root, path, out ok)`
- add child / property เดี่ยว → `InstantiatePrefab` + edit + `ApplyPrefabInstance(AutomatedAction)` + `DestroyImmediate` — **single-purpose เท่านั้น** (combined add+property/remove+add → null ไม่ persist)
- **ไม่เชื่อถือ:** `renderer.bounds` บน prefab asset (hang), instance `.enabled`/`GetComponent<T>()` child (อ่านผิด/null), ternary/local-func/big-ConvertAll ใน snippet (คืน null)
- verify ทุกอย่างด้วย **asset-read** (`LoadAssetAtPath` + `GetComponentsInChildren`) ไม่เชื่อ return ของ write

## ยัง NOT verified — play-review (Q9, ผู้ใช้)

pawn โผล่แทน cube · สี 04/07/02 distinct/ดูดี · เท้าจม/ลอย (pivot pawn vs collider) · **owner-hide 2-peer** (เจ้าของซ่อน/คนอื่นเห็น) · **enemy nav หลังแต่งฉาก** (จุดเสี่ยงสุด: A* rescan + cover ไม่บล็อคทาง) · hit-flash บนโมเดล

## ตัด/เลื่อน

animation/rigged · viewmodel/hands · level-design layout · retune collider ให้ตรง silhouette · dead-script cleanup (PlayerCubeCreator/EmptyNetworkBehaviour — hygiene แยก)
