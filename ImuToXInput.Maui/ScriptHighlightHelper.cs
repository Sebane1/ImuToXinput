using System.Text.RegularExpressions;

namespace ImuToXInput.Maui;

/// <summary>
/// Computes syntax highlight spans for the config script (keywords, strings, numbers, comments).
/// Used for read-only preview; MAUI Editor has no built-in rich text.
/// </summary>
public static class ScriptHighlightHelper
{
    public const int DefaultIndex = 0;
    public const int StringIndex = 1;
    public const int KeywordIndex = 2;
    public const int NumberIndex = 3;
    public const int CommentIndex = 4;

    /// <summary>Returns (Start, Length, ColorIndex) for the given script text.</summary>
    public static List<(int Start, int Length, int ColorIndex)> GetScriptHighlightSpans(string text)
    {
        const int stringIdx = 1, keywordIdx = 2, numberIdx = 3, commentIdx = 4;
        var spans = new List<(int Start, int Length, int ColorIndex)>();
        var used = new bool[Math.Max(text.Length, 1)];

        // Comments: // to end of line
        foreach (Match m in Regex.Matches(text, @"//[^\r\n]*"))
        {
            for (int i = m.Index; i < m.Index + m.Length && i < used.Length; i++)
                used[i] = true;
            spans.Add((m.Index, m.Length, commentIdx));
        }

        // Strings
        var stringRanges = new List<(int Start, int End)>();
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] != '"') continue;
            int start = i;
            i++;
            while (i < text.Length)
            {
                if (text[i] == '\\' && i + 1 < text.Length) { i += 2; continue; }
                if (text[i] == '"') { i++; break; }
                i++;
            }
            int end = i;
            if (end > start + 1)
            {
                for (int j = start; j < end && j < used.Length; j++) used[j] = true;
                spans.Add((start, end - start, stringIdx));
                stringRanges.Add((start, end));
            }
            i--;
        }

        bool IsInsideString(int start, int length)
        {
            int end = start + length;
            foreach (var (rStart, rEnd) in stringRanges)
                if (start < rEnd && end > rStart) return true;
            return false;
        }

        // Keywords at line start
        var lineStartKeywords = new[] { "processNames", "trackers", "name", "axis", "button", "trigger" };
        foreach (var kw in lineStartKeywords)
        {
            int pos = 0;
            while (pos < text.Length && (pos = text.IndexOf(kw, pos, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                bool atLineStart = IsAtLineStart(text, pos);
                bool wordEnd = pos + kw.Length >= text.Length || !IsWordChar(text[pos + kw.Length]);
                if (atLineStart && wordEnd && !IsInsideCommentLine(text, pos) && !IsInsideString(pos, kw.Length))
                {
                    bool anyUsed = false;
                    for (int i = pos; i < pos + kw.Length && i < used.Length; i++)
                    { if (used[i]) { anyUsed = true; break; } }
                    if (!anyUsed)
                    {
                        for (int i = pos; i < pos + kw.Length && i < used.Length; i++)
                            used[i] = true;
                        spans.Add((pos, kw.Length, keywordIdx));
                    }
                }
                pos += kw.Length;
            }
        }

        // "when" mid-line
        const string whenKw = "when";
        int whenPos = 0;
        while (whenPos < text.Length && (whenPos = text.IndexOf(whenKw, whenPos, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            bool wordStart = whenPos == 0 || !IsWordChar(text[whenPos - 1]);
            bool wordEnd = whenPos + whenKw.Length >= text.Length || !IsWordChar(text[whenPos + whenKw.Length]);
            if (wordStart && wordEnd && !IsInsideCommentLine(text, whenPos) && !IsInsideString(whenPos, whenKw.Length))
            {
                bool anyUsed = false;
                for (int i = whenPos; i < whenPos + whenKw.Length && i < used.Length; i++)
                { if (used[i]) { anyUsed = true; break; } }
                if (!anyUsed)
                {
                    for (int i = whenPos; i < whenPos + whenKw.Length && i < used.Length; i++)
                        used[i] = true;
                    spans.Add((whenPos, whenKw.Length, keywordIdx));
                }
            }
            whenPos += whenKw.Length;
        }

        // Numbers
        foreach (Match m in Regex.Matches(text, @"\b-?\d+\.?\d*\b"))
        {
            bool anyUsed = false;
            for (int i = m.Index; i < m.Index + m.Length && i < used.Length; i++)
            { if (used[i]) { anyUsed = true; break; } }
            if (!anyUsed)
                spans.Add((m.Index, m.Length, numberIdx));
        }

        int Order(int idx) => idx == commentIdx ? 0 : idx == numberIdx ? 1 : idx == keywordIdx ? 2 : 3;
        spans.Sort((a, b) => Order(a.ColorIndex) != Order(b.ColorIndex) ? Order(a.ColorIndex).CompareTo(Order(b.ColorIndex)) : a.Start.CompareTo(b.Start));
        return spans;
    }

    /// <summary>One line for preview: list of (text segment, color index).</summary>
    public static List<List<(string Text, int ColorIndex)>> GetHighlightedLines(string text)
    {
        var spans = GetScriptHighlightSpans(text);
        var colorAt = new int[text.Length];
        foreach (var (start, length, idx) in spans)
            for (int i = start; i < start + length && i < text.Length; i++)
                colorAt[i] = idx;

        var lines = new List<List<(string Text, int ColorIndex)>>();
        int pos = 0;
        while (pos < text.Length)
        {
            int lineEnd = text.IndexOf('\n', pos);
            if (lineEnd < 0) lineEnd = text.Length;
            var lineLen = lineEnd - pos;
            var lineSegments = new List<(string Text, int ColorIndex)>();
            int i = pos;
            while (i < lineEnd)
            {
                int segColor = colorAt[i];
                int j = i + 1;
                while (j < lineEnd && colorAt[j] == segColor) j++;
                string seg = text.Substring(i, j - i);
                if (seg.Length > 0)
                    lineSegments.Add((seg, segColor));
                i = j;
            }
            lines.Add(lineSegments);
            pos = lineEnd + (lineEnd < text.Length ? 1 : 0);
        }
        if (lines.Count == 0)
            lines.Add(new List<(string Text, int ColorIndex)>());
        return lines;
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static bool IsAtLineStart(string text, int position)
    {
        if (position <= 0) return true;
        int i = position - 1;
        while (i >= 0 && (text[i] == ' ' || text[i] == '\t')) i--;
        if (i < 0) return true;
        return text[i] == '\n' || text[i] == '\r';
    }

    private static bool IsInsideCommentLine(string text, int position)
    {
        int lineStart = position;
        while (lineStart > 0 && text[lineStart - 1] != '\n' && text[lineStart - 1] != '\r')
            lineStart--;
        int i = lineStart;
        while (i < text.Length && (text[i] == ' ' || text[i] == '\t')) i++;
        return i + 1 < text.Length && text[i] == '/' && text[i + 1] == '/';
    }
}
