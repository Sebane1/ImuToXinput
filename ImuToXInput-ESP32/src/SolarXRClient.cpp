/**
 * SolarXR WebSocket client implementation.
 * Uses ArduinoWebsockets + SolarXR FlatBuffers protocol.
 */

#include "SolarXRClient.h"
#include "TrackerState.h"
#include <ArduinoWebsockets.h>
#include "solarxr_protocol/generated/all_generated.h"
#include <string.h>
#include <math.h>

using namespace websockets;

static WebsocketsClient wsClient;
static bool s_connected = false;
static bool s_subscriptionSent = false;

static const int DATA_FEED_UPDATE_MS = 5;  // 200 Hz

// RHS to LHS: SlimeVR uses right-handed, we use left-handed
static void rhsToLhsVec3(float x, float y, float z, float* out) {
  out[0] = x;
  out[1] = y;
  out[2] = -z;
}

// RHS quat to LHS: (x, y, -z, -w), then apply -90 deg around X
static void rhsToLhsQuat(float qx, float qy, float qz, float qw, float* out) {
  float lx = qx, ly = qy, lz = -qz, lw = -qw;
  // Rotate -90 around X: qx90 = (sin(-45), 0, 0, cos(-45)) = (-0.707, 0, 0, 0.707)
  float cx = -0.7071068f, cw = 0.7071068f;
  out[0] = cw * lx + cx * lw;
  out[1] = cw * ly - cx * lz;
  out[2] = cw * lz + cx * ly;
  out[3] = cw * lw - cx * lx;
}

// Quaternion to Euler (degrees), matching CoordinateUtility.QuaternionToEuler
static void quatToEuler(float qx, float qy, float qz, float qw, float* ex, float* ey, float* ez) {
  float sinr_cosp = 2.0f * (qw * qx + qy * qz);
  float cosr_cosp = 1.0f - 2.0f * (qx * qx + qy * qy);
  *ex = atan2f(sinr_cosp, cosr_cosp) * (180.0f / 3.14159265f);

  float sinp = 2.0f * (qw * qy - qz * qx);
  if (fabsf(sinp) >= 1.0f)
    *ey = copysignf(90.0f, sinp);
  else
    *ey = asinf(sinp) * (180.0f / 3.14159265f);

  float siny_cosp = 2.0f * (qw * qz + qx * qy);
  float cosy_cosp = 1.0f - 2.0f * (qy * qy + qz * qz);
  *ez = atan2f(siny_cosp, cosy_cosp) * (180.0f / 3.14159265f);
}

// Build and send StartDataFeed subscription message
static void sendStartDataFeed(void) {
  flatbuffers::FlatBufferBuilder builder(512);

  using namespace solarxr_protocol::data_feed;
  using namespace solarxr_protocol::data_feed::tracker;
  using namespace solarxr_protocol::data_feed::device_data;

  auto physicalMask = CreateTrackerDataMask(builder, true, true, true, true,
      false, false, false, false, true, true, false, false, false);
  auto deviceMask = CreateDeviceDataMask(builder, physicalMask, true);
  auto syntheticMask = CreateTrackerDataMask(builder, true, true, true, true,
      false, false, false, false, true, true, false, false, false);

  auto config = CreateDataFeedConfig(builder, DATA_FEED_UPDATE_MS, deviceMask,
      syntheticMask, true, false);
  flatbuffers::Offset<flatbuffers::Vector<flatbuffers::Offset<DataFeedConfig>>> configs =
      builder.CreateVector(std::vector<flatbuffers::Offset<DataFeedConfig>>{ config });

  auto startFeed = CreateStartDataFeed(builder, configs);
  auto msgHeader = CreateDataFeedMessageHeader(builder, DataFeedMessage::StartDataFeed, startFeed.Union());
  auto dfMsgs = builder.CreateVector(std::vector<flatbuffers::Offset<DataFeedMessageHeader>>{ msgHeader });

  auto rpcMsgs = builder.CreateVector(std::vector<flatbuffers::Offset<solarxr_protocol::rpc::RpcMessageHeader>>{});
  auto pubSubMsgs = builder.CreateVector(std::vector<flatbuffers::Offset<solarxr_protocol::pub_sub::PubSubHeader>>{});

  auto bundle = solarxr_protocol::CreateMessageBundle(builder, dfMsgs, rpcMsgs, pubSubMsgs);
  builder.Finish(bundle);

  wsClient.sendBinary((const char*)builder.GetBufferPointer(), builder.GetSize());
  Serial.println("SolarXR: sent StartDataFeed subscription");
}

static void handleWebSocketEvent(WebsocketsEvent event, WSInterfaceString data) {
  switch (event) {
    case WebsocketsEvent::ConnectionOpened:
      s_connected = true;
      s_subscriptionSent = false;
      Serial.println("SolarXR: connected");
      break;
    case WebsocketsEvent::ConnectionClosed:
      s_connected = false;
      s_subscriptionSent = false;
      Serial.println("SolarXR: disconnected");
      break;
    case WebsocketsEvent::GotPing:
    case WebsocketsEvent::GotPong:
      break;
    default:
      break;
  }
}

static void handleBinaryMessage(WebsocketsMessage msg) {
  if (!msg.isBinary() || msg.length() < 8) return;

  const uint8_t* buf = (const uint8_t*)msg.rawData().c_str();
  size_t len = msg.length();
  if (len < 8) return;

  auto bundle = solarxr_protocol::GetMessageBundle(buf);
  if (!bundle || !bundle->data_feed_msgs()) return;

  for (flatbuffers::uoffset_t i = 0; i < bundle->data_feed_msgs()->size(); i++) {
    auto dfMsg = bundle->data_feed_msgs()->Get(i);
    if (!dfMsg || dfMsg->message_type() != solarxr_protocol::data_feed::DataFeedMessage::DataFeedUpdate)
      continue;

    auto update = dfMsg->message_as_DataFeedUpdate();
    if (!update || !update->bones()) continue;

    for (flatbuffers::uoffset_t b = 0; b < update->bones()->size(); b++) {
      auto bone = update->bones()->Get(b);
      if (!bone || !bone->rotation_g()) continue;

      auto bodyPart = bone->body_part();
      const char* name = solarxr_protocol::datatypes::EnumNameBodyPart(bodyPart);
      if (!name || !name[0]) continue;

      auto* q = bone->rotation_g();
      float qx = q->x(), qy = q->y(), qz = q->z(), qw = q->w();
      float lhs[4];
      rhsToLhsQuat(qx, qy, qz, qw, lhs);
      float ex, ey, ez;
      quatToEuler(lhs[0], lhs[1], lhs[2], lhs[3], &ex, &ey, &ez);

      float px = 0, py = 0, pz = 0;
      if (bone->head_position_g()) {
        auto* p = bone->head_position_g();
        float posLhs[3];
        rhsToLhsVec3(p->x(), p->y(), p->z(), posLhs);
        px = posLhs[0]; py = posLhs[1]; pz = posLhs[2];
      }

      TrackerState* existing = trackerMapGet(name);
      TrackerState state = {};
      strncpy(state.bodyPart, name, MAX_TRACKER_ID_LEN - 1);
      state.bodyPart[MAX_TRACKER_ID_LEN - 1] = '\0';
      state.position.x = px;
      state.position.y = py;
      state.position.z = pz;
      state.euler.x = ex;
      state.euler.y = ey;
      state.euler.z = ez;

      if (existing) {
        state.positionCalibration = existing->positionCalibration;
        state.eulerCalibration = existing->eulerCalibration;
      } else {
        state.positionCalibration.x = px;
        state.positionCalibration.y = py;
        state.positionCalibration.z = pz;
        state.eulerCalibration.x = ex;
        state.eulerCalibration.y = ey;
        state.eulerCalibration.z = ez;
      }

      trackerMapSet(name, &state);
    }
  }
}

static void handleMessage(WebsocketsMessage msg) {
  if (msg.isBinary())
    handleBinaryMessage(msg);
}

void solarxrClientConnect(const char* host, uint16_t port) {
  if (!host || !host[0]) return;

  String uri = "ws://" + String(host) + ":" + String(port);
  wsClient.onEvent(handleWebSocketEvent);
  wsClient.onMessage(handleMessage);
  wsClient.connect(host, port, "/");
  Serial.println("SolarXR: connecting to " + uri);
}

void solarxrClientPoll(void) {
  wsClient.poll();
  if (s_connected && !s_subscriptionSent) {
    sendStartDataFeed();
    s_subscriptionSent = true;
  }
}

bool solarxrClientConnected(void) {
  return s_connected;
}
