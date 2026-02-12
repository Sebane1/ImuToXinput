/**
 * Xbox 360 controller HID report (wired).
 * Axes: left/right thumb X/Y as int16 (-32768..32767).
 * Buttons: bitmask. Triggers: 0..255.
 */

#ifndef XBOX360_REPORT_H
#define XBOX360_REPORT_H

#include <stdint.h>

// Axis indices for internal use
enum AxisIndex {
  AXIS_LEFT_THUMB_X,
  AXIS_LEFT_THUMB_Y,
  AXIS_RIGHT_THUMB_X,
  AXIS_RIGHT_THUMB_Y,
  AXIS_COUNT
};

// Button bitmask (match XInput order)
#define BUTTON_A            (1 << 0)
#define BUTTON_B            (1 << 1)
#define BUTTON_X            (1 << 2)
#define BUTTON_Y            (1 << 3)
#define BUTTON_LEFT_SHOULDER (1 << 4)
#define BUTTON_RIGHT_SHOULDER (1 << 5)
#define BUTTON_BACK         (1 << 6)
#define BUTTON_START        (1 << 7)
#define BUTTON_UP           (1 << 8)
#define BUTTON_DOWN         (1 << 9)
#define BUTTON_LEFT         (1 << 10)
#define BUTTON_RIGHT        (1 << 11)

#define TRIGGER_LEFT  0
#define TRIGGER_RIGHT 1

struct Xbox360Report {
  int16_t axisLeftX;
  int16_t axisLeftY;
  int16_t axisRightX;
  int16_t axisRightY;
  uint16_t buttons;
  uint8_t triggerLeft;
  uint8_t triggerRight;
};

void reportClear(Xbox360Report* r);

#endif
