#include "../include/MappingEngine.h"
#include <string.h>
#include <math.h>

static float getAxisSourceValue(const TrackerState* t, const char* source) {
  Vec3 cal = calibratedPosition(t);
  Vec3 flr = floorRelativePosition(t);
  if (strcasecmp(source, "EulerX") == 0) return t->euler.x - t->eulerCalibration.x;
  if (strcasecmp(source, "EulerY") == 0) return t->euler.y - t->eulerCalibration.y;
  if (strcasecmp(source, "EulerZ") == 0) return t->euler.z - t->eulerCalibration.z;
  if (strcasecmp(source, "PosX") == 0) return cal.x;
  if (strcasecmp(source, "PosY") == 0) return cal.y;
  if (strcasecmp(source, "PosZ") == 0) return cal.z;
  if (strcasecmp(source, "FloorRelX") == 0) return flr.x;
  if (strcasecmp(source, "FloorRelY") == 0) return flr.y;
  if (strcasecmp(source, "FloorRelZ") == 0) return flr.z;
  return 0.0f;
}

static float getEulerComponent(const TrackerState* t, const char* component) {
  float e = t->euler.x - t->eulerCalibration.x;
  if (component[0] == 'Y' || component[0] == 'y') e = t->euler.y - t->eulerCalibration.y;
  else if (component[0] == 'Z' || component[0] == 'z') e = t->euler.z - t->eulerCalibration.z;
  return e;
}

static float getPositionComponent(const TrackerState* t, const char* source) {
  Vec3 cal = calibratedPosition(t);
  Vec3 flr = floorRelativePosition(t);
  if (strcasecmp(source, "CalibratedX") == 0) return cal.x;
  if (strcasecmp(source, "CalibratedY") == 0) return cal.y;
  if (strcasecmp(source, "CalibratedZ") == 0) return cal.z;
  if (strcasecmp(source, "FloorRelX") == 0) return flr.x;
  if (strcasecmp(source, "FloorRelY") == 0) return flr.y;
  if (strcasecmp(source, "FloorRelZ") == 0) return flr.z;
  return 0.0f;
}

static bool evalOp(float a, const char* op, float value) {
  if (!op) return false;
  if (strcasecmp(op, "less_than") == 0 || strcasecmp(op, "lt") == 0) return a < value;
  if (strcasecmp(op, "greater_than") == 0 || strcasecmp(op, "gt") == 0) return a > value;
  if (strcasecmp(op, "less_than_or_equal") == 0 || strcasecmp(op, "lte") == 0) return a <= value;
  if (strcasecmp(op, "greater_than_or_equal") == 0 || strcasecmp(op, "gte") == 0) return a >= value;
  return false;
}

bool evaluateCondition(const MappingCondition* c) {
  if (!c || !c->type[0]) return false;

  if (strcmp(c->type, "euler_threshold") == 0) {
    TrackerState* t = trackerMapGet(c->tracker);
    if (!t) return false;
    return evalOp(getEulerComponent(t, c->component), c->op, c->value);
  }
  if (strcmp(c->type, "euler_diff") == 0) {
    TrackerState* ta = trackerMapGet(c->trackerA);
    TrackerState* tb = trackerMapGet(c->trackerB);
    if (!ta || !tb) return false;
    float diff = getEulerComponent(ta, c->component) - getEulerComponent(tb, c->component);
    return evalOp(diff, c->op, c->value);
  }
  if (strcmp(c->type, "euler_sum") == 0) {
    TrackerState* sa = trackerMapGet(c->trackerA);
    TrackerState* sb = trackerMapGet(c->trackerB);
    if (!sa || !sb) return false;
    float sum = getEulerComponent(sa, c->component) + getEulerComponent(sb, c->component);
    return evalOp(sum, c->op, c->value);
  }
  if (strcmp(c->type, "position_threshold") == 0) {
    TrackerState* t = trackerMapGet(c->tracker);
    if (!t) return false;
    return evalOp(getPositionComponent(t, c->source), c->op, c->value);
  }
  return false;
}

int16_t applyDeadzone(float value, float deadzone) {
  if (value > 15.0f) value = 15.0f;
  else if (value < -15.0f) value = -15.0f;
  value /= 15.0f;
  if (fabsf(value) < deadzone) return 0;
  float sign = value >= 0 ? 1.0f : -1.0f;
  float scaled = (fabsf(value) - deadzone) / (1.0f - deadzone);
  return (int16_t)(sign * scaled * 32767.0f);
}

static bool parseAxis(const char* name, int* outIdx) {
  if (!name || !outIdx) return false;
  if (strcasecmp(name, "LeftThumbX") == 0) { *outIdx = AXIS_LEFT_THUMB_X; return true; }
  if (strcasecmp(name, "LeftThumbY") == 0) { *outIdx = AXIS_LEFT_THUMB_Y; return true; }
  if (strcasecmp(name, "RightThumbX") == 0) { *outIdx = AXIS_RIGHT_THUMB_X; return true; }
  if (strcasecmp(name, "RightThumbY") == 0) { *outIdx = AXIS_RIGHT_THUMB_Y; return true; }
  return false;
}

static bool parseButton(const char* name, uint16_t* outMask) {
  if (!name || !outMask) return false;
  if (strcasecmp(name, "A") == 0) { *outMask = BUTTON_A; return true; }
  if (strcasecmp(name, "B") == 0) { *outMask = BUTTON_B; return true; }
  if (strcasecmp(name, "X") == 0) { *outMask = BUTTON_X; return true; }
  if (strcasecmp(name, "Y") == 0) { *outMask = BUTTON_Y; return true; }
  if (strcasecmp(name, "LeftShoulder") == 0) { *outMask = BUTTON_LEFT_SHOULDER; return true; }
  if (strcasecmp(name, "RightShoulder") == 0) { *outMask = BUTTON_RIGHT_SHOULDER; return true; }
  if (strcasecmp(name, "Back") == 0) { *outMask = BUTTON_BACK; return true; }
  if (strcasecmp(name, "Start") == 0) { *outMask = BUTTON_START; return true; }
  if (strcasecmp(name, "Up") == 0) { *outMask = BUTTON_UP; return true; }
  if (strcasecmp(name, "Down") == 0) { *outMask = BUTTON_DOWN; return true; }
  if (strcasecmp(name, "Left") == 0) { *outMask = BUTTON_LEFT; return true; }
  if (strcasecmp(name, "Right") == 0) { *outMask = BUTTON_RIGHT; return true; }
  return false;
}

static bool parseTrigger(const char* name, int* outIdx) {
  if (!name || !outIdx) return false;
  if (strcasecmp(name, "LeftTrigger") == 0) { *outIdx = TRIGGER_LEFT; return true; }
  if (strcasecmp(name, "RightTrigger") == 0) { *outIdx = TRIGGER_RIGHT; return true; }
  return false;
}

void applyProfile(const GameProfile* profile, Xbox360Report* report, float axisDeadzone) {
  if (!profile || !report) return;

  if (profile->numTrackers > 0) {
    const char* ids[MAX_TRACKERS];
    for (int i = 0; i < profile->numTrackers && i < MAX_TRACKERS; i++)
      ids[i] = profile->trackers[i];
    updateFloorFromTrackers(ids, profile->numTrackers);
  }

  for (int i = 0; i < profile->numAxisMappings; i++) {
    const AxisMapping* m = &profile->axisMappings[i];
    TrackerState* t = trackerMapGet(m->tracker);
    if (!t) continue;
    float raw = getAxisSourceValue(t, m->source) * m->scale * (m->invert ? -1.0f : 1.0f);
    int idx;
    if (!parseAxis(m->axis, &idx)) continue;
    int16_t v = applyDeadzone(raw, axisDeadzone);
    if (idx == AXIS_LEFT_THUMB_X)  report->axisLeftX  = v;
    if (idx == AXIS_LEFT_THUMB_Y)  report->axisLeftY  = v;
    if (idx == AXIS_RIGHT_THUMB_X) report->axisRightX = v;
    if (idx == AXIS_RIGHT_THUMB_Y) report->axisRightY = v;
  }

  for (int i = 0; i < profile->numButtonMappings; i++) {
    const ButtonMapping* m = &profile->buttonMappings[i];
    bool value = evaluateCondition(&m->condition);
    uint16_t mask;
    if (!parseButton(m->button, &mask)) continue;
    if (value) report->buttons |= mask;
    else report->buttons &= ~mask;
  }

  for (int i = 0; i < profile->numTriggerMappings; i++) {
    const TriggerMapping* m = &profile->triggerMappings[i];
    bool cond = evaluateCondition(&m->condition);
    uint8_t v = cond ? m->valueWhenTrue : m->valueWhenFalse;
    int idx;
    if (!parseTrigger(m->trigger, &idx)) continue;
    if (idx == TRIGGER_LEFT)  report->triggerLeft  = v;
    if (idx == TRIGGER_RIGHT) report->triggerRight = v;
  }
}
