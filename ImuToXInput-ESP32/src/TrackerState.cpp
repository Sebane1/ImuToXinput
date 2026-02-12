#include "../include/TrackerState.h"
#include <string.h>

float g_floorY = 0.0f;

static TrackerState s_trackers[MAX_TRACKERS];
static int s_count = 0;

static int findSlot(const char* bodyPart) {
  for (int i = 0; i < s_count; i++) {
    if (strcasecmp(s_trackers[i].bodyPart, bodyPart) == 0)
      return i;
  }
  return -1;
}

void trackerMapInit(void) {
  memset(s_trackers, 0, sizeof(s_trackers));
  s_count = 0;
}

TrackerState* trackerMapGet(const char* bodyPart) {
  int i = findSlot(bodyPart);
  return i >= 0 ? &s_trackers[i] : nullptr;
}

void trackerMapSet(const char* bodyPart, const TrackerState* state) {
  int i = findSlot(bodyPart);
  if (i >= 0) {
    s_trackers[i] = *state;
    return;
  }
  if (s_count >= MAX_TRACKERS) return;
  s_trackers[s_count] = *state;
  strncpy(s_trackers[s_count].bodyPart, bodyPart, MAX_TRACKER_ID_LEN - 1);
  s_trackers[s_count].bodyPart[MAX_TRACKER_ID_LEN - 1] = '\0';
  s_count++;
}

void updateFloorFromTrackers(const char* trackerIds[], int count) {
  float minY = 1e9f;
  for (int i = 0; i < count; i++) {
    const char* id = trackerIds[i];
    if (strcasecmp(id, "LEFT_FOOT") != 0 && strcasecmp(id, "RIGHT_FOOT") != 0)
      continue;
    TrackerState* t = trackerMapGet(id);
    if (!t) continue;
    Vec3 c = calibratedPosition(t);
    if (c.y < minY) minY = c.y;
  }
  if (minY < 1e9f)
    g_floorY = minY;
}
