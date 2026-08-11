# 0019 — Lobby roster + ชื่อผู้เล่น + จัดหน้า LobbyPanel ใหม่

> Task **L1** (post-MVP) · ตัดสิน 2026-08-11 (ผ่าน grill session) · สถานะ: **build เสร็จ · verified (host เดี่ยว, MCP) · ยังไม่ได้ verify 2 peer**

บันทึกว่า "ทำไมโค้ดถึงเป็นรูปนี้" เพื่อกันรื้อผิดทีหลัง — ถ้าจะเปลี่ยนโครง อ่านนี่ก่อน

## บริบท

ผู้ใช้บ่นว่า "หน้า UI lobby แย่ไปหน่อย ไม่เห็นว่าใครอยู่ในห้องบ้าง" ตรวจโค้ดแล้วพบว่า **จริง**:

- `LobbyPanel` (ห้องในเมนู) **ไม่มี roster เลย** — มีแค่ชื่อห้อง / ช่อง IP / ปุ่ม Start-Copy-Exit
- `LobbyManager.OnPlayerListChanged` มีอยู่ แต่ **ไม่มีใคร subscribe** และมันยิงจาก `ServerManager.OnRemoteConnectionState` = **ฝั่ง server เท่านั้น** → client ไม่มีทางรู้ว่าใครอยู่ในห้อง
- ทั้งโปรเจกต์ **ไม่มีระบบชื่อผู้เล่น** — [0013](0013-lobby-class-ready-staging.md) ระบุ "ระบบชื่อผู้เล่นจริง" อยู่นอก scope
- `StagingController` (ในเกม) มี roster อยู่แล้ว แต่โชว์ `Player {ClientId}`

**ปมสถาปัตยกรรม (ตัวเดียวกับที่ 0013 หลบ):** menu scene **ไม่ใช่ FishNet-managed scene** — โหลดนอก networking และถูก `ReplaceOption.All` ทำลายตอนเริ่มแมตช์ → ใช้ spawned NetworkObject + SyncList ทำ roster ในเมนูไม่ได้ 0013 เลี่ยงด้วยการย้าย coordination ไป in-game staging **task นี้เปิดปมนั้นจริง** ด้วย FishNet Broadcast

## การตัดสินใจ (grill)

| # | ประเด็น | เลือก | เหตุผลย่อ |
|---|---|---|---|
| 1 | หน้าไหน | **LobbyPanel (ห้องในเมนู)** | เป็นหน้าที่ไม่มี roster เลย = ตรงกับที่ผู้ใช้บ่น (staging มีอยู่แล้ว) |
| 2 | ชื่อผู้เล่น | **พิมพ์เอง + PlayerPrefs** | `Player 1/2` บอกได้แค่ "มีกี่คน" ไม่ได้บอก "ใคร" — ไม่ตอบโจทย์. Steam name ตัดทิ้ง (MVP โฟกัส Direct IP) |
| 3 | roster ถืออะไร | **presence + ชื่อ + HOST badge เท่านั้น** | ready/class คงอยู่ที่ staging — **เคารพ 0013** ไม่ให้กด Ready ซ้ำสองที่ |
| 4 | หน้าตา | **จัด layout ใหม่ทั้งหน้า** ใช้สไตล์/asset เดิม | ผู้ใช้บ่นเรื่องหน้าตาด้วย แต่ art pass จริง = task 18 |
| 5 | ความจุ | **cap 4 + โชว์ "x/4"** · ไม่มี kick | kick = ปุ่ม+RPC+กัน client กดเอง ที่เล่น LAN กับเพื่อนไม่จำเป็น |
| 6 | ชื่อใช้ที่ไหน | **เมนู + staging roster** | `LobbyManager` เป็น dontDestroyOnLoad → map ClientId→ชื่อ รอดข้าม scene ฟรี |
| 7 | ช่องชื่อ | **หน้า MainMenu · ล็อกตอนเข้าห้อง** | ส่งครั้งเดียวตอน connect — ไม่ต้องมี rename path + debounce + กัน spam |
| 8 | กลไกส่งชื่อ | **Broadcast หลัง connect** (ไม่ใช่ custom Authenticator) | ไม่แตะ auth pipeline เดิม. ผลข้างเคียง (แถวขึ้นชื่อ fallback ชั่วขณะ) แก้ด้วย re-broadcast |
| 9 | ทรง | **รายชื่อแนวตั้ง 4 แถว** แถวว่าง = "— empty —" | เห็นความจุครบทั้ง 4 สลอตพร้อมกัน, list ไม่ reflow ตอนคนเข้า/ออก |
| 10 | คนหาย | roster หายแถวเอง + **แยกข้อความ "host ปิดห้อง"** | เดิมมีข้อความเดียว `"การเชื่อมต่อหลุด"` แยกไม่ออกว่า host ปิดหรือเน็ตหลุด |
| 11 | เกณฑ์ปิดงาน | **MPM 2 instance** | ฟีเจอร์นี้ทั้งฟีเจอร์คือ multi-peer — verify host เดี่ยวพิสูจน์ไม่ครบ |
| 12 | Start gating | **กดได้ตลอด แม้คนเดียว** | คงพฤติกรรมเดิม, dev flow ทั้งโปรเจกต์พึ่ง solo host. ready-gate จริงอยู่ที่ staging แล้ว |

## รายละเอียด implementation

**ไฟล์ใหม่**
- `Networking/LobbyRoster.cs` — 3 broadcast struct: `PlayerNameBroadcast` (client→server), `LobbyRosterBroadcast` (server→all, **full snapshot ไม่ใช่ delta** — cap 4 แถว, diff ไม่คุ้ม + snapshot self-correcting), `LobbyClosedBroadcast` (server→clients ก่อนปิด) + `LobbyRosterEntry {ClientId, Name, IsHost}`
- `GameUI/MainMenu/LobbyRosterView.cs` — การ์ด roster สร้างด้วยโค้ด (แพตเทิร์นเดียวกับ `StagingController`) จำนวนแถว = ความจุคงที่ · plain C# ไม่ใช่ MonoBehaviour (ไม่มีใครนอก panel อ้างถึงแถว)
- `DevTool/DevLobbyRosterTest.cs` — harness เทส 2 peer, **inert by default** (`#if L1_ROSTER_TEST`)

**แก้**
- `Networking/LobbyManager.cs` — `MaxPlayers = 4` (ค่าเดียวคุมทั้งป้าย "x/4" และจุดปฏิเสธจริงของ transport) · `serverNames` map · `Roster`/`RosterMaxPlayers`/`OnRosterChanged`/`GetPlayerName(clientId)` · `GetLocalPlayerName`/`SetLocalPlayerName`/`SanitizeName` · `ApplyMaxPlayersToTransport()` · `RebuildAndBroadcastRoster()` · `ConsumeDisconnectReason()`
- `GameUI/MainMenu/LobbyPanel.cs` — `RosterRoot` + สร้าง/refresh roster view + header เป็น `"L O B B Y"` คงที่ (เดิมพิมพ์ lobby code ดิบ ซึ่งอ่านว่า `LOCAL` บน Tugboat — ไม่บอกอะไรเลย)
- `GameUI/MainMenu/MainMenuPanel.cs` — `NameInputField` (+ PlayerPrefs) + `NoticeText` + `ShowNotice()`
- `GameUI/MainMenu/MenuFlowController.cs` — `ShowMainMenu` consume disconnect reason
- `GameUI/Staging/StagingController.cs` — `Player {id}` → ชื่อจริงผ่าน `LobbyManager.GetPlayerName(pc.OwnerId)`
- scene `MainMenu.unity` — layout LobbyPanel ใหม่ (backdrop + title + RosterRoot + แถบ IP/Copy + แถบปุ่ม + hint), เพิ่ม `NameInputField`/`NoticeText` ในหน้า MainMenu, wire ref ทั้งหมด

### จุดที่ตั้งใจทำแบบนี้ (อย่ารื้อโดยไม่อ่าน)

1. **register broadcast ที่ `LobbyManager` ไม่ใช่ที่ panel** — LobbyManager เป็น dontDestroyOnLoad → ไม่มี handler ค้างตอน `ReplaceOption.All` ทำลาย menu scene และเป็นตัวเดียวกับที่ทำให้ `StagingController` หาชื่อเจอหลัง scene swap
2. **`RebuildAndBroadcastRoster` อ่านจาก `ServerManager.Clients`** ไม่ใช่ `connectedPlayers` ของเราเอง — ตารางของ FishNet เป็น authoritative, roster ดริฟต์จากความจริงไม่ได้
3. **safety net แถว host** — โค้ดใส่แถว host เองถ้าไม่เจอใน `Clients` *(หมายเหตุ: verify แล้วว่า loopback client ของ host **ขึ้นใน `Clients` จริง** → safety net นี้ redundant แต่เก็บไว้เพราะไม่มีต้นทุนและกันกรณี transport อื่น)*
4. **re-broadcast ตอนรับชื่อ** — roster ออกก่อนชื่อได้ (คนละ path) แถวจะขึ้น `Player {id}` ก่อน ถ้าไม่ยิงซ้ำจะค้างชื่อ fallback ตลอดกาล ไม่ใช่แค่ชั่วขณะ
5. **`SanitizeName` ตัด `<` `>` และรันบน server ด้วย** — แถว roster เป็น TMP label → ชื่อที่ไม่ sanitize inject rich-text ทาสีแถวคนอื่นได้
6. **`ApplyMaxPlayersToTransport` ต้องรันก่อน `StartConnection`** — Tugboat ส่งค่าเข้า socket ตอน start
7. **`MainMenuPanel.Start` ไม่ล้าง notice** — drop กลางเกมทำ `LoadScene(menu)` → `Start` ของ MainMenuPanel กับ MenuFlowController รันพร้อมกันแบบไม่มีลำดับที่แน่นอน ถ้าล้างตรงนั้นข้อความ "Host closed the lobby" จะหายเงียบบน path ที่ผู้ใช้ขอพอดี
8. **`childControlHeight = true` บน VerticalLayoutGroup ของการ์ด** — ถ้า false layout จะใช้ความสูงดิบของ RectTransform (100 สำหรับ GameObject ใหม่) แล้วแถวล้นการ์ดไปทับปุ่ม (เจอจริงในรอบแรก)
9. **broadcast ตอนคนเข้า ผูกกับ `ServerManager.OnAuthenticationResult` ไม่ใช่ `OnRemoteConnectionState.Started`** — ตอน Started connection **ยังไม่ authenticate** และ `Broadcast` ข้าม connection ที่ยังไม่ auth (พร้อม log warning ต่อ 1 ตัว) → roster ที่ยิงตอนนั้น **ไปไม่ถึง client ที่เพิ่งเข้ามาเอง**. `OnRemoteConnectionState` เหลือหน้าที่ rebuild เฉพาะตอน **disconnect** (คนที่เหลือ auth แล้ว ไม่มี warning)

## ⚠️ ภาษา: TMP font ไม่มี glyph ไทย

play test พิสูจน์แล้วว่า **ข้อความภาษาไทยเรนเดอร์เป็นกล่อง □□□** (font asset ในโปรเจกต์ไม่มีสระ/พยัญชนะไทย) → string ที่ task นี้เพิ่มทั้งหมดใช้ **อังกฤษ**

ผลข้างเคียงที่ต้องรู้:
- เปลี่ยน string เดิม 1 ตัว: `"การเชื่อมต่อหลุด"` → `"Connection lost"` (เพราะตอนนี้มันไปโผล่บน NoticeText หน้าเมนูจริงๆ แล้ว)
- **ยังไม่ได้แก้:** ข้อความไทยใน `JoinPanel` (`"IP ไม่ถูกต้อง..."`, `"เข้าห้องไม่สำเร็จ"`, `"เชื่อมต่อไม่สำเร็จ — ..."`) **เป็นกล่องอยู่ตั้งแต่ก่อน task นี้** — ทางแก้จริงคือใส่ TMP font asset ที่มี glyph ไทย = งาน task 18

## Runtime verified (host เดี่ยว, MCP + play mode จริง)

รันผ่าน flow เมนูจริง (กดปุ่ม Host ผ่าน `onClick.Invoke()` ไม่ใช่เรียก API ตรง):

- **name round-trip ครบวง** — `PlayerPrefs = "kao"` → client broadcast → server รับ → rebuild → roster โชว์ `id=0 name='kao' host=True` (ถ้า broadcast ไม่ถึง จะขึ้น `Player 0`) ✓
- **`ServerManager.Clients.Count = 1` ตอน host เดี่ยว** → loopback client ของ host **นับเป็น connection จริง** → แถว host มาเองตามธรรมชาติ ✓
- **cap semantics ชัดแล้ว** — host กิน 1 ใน 4 สลอต → `_maximumClients = 4` = **4 คนรวม host** ตรงกับป้าย "x/4" พอดี · `GetMaximumClients() = 4` ยืนยันในโปรเซส host ✓
- **UI เรนเดอร์ถูก** — `PLAYERS` / `1/4` / `● kao (you) HOST` / 3 แถว `○ — empty —` / title `L O B B Y` / IP `192.168.1.74` / ปุ่ม Start โผล่เฉพาะ host ✓
- **layout ไม่ทับกัน** (ยืนยันด้วย screenshot จริง หลังแก้บั๊ก `childControlHeight`) ✓
- **✅ `LobbyRosterEntry[]` deserialize ผ่าน wire จริง — จุดเสี่ยงสูงสุดปิดแล้ว** ได้ log `[Lobby] roster received over the wire — 1/4 entries` โดย **stack trace วิ่งผ่าน socket จริง**: `ClientSocket.IterateIncoming → Tugboat.HandleClientReceivedDataArgs → ClientManager.ParseBroadcast → ServerBroadcastHandler<LobbyRosterBroadcast>.InvokeHandlers → OnClientReceiveRoster` → **FishNet codegen สร้าง serializer ให้ array ของ custom struct ได้จริง** (host mode ไม่ได้ลัดวงจร — client ของ host รับผ่าน Tugboat loopback เหมือน peer จริงทุกประการ)
- **0 warning** หลังย้าย broadcast ไป `OnAuthenticationResult` ✓
- compile clean 0 error ตลอด

## ❌ ยัง NOT verified — ต้องรัน 2 peer

MPPM virtual player ในเครื่องนี้ **ไม่ยอมเข้า play mode ตาม main editor** (log ของ clone ไม่มี `InvokePlay` รอบล่าสุด) และ MPPM editor API เข้าผ่าน reflection ไม่ได้ → **เกณฑ์ปิดงานข้อ 11 ยังไม่ผ่าน**

*(หมายเหตุ: ข้อเสี่ยงอันดับ 1 เดิม — client deserialize `LobbyRosterEntry[]` — **ปิดไปแล้ว** ดูหัวข้อบน)*

สิ่งที่ยังพิสูจน์ไม่ได้ (ทั้งหมดต้องมี peer ที่ 2 จริง):

1. roster โชว์ **2+ แถว** + ชื่อของ *อีกคน* (ตอนนี้เทสได้แค่ 1 แถว)
2. `LobbyClosedBroadcast` → notice "Host closed the lobby" ฝั่ง client (โดยเฉพาะว่ามันถูก flush ทัน `StopConnection` จริงไหม)
3. ชื่อรอดเข้า staging **ฝั่ง client** หลัง `LoadGlobalScenes`
4. คนที่ 5 ต่อไม่ติดจริง (cap enforcement ปลายทาง — semantics ยืนยันแล้ว แต่ยังไม่เห็นการปฏิเสธจริง)
5. path `OnAuthenticationResult` กับ **remote** connection (เทสแล้วเฉพาะ loopback client ของ host)

**วิธีรัน:** ดูหัวไฟล์ `Assets/Scripts/DevTool/DevLobbyRosterTest.cs` (เปิด define `L1_ROSTER_TEST` → กด Play → อ่าน `[L1TEST]` ทั้งใน Console และ `Library/VP/<clone>/Logs/Editor.log`)

## ขอบเขต — task นี้ *ไม่* ทำ

kick · ready/class ในเมนู (คงอยู่ staging ตาม 0013) · nameplate เหนือหัวในเกม · Steam persona name ·
room browser / server list · เปลี่ยนชื่อระหว่างอยู่ในห้อง · art pass เต็ม (task 18) · TMP font ไทย ·
ข้อความไทยเดิมใน JoinPanel

## เจอระหว่างทาง (นอก scope — บันทึกไว้)

- **`GamePlayerSpawner` มี spawn point แค่ 2 จุด** (`spawns=[1,2]`, PROGRESS 2026-07-30) แต่ตอนนี้เราประกาศรองรับ 4 คน → เทส 3–4 คนจริงตัวละครจะ spawn ซ้อนกัน **ยังไม่แก้**
- ปุ่มหน้า MainMenu ยังใช้ label placeholder `"Host Button"` / `"Join Button"` / `"Exit Button"` (มาจาก `MainMenuPanel.Rename()`) — ไม่แตะ เพราะนอกขอบเขต "หน้า lobby"
- `Assets/FishNet/` ที่ untracked อยู่ = โฟลเดอร์เปล่า (18 ไฟล์, 0 ไฟล์ `.cs`) **ไม่ใช่ FishNet ซ้ำ** ไม่เสี่ยง assembly ชน
- `LobbyPanel.MenuScene` ยังเป็น dead field (ค้างมาจาก task ก่อน)
