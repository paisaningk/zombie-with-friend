# 0017 — Direct IP connect (LAN)

> Task 16b (P5, ปิด MVP) · ตัดสิน 2026-08-03 (grill 12 decisions + advisor) · สถานะ: **เสร็จ ✅ · G-a ผ่าน (LAN 2 peer จริง)** · เจอ+แก้บั๊ก multi-peer ค้างเดิม (SceneCondition)

บันทึกว่า "ทำไมทำเป็นรูปนี้" — ทำให้เข้าห้องด้วย Direct IP บน LAN ได้จริง

## บริบท

เกมปิด core loop ครบ (task 1–16a) เล่นได้จริง แต่ทุกอย่าง hardcode `127.0.0.1` → ต่อได้แค่ loopback เครื่องเดียว. หนี้ค้างจาก grill 2026-07-30 (localhost-only ก่อน, Direct IP ทีหลัง):
- `TugboatTransprotProvider.ConnectionAddress` hardcode `"127.0.0.1"` · `JoinLobby(addr)` ทิ้ง param
- host โชว์/copy `"127.0.0.1"` → เพื่อนต่อไม่ได้
- join flow optimistic (เข้า lobby ก่อน เด้งทีหลัง) — บน network จริง fail = common path
- `LobbyManager.OnError` ไม่มี UI subscriber → error เป็นแค่ `Debug.LogError` (advisor จับ)

**16b คือ task แรกที่ loopback พิสูจน์ไม่ได้** — 127.0.0.1 เวิร์กอยู่แล้วโดยไม่ต้องแก้ งานจริง (typed IP, resolve LAN IP, await-auth) เห็นผลต่อเมื่อต่อผ่าน address ที่ไม่ใช่ loopback → หนี้ multi-peer ที่เลื่อนมาทุก task มากองที่นี่

## Facts (ยืนยันจาก FishNet source)

- `ClientManager.StartConnection(string address)` **มีอยู่แล้ว** → `SetClientAddress` ก่อน connect
- สัญญาณ join สำเร็จจริง = `ClientManager.OnAuthenticated` **ไม่ใช่** `Started` (Started = แค่ socket ต่อ · Authenticated = server รับ + `Connection` valid + scene objects register)
- ต่อ IP ตาย → `Starting→Stopping→Stopped` ไม่มี `Started`, **~5.5วิ** (LiteNetLib `MaxConnectAttempts=10 × ReconnectDelay=500ms → ConnectionFailed`)
- Server bind default ว่าง = bind ทุก interface · Tugboat `_enableIpv6=true` default → ปิดกัน dual-stack trap
- Tugboat: `SetClientAddress`/`SetServerBindAddress(addr,IPAddressType)`/`SetPort` (default 7770) · Multipass forward ให้ทุก transport

## การตัดสินใจ (grill)

| # | ประเด็น | เลือก | เหตุผลย่อ |
|---|---|---|---|
| A | IP threading | **A1 — ผ่าน transport** | typed IP เขียนลง `_joinAddress` → `ConnectionAddress` เป็น source-of-truth เดียว · consumer ไม่ต้องรู้ว่า address มาจากไหน (Steam เติม property เดียวกันด้วย SteamID). A2 (bypass) ทิ้ง property เป็น latent lie |
| B | host LAN IP | **B3 — best-guess ใส่ช่องแก้ได้ + copy** | เครื่อง dev มี virtual adapter เยอะ (เดาผิดได้) → editable = escape hatch. cost ≈ B1 (swap widget). display-only ไม่กระทบ host self-connect |
| C | join flow | **C1 — await-real** | รอ `OnAuthenticated`/`Stopped`/timeout ก่อนเปิด LobbyPanel · fail บน network = common path → optimistic แว้บ-แล้ว-เด้ง UX พัง |
| C-t | timeout | **T1 — พึ่ง LiteNetLib ~5.5วิ + backstop 10** | ~5.5วิ tune มาสำหรับ packet loss แล้ว · react `Stopped` = ได้ reason จริง · backstop 10 กันค้าง. T2 (แก้ config) กระทบ global + เสี่ยง false-fail |
| C-f | feedback | **F1 — disable ปุ่ม + "Connecting..."** | reuse `ButtonFx.Text` · กันกดซ้ำ (ยิง StartConnection ซ้อน) · จอไม่นิ่งระหว่างรอ |
| D | validate | **D1 — `IPAddress.TryParse` IPv4 only** | reject รูปแบบผิดทันที ไม่ต้องรอ 5.5วิ · IPv4-only ตรงกับ transport (I1) + MVP scope |
| E | naming | **E1 — label ผู้เล่นเห็นเท่านั้น** | rename identifier = เสี่ยง unwire serialized field โดยไม่ได้ประโยชน์ runtime · "code" เชิง abstraction ยังจริง (IP/SteamID) |
| E1a | **error UI** | **in-scope** (advisor จับ) | `OnError` ไม่มี UI subscriber → C1/D1/F1 ตั้งบนสมมติฐานว่าผู้เล่นเห็น error ที่ไม่มีวันถึงตา. label เล็ก (subscribe event ที่มีอยู่) แต่ทำให้ทุกอย่างมีความหมาย |
| F | safety | host self-connect 127.0.0.1 เสมอ | `_joinAddress` default = loopback · host ไม่เรียก `JoinLobby` → คง default · 1 instance เป็น host/client อย่างใดอย่างหนึ่ง ไม่ปน · dev/Offline ไม่แตะ |
| I | **IPv6** | **I1 — force IPv4-only** (advisor เตือน) | dual-stack + empty bind บน Windows ต่อ loopback ฉลุยแต่บางทีปฏิเสธ IPv4 LAN client · MVP=IPv4 อยู่แล้ว → ปิด IPv6 pre-empt trap |
| H | host feedback | **H1 — เห็น client ตอน staging + log** | coordination ย้ายไป in-game staging แล้ว (0013) · menu roster = งานที่ตัดออกโดยตั้งใจ · เติม `Debug.Log` ช่วย G-a test |
| G | verify | **G-a ปิด task · G-b ปิด MVP** | loopback พิสูจน์ไม่ได้ → G-a: 2 instance ต่อ LAN IP จริง (code path ถูก) · G-b: 2 เครื่อง (firewall/reachability) |

## กลไกสำคัญ

**await-real bridge (`LobbyManager.AwaitClientJoin`):** subscribe `OnAuthenticated`(=success) + `OnClientConnectionState` Stopped(=fail) **ก่อน** `StartConnection` — กัน success-race บน loopback ที่ auth ยิงก่อน awaiter setup (advisor เตือน). `UniTaskCompletionSource<bool>` + `.AttachExternalCancellation` + `.Timeout(10s)`. `_isJoining` flag ให้ persistent `OnClientConnectionState` handler **skip drop-logic** ตอน join-fail (กัน Stopped ไปชน path "หลุดกลางเกม → เด้งเมนู")

**LAN IP resolver (`GetHostDisplayAddress`):** display-only (host ยัง self-connect loopback). เดิน `NetworkInterface` กรอง Up + ไม่ Loopback/Tunnel + IPv4 + ไม่ 169.254 · score: **default gateway +10 (สัญญาณหลัก)** + range (192.168=3/10=2/172.16-31=1) + Ethernet/WiFi +1 · log candidate ทั้งหมด

## บั๊กเจอตอน static verify + แก้

resolver เดิม (score by range+type เท่านั้น) **เดาผิดบนเครื่อง dev**: Hyper-V `vEthernet (Internal Switch)` (`192.168.217.1`) รายงานเป็น `Ethernet` + อยู่ `192.168.x` → tie score กับ LAN จริง (`192.168.1.71`) → enumeration order คืน virtual (เพื่อนต่อไม่ได้). **แก้: เพิ่ม default-gateway เป็นสัญญาณหลัก (+10)** — virtual switch (Hyper-V/WSL) ไม่มี gateway, LAN จริงมี → LAN จริงชนะ (14 vs 4). B3 (editable) เป็น safety net ชั้นสอง

## Static verified (MCP, host)

compile errors=0 · resolver ranking LAN จริง (192.168.1.71 score14) ชนะ virtual (score 4/2) · await-flow code path ถูก · host loopback ไม่ regression · Tugboat `_enableIpv6=false` saved (Init) · MainMenu UI wired+saved (ErrorText/IpInputField/Copy IP)

## G-a ผ่านแล้ว ✅ (MPM 2 peer, 2026-08-03)

client พิมพ์ **LAN IP จริง `192.168.1.71`** (ไม่ใช่ loopback) → ต่อสำเร็จ + เล่นได้ = **Direct IP + multi-peer verified ครั้งแรกของโปรเจกต์** G-b (2 เครื่องจริง + firewall) ปิด MVP ภายหลัง

## บั๊กที่ G-a เปิดเผย — FishNet SceneCondition (multi-peer ค้างเดิม, ไม่เกี่ยว 16b)

ตอน G-a เจอ `SceneId not found in SceneObjects` (GameManager + WaveManager) บน client → manager ไม่ spawn ฝั่ง client. **ไม่ใช่บั๊ก connect** (เกิดหลัง auth + บน loopback ด้วย) แต่เป็น scene-observation ที่ Direct IP แค่กระตุ้นให้โผล่ (peer ที่ 2 จริงตัวแรก)

- **Root cause:** NetworkManager ไม่มี `ObserverManager` ใน scene → FishNet auto-add runtime ด้วย `_defaultConditions` ว่าง → object ถูก observe โดยทุก client ไม่ว่าอยู่ scene ไหน → client ในลอบบี้ได้ spawn ของ scene object ใน SampleScene → หา SceneId ไม่เจอ → drop
- **Fix (scene/asset เท่านั้น):** `Assets/Data/DefaultSceneCondition.asset` (FishNet SceneCondition) + เพิ่ม `ObserverManager` ลง NetworkManager (Init) set `_defaultConditions=[SceneCondition]` → object sync เฉพาะ client ที่โหลด scene เดียวกัน
- **Convention:** scene game บน FishNet ต้องมี SceneCondition เสมอ (ดู PROGRESS log 2026-08-03)
- **Red herrings:** reserialize SceneId (ไม่ช่วย), DDOL, inactive, duplicate, scene pre-open

## ตัด/เลื่อน (อย่าเผลอทำ)

internet/port-forward · `IP:port` input (port fixed 7770) · Steam/Relay lobby · reconnect · menu synced roster (H1) · cancel-ระหว่างรอ (backstop 10 ครอบ) · rename code identifier (E1)
