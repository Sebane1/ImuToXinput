/**
 * BLE HID gamepad output implementation.
 * Only compiled when USE_BLE_GAMEPAD is defined (esp32-pico-d4 env).
 */

#ifdef USE_BLE_GAMEPAD

#include "OutputBleGamepad.h"
#include <BleGamepad.h>

static BleGamepad* bleGamepad = nullptr;

// Map Xbox360Report buttons (bits 0-11) to BleGamepad BUTTON_1..BUTTON_12
#define XB_TO_BLE(bit) ((bit) + 1)  // BUTTON_1 = 1, etc.

void outputBleGamepadInit(void) {
  if (bleGamepad) return;
  bleGamepad = new BleGamepad("ImuToXInput", "SlimeVR", 100);
  bleGamepad->begin();
}

void outputBleGamepadSend(const Xbox360Report* report) {
  if (!bleGamepad || !report) return;
  if (!bleGamepad->isConnected()) return;

  // Left stick
  bleGamepad->setLeftThumb(report->axisLeftX, -report->axisLeftY);

  // Right stick
  bleGamepad->setRightThumb(report->axisRightX, -report->axisRightY);

  // Triggers: Xbox 0-255 -> HID 0-32767
  bleGamepad->setLeftTrigger((int16_t)((report->triggerLeft * 32767) / 255));
  bleGamepad->setRightTrigger((int16_t)((report->triggerRight * 32767) / 255));

  // Buttons: reset then press each set bit (1-12)
  bleGamepad->resetButtons();
  for (int i = 0; i < 12; i++) {
    if (report->buttons & (1 << i)) {
      bleGamepad->press(XB_TO_BLE(i));
    }
  }

  bleGamepad->sendReport();
}

bool outputBleGamepadConnected(void) {
  return bleGamepad && bleGamepad->isConnected();
}

#endif
