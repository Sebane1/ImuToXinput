/**
 * Applies GameProfile to tracker state and fills an Xbox360Report.
 * Port of PC ConfigApplier logic (axis source, conditions, deadzone).
 */

#ifndef MAPPING_ENGINE_H
#define MAPPING_ENGINE_H

#include "Config.h"
#include "TrackerState.h"
#include "Xbox360Report.h"

const float DEFAULT_DEADZONE = 0.2f;

// Clamp raw value to [-1,1], apply deadzone, scale to int16
int16_t applyDeadzone(float value, float deadzone = DEFAULT_DEADZONE);

// Evaluate a condition against current tracker map
bool evaluateCondition(const MappingCondition* c);

// Apply profile to current trackers and fill report
void applyProfile(const GameProfile* profile, Xbox360Report* report, float axisDeadzone = DEFAULT_DEADZONE);

#endif
