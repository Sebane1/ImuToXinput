/**
 * Tracker state: position, rotation (Euler), floor-relative position.
 * Mirrors PC ImuToXInput TrackerState + TrackingEnvironment.
 */

#ifndef TRACKER_STATE_H
#define TRACKER_STATE_H

#define MAX_TRACKER_ID_LEN 32
#define MAX_TRACKERS       32

struct Vec3 {
  float x, y, z;
};

struct TrackerState {
  char bodyPart[MAX_TRACKER_ID_LEN];
  Vec3 position;           // raw position
  Vec3 positionCalibration;
  Vec3 euler;              // rotation (degrees): X, Y, Z
  Vec3 eulerCalibration;
};

// Floor Y (world) for FloorRelativePosition; updated from LEFT_FOOT / RIGHT_FOOT
extern float g_floorY;

inline Vec3 calibratedPosition(const TrackerState* t) {
  return {
    t->position.x - t->positionCalibration.x,
    t->position.y - t->positionCalibration.y,
    t->position.z - t->positionCalibration.z
  };
}

inline Vec3 floorRelativePosition(const TrackerState* t) {
  Vec3 c = calibratedPosition(t);
  return { c.x, c.y - g_floorY, c.z };
}

void trackerMapInit(void);
TrackerState* trackerMapGet(const char* bodyPart);
void trackerMapSet(const char* bodyPart, const TrackerState* state);
void updateFloorFromTrackers(const char* trackerIds[], int count);

#endif
