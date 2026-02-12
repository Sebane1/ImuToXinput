/**
 * SolarXR / SlimeVR WebSocket client.
 * Connects to ws://host:21110, subscribes to data feed, parses MessageBundle,
 * updates TrackerState for bones (BodyPart, HeadPositionG, RotationG).
 * Protocol: https://github.com/SlimeVR/SolarXR-Protocol
 */

#ifndef SOLARXR_CLIENT_H
#define SOLARXR_CLIENT_H

#include <Arduino.h>

// Connect to SolarXR server (call when WiFi ready)
void solarxrClientConnect(const char* host, uint16_t port = 21110);

// Poll for messages (call every loop)
void solarxrClientPoll(void);

// Check if connected
bool solarxrClientConnected(void);

#endif
