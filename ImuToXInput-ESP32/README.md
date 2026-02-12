# ImuToXInput-ESP32

Alternate firmware for **ESP32-based USB dongles** (e.g. [this ESP32 controller dongle](https://www.aliexpress.com/item/1005006594238178.html)) that:

- Runs a **temporary WiFi AP + web server** to configure WiFi credentials (no serial needed after first flash).
- **Reads SolarXR/SlimeVR data** from a device on the same network (e.g. SlimeVR Server or SolarXR app).
- **Maps tracker data to a virtual Xbox 360 controller** and exposes it over **USB HID** (wired gamepad).
- **Loads and swaps input config files** (same JSON format as PC ImuToXInput) from onboard storage.

Future goals: support other console controller report descriptors (e.g. DualShock-style) and more mapping options.

---

## Key features

| Feature | Description |
|--------|-------------|
| **WiFi provisioning** | On first boot (or when not configured), the dongle starts an AP (e.g. `ImuToXInput-Setup`). Connect with phone/PC, open the captive portal or `http://192.168.4.1`, enter your WiFi SSID/password and (optionally) SolarXR server IP. Credentials are saved and the dongle joins your network. |
| **SolarXR / SlimeVR input** | Connect to a SolarXR/SlimeVR server on the same network (default `ws://<server-ip>:21110`). Receive tracker poses (position, rotation) and optional skeleton. Same data source as the PC ImuToXInput app. |
| **Xbox 360 controller over USB** | Translate tracker data into thumbsticks, buttons, and triggers using the same mapping rules as the PC app. Expose as a **wired Xbox 360–compatible USB HID gamepad** so consoles/PC see a normal controller. |
| **Config files** | Store multiple game configs (e.g. `default.json`, `portal.json`) on the device (LittleFS/SPIFFS). Select active config via web UI or a simple “next config” action. Same JSON schema as [ImuToXInput configs](https://github.com/Sebane1/ImuToinput): `axisMappings`, `buttonMappings`, `triggerMappings`, conditions (euler_threshold, euler_diff, euler_sum, position_threshold). |

---

## Hardware

- **ESP32 with USB** – Board must support **USB** (e.g. **ESP32-S2** or **ESP32-S3** with native USB). Classic ESP32 (no native USB) would need a separate USB-HID chip or a different approach.
- **Tested/target dongle:** [AliExpress ESP32 controller dongle (item 1005006594238178)](https://www.aliexpress.com/item/1005006594238178.html). Confirm whether it is ESP32-S2 or S3 (or another variant) and set the matching board in `platformio.ini`.

---

## SolarXR protocol (tracker data source)

Tracker data is received using the **[SolarXR Protocol](https://github.com/SlimeVR/SolarXR-Protocol)** – a hardware-agnostic serialization protocol for full-body tracking used by SlimeVR and compatible apps.

- **Repo:** [SlimeVR/SolarXR-Protocol](https://github.com/SlimeVR/SolarXR-Protocol)
- **Format:** **FlatBuffers**. Message types and layouts are defined in the `schema/` folder; most code in the repo is generated from these schemas.
- **Transport:** The PC ImuToXInput app connects to the SlimeVR/SolarXR server over **WebSocket** (e.g. `ws://<server>:21110`), subscribes to the data feed, and receives binary FlatBuffer bundles (e.g. `MessageBundle` with tracker position/rotation).
- **ESP32 implementation:** Use a WebSocket client to connect to the same server on your network, then parse the binary messages. You can either:
  - Use the **FlatBuffers** C++ library and the generated C++ code from the SolarXR-Protocol repo (run `generate-flatbuffer.sh` / `generate-flatbuffer.ps1` with [flatc v22.10.26](https://github.com/google/flatbuffers/releases) to generate from `schema/`), or
  - Implement a minimal parser that only reads the tracker fields you need (position, rotation / quaternion or Euler) to keep firmware size down.

---

## Software stack (planned)

- **Framework:** Arduino on ESP32 (PlatformIO or Arduino IDE).
- **WiFi:** Built-in WiFi + AP mode; simple async or sync web server for the setup page.
- **Storage:** LittleFS (or SPIFFS) for WiFi credentials, SolarXR server IP, and config JSON files.
- **SolarXR client:** WebSocket client connecting to `ws://<server>:21110`, parsing the same protocol the PC app uses (FlatBuffers / message types). Optionally start with a minimal subset (e.g. tracker positions/rotations only).
- **Mapping engine:** Port of the PC `ConfigApplier` logic: evaluate conditions, compute axis/button/trigger from tracker state, apply deadzone.
- **USB HID:** TinyUSB (or ESP-IDF USB) with an **Xbox 360 controller** report descriptor so the host sees a wired 360 pad. Later: alternate descriptors for other consoles.

---

## Config file format

Same as PC ImuToXInput. One JSON file per “game” or profile:

- **name**, **processNames** (optional; on ESP32 “process” selection can be manual or via web UI).
- **axisMappings:** `tracker`, `source` (EulerX/Y/Z, PosX/Y/Z, FloorRelX/Y/Z), `scale`, `invert`, `axis` (LeftThumbX/Y, RightThumbX/Y).
- **buttonMappings** / **triggerMappings:** Conditions (`euler_threshold`, `euler_diff`, `euler_sum`, `position_threshold`) → button or trigger.
- **trackers:** Optional list for floor-relative math (e.g. LEFT_FOOT, RIGHT_FOOT).

You can copy existing `configs/*.json` from the PC app into the firmware’s data partition or upload them via the web UI (future).

---

## Project layout

```
ImuToXInput-ESP32/
├── README.md           # This file
├── platformio.ini     # Board, libs, build
├── src/
│   └── main.cpp        # Setup, loop, WiFi, SolarXR stub, HID, config load
├── data/               # Optional: default configs to flash with LittleFS
│   └── default.json
└── include/            # Optional: headers for config parser, mapping engine
```

---

## Build and flash

1. Install [PlatformIO](https://platformio.org/) (VS Code extension or CLI).
2. Open `ImuToXInput-ESP32` as a project.
3. In `platformio.ini` set the correct board (e.g. `esp32-s2-saola-1` or your dongle’s board).
4. Build: `pio run`
5. Flash: `pio run -t upload`
6. (Optional) Upload filesystem (configs): `pio run -t uploadfs`

---

## First run

1. Power the dongle. If WiFi is not configured, it starts AP `ImuToXInput-Setup` (or similar).
2. Connect your phone/PC to that AP; open `http://192.168.4.1` (or follow captive portal).
3. Enter your home WiFi SSID/password and, if needed, the SolarXR/SlimeVR server IP (e.g. your PC’s IP).
4. Save; the dongle reboots and connects to your network.
5. Ensure SlimeVR Server (or SolarXR app) is running on the configured host and listening on port 21110.
6. Plug the dongle into the console/PC via USB; it should enumerate as an Xbox 360 controller and start applying the active config.

---

## References

- **[ImuToXInput](https://github.com/Sebane1/ImuToinput)** (PC version) – config format, mapping logic, SlimeVR/SolarXR usage.
- **[SolarXR-Protocol](https://github.com/SlimeVR/SolarXR-Protocol)** – interoperability protocol for VR/FBT; FlatBuffers schemas in `schema/`, codegen with flatc; used by SlimeVR server (WebSocket, port 21110).
- **[SlimeVR](https://slimevr.dev/)** – tracker setup and server that speaks SolarXR.
- **[ESP32 dongle (AliExpress)](https://www.aliexpress.com/item/1005006594238178.html)** – reference hardware for this firmware.
