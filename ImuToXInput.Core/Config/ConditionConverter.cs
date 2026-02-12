using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ImuToXInput.Config
{
    public class ConditionConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(MappingCondition);

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            var jo = JObject.Load(reader);
            var type = jo["type"]?.ToString() ?? "";

            return type switch
            {
                "euler_threshold" => ReadEulerThreshold(jo),
                "euler_diff" => ReadEulerDiff(jo),
                "euler_sum" => ReadEulerSum(jo),
                "position_threshold" => ReadPositionThreshold(jo),
                _ => throw new JsonException($"Unknown condition type: {type}")
            };
        }

        private static EulerThresholdCondition ReadEulerThreshold(JObject jo)
        {
            return new EulerThresholdCondition
            {
                Type = "euler_threshold",
                Tracker = jo["tracker"]?.ToString() ?? "",
                Component = jo["component"]?.ToString() ?? "",
                Op = jo["op"]?.ToString() ?? "greater_than",
                Value = jo["value"]?.Value<float>() ?? 0
            };
        }

        private static EulerDiffCondition ReadEulerDiff(JObject jo)
        {
            return new EulerDiffCondition
            {
                Type = "euler_diff",
                TrackerA = jo["trackerA"]?.ToString() ?? "",
                TrackerB = jo["trackerB"]?.ToString() ?? "",
                Component = jo["component"]?.ToString() ?? "",
                Op = jo["op"]?.ToString() ?? "greater_than",
                Value = jo["value"]?.Value<float>() ?? 0
            };
        }

        private static EulerSumCondition ReadEulerSum(JObject jo)
        {
            return new EulerSumCondition
            {
                Type = "euler_sum",
                TrackerA = jo["trackerA"]?.ToString() ?? "",
                TrackerB = jo["trackerB"]?.ToString() ?? "",
                Component = jo["component"]?.ToString() ?? "",
                Op = jo["op"]?.ToString() ?? "greater_than",
                Value = jo["value"]?.Value<float>() ?? 0
            };
        }

        private static PositionThresholdCondition ReadPositionThreshold(JObject jo)
        {
            return new PositionThresholdCondition
            {
                Type = "position_threshold",
                Tracker = jo["tracker"]?.ToString() ?? "",
                Source = jo["source"]?.ToString() ?? "",
                Op = jo["op"]?.ToString() ?? "greater_than",
                Value = jo["value"]?.Value<float>() ?? 0
            };
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }
            var jo = value switch
            {
                EulerThresholdCondition e => new JObject
                {
                    ["type"] = e.Type,
                    ["tracker"] = e.Tracker,
                    ["component"] = e.Component,
                    ["op"] = e.Op,
                    ["value"] = e.Value
                },
                EulerDiffCondition e => new JObject
                {
                    ["type"] = e.Type,
                    ["trackerA"] = e.TrackerA,
                    ["trackerB"] = e.TrackerB,
                    ["component"] = e.Component,
                    ["op"] = e.Op,
                    ["value"] = e.Value
                },
                EulerSumCondition e => new JObject
                {
                    ["type"] = e.Type,
                    ["trackerA"] = e.TrackerA,
                    ["trackerB"] = e.TrackerB,
                    ["component"] = e.Component,
                    ["op"] = e.Op,
                    ["value"] = e.Value
                },
                PositionThresholdCondition e => new JObject
                {
                    ["type"] = e.Type,
                    ["tracker"] = e.Tracker,
                    ["source"] = e.Source,
                    ["op"] = e.Op,
                    ["value"] = e.Value
                },
                _ => throw new JsonException($"Unknown condition type: {value.GetType().Name}")
            };
            jo.WriteTo(writer);
        }
    }
}
