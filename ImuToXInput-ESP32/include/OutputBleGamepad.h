/**
 * BLE HID gamepad output (ESP32-PICO-D4, no native USB).
 * Translates Xbox360Report to ESP32-BLE-Gamepad API.
 * Works with PC, Steam Deck, and Mayflash adapters (generic BLE controller).
 */

#ifndef OUTPUT_BLE_GAMEPAD_H
#define OUTPUT_BLE_GAMEPAD_H

#include "Xbox360Report.h"

#ifdef USE_BLE_GAMEPAD

void outputBleGamepadInit(void);
void outputBleGamepadSend(const Xbox360Report* report);
bool outputBleGamepadConnected(void);

#else

static inline void outputBleGamepadInit(void) { (void)0; }
static inline void outputBleGamepadSend(const Xbox360Report* report) { (void)report; }
static inline bool outputBleGamepadConnected(void) { return false; }

#endif

#endif
