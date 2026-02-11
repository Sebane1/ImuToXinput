using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ImuToXInput.Config
{
    /// <summary>
    /// Deserializes "condition" objects based on "type" field into the correct condition class.
    /// Populates manually to avoid re-entering this converter (which would cause stack overflow).
    /// </summary>
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
            throw new NotImplementedException("Serialization not needed for config");
        }
    }
}
