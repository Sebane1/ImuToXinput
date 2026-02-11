using System.Text.RegularExpressions;
using ImuToXInput.Config;

namespace ImuToXInput.ConfigEditor;

public partial class GameProfileEditorForm : Form
{
    private readonly GameProfile _profile;
    private readonly bool _isNew;
    private string _fileName;
    private System.Windows.Forms.Timer? _scriptHighlightTimer;
    private bool _applyingScriptHighlight;

    public GameProfile? SavedProfile { get; private set; }
    public string? SavedFileName { get; private set; }

    public GameProfileEditorForm(GameProfile profile, bool isNew, string suggestedFileName)
    {
        _profile = profile;
        _isNew = isNew;
        _fileName = suggestedFileName;
        InitializeComponent();
        _scriptHighlightTimer = new System.Windows.Forms.Timer { Interval = 250 };
        _scriptHighlightTimer.Tick += (_, _) =>
        {
            _scriptHighlightTimer!.Stop();
            ApplyScriptHighlighting();
        };
        LoadProfile();
    }

    private void txtScript_TextChanged(object? sender, EventArgs e)
    {
        if (_applyingScriptHighlight) return;
        _scriptHighlightTimer?.Stop();
        _scriptHighlightTimer?.Start();
    }

    private void ApplyScriptHighlighting()
    {
        if (txtScript == null || _applyingScriptHighlight) return;
        _applyingScriptHighlight = true;
        var raw = txtScript.Text;
        if (raw.Length == 0) { _applyingScriptHighlight = false; return; }
        // Normalize line endings so span positions match the control
        var text = raw.Replace("\r\n", "\n").Replace("\r", "\n");
        if (text != raw) txtScript.Text = text;
        int selStart = txtScript.SelectionStart;
        int selLen = txtScript.SelectionLength;

        try
        {
            var spans = GetScriptHighlightSpans(text);
            var rtf = BuildRtf(text, spans);
            if (!string.IsNullOrEmpty(rtf))
            {
                txtScript.Rtf = rtf;
                selStart = Math.Clamp(selStart, 0, text.Length);
                selLen = Math.Clamp(selLen, 0, text.Length - selStart);
                txtScript.SelectionStart = selStart;
                txtScript.SelectionLength = selLen;
            }
        }
        finally
        {
            _applyingScriptHighlight = false;
        }
    }

    // RTF \cf0=default \cf1=string(red) \cf2=keyword(blue) \cf3=number \cf4=comment
    private static string BuildRtf(string text, List<(int Start, int Length, int ColorIndex)> spans)
    {
        var colorIndex = new int[text.Length];
        foreach (var (start, length, idx) in spans)
            for (int i = start; i < start + length && i < text.Length; i++)
                colorIndex[i] = idx;

        var sb = new System.Text.StringBuilder();
        sb.Append("{\\rtf1\\ansi\\deff0");
        sb.Append("{\\fonttbl{\\f0 Consolas;}}");
        sb.Append("{\\colortbl;\\red0\\green0\\blue0;\\red0\\green0\\blue255;\\red128\\green0\\blue0;\\red0\\green128\\blue0;\\red128\\green128\\blue128;}");
        sb.Append("\\f0\\fs18 ");
        int cur = -1;
        for (int i = 0; i < text.Length; i++)
        {
            int cf = colorIndex[i];
            if (cf != cur) { sb.Append("\\cf").Append(cf).Append(' '); cur = cf; }
            char c = text[i];
            if (c == '\\') sb.Append("\\\\");
            else if (c == '{') sb.Append("\\{");
            else if (c == '}') sb.Append("\\}");
            else if (c == '\n') sb.Append("\\par");
            else if (c != '\r') sb.Append(c);
        }
        sb.Append('}');
        return sb.ToString();
    }

    private static List<(int Start, int Length, int ColorIndex)> GetScriptHighlightSpans(string text)
    {
        const int defaultIdx = 0, stringIdx = 1, keywordIdx = 2, numberIdx = 3, commentIdx = 4;
        var spans = new List<(int Start, int Length, int ColorIndex)>();
        var used = new bool[text.Length];

        // Comments: // to end of line
        foreach (Match m in Regex.Matches(text, @"//[^\r\n]*"))
        {
            for (int i = m.Index; i < m.Index + m.Length; i++)
                used[i] = true;
            spans.Add((m.Index, m.Length, commentIdx));
        }

        // Strings: find " then matching " (respect \") so we always detect quoted regions
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
            if (end > start + 1) // has closing quote
            {
                for (int j = start; j < end; j++) used[j] = true;
                spans.Add((start, end - start, stringIdx));
                stringRanges.Add((start, end));
            }
            i--; // loop will increment
        }

        bool IsInsideString(int start, int length)
        {
            int end = start + length;
            foreach (var (rStart, rEnd) in stringRanges)
                if (start < rEnd && end > rStart) return true;
            return false;
        }

        // Keywords: only highlight command words at line start (name, axis, button, trigger, etc.)
        // so we never color "trigger" inside "RightTrigger" or identifiers like A, B, RightThumbY
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
                    for (int i = pos; i < pos + kw.Length; i++)
                    {
                        if (used[i]) { anyUsed = true; break; }
                    }
                    if (!anyUsed)
                    {
                        for (int i = pos; i < pos + kw.Length; i++)
                            used[i] = true;
                        spans.Add((pos, kw.Length, keywordIdx));
                    }
                }
                pos += kw.Length;
            }
        }

        // Numbers (only in unused regions)
        foreach (Match m in Regex.Matches(text, @"\b-?\d+\.?\d*\b"))
        {
            bool anyUsed = false;
            for (int i = m.Index; i < m.Index + m.Length; i++)
            {
                if (used[i]) { anyUsed = true; break; }
            }
            if (!anyUsed)
                spans.Add((m.Index, m.Length, numberIdx));
        }

        // Apply in order: comment, number, keyword, then STRING last (same as when highlighting worked)
        int Order(int idx) => idx == commentIdx ? 0 : idx == numberIdx ? 1 : idx == keywordIdx ? 2 : 3;
        spans.Sort((a, b) => Order(a.ColorIndex) != Order(b.ColorIndex) ? Order(a.ColorIndex).CompareTo(Order(b.ColorIndex)) : a.Start.CompareTo(b.Start));
        return spans;
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    /// <summary>True if position is at the start of a line (after optional whitespace).</summary>
    private static bool IsAtLineStart(string text, int position)
    {
        if (position <= 0) return true;
        int i = position - 1;
        while (i >= 0 && (text[i] == ' ' || text[i] == '\t')) i--;
        if (i < 0) return true;
        return text[i] == '\n' || text[i] == '\r';
    }

    /// <summary>True if position is on a line that starts with // (comment line).</summary>
    private static bool IsInsideCommentLine(string text, int position)
    {
        int lineStart = position;
        while (lineStart > 0 && text[lineStart - 1] != '\n' && text[lineStart - 1] != '\r')
            lineStart--;
        int i = lineStart;
        while (i < text.Length && (text[i] == ' ' || text[i] == '\t')) i++;
        return i + 1 < text.Length && text[i] == '/' && text[i + 1] == '/';
    }

    private void LoadProfile()
    {
        txtName.Text = _profile.Name ?? "";
        listProcessNames.Items.Clear();
        if (_profile.ProcessNames != null)
            foreach (var p in _profile.ProcessNames) listProcessNames.Items.Add(p);
        listFloorTrackers.Items.Clear();
        if (_profile.Trackers != null)
            foreach (var t in _profile.Trackers) listFloorTrackers.Items.Add(t);

        dgvAxis.Rows.Clear();
        foreach (var a in _profile.AxisMappings)
            dgvAxis.Rows.Add(a.Tracker, a.Source, a.Scale, a.Invert, a.Axis);

        _buttonMappings = _profile.ButtonMappings?.ToList() ?? new List<ButtonMapping>();
        _triggerMappings = _profile.TriggerMappings?.ToList() ?? new List<TriggerMapping>();
        RefreshButtonList();
        RefreshTriggerList();
        UpdateScriptFromProfile(fromForm: false);

        if (_isNew)
        {
            txtFileName.Visible = true;
            lblFileName.Visible = true;
            txtFileName.Text = _fileName;
        }
    }

    private void SaveToProfile()
    {
        _profile.Name = txtName.Text?.Trim() ?? "";
        _profile.ProcessNames = new List<string>();
        foreach (var item in listProcessNames.Items) _profile.ProcessNames.Add(item?.ToString() ?? "");
        _profile.Trackers = new List<string>();
        foreach (var item in listFloorTrackers.Items) _profile.Trackers.Add(item?.ToString() ?? "");

        _profile.AxisMappings.Clear();
        foreach (DataGridViewRow row in dgvAxis.Rows)
        {
            if (row.IsNewRow || row.Cells[0].Value == null) continue;
            _profile.AxisMappings.Add(new AxisMapping
            {
                Tracker = row.Cells[0].Value?.ToString() ?? "",
                Source = row.Cells[1].Value?.ToString() ?? "",
                Scale = float.TryParse(row.Cells[2].Value?.ToString(), out var s) ? s : 1f,
                Invert = row.Cells[3].Value is true,
                Axis = row.Cells[4].Value?.ToString() ?? ""
            });
        }

        _profile.ButtonMappings = _buttonMappings.ToList();
        _profile.TriggerMappings = _triggerMappings.ToList();
    }

    private List<ButtonMapping> _buttonMappings = new();
    private List<TriggerMapping> _triggerMappings = new();

    private void RefreshButtonList()
    {
        listButtons.Items.Clear();
        foreach (var m in _buttonMappings)
            listButtons.Items.Add(Summarize(m.Condition) + " → " + (m.Button ?? ""));
    }

    private void RefreshTriggerList()
    {
        listTriggers.Items.Clear();
        foreach (var m in _triggerMappings)
            listTriggers.Items.Add(Summarize(m.Condition) + " → " + (m.Trigger ?? ""));
    }

    private static string Summarize(MappingCondition? c)
    {
        if (c == null) return "(invalid)";
        if (c is EulerThresholdCondition et) return $"{et.Tracker}.{et.Component} {et.Op} {et.Value}";
        if (c is EulerDiffCondition ed) return $"{ed.TrackerA}-{ed.TrackerB}.{ed.Component} {ed.Op} {ed.Value}";
        if (c is EulerSumCondition es) return $"{es.TrackerA}+{es.TrackerB}.{es.Component} {es.Op} {es.Value}";
        if (c is PositionThresholdCondition pt) return $"{pt.Tracker}.{pt.Source} {pt.Op} {pt.Value}";
        return c.Type ?? "";
    }

    private void btnSave_Click(object sender, EventArgs e)
    {
        SaveToProfile();
        SavedProfile = _profile;
        SavedFileName = _isNew ? (txtFileName.Text?.Trim() ?? "mygame.json") : null;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void btnAddProcessName_Click(object sender, EventArgs e)
    {
        var s = txtProcessName.Text?.Trim();
        if (string.IsNullOrEmpty(s)) return;
        listProcessNames.Items.Add(s);
        txtProcessName.Clear();
    }

    private void btnRemoveProcessName_Click(object sender, EventArgs e)
    {
        if (listProcessNames.SelectedIndex >= 0) listProcessNames.Items.RemoveAt(listProcessNames.SelectedIndex);
    }

    private void btnAddFloorTracker_Click(object sender, EventArgs e)
    {
        if (cmbFloorTracker.SelectedItem == null) return;
        listFloorTrackers.Items.Add(cmbFloorTracker.SelectedItem.ToString());
    }

    private void btnRemoveFloorTracker_Click(object sender, EventArgs e)
    {
        if (listFloorTrackers.SelectedIndex >= 0) listFloorTrackers.Items.RemoveAt(listFloorTrackers.SelectedIndex);
    }

    private void btnAddAxis_Click(object sender, EventArgs e)
    {
        dgvAxis.Rows.Add("HEAD", "EulerX", 1f, false, "RightThumbX");
    }

    private void btnRemoveAxis_Click(object sender, EventArgs e)
    {
        if (dgvAxis.CurrentRow != null && !dgvAxis.CurrentRow.IsNewRow)
            dgvAxis.Rows.Remove(dgvAxis.CurrentRow);
    }

    private void btnAddButton_Click(object sender, EventArgs e)
    {
        using var dlg = new ConditionEditDialog(null);
        if (dlg.ShowDialog() != DialogResult.OK || dlg.Result == null) return;
        var button = cmbButton.SelectedItem?.ToString() ?? "A";
        _buttonMappings.Add(new ButtonMapping { Condition = dlg.Result, Button = button });
        RefreshButtonList();
    }

    private void btnEditButton_Click(object sender, EventArgs e)
    {
        var i = listButtons.SelectedIndex;
        if (i < 0 || i >= _buttonMappings.Count) return;
        var m = _buttonMappings[i];
        using var dlg = new ConditionEditDialog(m.Condition);
        if (dlg.ShowDialog() != DialogResult.OK || dlg.Result == null) return;
        _buttonMappings[i] = new ButtonMapping { Condition = dlg.Result, Button = m.Button };
        RefreshButtonList();
    }

    private void btnRemoveButton_Click(object sender, EventArgs e)
    {
        var i = listButtons.SelectedIndex;
        if (i >= 0 && i < _buttonMappings.Count) { _buttonMappings.RemoveAt(i); RefreshButtonList(); }
    }

    private void btnAddTrigger_Click(object sender, EventArgs e)
    {
        using var dlg = new ConditionEditDialog(null);
        if (dlg.ShowDialog() != DialogResult.OK || dlg.Result == null) return;
        var trigger = cmbTrigger.SelectedItem?.ToString() ?? "LeftTrigger";
        _triggerMappings.Add(new TriggerMapping { Condition = dlg.Result, Trigger = trigger, ValueWhenTrue = 255, ValueWhenFalse = 0 });
        RefreshTriggerList();
    }

    private void btnEditTrigger_Click(object sender, EventArgs e)
    {
        var i = listTriggers.SelectedIndex;
        if (i < 0 || i >= _triggerMappings.Count) return;
        var m = _triggerMappings[i];
        using var dlg = new ConditionEditDialog(m.Condition);
        if (dlg.ShowDialog() != DialogResult.OK || dlg.Result == null) return;
        _triggerMappings[i] = new TriggerMapping { Condition = dlg.Result, Trigger = m.Trigger, ValueWhenTrue = m.ValueWhenTrue, ValueWhenFalse = m.ValueWhenFalse };
        RefreshTriggerList();
    }

    private void btnRemoveTrigger_Click(object sender, EventArgs e)
    {
        var i = listTriggers.SelectedIndex;
        if (i >= 0 && i < _triggerMappings.Count) { _triggerMappings.RemoveAt(i); RefreshTriggerList(); }
    }

    /// <summary>
    /// Updates the script text box from current profile. If fromForm is true, writes form state to _profile first.
    /// </summary>
    private void UpdateScriptFromProfile(bool fromForm = false)
    {
        if (txtScript == null) return;
        if (fromForm) SaveToProfile();
        txtScript.Text = ScriptFormat.FormatProfile(_profile);
        ApplyScriptHighlighting();
    }

    private void btnRefreshFromForm_Click(object sender, EventArgs e)
    {
        UpdateScriptFromProfile(fromForm: true);
    }

    private void btnApplyScript_Click(object sender, EventArgs e)
    {
        try
        {
            var parsed = ScriptFormat.ParseProfile(txtScript.Text);
            _profile.Name = parsed.Name;
            _profile.ProcessNames = parsed.ProcessNames ?? new List<string>();
            _profile.Trackers = parsed.Trackers;
            _profile.AxisMappings = parsed.AxisMappings;
            _profile.ButtonMappings = parsed.ButtonMappings;
            _profile.TriggerMappings = parsed.TriggerMappings;
            LoadProfile();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Script parse error: " + ex.Message, "Apply script", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
