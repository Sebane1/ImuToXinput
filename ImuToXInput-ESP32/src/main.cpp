/**
 * ImuToXInput-ESP32
 *
 * - WiFi AP + web server for provisioning (SSID/password, SolarXR server IP).
 * - Connect to SolarXR/SlimeVR server on the network (ws://host:21110).
 * - Map tracker data to Xbox 360 controller; expose over USB HID.
 * - Load/swap config JSON files from LittleFS (same format as PC ImuToXInput).
 */

#include <Arduino.h>
#include <WiFi.h>
#include <WebServer.h>
#include <LittleFS.h>
#include <ESPmDNS.h>
#include <Preferences.h>

#include "Config.h"
#include "ConfigLoader.h"
#include "TrackerState.h"
#include "MappingEngine.h"
#include "Xbox360Report.h"
#include "OutputBleGamepad.h"
#include "SolarXRClient.h"

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

const char* AP_SSID     = "ImuToXInput-Setup";
const char* AP_PASSWORD = "imutoxinput";
const char* HOSTNAME    = "imutoxinput";
const char* PREF_NAMESPACE = "imux";

const uint16_t WEBSERVER_PORT = 80;
const uint16_t SOLARXR_PORT   = 21110;

// ---------------------------------------------------------------------------
// State
// ---------------------------------------------------------------------------

WebServer server(WEBSERVER_PORT);
Preferences prefs;

bool wifiConfigured = false;
String savedSSID;
String savedPassword;
String solarxrServerIP;
uint8_t activeConfigIndex = 0;

GameProfile activeProfile;
Xbox360Report report;

const char* CONFIG_FILES[] = { "/default.json", "/portal.json" };
const int NUM_CONFIG_FILES = sizeof(CONFIG_FILES) / sizeof(CONFIG_FILES[0]);

// ---------------------------------------------------------------------------
// Persist WiFi + SolarXR IP with Preferences
// ---------------------------------------------------------------------------

void loadCredentials() {
  prefs.begin(PREF_NAMESPACE, true);
  savedSSID = prefs.getString("ssid", "");
  savedPassword = prefs.getString("pass", "");
  solarxrServerIP = prefs.getString("solarxr", "");
  prefs.end();
}

void saveCredentials() {
  prefs.begin(PREF_NAMESPACE, false);
  prefs.putString("ssid", savedSSID);
  prefs.putString("pass", savedPassword);
  prefs.putString("solarxr", solarxrServerIP);
  prefs.end();
}

// ---------------------------------------------------------------------------
// WiFi provisioning: start AP and serve setup page
// ---------------------------------------------------------------------------

void startProvisioningAP() {
  WiFi.mode(WIFI_AP);
  WiFi.softAP(AP_SSID, AP_PASSWORD);
  Serial.println("AP started: " + String(AP_SSID));
  Serial.println("Connect and open http://192.168.4.1 to configure WiFi.");
}

void handleRoot() {
  String html = R"raw(
<!DOCTYPE html><html><head><meta name="viewport" content="width=device-width,initial-scale=1"/><title>ImuToXInput Setup</title></head><body>
<h1>ImuToXInput WiFi Setup</h1>
<form method="POST" action="/save">
  <label>WiFi SSID:</label><br/>
  <input type="text" name="ssid" required/><br/>
  <label>Password:</label><br/>
  <input type="password" name="pass"/><br/>
  <label>SolarXR server IP (optional, e.g. 192.168.1.100):</label><br/>
  <input type="text" name="solarxr_ip" placeholder="192.168.1.100"/><br/>
  <button type="submit">Save and connect</button>
</form>
</body></html>
)raw";
  server.send(200, "text/html", html);
}

void handleSave() {
  if (server.method() != HTTP_POST) {
    server.send(405, "text/plain", "Method Not Allowed");
    return;
  }
  savedSSID       = server.hasArg("ssid") ? server.arg("ssid") : "";
  savedPassword   = server.hasArg("pass") ? server.arg("pass") : "";
  solarxrServerIP = server.hasArg("solarxr_ip") ? server.arg("solarxr_ip") : "";

  saveCredentials();
  Serial.println("Saved SSID: " + savedSSID + " SolarXR IP: " + solarxrServerIP);

  server.send(200, "text/html",
    "<p>Saved. Connecting to WiFi...</p><p>If connection fails, reconnect to AP and try again.</p>");
  delay(500);
  wifiConfigured = true;
  WiFi.softAPdisconnect(true);
  WiFi.mode(WIFI_STA);
  WiFi.begin(savedSSID.c_str(), savedPassword.c_str());
}

void setupProvisioningServer() {
  server.on("/", handleRoot);
  server.on("/save", handleSave);
  server.begin();
}

// ---------------------------------------------------------------------------
// SolarXR / SlimeVR client
// Protocol: https://github.com/SlimeVR/SolarXR-Protocol
// ---------------------------------------------------------------------------

static bool solarxrConnectAttempted = false;

void solarxrConnect() {
  if (solarxrServerIP.length() == 0) return;
  if (WiFi.status() != WL_CONNECTED) return;
  if (solarxrConnectAttempted) return;
  solarxrConnectAttempted = true;
  solarxrClientConnect(solarxrServerIP.c_str(), SOLARXR_PORT);
}

void solarxrPoll() {
  if (wifiConfigured && WiFi.status() == WL_CONNECTED && !solarxrConnectAttempted)
    solarxrConnect();
  solarxrClientPoll();
}

// ---------------------------------------------------------------------------
// Config: load active profile from LittleFS
// ---------------------------------------------------------------------------

void loadActiveConfig() {
  const char* path = CONFIG_FILES[activeConfigIndex % NUM_CONFIG_FILES];
  if (loadConfig(path, &activeProfile)) {
    Serial.println("Loaded config: " + String(path) + " (" + String(activeProfile.name) + ")");
    return;
  }
  if (loadConfig(CONFIG_FILES[0], &activeProfile))
    Serial.println("Loaded fallback: " + String(CONFIG_FILES[0]));
  else
    Serial.println("No config loaded; report will be zeroed.");
}

// ---------------------------------------------------------------------------
// Mapping + HID
// ---------------------------------------------------------------------------

void applyMappingsToReport() {
  reportClear(&report);
  applyProfile(&activeProfile, &report);
}

void hidSendReport() {
#ifdef USE_BLE_GAMEPAD
  outputBleGamepadSend(&report);
#else
  // TODO: send report via USB HID (Xbox 360 descriptor) for S2/S3
  (void)report;
#endif
}

// ---------------------------------------------------------------------------
// Setup / Loop
// ---------------------------------------------------------------------------

void setup() {
  Serial.begin(115200);
  delay(1000);
  Serial.println("ImuToXInput-ESP32");

  if (!LittleFS.begin(true))
    Serial.println("LittleFS mount failed");

  trackerMapInit();
  loadCredentials();

  wifiConfigured = (savedSSID.length() > 0);
  if (!wifiConfigured) {
    startProvisioningAP();
    setupProvisioningServer();
  } else {
    WiFi.mode(WIFI_STA);
    WiFi.begin(savedSSID.c_str(), savedPassword.c_str());
    int wait = 0;
    while (WiFi.status() != WL_CONNECTED && wait < 20) {
      delay(500);
      wait++;
    }
    if (WiFi.status() == WL_CONNECTED) {
      Serial.println("WiFi OK: " + WiFi.localIP().toString());
      if (!MDNS.begin(HOSTNAME)) Serial.println("mDNS failed");
    } else {
      Serial.println("WiFi failed; starting AP for provisioning.");
      startProvisioningAP();
      setupProvisioningServer();
    }
  }

  loadActiveConfig();
  solarxrConnect();

#ifdef USE_BLE_GAMEPAD
  outputBleGamepadInit();
  Serial.println("BLE gamepad advertising as 'ImuToXInput'. Pair from your device.");
#endif
}

void loop() {
  if (!wifiConfigured)
    server.handleClient();

  solarxrPoll();
  applyMappingsToReport();
  hidSendReport();

  delay(8);  // ~120 Hz
}
