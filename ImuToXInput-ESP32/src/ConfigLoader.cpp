/**
 * Load GameProfile from a JSON file on LittleFS (same schema as PC ImuToXInput).
 */

#include "../include/Config.h"
#include "../include/ConfigLoader.h"
#include <ArduinoJson.h>
#include <LittleFS.h>
#include <string.h>

static void copyStr(char* dst, const char* src, size_t maxLen) {
  if (!dst || maxLen == 0) return;
  if (!src) { dst[0] = '\0'; return; }
  strncpy(dst, src, maxLen - 1);
  dst[maxLen - 1] = '\0';
}

static void parseCondition(JsonObject cond, MappingCondition* out) {
  if (!cond || !out) return;
  memset(out, 0, sizeof(MappingCondition));
  copyStr(out->type, cond["type"] | "", MAX_STRING_LEN);
  copyStr(out->tracker, cond["tracker"] | "", MAX_STRING_LEN);
  copyStr(out->trackerA, cond["trackerA"] | "", MAX_STRING_LEN);
  copyStr(out->trackerB, cond["trackerB"] | "", MAX_STRING_LEN);
  copyStr(out->component, cond["component"] | "", 4);
  copyStr(out->source, cond["source"] | "", MAX_STRING_LEN);
  copyStr(out->op, cond["op"] | "greater_than", MAX_STRING_LEN);
  out->value = cond["value"] | 0.0f;
}

bool loadConfig(const char* path, GameProfile* out) {
  if (!path || !out || !LittleFS.exists(path)) return false;
  File f = LittleFS.open(path, "r");
  if (!f) return false;

  DynamicJsonDocument doc(8192);
  DeserializationError err = deserializeJson(doc, f);
  f.close();
  if (err) return false;

  profileInit(out);
  JsonObject root = doc.as<JsonObject>();
  copyStr(out->name, root["name"] | "default", MAX_STRING_LEN);

  JsonArray axes = root["axisMappings"];
  for (size_t i = 0; i < axes.size() && out->numAxisMappings < MAX_AXIS_MAPPINGS; i++) {
    JsonObject m = axes[i];
    AxisMapping* a = &out->axisMappings[out->numAxisMappings];
    copyStr(a->tracker, m["tracker"] | "", MAX_STRING_LEN);
    copyStr(a->source, m["source"] | "", MAX_STRING_LEN);
    a->scale = m["scale"] | 1.0f;
    a->invert = m["invert"] | false;
    copyStr(a->axis, m["axis"] | "", MAX_STRING_LEN);
    out->numAxisMappings++;
  }

  JsonArray buttons = root["buttonMappings"];
  for (size_t i = 0; i < buttons.size() && out->numButtonMappings < MAX_BUTTON_MAPPINGS; i++) {
    JsonObject m = buttons[i];
    ButtonMapping* b = &out->buttonMappings[out->numButtonMappings];
    parseCondition(m["condition"], &b->condition);
    copyStr(b->button, m["button"] | "", MAX_STRING_LEN);
    out->numButtonMappings++;
  }

  JsonArray triggers = root["triggerMappings"];
  for (size_t i = 0; i < triggers.size() && out->numTriggerMappings < MAX_TRIGGER_MAPPINGS; i++) {
    JsonObject m = triggers[i];
    TriggerMapping* t = &out->triggerMappings[out->numTriggerMappings];
    parseCondition(m["condition"], &t->condition);
    copyStr(t->trigger, m["trigger"] | "", MAX_STRING_LEN);
    t->valueWhenTrue = m["valueWhenTrue"] | 255;
    t->valueWhenFalse = m["valueWhenFalse"] | 0;
    out->numTriggerMappings++;
  }

  JsonArray tr = root["trackers"];
  for (size_t i = 0; i < tr.size() && out->numTrackers < MAX_TRACKERS; i++) {
    copyStr(out->trackers[out->numTrackers], tr[i] | "", MAX_STRING_LEN);
    out->numTrackers++;
  }

  return true;
}
