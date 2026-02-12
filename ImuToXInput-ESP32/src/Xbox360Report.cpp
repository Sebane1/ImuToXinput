#include "../include/Xbox360Report.h"
#include <string.h>

void reportClear(Xbox360Report* r) {
  if (!r) return;
  memset(r, 0, sizeof(Xbox360Report));
}
