/**
 * Config structures matching PC ImuToXInput GameProfile (axis/button/trigger mappings).
 * Fixed-size arrays to bound RAM on ESP32.
 */

#ifndef CONFIG_H
#define CONFIG_H

#define MAX_AXIS_MAPPINGS   12
#define MAX_BUTTON_MAPPINGS 24
#define MAX_TRIGGER_MAPPINGS 12
#define MAX_TRACKERS        8
#define MAX_STRING_LEN      32

// Condition types: euler_threshold, euler_diff, euler_sum, position_threshold
struct MappingCondition {
  char type[MAX_STRING_LEN];
  char tracker[MAX_STRING_LEN];
  char trackerA[MAX_STRING_LEN];
  char trackerB[MAX_STRING_LEN];
  char component[4];   // "X", "Y", "Z"
  char source[MAX_STRING_LEN];
  char op[MAX_STRING_LEN];
  float value;
};

struct AxisMapping {
  char tracker[MAX_STRING_LEN];
  char source[MAX_STRING_LEN];
  float scale;
  bool invert;
  char axis[MAX_STRING_LEN];
};

struct ButtonMapping {
  MappingCondition condition;
  char button[MAX_STRING_LEN];
};

struct TriggerMapping {
  MappingCondition condition;
  char trigger[MAX_STRING_LEN];
  uint8_t valueWhenTrue;
  uint8_t valueWhenFalse;
};

struct GameProfile {
  char name[MAX_STRING_LEN];
  AxisMapping axisMappings[MAX_AXIS_MAPPINGS];
  int numAxisMappings;
  ButtonMapping buttonMappings[MAX_BUTTON_MAPPINGS];
  int numButtonMappings;
  TriggerMapping triggerMappings[MAX_TRIGGER_MAPPINGS];
  int numTriggerMappings;
  char trackers[MAX_TRACKERS][MAX_STRING_LEN];
  int numTrackers;
};

void profileInit(GameProfile* p);

#endif
