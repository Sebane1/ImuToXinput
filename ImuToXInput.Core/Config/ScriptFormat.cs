using System.Text;
using System.Text.RegularExpressions;

namespace ImuToXInput.Config
{
    public static class ScriptFormat
    {
        public static string FormatProfile(GameProfile profile)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"name \"{Escape(profile.Name)}\"");
            if (profile.ProcessNames is { Count: > 0 })
                sb.AppendLine("processNames " + string.Join(", ", profile.ProcessNames));
            if (profile.Trackers is { Count: > 0 })
                sb.AppendLine("trackers " + string.Join(", ", profile.Trackers));
            sb.AppendLine();

            sb.AppendLine("// Axis: [−]Tracker.Source [* scale] [invert] -> Axis  (leading − or invert)");
            foreach (var a in profile.AxisMappings)
            {
                var scale = (a.Scale != 1f || a.Invert) ? $" * {a.Scale:G}" : "";
                var prefix = a.Invert ? "-" : "";
                sb.AppendLine($"axis {prefix}{a.Tracker}.{a.Source}{scale} -> {a.Axis}");
            }
            sb.AppendLine();

            sb.AppendLine("// Button: when <expr> -> Button  (e.g. Tracker.Euler.X < 20  or  A.Euler.Y - B.Euler.Y > 30  or  Tracker.FloorRelY > 0.1)");
            foreach (var m in profile.ButtonMappings)
                sb.AppendLine("button " + FormatCondition("when", m.Condition) + " -> " + m.Button);
            sb.AppendLine();

            sb.AppendLine("// Trigger: when <expr> -> Trigger [= value] [= trueVal / falseVal] | trigger LeftTrigger = 128 | trigger LeftTrigger = Tracker.Source");
            sb.AppendLine("// Multiple mappings to same trigger: highest value wins (max of all 0–255).");
            foreach (var m in profile.TriggerMappings)
            {
                if (m.FixedValue.HasValue)
                    sb.AppendLine("trigger " + m.Trigger + " = " + m.FixedValue.Value);
                else if (!string.IsNullOrEmpty(m.Tracker) && !string.IsNullOrEmpty(m.Source))
                    sb.AppendLine("trigger " + m.Trigger + " = " + (m.Invert ? "-" : "") + m.Tracker + "." + m.Source + (m.Scale != 1f ? " * " + m.Scale : ""));
                else if (m.Condition != null)
                {
                    var line = "trigger " + FormatCondition("when", m.Condition) + " -> " + m.Trigger;
                    if (m.ValueWhenTrue != 255 || m.ValueWhenFalse != 0)
                        line += " = " + m.ValueWhenTrue + (m.ValueWhenFalse != 0 ? " / " + m.ValueWhenFalse : "");
                    sb.AppendLine(line);
                }
                else
                    sb.AppendLine("// trigger " + m.Trigger + " (invalid: no condition/fixed/axis)");
            }
            return sb.ToString();
        }

        private static string FormatCondition(string prefix, MappingCondition? c)
        {
            if (c == null) return prefix + " (invalid)";
            var opStr = OpToSymbol(GetConditionOp(c));
            if (c is EulerThresholdCondition e)
                return $"{prefix} {e.Tracker}.Euler.{e.Component} {opStr} {e.Value}";
            if (c is EulerDiffCondition d)
                return $"{prefix} {d.TrackerA}.Euler.{d.Component} - {d.TrackerB}.Euler.{d.Component} {opStr} {d.Value}";
            if (c is EulerSumCondition s)
                return $"{prefix} {s.TrackerA}.Euler.{s.Component} + {s.TrackerB}.Euler.{s.Component} {opStr} {s.Value}";
            if (c is PositionThresholdCondition p)
                return $"{prefix} {p.Tracker}.{p.Source} {opStr} {p.Value}";
            return prefix + " (unknown)";
        }

        private static string GetConditionOp(MappingCondition c)
        {
            if (c is EulerThresholdCondition et) return et.Op;
            if (c is EulerDiffCondition ed) return ed.Op;
            if (c is EulerSumCondition es) return es.Op;
            if (c is PositionThresholdCondition pt) return pt.Op;
            return "greater_than";
        }

        private static string OpToSymbol(string op)
        {
            return op switch
            {
                "less_than" or "lt" => "<",
                "greater_than" or "gt" => ">",
                "less_than_or_equal" or "lte" => "<=",
                "greater_than_or_equal" or "gte" => ">=",
                _ => op
            };
        }

        private static string Escape(string s)
        {
            return (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string Unescape(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Replace("\\\"", "\"").Replace("\\\\", "\\");
        }

        public static GameProfile ParseProfile(string script)
        {
            var profile = new GameProfile
            {
                AxisMappings = new List<AxisMapping>(),
                ButtonMappings = new List<ButtonMapping>(),
                TriggerMappings = new List<TriggerMapping>()
            };
            var lines = script.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var lineNum = 0;
            foreach (var raw in lines)
            {
                lineNum++;
                var line = raw.Trim();
                var comment = line.IndexOf("//", StringComparison.Ordinal);
                if (comment >= 0) line = line.Substring(0, comment).Trim();
                if (string.IsNullOrEmpty(line)) continue;

                if (line.StartsWith("name ", StringComparison.OrdinalIgnoreCase))
                {
                    var rest = line.Substring(5).Trim();
                    profile.Name = ParseQuotedString(rest);
                    continue;
                }
                if (line.StartsWith("processNames ", StringComparison.OrdinalIgnoreCase))
                {
                    var rest = line.Substring(12).Trim();
                    profile.ProcessNames = rest.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                    continue;
                }
                if (line.StartsWith("trackers ", StringComparison.OrdinalIgnoreCase))
                {
                    var rest = line.Substring(9).Trim();
                    profile.Trackers = rest.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                    continue;
                }
                if (line.StartsWith("axis ", StringComparison.OrdinalIgnoreCase))
                {
                    var a = ParseAxisLine(line.Substring(5).Trim());
                    if (a != null) profile.AxisMappings.Add(a);
                    continue;
                }
                if (line.StartsWith("button ", StringComparison.OrdinalIgnoreCase))
                {
                    var (cond, btn) = ParseWhenLine(line.Substring(7).Trim(), lineNum);
                    if (cond != null && btn != null)
                        profile.ButtonMappings.Add(new ButtonMapping { Condition = cond, Button = btn });
                    continue;
                }
                if (line.StartsWith("trigger ", StringComparison.OrdinalIgnoreCase))
                {
                    var rest = line.Substring(8).Trim();
                    // trigger when ... -> LeftTrigger [= 128] or [= 128 / 0] (check first so " = " in RHS isn't treated as fixed)
                    if (rest.StartsWith("when ", StringComparison.OrdinalIgnoreCase))
                    {
                        var (cond, trigWhen) = ParseWhenLine(rest, lineNum);
                        if (cond != null && trigWhen != null)
                        {
                            byte valTrue = 255, valFalse = 0;
                            var triggerName = trigWhen;
                            var eqIdx = trigWhen.IndexOf(" = ", StringComparison.Ordinal);
                            if (eqIdx > 0)
                            {
                                triggerName = trigWhen.Substring(0, eqIdx).Trim();
                                var valPart = trigWhen.Substring(eqIdx + 3).Trim();
                                var slash = valPart.IndexOf(" / ", StringComparison.Ordinal);
                                if (slash > 0)
                                {
                                    if (byte.TryParse(valPart.Substring(0, slash).Trim(), out var vt)) valTrue = vt;
                                    if (byte.TryParse(valPart.Substring(slash + 3).Trim(), out var vf)) valFalse = vf;
                                }
                                else if (byte.TryParse(valPart, out var v))
                                    valTrue = v;
                            }
                            profile.TriggerMappings.Add(new TriggerMapping { Condition = cond, Trigger = triggerName, ValueWhenTrue = valTrue, ValueWhenFalse = valFalse });
                        }
                        continue;
                    }
                    // trigger LeftTrigger = 128 (unconditional fixed) or trigger LeftTrigger = Tracker.Source [* scale] [invert]
                    var eq = rest.IndexOf(" = ", StringComparison.Ordinal);
                    if (eq > 0)
                    {
                        var trig = rest.Substring(0, eq).Trim();
                        var rhs = rest.Substring(eq + 3).Trim();
                        if (!string.IsNullOrEmpty(trig))
                        {
                            if (byte.TryParse(rhs, out byte fixedVal))
                            {
                                profile.TriggerMappings.Add(new TriggerMapping { Trigger = trig, FixedValue = fixedVal });
                                continue;
                            }
                            // Axis: RHS is "[−]Tracker.Source [* scale] [invert]"
                            var parts = rhs.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 1)
                            {
                                var firstPart = parts[0];
                                var invertFromMinus = firstPart.StartsWith("-");
                                if (invertFromMinus) firstPart = firstPart.Substring(1).Trim();
                                var dot = firstPart.IndexOf('.');
                                if (dot >= 0)
                                {
                                    var tracker = firstPart.Substring(0, dot);
                                    var source = firstPart.Substring(dot + 1);
                                    if (!string.IsNullOrEmpty(tracker) && !string.IsNullOrEmpty(source))
                                    {
                                        var axis = new TriggerMapping { Trigger = trig, Tracker = tracker, Source = source, Scale = 1f, Invert = invertFromMinus };
                                        for (var i = 1; i < parts.Length; i++)
                                        {
                                            if (parts[i].Equals("invert", StringComparison.OrdinalIgnoreCase)) axis.Invert = true;
                                            else if (parts[i] == "*" && i + 1 < parts.Length && float.TryParse(parts[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float sc)) { axis.Scale = sc; i++; }
                                        }
                                        profile.TriggerMappings.Add(axis);
                                    }
                                }
                                continue;
                            }
                        }
                    }
                }
            }
            return profile;
        }

        private static string ParseQuotedString(string s)
        {
            s = s.Trim();
            if (s.Length >= 2 && s[0] == '"')
            {
                var end = s.IndexOf('"', 1);
                if (end < 0) return Unescape(s.Substring(1));
                return Unescape(s.Substring(1, end - 1));
            }
            return s;
        }

        private static AxisMapping? ParseAxisLine(string line)
        {
            var arrow = line.IndexOf("->", StringComparison.Ordinal);
            if (arrow < 0) return null;
            var right = line.Substring(arrow + 2).Trim();
            var left = line.Substring(0, arrow).Trim();
            var axis = right;
            var invert = left.StartsWith("-");
            if (invert) left = left.Substring(1).Trim();
            invert = invert || left.Contains("invert", StringComparison.OrdinalIgnoreCase);
            if (left.Contains("invert", StringComparison.OrdinalIgnoreCase)) left = Regex.Replace(left, @"\binvert\b", "", RegexOptions.IgnoreCase).Trim();
            float scale = 1f;
            var star = left.IndexOf('*');
            if (star >= 0)
            {
                var after = left.Substring(star + 1).Trim();
                left = left.Substring(0, star).Trim();
                if (float.TryParse(after, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var sc))
                    scale = sc;
            }
            var dot = left.IndexOf('.');
            if (dot < 0) return null;
            var tracker = left.Substring(0, dot).Trim();
            var source = left.Substring(dot + 1).Trim();
            return new AxisMapping { Tracker = tracker, Source = source, Scale = scale, Invert = invert, Axis = axis };
        }

        private static (MappingCondition? cond, string? output) ParseWhenLine(string line, int lineNum)
        {
            if (!line.StartsWith("when ", StringComparison.OrdinalIgnoreCase)) return (null, null);
            var rest = line.Substring(5).Trim();
            var arrow = rest.IndexOf("->", StringComparison.Ordinal);
            if (arrow < 0) return (null, null);
            var output = rest.Substring(arrow + 2).Trim();
            var expr = rest.Substring(0, arrow).Trim();
            var cond = ParseCondition(expr);
            return (cond, output);
        }

        private static MappingCondition? ParseCondition(string expr)
        {
            expr = expr.Trim();
            var opMatch = Regex.Match(expr, @"\s+(<|>|<=|>=)\s+(-?[\d.]+)\s*$");
            if (!opMatch.Success) return null;
            var opStr = opMatch.Groups[1].Value;
            var valueStr = opMatch.Groups[2].Value;
            if (!float.TryParse(valueStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value))
                return null;
            var op = SymbolToOp(opStr);
            var leftPart = expr.Substring(0, opMatch.Index).Trim();

            var diffIdx = leftPart.IndexOf(" - ", StringComparison.Ordinal);
            if (diffIdx > 0)
            {
                var a = leftPart.Substring(0, diffIdx).Trim();
                var b = leftPart.Substring(diffIdx + 3).Trim();
                if (ParseEulerPart(a, out var trackerA, out var comp) && ParseEulerPart(b, out var trackerB, out _))
                    return new EulerDiffCondition { Type = "euler_diff", TrackerA = trackerA, TrackerB = trackerB, Component = comp, Op = op, Value = value };
            }
            var sumIdx = leftPart.IndexOf(" + ", StringComparison.Ordinal);
            if (sumIdx > 0)
            {
                var a = leftPart.Substring(0, sumIdx).Trim();
                var b = leftPart.Substring(sumIdx + 3).Trim();
                if (ParseEulerPart(a, out var trackerA, out var comp) && ParseEulerPart(b, out var trackerB, out _))
                    return new EulerSumCondition { Type = "euler_sum", TrackerA = trackerA, TrackerB = trackerB, Component = comp, Op = op, Value = value };
            }
            if (leftPart.Contains(".Euler.", StringComparison.Ordinal))
            {
                if (ParseEulerPart(leftPart, out var tracker, out var comp))
                    return new EulerThresholdCondition { Type = "euler_threshold", Tracker = tracker, Component = comp, Op = op, Value = value };
            }
            var dot = leftPart.IndexOf('.');
            if (dot > 0)
            {
                var tracker = leftPart.Substring(0, dot).Trim();
                var source = leftPart.Substring(dot + 1).Trim();
                return new PositionThresholdCondition { Type = "position_threshold", Tracker = tracker, Source = source, Op = op, Value = value };
            }
            return null;
        }

        private static bool ParseEulerPart(string part, out string tracker, out string component)
        {
            tracker = ""; component = "";
            var euler = part.IndexOf(".Euler.", StringComparison.OrdinalIgnoreCase);
            if (euler < 0) return false;
            tracker = part.Substring(0, euler).Trim();
            component = part.Substring(euler + 7).Trim();
            if (component.Length != 1 || "XYZ".IndexOf(component.ToUpperInvariant()[0]) < 0) return false;
            component = component.ToUpperInvariant();
            return true;
        }

        private static string SymbolToOp(string sym)
        {
            return sym switch
            {
                "<" => "less_than",
                ">" => "greater_than",
                "<=" => "less_than_or_equal",
                ">=" => "greater_than_or_equal",
                _ => "greater_than"
            };
        }
    }
}
