#include "../include/Config.h"
#include <string.h>

void profileInit(GameProfile* p) {
  memset(p, 0, sizeof(GameProfile));
  p->numAxisMappings = 0;
  p->numButtonMappings = 0;
  p->numTriggerMappings = 0;
  p->numTrackers = 0;
}
