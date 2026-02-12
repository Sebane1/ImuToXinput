#ifndef CONFIG_LOADER_H
#define CONFIG_LOADER_H

#include "Config.h"

// Load a GameProfile from a JSON file on LittleFS. Returns true on success.
bool loadConfig(const char* path, GameProfile* out);

#endif
